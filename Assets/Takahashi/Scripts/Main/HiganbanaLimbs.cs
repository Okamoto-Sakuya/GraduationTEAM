using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 四肢欠損時の彼岸花演出（LogicModeScreen から使う）。UI の Image だけで動く「自前パーティクル」。
///
/// 使う画像は 6 枚だけ（Assets/Resources/Higanbana/）
///   base_no_limbs.png     … 四肢を消した人体図（centerImage に表示）
///   limb_legL.png など 4 枚 … 手足の切り抜き（土台に重ねると元の絵に戻る）
///   flower_bloom_sheet.png … 花 1 輪が咲く 24 コマ（6×4、1 コマ 256px）
/// 花の位置・咲く順番・赤い脈・火の粉はすべてここで生成する（毎回少しずつ変わる）。
/// </summary>
public static class HiganbanaLimbs
{
    public enum Direction { FromRoot, FromTip }   // 付け根から咲く / 先端（指先・つま先）から咲く

    [System.Serializable]
    public class Settings
    {
        [Tooltip("咲き始めから、最後の花が咲き始めるまでの秒数（浸食の速さ）")]
        public float spreadSeconds = 4.5f;
        [Tooltip("花 1 輪が、つぼみから咲き切るまでの秒数")]
        public float flowerSeconds = 1.8f;
        [Tooltip("手足の切り抜きが消えるまでの秒数")]
        public float limbFadeSeconds = 1.8f;
        [Tooltip("付け根（先端）が光り始めてから最初の花が出るまでの秒数")]
        public float leadSeconds = 1.2f;
        [Tooltip("花の大きさの倍率")]
        public float flowerSize = 1f;
        [Tooltip("花の密度の倍率（大きいほど数が増える）")]
        public float density = 1f;
        [Tooltip("花から花へ伸びる赤い脈")]
        public bool veins = true;
        [Tooltip("同時に舞う火の粉の数（0 で無し）")]
        public int embers = 10;
        [Tooltip("true = 毎回ちがう咲き方 / false = 毎回同じ")]
        public bool randomEachTime = true;
    }

    // ---------- 素材キャンバス（1080×1528）上の配置（左上原点 px） ----------
    public const float CanvasW = 1080f, CanvasH = 1528f;
    const int SheetCols = 6, SheetRows = 4;
    const float FrameUnits = 220f;    // シート 1 コマが表す範囲（元絵の px、花の大きさ 1 のとき）

    static readonly Dictionary<string, Rect> LimbRects = new Dictionary<string, Rect>
    {
        { "legL", new Rect(41, 816, 398, 541) },
        { "legR", new Rect(638, 791, 389, 544) },
        { "armL", new Rect(270, 443, 205, 107) },
        { "armR", new Rect(621, 425, 204, 115) },
    };

    class Shape
    {
        public Vector2[] poly;    // 部位の輪郭
        public Vector2[] line;    // 付け根 → 先端 の中心線
        public float sizeRoot, sizeTip, gapRoot, gapTip, thin;
    }

    static Vector2 V(float x, float y) => new Vector2(x, y);

