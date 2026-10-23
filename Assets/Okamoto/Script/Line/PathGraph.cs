using System.Collections.Generic;
using UnityEngine;

public class Junction
{
    public float s;          // 線上の位置(始点からの距離)
    public PathVertex v;

    // 四肢の吸着範囲に入ったとき、四肢の頂点にまとめるか。
    // 造線の端がつながる位置はfalse(四肢の近くに造線をつないでも、
    // 手前でノードが回収されないようにする)
    public bool snapToLimb = true;
}

public class PathVertex
{
    public Vector2 pos;
    public LimbController limb;   // 生存している四肢(回収点)なら設定
    public float dist;            // 最寄りの四肢までの経路距離(一方通行を考慮)
    public float distFree;        // 最寄りの四肢までの経路距離(向きを無視)
    public readonly List<(PathLine line, int index)> members =
        new List<(PathLine line, int index)>();
}

public class PathLine
{
    public object key;
    public bool isCreated;
    public bool blocked;     // 消滅待ちの造線(新たに入れない)
    public bool removed;

    public readonly List<Vector2> pts = new List<Vector2>();
    public readonly List<Junction> junctions = new List<Junction>();
    private readonly List<float> cum = new List<float>();

    public float Length { get; private set; }

    public void SetPoints(List<Vector2> p)
    {
        pts.Clear();
        pts.AddRange(p);
        cum.Clear();
        cum.Add(0f);
        float t = 0f;
        for (int i = 1; i < pts.Count; i++)
        {
            t += Vector2.Distance(pts[i - 1], pts[i]);
            cum.Add(t);
        }
        Length = t;
    }

    public Vector2 PointAt(float s)
    {
        s = Mathf.Clamp(s, 0f, Length);
        int i = 0;
        while (i < pts.Count - 2 && cum[i + 1] < s) i++;
        float seg = cum[i + 1] - cum[i];
        float t = seg > 1e-6f ? (s - cum[i]) / seg : 0f;
        return Vector2.Lerp(pts[i], pts[i + 1], t);
    }

    // 位置sから dir 方向へ進むときの進行方向ベクトル
    public Vector2 TangentAt(float s, int dir)
    {
        int i;
        if (dir > 0)
        {
            i = 0;
            while (i < pts.Count - 2 && cum[i + 1] <= s + 1e-4f) i++;
        }
        else
        {
            i = pts.Count - 2;
            while (i > 0 && cum[i] >= s - 1e-4f) i--;
        }
        return (pts[i + 1] - pts[i]).normalized * dir;
    }

    // 点pに最も近い線上の位置(s)と距離を返す
    public float Project(Vector2 p, out float dist)
    {
        float bestD = float.MaxValue, bestS = 0f;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector2 a = pts[i], ab = pts[i + 1] - a;
            float sq = ab.sqrMagnitude;
            float t = sq > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / sq) : 0f;
            float d = Vector2.Distance(p, a + ab * t);
            if (d < bestD)
            {
                bestD = d;
                bestS = cum[i] + t * Mathf.Sqrt(sq);
            }
        }
        dist = bestD;
        return bestS;
    }

    public void AddJunction(float s, float tol, bool snapToLimb = true)
    {
        foreach (var j in junctions)
        {
            if (Mathf.Abs(j.s - s) <= tol) return;
        }
        junctions.Add(new Junction { s = s, snapToLimb = snapToLimb });
    }
}

public class PathGraph : MonoBehaviour
{
    public static PathGraph Instance { get; private set; }

    [Header("参照")]
    [Tooltip("人体図の中心(中心方向・時計回りの判定に使用)")]
    [SerializeField] private Transform center;

    [Header("設定")]
    [Tooltip("線の端と線、線と四肢を接続とみなす距離")]
    [SerializeField] private float tolerance = 0.2f;
    [SerializeField] private int curveSamples = 40;
    [Tooltip("この経路距離以内に四肢があれば、そちらを優先して進む")]
    [SerializeField] private float limbSeekDistance = 3f;
    [Tooltip("四肢の位置から、この距離以内を通る線・端点は四肢に接続されているとみなす")]
    [SerializeField] private float limbSnapDistance = 0.6f;
    [Tooltip("ONにすると、造線(あみだくじの横線)に出会ったら必ず渡る")]
    [SerializeField] private bool alwaysCrossCreatedLines = true;

