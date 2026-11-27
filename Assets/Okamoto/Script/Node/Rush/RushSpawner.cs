using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

/// <summary>
/// ラッシュの出現抽選。CSV（rush_stage / rush_nodegroup / rush_sidespawn / rush_linespwan）から作る。
///
///   rush_stage_master      : id(=stage), time, level, nodegroup, rate, speed
///                            time = 開始時刻（10 = 1.0 秒）/ rate = 1 分あたりの出現回数 / speed = 10 で等速（15 で 1.5 倍）
///                            （新形式 id, linegauge, level, nodecount, group, rate, speed も可。
///                              time が無いときは「前のレベルの出現ノード数 × 間隔」で次のレベルの開始時刻を計算）
///   rush_nodegroup_master  : id, nodegroup(group), nodetype(type) [, base] [, weight]
///                            base(1 良性 / 2 悪性) 列があればそれ、無ければ nodestatus の hp の符号（＋良性 / －悪性）
///                            1 行 = 抽選の 1 枠（同じ行が多いほど出やすい）。weight 列があればその重み
///   rush_sidespawn_master  : id, count, same, opposite   … 直前と同じ側か反対側かの確率（count = 連続回数、"4!" は 4 回以上）
///   rush_linespwan_master  : id, count, sameline, differentline … 側が決まった後、直前と同じ基本線か別の基本線か
/// </summary>
public class RushSpawner
{
    public const float RateUnitSeconds = 60f;   // rate = 1 分あたりの出現回数
    public const float SpeedUnit = 10f;         // speed 10 = 等速

    public class LevelDef
    {
        public int level;
        public float startTime;    // このレベルが始まる経過秒数
        public float interval;     // 出現間隔（秒）
        public float speedRate;    // 移動速度の倍率
        public int group;          // 使うノードグループ
        public int nodeCount;      // 新形式のみ：このレベルで出すノード数（0 = 不明）
        public float transition = -1f;   // このレベルに入るとき、前のレベルから速度・間隔を変えていく秒数（-1 = 列なし → Inspector の値）
    }

    public class Spawn
    {
        public int line;      // 出現する基本線（lines の番号）
        public bool bad;      // true = 悪性
        public int type;      // nodetype（1 通常 / 2 同時出現 / 3 迅速 …）
    }

    class Entry { public bool bad; public int type; public float weight = 1f; }
    class StreakRow { public int count; public bool orMore; public float same; public float other; }

    public int GroupSize(int group) => groups.TryGetValue(group, out var l) ? l.Count : 0;
    public string GroupSummary(int group)
    {
        if (!groups.TryGetValue(group, out var l)) return "（グループなし）";
        int good = l.Count(e => !e.bad), bad = l.Count - good;
        return $"{l.Count}枠（良性{good}・悪性{bad}）";
    }

    public List<LevelDef> Levels { get; private set; } = new List<LevelDef>();
    public float LineGauge { get; private set; }   // ステージ表の linegauge 列（最初の行の値。無ければ 0）

    readonly Dictionary<int, List<Entry>> groups = new Dictionary<int, List<Entry>>();
    readonly List<StreakRow> sideTable = new List<StreakRow>();
    readonly List<StreakRow> lineTable = new List<StreakRow>();

    // 直前の出現
    int lastSide = -1;          // 0 = 左 / 1 = 右
    int sideStreak;             // 同じ側が続いている回数
    int lastLine = -1;
    int lineStreak;             // 同じ基本線が続いている回数

    // ======================= 読み込み =======================