    static readonly Dictionary<string, Shape> Shapes = new Dictionary<string, Shape>
    {
        { "armL", new Shape {
            poly = new[] { V(287.5f, 457.5f), V(350.0f, 452.5f), V(410.0f, 455.0f), V(450.0f, 465.0f), V(465.0f, 490.0f), V(455.0f, 530.0f), V(425.0f, 540.0f), V(380.0f, 530.0f), V(340.0f, 520.0f), V(300.0f, 505.0f), V(280.0f, 485.0f) },
            line = new[] { V(452.5f, 500.0f), V(380.0f, 487.5f), V(290.0f, 475.0f) },
            sizeRoot = 1.15f, sizeTip = 0.85f,
            gapRoot = 30.0f, gapTip = 40.0f, thin = 0.45f } },
        { "armR", new Shape {
            poly = new[] { V(640.0f, 475.0f), V(690.0f, 452.5f), V(740.0f, 440.0f), V(790.0f, 435.0f), V(815.0f, 450.0f), V(815.0f, 480.0f), V(790.0f, 505.0f), V(740.0f, 522.5f), V(690.0f, 530.0f), V(645.0f, 530.0f), V(630.0f, 505.0f) },
            line = new[] { V(642.5f, 500.0f), V(725.0f, 482.5f), V(805.0f, 465.0f) },
            sizeRoot = 1.15f, sizeTip = 0.85f,
            gapRoot = 30.0f, gapTip = 40.0f, thin = 0.45f } },
        { "legL", new Shape {
            poly = new[] { V(387.5f, 830.0f), V(426.0f, 826.0f), V(429.0f, 860.0f), V(425.0f, 910.0f), V(415.0f, 970.0f), V(400.0f, 1025.0f), V(380.0f, 1075.0f), V(355.0f, 1125.0f), V(325.0f, 1170.0f), V(297.5f, 1210.0f), V(270.0f, 1242.5f), V(260.0f, 1280.0f), V(252.5f, 1307.5f), V(235.0f, 1332.5f), V(190.0f, 1347.5f), V(135.0f, 1337.5f), V(102.5f, 1312.5f), V(62.5f, 1282.5f), V(50.0f, 1257.5f), V(100.0f, 1245.0f), V(165.0f, 1240.0f), V(210.0f, 1232.5f), V(232.5f, 1210.0f), V(257.5f, 1167.5f), V(277.5f, 1117.5f), V(292.5f, 1072.5f), V(312.5f, 1022.5f), V(332.5f, 972.5f), V(352.5f, 912.5f), V(370.0f, 857.5f) },
            line = new[] { V(407.5f, 832.5f), V(387.5f, 950.0f), V(350.0f, 1075.0f), V(295.0f, 1190.0f), V(240.0f, 1250.0f), V(165.0f, 1280.0f), V(75.0f, 1270.0f) },
            sizeRoot = 1.35f, sizeTip = 0.85f,
            gapRoot = 32.0f, gapTip = 55.0f, thin = 0.78f } },
        { "legR", new Shape {
            poly = new[] { V(648.3f, 801.7f), V(654.2f, 875.0f), V(666.7f, 958.3f), V(687.5f, 1025.0f), V(716.7f, 1083.3f), V(741.7f, 1133.3f), V(766.7f, 1183.3f), V(800.0f, 1225.0f), V(825.0f, 1250.0f), V(866.7f, 1283.3f), V(908.3f, 1325.0f), V(966.7f, 1316.7f), V(1016.7f, 1258.3f), V(1012.5f, 1245.8f), V(941.7f, 1241.7f), V(900.0f, 1241.7f), V(866.7f, 1216.7f), V(841.7f, 1175.0f), V(816.7f, 1125.0f), V(791.7f, 1066.7f), V(762.5f, 1000.0f), V(737.5f, 933.3f), V(720.8f, 875.0f), V(708.3f, 801.7f) },
            line = new[] { V(672.5f, 807.5f), V(700.0f, 950.0f), V(740.0f, 1075.0f), V(780.0f, 1175.0f), V(840.0f, 1240.0f), V(925.0f, 1280.0f), V(995.0f, 1255.0f) },
            sizeRoot = 1.35f, sizeTip = 0.85f,
            gapRoot = 32.0f, gapTip = 55.0f, thin = 0.78f } },
    };

    /// <summary>LogicModeScreen の部位名（画面上の左右）→ 素材の部位名</summary>
    public static string KeyOf(string limbName) => limbName switch
    {
        "LeftArm" => "armL",
        "RightArm" => "armR",
        "LeftLeg" => "legL",
        "RightLeg" => "legR",
        _ => null,
    };

    public static Rect LimbRect(string key) => LimbRects[key];