    // 四肢・LineManager・基本線は GameManager / LineManager から取得する
    private LineManager lineManager;

    private readonly Dictionary<object, PathLine> lines =
        new Dictionary<object, PathLine>();
    private readonly List<PathVertex> vertices = new List<PathVertex>();
    private bool dirty = true;

    private class Cand
    {
        public PathLine line;
        public int dir;
        public float s, len, cost, costFree, center, cw;
        public bool toLimb;
    }

    private LimbController[] Limbs
    {
        get
        {
            if (GameManager.Instance != null &&
                GameManager.Instance.limbs != null)
            {
                return GameManager.Instance.limbs;
            }
            return System.Array.Empty<LimbController>();
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            lineManager = GameManager.Instance.lineManager;
        }

        if (lineManager != null)
        {
            lineManager.LinesChanged += MarkDirty;
        }
        else
        {
            Debug.LogWarning(
                "PathGraph: GameManager に LineManager が設定されていません。", this);
        }

        foreach (var limb in Limbs)
        {
            if (limb != null) limb.OnDestroyed += _ => MarkDirty();
        }
    }

    private void OnDestroy()
    {
        if (lineManager != null) lineManager.LinesChanged -= MarkDirty;
        if (Instance == this) Instance = null;
    }

    // 基本線のisActiveを切り替えたときなどに呼ぶ
    public void MarkDirty()
    {
        dirty = true;
    }

    public void RebuildIfDirty()
    {
        if (dirty) Rebuild();
    }

    public PathLine GetLine(object key)
    {
        RebuildIfDirty();
        return lines.TryGetValue(key, out var l) ? l : null;
    }

    private PathLine GetOrCreate(object key, bool isCreated)
    {
        if (!lines.TryGetValue(key, out var l))
        {
            l = new PathLine { key = key, isCreated = isCreated };
            lines[key] = l;
        }
        return l;
    }