    /// <param name="nodeStatusCsv">nodestatus_master（base 列が無いときの良性/悪性判定に使う。null 可）</param>
    public static RushSpawner Load(string stageCsv, string nodeGroupCsv, string sideCsv, string lineCsv,
                                   int stageNumber, float defaultLevelSeconds, string nodeStatusCsv = null)
    {
        var r = new RushSpawner();
        r.LoadStatusSign(nodeStatusCsv);
        r.LoadGroups(nodeGroupCsv);
        r.LoadStreak(sideCsv, new[] { "same", "opposite" }, r.sideTable, "rush_sidespawn");
        r.LoadStreak(lineCsv, new[] { "sameline", "differentline" }, r.lineTable, "rush_linespwan");
        r.LoadStages(stageCsv, stageNumber, defaultLevelSeconds);
        return r;
    }

    readonly Dictionary<int, bool> badByType = new Dictionary<int, bool>();

    void LoadStatusSign(string csv)
    {
        var t = RushCsvLoader.ReadTable(csv, new[] { "id", "hp" }, "nodestatus(sign)");
        if (t == null) return;
        int cId = t.Col("id"), cHp = t.Col("hp");
        foreach (var row in t.rows)
        {
            if (!int.TryParse(Cell(row, cId), out int id)) continue;
            if (!TryFloat(Cell(row, cHp), out float hp)) continue;
            badByType[id] = hp < 0f;
        }
    }

    void LoadGroups(string csv)
    {
        var t = RushCsvLoader.ReadTable(csv, new[] { "id" }, "rush_nodegroup");
        if (t != null && t.Col("nodetype", "type") < 0) { Debug.LogWarning("[CSV:rush_nodegroup] nodetype（type）列がありません"); t = null; }
        if (t != null)
        {
            int cGroup = t.Col("nodegroup", "group"), cType = t.Col("nodetype", "type");
            int cBase = t.Col("base"), cWeight = t.Col("weight");
            foreach (var row in t.rows)
            {
                if (!int.TryParse(Cell(row, cGroup), out int g)) continue;
                if (!int.TryParse(Cell(row, cType), out int type)) continue;

                bool bad;
                if (cBase >= 0 && RushNodeTypes.TryParseBase(Cell(row, cBase), out bool b)) bad = b;
                else if (badByType.TryGetValue(type, out bool sb)) bad = sb;
                else bad = false;

                float w = 1f;
                if (cWeight >= 0 && TryFloat(Cell(row, cWeight), out float ww) && ww > 0f) w = ww;

                if (!groups.TryGetValue(g, out var list)) groups[g] = list = new List<Entry>();
                list.Add(new Entry { bad = bad, type = type, weight = w });
            }
        }
        if (groups.Count == 0)
        {
            Debug.LogWarning("[CSV:rush_nodegroup] 読めません → 通常ノード（良性・悪性 半々）で出します");
            groups[1] = new List<Entry>
            {
                new Entry { bad = false, type = RushNodeTypes.Normal },
                new Entry { bad = true, type = RushNodeTypes.Normal },
            };
        }
    }

    void LoadStreak(string csv, string[] cols, List<StreakRow> into, string label)
    {
        var t = RushCsvLoader.ReadTable(csv, new[] { "count", cols[0], cols[1] }, label);
        if (t == null) return;
        int cCount = t.Col("count"), cSame = t.Col(cols[0]), cOther = t.Col(cols[1]);
        foreach (var row in t.rows)
        {
            string c = Cell(row, cCount);
            bool more = c.EndsWith("!") || c.EndsWith("！") || c.EndsWith("+") || c.EndsWith("以上");
            c = c.TrimEnd('!', '！', '+', '以', '上');
            if (!int.TryParse(c, out int n)) continue;
            if (!TryFloat(Cell(row, cSame), out float s) || !TryFloat(Cell(row, cOther), out float o)) continue;
            into.Add(new StreakRow { count = n, orMore = more, same = Mathf.Max(0f, s), other = Mathf.Max(0f, o) });
        }
    }