    public static Sprite LoadSprite(string resourcesFolder, string name) =>
        Resources.Load<Sprite>(resourcesFolder + "/" + name);

    /// <summary>Resources から花のシートを読んで 24 コマに切り分ける</summary>
    public static Sprite[] LoadSheet(string resourcesFolder)
    {
        var tex = Resources.Load<Texture2D>(resourcesFolder + "/flower_bloom_sheet");
        return tex == null ? null : Slice(tex, new Rect(0, 0, tex.width, tex.height));
    }

    /// <summary>Inspector で入れた花の画像を 24 コマに切り分ける（スライス設定は不要）</summary>
    public static Sprite[] SliceSheet(Sprite sheet) =>
        sheet == null ? null : Slice(sheet.texture, sheet.textureRect);

    static Sprite[] Slice(Texture2D tex, Rect area)
    {
        float cw = area.width / SheetCols, ch = area.height / SheetRows;
        var frames = new Sprite[SheetCols * SheetRows];
        for (int i = 0; i < frames.Length; i++)
        {
            int c = i % SheetCols, r = i / SheetCols;
            var rect = new Rect(area.x + c * cw, area.y + area.height - (r + 1) * ch, cw, ch);   // 左上から順に
            frames[i] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
            frames[i].name = "flower_" + i;
        }
        return frames;
    }

    static Sprite softDot;
    /// <summary>ぼかした丸（光・火の粉用）。画像を使わずコードで作る</summary>
    static Sprite SoftDot()
    {
        if (softDot != null) return softDot;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = Mathf.Clamp01(1f - d);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        softDot = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return softDot;
    }

    /// <summary>preserveAspect の Image 内で、実際に絵が描かれている大きさ</summary>
    public static Vector2 FittedSize(Vector2 boxSize, bool preserveAspect)
    {
        if (!preserveAspect) return boxSize;
        float aspect = CanvasW / CanvasH;
        return boxSize.x / boxSize.y > aspect
            ? new Vector2(boxSize.y * aspect, boxSize.y)
            : new Vector2(boxSize.x, boxSize.x / aspect);
    }

    /// <summary>素材キャンバス上の矩形 r を、parent 上の Image として作る</summary>
    public static Image CreateLayer(RectTransform parent, string name, Rect r, Vector2 fitted, Sprite sprite)
    {
        float s = fitted.x / CanvasW;
        var img = NewImage(parent, name, sprite, Color.white);
        var rt = img.rectTransform;
        rt.sizeDelta = new Vector2(r.width, r.height) * s;
        rt.anchoredPosition = ToLocal(new Vector2(r.x + r.width * 0.5f, r.y + r.height * 0.5f), s);
        img.enabled = sprite != null;
        return img;
    }