    // ===== グラフ構築 =====
    private void Rebuild()
    {
        dirty = false;

        // Start前に呼ばれた場合に備えて取得し直す
        if (lineManager == null && GameManager.Instance != null)
        {
            lineManager = GameManager.Instance.lineManager;
        }

        var alive = new HashSet<object>();

        if (lineManager != null && lineManager.BasicLines != null)
        {
            foreach (var bl in lineManager.BasicLines)
            {
                if (bl == null || !bl.isActive ||
                    bl.startPoint == null || bl.endPoint == null) continue;

                var pl = GetOrCreate(bl, false);
                pl.blocked = false;
                pl.SetPoints(bl.GetPolyline(curveSamples));
                alive.Add(bl);
            }

            foreach (var c in lineManager.GetActiveCreatedLines())
            {
                object key = c.Id;
                var pl = GetOrCreate(key, true);
                pl.blocked = c.IsBlocked;
                pl.SetPoints(new List<Vector2> { c.Start, c.End });
                alive.Add(key);
            }
        }

        var stale = new List<object>();
        foreach (var kv in lines)
        {
            if (!alive.Contains(kv.Key)) stale.Add(kv.Key);
        }
        foreach (var k in stale)
        {
            lines[k].removed = true;
            lines.Remove(k);
        }

        var list = new List<PathLine>(lines.Values);

        // 各線の両端は必ず分岐点
        foreach (var l in list)
        {
            l.junctions.Clear();
            l.AddJunction(0f, tolerance, !l.isCreated);
            // 終端は必ず分岐点にする(短い線で始点と間引かれないように直接追加)
            if (l.Length > 0.001f)
            {
                l.junctions.Add(new Junction
                {
                    s = l.Length,
                    snapToLimb = !l.isCreated
                });
            }
        }

        // 線の端が他の線の途中に接していたら、そこも分岐点にする
        foreach (var a in list)
        {
            Vector2[] ends = { a.pts[0], a.pts[a.pts.Count - 1] };
            foreach (var end in ends)
            {
                foreach (var b in list)
                {
                    if (b == a) continue;
                    float s = b.Project(end, out float d);
                    // 造線の端がつながる位置は、四肢には吸着させない
                    if (d <= tolerance) b.AddJunction(s, tolerance, !a.isCreated);
                }
            }
        }

        // 四肢の近くを通る線は、その位置を分岐点にする
        foreach (var limb in Limbs)
        {
            if (limb == null || !limb.IsAlive) continue;

            Vector2 lp = limb.transform.position;
            foreach (var l in list)
            {
                float s = l.Project(lp, out float d);
                if (d <= limbSnapDistance) l.AddJunction(s, tolerance);
            }
        }

        // 先に四肢(回収点)の頂点を作る
        vertices.Clear();
        foreach (var limb in Limbs)
        {
            if (limb == null || !limb.IsAlive) continue;

            vertices.Add(new PathVertex
            {
                pos = limb.transform.position,
                limb = limb
            });
        }

        // 近い分岐点を1つの頂点にまとめる(四肢の頂点は広めに吸着)
        float tolSq = tolerance * tolerance;
        float snapSq = limbSnapDistance * limbSnapDistance;
        foreach (var l in list)
        {
            l.junctions.Sort((x, y) => x.s.CompareTo(y.s));
            for (int i = 0; i < l.junctions.Count; i++)
            {
                var j = l.junctions[i];
                Vector2 p = l.PointAt(j.s);
                bool snap = j.snapToLimb;

                var v = vertices.Find(x =>
                    (x.pos - p).sqrMagnitude <=
                    ((x.limb != null && snap) ? snapSq : tolSq));
                if (v == null)
                {
                    v = new PathVertex { pos = p };
                    vertices.Add(v);
                }
                j.v = v;
                v.members.Add((l, i));
            }
        }

        // 各頂点から四肢までの経路距離(一方通行を考慮)
        foreach (var v in vertices)
        {
            v.dist = v.limb != null ? 0f : float.PositiveInfinity;
            v.distFree = v.dist;
        }

        bool changed = true;
        for (int guard = 0; changed && guard < 200; guard++)
        {
            changed = false;
            foreach (var l in list)
            {
                if (l.blocked) continue;
                for (int i = 0; i < l.junctions.Count - 1; i++)
                {
                    var a = l.junctions[i].v;
                    var b = l.junctions[i + 1].v;
                    float len = l.junctions[i + 1].s - l.junctions[i].s;

                    // 向きを無視した距離(造線から出た後の進路選択用)
                    if (b.distFree + len < a.distFree)
                    {
                        a.distFree = b.distFree + len;
                        changed = true;
                    }
                    if (a.distFree + len < b.distFree)
                    {
                        b.distFree = a.distFree + len;
                        changed = true;
                    }

                    if (b.dist + len < a.dist)
                    {
                        a.dist = b.dist + len;
                        changed = true;
                    }
                    if (l.isCreated && a.dist + len < b.dist)
                    {
                        b.dist = a.dist + len;
                        changed = true;
                    }
                }
            }
        }
    }

    // ===== ノード用API =====

    // 位置sから dir 方向にある次の分岐点
    public Junction NextJunction(PathLine l, float s, int dir)
    {
        const float eps = 1e-4f;
        if (dir > 0)
        {
            foreach (var j in l.junctions)
            {
                if (j.s > s + eps) return j;
            }
        }
        else
        {
            for (int i = l.junctions.Count - 1; i >= 0; i--)
            {
                if (l.junctions[i].s < s - eps) return l.junctions[i];
            }
        }
        return null;
    }