    void LoadStages(string csv, int stageNumber, float defaultSeconds)
    {
        var t = RushCsvLoader.ReadTable(csv, new[] { "level", "rate", "speed" }, "rush_stage");
        if (t != null)
        {
            int cStage = t.Col("stage", "id"), cTime = t.Col("time", "starttime");
            int cLevel = t.Col("level"), cGroup = t.Col("nodegroup", "group");
            int cRate = t.Col("rate"), cSpeed = t.Col("speed"), cCount = t.Col("nodecount"), cTrans = t.Col("transition", "transitionseconds"), cGauge = t.Col("linegauge");

            var rows = new List<string[]>();
            foreach (var row in t.rows)
                if (int.TryParse(Cell(row, cStage), out int st) && st == stageNumber) rows.Add(row);
            if (rows.Count == 0)   // 該当ステージが無ければ全行
            {
                Debug.LogWarning($"[CSV:rush_stage] ステージ{stageNumber}の行がありません → 全行を使います");
                rows = t.rows;
            }

            if (rows.Count > 0 && cGauge >= 0 && TryFloat(Cell(rows[0], cGauge), out float lg)) LineGauge = lg;
            float cumulative = 0f;   // 新形式：レベルの開始時刻を積み上げる
            int idx = 0;
            foreach (var row in rows)
            {
                if (!TryFloat(Cell(row, cRate), out float rate) || rate <= 0f) continue;
                TryFloat(Cell(row, cSpeed), out float speed);
                int.TryParse(Cell(row, cLevel), out int level);
                int.TryParse(Cell(row, cGroup), out int group);
                int.TryParse(Cell(row, cCount), out int nodeCount);

                float interval = RateUnitSeconds / rate;
                float start;
                if (cTime >= 0 && TryFloat(Cell(row, cTime), out float tm)) start = tm * 0.1f;   // 100 = 10.0 秒
                else if (cCount >= 0) start = cumulative;
                else start = idx * Mathf.Max(0.1f, defaultSeconds);

                Levels.Add(new LevelDef
                {
                    level = level > 0 ? level : idx + 1,
                    startTime = start,
                    interval = interval,
                    speedRate = speed > 0f ? speed / SpeedUnit : 1f,
                    group = group,
                    nodeCount = nodeCount,
                    transition = cTrans >= 0 && TryFloat(Cell(row, cTrans), out float tr) ? tr : -1f,
                });
                cumulative = start + Mathf.Max(0, nodeCount) * interval;
                idx++;
            }
            Levels.Sort((a, b) => a.startTime.CompareTo(b.startTime));
        }

        if (Levels.Count == 0)
        {
            Debug.LogWarning("[CSV:rush_stage] 読めません → 既定のレベル（間隔 6 秒・等速）で出します");
            Levels.Add(new LevelDef { level = 1, startTime = 0f, interval = 6f, speedRate = 1f, group = groups.Keys.First() });
        }
        if (Levels[0].startTime > 0f) Levels[0].startTime = 0f;   // 最初のレベルは 0 秒から
    }

    // ======================= 抽選 =======================

    /// <summary>1 回の出現。同時出現（type 2）なら 2 個返す。leftLines / rightLines は出せる基本線の番号</summary>
    public List<Spawn> Next(LevelDef level, List<int> leftLines, List<int> rightLines)
    {
        var result = new List<Spawn>();
        var entry = PickEntry(level);
        if (entry == null) return result;

        int count = RushNodeTypes.SpawnCount(entry.type);
        var used = new List<int>();   // 同時出現：2 個は別々の基本線に出す
        for (int i = 0; i < count; i++)
        {
            int line = used.Count == 0 ? PickLine(leftLines, rightLines)
                                       : PickLine(Without(leftLines, used), Without(rightLines, used));
            if (line < 0 && used.Count > 0) line = PickLine(leftLines, rightLines);   // 空きが無ければ重なってもよい
            if (line < 0) break;
            used.Add(line);
            result.Add(new Spawn { line = line, bad = entry.bad, type = entry.type });
        }
        return result;
    }

    static List<int> Without(List<int> src, List<int> ex) =>
        src == null ? null : src.Where(l => !ex.Contains(l)).ToList();