    /// <summary>花・脈・火の粉を入れる空の入れ物</summary>
    public static RectTransform CreateContainer(RectTransform parent, string name, Vector2 fitted)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = fitted;
        return rt;
    }

    static Vector2 ToLocal(Vector2 canvasPx, float s) =>
        new Vector2(canvasPx.x - CanvasW * 0.5f, CanvasH * 0.5f - canvasPx.y) * s;

    static Image NewImage(RectTransform parent, string name, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // ======================= 生成 =======================

    class Flower
    {
        public Vector2 pos;       // 素材キャンバス px
        public float u;           // 付け根 0 → 先端 1
        public float order, size, start, rot, phase;
        public Flower parent;
        public Vector2[] vein;    // 親 → 自分 の脈（折れ線）
        public float veinStart, veinEnd;
        public Image img;
        public Image[] veinCore, veinGlow;
    }

    class Ember { public Image img; public Vector2 from; public float born, life, size, sway; }

    static float Along(Shape sh, Vector2 p, float[] segLen, float total)
    {
        float best = 0f, bd = float.MaxValue, acc = 0f;
        for (int i = 0; i < sh.line.Length - 1; i++)
        {
            Vector2 a = sh.line[i], ab = sh.line[i + 1] - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            float d = (p - (a + ab * t)).sqrMagnitude;
            if (d < bd) { bd = d; best = (acc + t * segLen[i]) / total; }
            acc += segLen[i];
        }
        return best;
    }

    static bool Inside(Vector2[] poly, Vector2 p)
    {
        bool c = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
        return c;
    }

    static float EdgeDist(Vector2[] poly, Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            Vector2 a = poly[j], ab = poly[i] - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
        }
        return best;
    }

    static float Rand(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    static float EaseOut(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x) * (1f - x); }

    static List<Flower> MakeFlowers(Shape sh, Direction dir, Settings st, System.Random rng)
    {
        var segLen = new float[sh.line.Length - 1];
        float total = 0f;
        for (int i = 0; i < segLen.Length; i++) { segLen[i] = Vector2.Distance(sh.line[i], sh.line[i + 1]); total += segLen[i]; }

        Vector2 min = sh.poly[0], max = sh.poly[0];
        foreach (var p in sh.poly) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
        min -= Vector2.one * 10f; max += Vector2.one * 10f;

        float dens = Mathf.Max(0.2f, st.density);
        var list = new List<Flower>();
        int tries = Mathf.RoundToInt(6000 * dens);
        for (int k = 0; k < tries; k++)
        {
            var p = new Vector2(Rand(rng, min.x, max.x), Rand(rng, min.y, max.y));
            if (!Inside(sh.poly, p) && EdgeDist(sh.poly, p) > 8f) continue;
            float u = Along(sh, p, segLen, total);
            if (rng.NextDouble() > 1f - sh.thin * u) continue;           // 先端ほどまばら
            float gap = Mathf.Lerp(sh.gapRoot, sh.gapTip, u) / Mathf.Sqrt(dens);
            bool near = false;
            foreach (var f in list) if ((f.pos - p).sqrMagnitude < gap * gap) { near = true; break; }
            if (near) continue;
            float order = dir == Direction.FromTip ? 1f - u : u;
            list.Add(new Flower
            {
                pos = p,
                u = u,
                order = order,
                size = Mathf.Lerp(sh.sizeRoot, sh.sizeTip, u) * Rand(rng, 0.85f, 1.12f) * st.flowerSize,
                start = st.leadSeconds + st.spreadSeconds * order + Rand(rng, -0.1f, 0.1f),
                rot = Rand(rng, 0f, 360f),
                phase = Rand(rng, 0f, 6.28f),
            });
        }
        list.Sort((a, b) => a.order.CompareTo(b.order));

        // 脈：咲く順で、すでに咲いた花（または起点）のうち一番近いものから伸ばす
        Vector2 rootPos = dir == Direction.FromTip ? sh.line[sh.line.Length - 1] : sh.line[0];
        var root = new Flower { pos = rootPos, start = st.leadSeconds * 0.4f };
        for (int i = 0; i < list.Count; i++)
        {
            var f = list[i];
            Flower best = root;
            float bd = (root.pos - f.pos).sqrMagnitude;
            for (int j = 0; j < i; j++)
            {
                float d = (list[j].pos - f.pos).sqrMagnitude;
                if (d < bd) { bd = d; best = list[j]; }
            }
            f.parent = best;
            Vector2 a = best.pos, b = f.pos, mid = (a + b) * 0.5f;
            Vector2 n = new Vector2(-(b.y - a.y), b.x - a.x).normalized;
            Vector2 c = mid + n * Rand(rng, -0.15f, 0.15f) * Vector2.Distance(a, b);
            f.vein = new Vector2[5];
            for (int q = 0; q < 5; q++)
            {
                float t = q / 4f;
                f.vein[q] = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b;
            }
            f.veinStart = best.start + 0.1f;
            f.veinEnd = Mathf.Max(f.start, f.veinStart + 0.4f);
        }
        return list;
    }

    // ======================= 再生 =======================

    /// <summary>
    /// layer の中に花を咲かせ、limb（手足の切り抜き）を消す。咲き切ったらそのまま残る。
    /// timeScale = 0 でも動く。instant = true なら咲き終わりの状態をすぐ表示（途中再開など）。
    /// </summary>
    public static IEnumerator Play(RectTransform layer, Image limb, string key, Direction dir,
                                   Sprite[] frames, Settings st, bool instant = false)
    {
        if (layer == null || frames == null || frames.Length == 0 || !Shapes.ContainsKey(key))
        {
            if (limb != null) limb.enabled = false;
            yield break;
        }

        var sh = Shapes[key];
        var rng = st.randomEachTime ? new System.Random() : new System.Random(key[0] * 31 + key[3]);
        float s = layer.sizeDelta.x / CanvasW;   // 素材キャンバス px → UI px
        var flowers = MakeFlowers(sh, dir, st, rng);
        Sprite dot = SoftDot();

        // 起点の光
        Vector2 rootPos = dir == Direction.FromTip ? sh.line[sh.line.Length - 1] : sh.line[0];
        var rootGlow = NewImage(layer, "RootGlow", dot, new Color(1f, 0.12f, 0.2f, 0f));
        rootGlow.rectTransform.anchoredPosition = ToLocal(rootPos, s);
        rootGlow.rectTransform.sizeDelta = Vector2.one * 170f * s;

        // 脈（花より下）
        if (st.veins)
            foreach (var f in flowers)
            {
                f.veinGlow = new Image[f.vein.Length - 1];
                f.veinCore = new Image[f.vein.Length - 1];
                for (int i = 0; i < f.veinCore.Length; i++)
                {
                    f.veinGlow[i] = NewSeg(layer, f.vein[i], s, 7f, new Color(1f, 0.14f, 0.21f, 0.28f));
                    f.veinCore[i] = NewSeg(layer, f.vein[i], s, 2.2f, new Color(0.71f, 0.06f, 0.12f, 0.9f));
                }
            }

        // 花（先端側を先に作り、付け根側が上に重なるように）
        var drawOrder = new List<Flower>(flowers);
        drawOrder.Sort((a, b) => b.u.CompareTo(a.u));
        foreach (var f in drawOrder)
        {
            f.img = NewImage(layer, "Flower", frames[0], new Color(1, 1, 1, 0));
            f.img.rectTransform.anchoredPosition = ToLocal(f.pos, s);
            f.img.rectTransform.localEulerAngles = new Vector3(0, 0, f.rot);
        }

        // 火の粉（いちばん上）
        var embers = new List<Ember>();
        for (int i = 0; i < Mathf.Max(0, st.embers); i++)
        {
            var e = new Ember { img = NewImage(layer, "Ember", dot, new Color(1f, 0.36f, 0.24f, 0f)), born = -99f };
            embers.Add(e);
        }

        float last = 0f;
        foreach (var f in flowers) last = Mathf.Max(last, f.start + st.flowerSeconds);
        float emberFrom = st.leadSeconds + st.flowerSeconds * 0.5f, emberTo = last + 0.5f;
        float end = emberTo + 2f;

        float t = instant ? end : 0f;
        while (true)
        {
            // 手足を消す
            if (limb != null)
            {
                float a = 1f - Mathf.Clamp01(t / Mathf.Max(0.01f, st.limbFadeSeconds));
                limb.color = new Color(1f, 1f, 1f, a);
                limb.enabled = a > 0f;
            }

            // 起点の光：強まってから少し落ち着く
            float g = 0.5f * Smooth(t / 1.2f) * (1f - 0.3f * Smooth((t - last) / 1.5f)) * (0.88f + 0.12f * Mathf.Sin(t * 6f));
            rootGlow.color = new Color(1f, 0.12f, 0.2f, g);

            // 脈
            if (st.veins)
                foreach (var f in flowers)
                {
                    float prog = Mathf.Clamp01((t - f.veinStart) / (f.veinEnd - f.veinStart));
                    SetVein(f, prog, s);
                }

            // 花
            foreach (var f in flowers)
            {
                float appear = Mathf.Clamp01((t - (f.start - 0.4f)) / 0.4f);
                float p = Mathf.Clamp01((t - f.start) / Mathf.Max(0.01f, st.flowerSeconds));
                int k = Mathf.Clamp(Mathf.RoundToInt(p * (frames.Length - 1)), 0, frames.Length - 1);
                float fill = Smooth(p / 0.32f), open = EaseOut((p - 0.28f) / 0.72f);
                float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f * 1.7f + f.phase)), 8f);
                float pulse = 1f + 0.08f * beat * fill * (1f - open);
                f.img.sprite = frames[k];
                f.img.color = new Color(1, 1, 1, appear);
                f.img.enabled = appear > 0f;
                f.img.rectTransform.sizeDelta = Vector2.one * FrameUnits * 0.5f * f.size * s * Mathf.Lerp(0.6f, 1f, appear) * pulse;
            }

            // 火の粉：咲いている間だけ、咲いた花のまわりから舞い上がる
            foreach (var e in embers)
            {
                float age = t - e.born;
                if (age > e.life && t >= emberFrom && t < emberTo)
                {
                    var src = flowers[rng.Next(flowers.Count)];
                    if (t > src.start)
                    {
                        e.born = t; age = 0f;
                        e.life = Rand(rng, 1.2f, 2.2f);
                        e.from = src.pos + new Vector2(Rand(rng, -35f, 35f), Rand(rng, -25f, 25f));
                        e.size = Rand(rng, 6f, 12f);
                        e.sway = Rand(rng, 0f, 6.28f);
                    }
                }
                if (age >= 0f && age < e.life)
                {
                    float u = age / e.life;
                    Vector2 pos = e.from + new Vector2(12f * Mathf.Sin(u * 7f + e.sway), -60f * u);
                    e.img.rectTransform.anchoredPosition = ToLocal(pos, s);
                    e.img.rectTransform.sizeDelta = Vector2.one * e.size * s;
                    e.img.color = new Color(1f, 0.36f, 0.24f, 0.7f * Mathf.Sin(u * Mathf.PI));
                    e.img.enabled = true;
                }
                else e.img.enabled = false;
            }

            if (t >= end) break;
            yield return null;
            t += Time.unscaledDeltaTime;
        }

        // 咲き終わり：火の粉は消して花と脈だけ残す
        foreach (var e in embers) Object.Destroy(e.img.gameObject);
    }

    static Image NewSeg(RectTransform layer, Vector2 from, float s, float thickness, Color c)
    {
        var img = NewImage(layer, "Vein", null, c);
        var rt = img.rectTransform;
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = ToLocal(from, s);
        rt.sizeDelta = new Vector2(0f, Mathf.Max(1f, thickness * s));
        img.enabled = false;
        return img;
    }

    static void SetVein(Flower f, float prog, float s)
    {
        int n = f.veinCore.Length;
        float total = 0f;
        for (int i = 0; i < n; i++) total += Vector2.Distance(f.vein[i], f.vein[i + 1]);
        float left = prog * total;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = f.vein[i], b = f.vein[i + 1];
            float len = Vector2.Distance(a, b);
            float show = Mathf.Clamp(left, 0f, len);
            left -= len;
            float ang = Mathf.Atan2(-(b.y - a.y), b.x - a.x) * Mathf.Rad2Deg;   // 素材は y 下向き
            foreach (var img in new[] { f.veinCore[i], f.veinGlow[i] })
            {
                img.enabled = show > 0f;
                img.rectTransform.localEulerAngles = new Vector3(0, 0, ang);
                img.rectTransform.sizeDelta = new Vector2(show * s, img.rectTransform.sizeDelta.y);
            }
        }
    }
}