    // 頂点vに到着したときの、次に進む線と向きを決める
    public bool TryChoose(PathVertex v, PathLine from, int fromDir,
        out PathLine nLine, out int nDir, out float nS)
    {
        nLine = null; nDir = 0; nS = 0f;

        Vector2 toCenter = center != null
            ? ((Vector2)center.position - v.pos).normalized
            : Vector2.zero;
        Vector2 radial = -toCenter;

        var cands = new List<Cand>();

        // 造線から降りた直後か
        bool fromRung = from != null && from.isCreated;

        // 基本線を逆向きに進んでいる途中か
        bool reversing = from != null && !from.isCreated && fromDir < 0;

        // 基本線の向きを無視して、四肢に近い方へ進む状態
        bool freeMode = fromRung || reversing;

        foreach (var m in v.members)
        {
            PathLine l = m.line;
            if (l.blocked && l != from) continue;   // 消滅待ちの造線には入れない

            for (int d = 1; d >= -1; d -= 2)
            {
                // 基本線は一方通行。ただし
                // ・造線から降りた直後は、四肢に近い方へ逆向きにも進める
                // ・逆向きに進んでいる途中は、線の継ぎ目でも逆向きに進み続ける
                if (d < 0 && !l.isCreated && !freeMode) continue;
                if (l == from && d == -fromDir) continue; // 来た道は戻らない

                int k = m.index + d;
                if (k < 0 || k >= l.junctions.Count) continue;

                Junction cur = l.junctions[m.index];
                Junction to = l.junctions[k];
                Vector2 t = l.TangentAt(cur.s, d);
                float len = Mathf.Abs(to.s - cur.s);

                cands.Add(new Cand
                {
                    line = l,
                    dir = d,
                    s = cur.s,
                    len = len,
                    cost = len + to.v.dist,
                    costFree = len + to.v.distFree,
                    toLimb = to.v.limb != null,
                    center = Vector2.Dot(t, toCenter),
                    // Unityはy上向きなので、時計回りは外積がマイナス
                    cw = -(radial.x * t.y - radial.y * t.x)
                });
            }
        }

        if (cands.Count == 0) return false;

        Cand best = null;

        // 0. あみだくじ: 造線があれば必ず渡る(複数あれば四肢に近い方)
        if (alwaysCrossCreatedLines)
        {
            foreach (var c in cands)
            {
                if (!c.line.isCreated) continue;
                if (best == null || BetterRung(c, best)) best = c;
            }
        }

        // 0.5 造線から降りた直後、または逆向きに進行中:
        //     最も近い四肢へ向かう(基本線の向きに関係なく、近い方向を選ぶ)
        if (best == null && freeMode)
        {
            foreach (var c in cands)
            {
                if (float.IsInfinity(c.costFree)) continue;
                if (best == null ||
                    c.costFree < best.costFree - 0.001f ||
                    (Mathf.Abs(c.costFree - best.costFree) <= 0.001f &&
                     Better(c, best)))
                {
                    best = c;
                }
            }

            // 四肢へ続く道がない場合、行き止まりになる逆走候補は使わない
            if (best == null)
            {
                cands.RemoveAll(c => c.dir < 0 && !c.line.isCreated);
                if (cands.Count == 0) return false;
            }
        }

        // 1. すぐ先が四肢ならそちらを優先
        if (best == null)
        {
            foreach (var c in cands)
            {
                if (c.toLimb && (best == null || c.len < best.len)) best = c;
            }
        }

        // 2. 近くに四肢があるなら、四肢までの経路が最短になる方
        if (best == null)
        {
            foreach (var c in cands)
            {
                if (c.cost <= limbSeekDistance &&
                    (best == null || c.cost < best.cost)) best = c;
            }
        }

        // 3. それ以外は、中心方向 → 時計回り
        if (best == null)
        {
            foreach (var c in cands)
            {
                if (best == null || Better(c, best)) best = c;
            }
        }

        nLine = best.line;
        nDir = best.dir;
        nS = best.s;
        return true;
    }

    // 造線が複数ある場合: 四肢までの経路が短い方、同じなら中心方向→時計回り
    private static bool BetterRung(Cand a, Cand b)
    {
        if (a.cost < b.cost - 0.001f) return true;
        if (a.cost > b.cost + 0.001f) return false;
        return Better(a, b);
    }

    private static bool Better(Cand a, Cand b)
    {
        if (Mathf.Abs(a.center - b.center) > 0.05f) return a.center > b.center;
        return a.cw > b.cw;
    }

    // 接続状況の確認用(シーンビューに頂点を表示)
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = Color.yellow;
        foreach (var v in vertices)
        {
            Gizmos.color = v.limb != null ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(
                v.pos,
                v.limb != null ? limbSnapDistance : 0.08f);
        }
    }
}