    // 1 つのレベルの間は、グループの行を「1 回ずつランダムな順番」で使い切る（使い切ったらまた混ぜ直す）。
    // ＝ グループの行数がそのままそのレベルの出現数になる（行数 10 → 10 個を順番だけランダムに）
    readonly List<Entry> bag = new List<Entry>();
    LevelDef bagLevel;
    public int DrawnInGroup { get; private set; }   // 今のグループ（袋）で何個目か
    public int GroupDrawSize { get; private set; }  // 今の袋の大きさ（グループの行数）

    Entry PickEntry(LevelDef level)
    {
        if (!groups.TryGetValue(level.group, out var list) || list.Count == 0)
        {
            if (groups.Count == 0) return null;
            list = groups.OrderBy(kv => kv.Key).First().Value;   // 指定のグループが無ければ最初のグループ
        }
        if (bagLevel != level) { bag.Clear(); bagLevel = level; }   // レベルが変わったら最初から
        if (bag.Count == 0)
        {
            DrawnInGroup = 0; GroupDrawSize = list.Count;
            bag.AddRange(list);
            for (int i = bag.Count - 1; i > 0; i--)   // シャッフル
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }
        }
        var e = bag[bag.Count - 1];
        bag.RemoveAt(bag.Count - 1);
        DrawnInGroup++;
        return e;
    }

    int PickLine(List<int> left, List<int> right)
    {
        bool hasL = left != null && left.Count > 0, hasR = right != null && right.Count > 0;
        if (!hasL && !hasR) return -1;

        // ① 左右（直前と同じ側にする確率は連続回数で決まる）
        int side;
        if (!hasL) side = 1;
        else if (!hasR) side = 0;
        else if (lastSide < 0) side = UnityEngine.Random.value < 0.5f ? 0 : 1;
        else
        {
            var row = Find(sideTable, sideStreak);
            bool same = Roll(row);
            side = same ? lastSide : 1 - lastSide;
        }
        if (side == lastSide) sideStreak++; else { sideStreak = 1; lastLine = -1; lineStreak = 0; }
        lastSide = side;

        // ② 基本線（その側の中で、直前と同じ線か別の線か）
        var pool = side == 0 ? left : right;
        int line;
        if (pool.Count == 1 || lastLine < 0 || !pool.Contains(lastLine))
            line = pool[UnityEngine.Random.Range(0, pool.Count)];
        else
        {
            var row = Find(lineTable, lineStreak);
            if (Roll(row)) line = lastLine;
            else
            {
                var others = pool.Where(l => l != lastLine).ToList();
                line = others[UnityEngine.Random.Range(0, others.Count)];
            }
        }
        if (line == lastLine) lineStreak++; else lineStreak = 1;
        lastLine = line;
        return line;
    }

    /// <summary>連続回数 n に対応する行。ちょうど一致する行 → 「n!」のうち n 以下で最大のもの → 最後の行</summary>
    static StreakRow Find(List<StreakRow> table, int n)
    {
        if (table.Count == 0) return null;
        foreach (var r in table) if (!r.orMore && r.count == n) return r;
        StreakRow best = null;
        foreach (var r in table) if (r.orMore && r.count <= n && (best == null || r.count > best.count)) best = r;
        return best ?? table[table.Count - 1];
    }

    /// <summary>true = same 側（確率 same : other）。表が無ければ 50:50</summary>
    static bool Roll(StreakRow r)
    {
        if (r == null) return UnityEngine.Random.value < 0.5f;
        float total = r.same + r.other;
        if (total <= 0f) return UnityEngine.Random.value < 0.5f;
        return UnityEngine.Random.value * total < r.same;
    }

    // ======================= 小物 =======================

    static string Cell(string[] row, int c) => c >= 0 && c < row.Length ? row[c].Trim() : "";

    static bool TryFloat(string s, out float v) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
}