using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

/// <summary>
/// ラッシュ用の出現抽選。
///   ステージ表   : stage, (level), (starttime), (interval), (speed), (nodegroup)  … レベルごとの設定
///   グループ表   : nodegroup, nodetype, (base), (weight)                          … グループの中身
///   左右表       : count, same, opposite                                          … 同じ側が続いた回数ごとの確率
///   基本線表     : count, sameline, differentline                                 … 同じ線が続いた回数ごとの確率
/// 表が無い・読めないときは、内蔵の初期値で動きます。
/// </summary>
public class RushSpawner
{
    public class LevelDef
    {
        public int level = 1;
        public float startTime = 0f;      // このレベルが始まる経過秒
        public float interval = 3f;       // 出現間隔（秒）
        public float speedRate = 1f;      // 移動速度の倍率
        public List<int> groups = new List<int>();   // 抽選するグループ（空なら全グループ）
    }

    public struct Spawn { public int line; public int type; public bool bad; }

    class NodeDef { public int group, type = 1; public int bad = -1; public float weight = 1f; }   // bad: -1 = 指定なし / 0 良性 / 1 悪性
    class Prob { public int count; public float a, b; }

    public readonly List<LevelDef> Levels = new List<LevelDef>();
    readonly List<NodeDef> nodes = new List<NodeDef>();
    readonly List<Prob> sideProbs = new List<Prob>();
    readonly List<Prob> lineProbs = new List<Prob>();

    int lastSide = -1, sideStreak;   // 0 左 / 1 右
    int lastLine = -1, lineStreak;

    // ======================= 読み込み =======================

    public static RushSpawner Load(string stageCsv, string nodeCsv, string sideCsv, string lineCsv,
                                   int stage, float defaultLevelSeconds)
    {
        var s = new RushSpawner();
        s.LoadNodes(nodeCsv);
        s.LoadProbs(sideCsv, s.sideProbs, new[] { "same" }, new[] { "opposite" }, "rush_side");
        s.LoadProbs(lineCsv, s.lineProbs, new[] { "sameline", "same" }, new[] { "differentline", "different" }, "rush_line");
        s.LoadLevels(stageCsv, stage, defaultLevelSeconds);
        Debug.Log($"[ラッシュ出現] レベル {s.Levels.Count} / ノード定義 {s.nodes.Count} / 左右表 {s.sideProbs.Count} / 基本線表 {s.lineProbs.Count}");
        return s;
    }

    static float F(string v, float fallback)
    {
        v = (v ?? "").Trim().TrimEnd('%');
        return float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : fallback;
    }

    void LoadNodes(string csv)
    {
        var t = LogicCsvLoader.ReadTable(csv, new[] { "nodegroup", "nodetype" }, "rush_nodegroup");
        if (t == null) return;
        int cG = t.Col("nodegroup"), cT = t.Col("nodetype"), cB = t.Col("base"), cW = t.Col("weight", "rate", "probability");
        foreach (var r in t.rows)
        {
            if (!int.TryParse(LogicCsvLoader.Table.Get(r, cG), out int g)) continue;
            if (!int.TryParse(LogicCsvLoader.Table.Get(r, cT), out int type) || !LogicNodeTypes.IsValid(type)) continue;
            var d = new NodeDef { group = g, type = type, weight = Mathf.Max(0f, F(LogicCsvLoader.Table.Get(r, cW), 1f)) };
            if (cB >= 0 && LogicNodeTypes.TryParseBase(LogicCsvLoader.Table.Get(r, cB), out bool bad)) d.bad = bad ? 1 : 0;
            nodes.Add(d);
        }
    }

    void LoadProbs(string csv, List<Prob> list, string[] aNames, string[] bNames, string label)
    {
        var t = LogicCsvLoader.ReadTable(csv, new[] { "count" }, label);
        if (t == null) return;
        int cC = t.Col("count"), cA = t.Col(aNames), cB = t.Col(bNames);
        foreach (var r in t.rows)
        {
            if (!int.TryParse(LogicCsvLoader.Table.Get(r, cC), out int c)) continue;
            list.Add(new Prob { count = c, a = F(LogicCsvLoader.Table.Get(r, cA), 50f), b = F(LogicCsvLoader.Table.Get(r, cB), 50f) });
        }
        list.Sort((x, y) => x.count.CompareTo(y.count));
    }

    void LoadLevels(string csv, int stage, float defaultLevelSeconds)
    {
        var t = LogicCsvLoader.ReadTable(csv, new[] { "stage" }, "rush_stage");
        if (t != null)
        {
            int cS = t.Col("stage"), cL = t.Col("level", "lv"), cT = t.Col("starttime", "start", "time"),
                cI = t.Col("interval", "spawninterval"), cV = t.Col("speed", "speedrate", "nodespeed"),
                cG = t.Col("nodegroup", "group", "nodegroups");
            foreach (var r in t.rows)
            {
                if (!int.TryParse(LogicCsvLoader.Table.Get(r, cS), out int st) || st != stage) continue;
                var d = new LevelDef();
                d.level = int.TryParse(LogicCsvLoader.Table.Get(r, cL), out int lv) ? lv : Levels.Count + 1;
                d.startTime = F(LogicCsvLoader.Table.Get(r, cT), Levels.Count * defaultLevelSeconds);
                d.interval = Mathf.Max(0.2f, F(LogicCsvLoader.Table.Get(r, cI), 3f));
                d.speedRate = Mathf.Max(0.1f, F(LogicCsvLoader.Table.Get(r, cV), 1f));
                foreach (var part in LogicCsvLoader.Table.Get(r, cG).Split('|', ';', ',', '/', ' '))
                    if (int.TryParse(part.Trim(), out int g)) d.groups.Add(g);
                Levels.Add(d);
            }
        }
        if (Levels.Count == 0)
        {
            Debug.LogWarning("[ラッシュ出現] ステージ表が読めないので、内蔵の5レベルで動かします");
            float[] iv = { 3.0f, 2.6f, 2.2f, 1.8f, 1.5f };
            float[] sp = { 1.00f, 1.00f, 1.12f, 1.20f, 1.20f };
            for (int i = 0; i < 5; i++)
                Levels.Add(new LevelDef { level = i + 1, startTime = i * defaultLevelSeconds, interval = iv[i], speedRate = sp[i] });
        }
        Levels.Sort((x, y) => x.startTime.CompareTo(y.startTime));
    }

    // ======================= 抽選 =======================

    /// <summary>1 回の出現。同時出現(type2)のときは 2 個返す</summary>
    public List<Spawn> Next(LevelDef lv, List<int> left, List<int> right)
    {
        var result = new List<Spawn>();
        if (left.Count == 0 && right.Count == 0) return result;

        var def = PickNode(lv);
        bool bad = def.bad >= 0 ? def.bad == 1 : Random.value < 0.5f;

        int side = PickSide(left.Count > 0, right.Count > 0);
        var cand = side == 0 ? left : right;
        int line = PickLine(cand, side == lastSide);
        Commit(side, line);
        result.Add(new Spawn { line = line, type = def.type, bad = bad });

        if (def.type == LogicNodeTypes.Double)
        {
            var pool = left.Concat(right).Where(x => x != line).ToList();
            if (pool.Count > 0) result.Add(new Spawn { line = pool[Random.Range(0, pool.Count)], type = def.type, bad = bad });
        }
        return result;
    }

    /// <summary>グループを 1 つ選び、その中から重みつきでノードを 1 つ選ぶ</summary>
    NodeDef PickNode(LevelDef lv)
    {
        if (nodes.Count == 0) return new NodeDef();   // 表が無いときは通常ノード

        var groups = lv.groups.Where(g => nodes.Any(n => n.group == g)).ToList();
        if (groups.Count == 0) groups = nodes.Select(n => n.group).Distinct().ToList();
        int group = groups[Random.Range(0, groups.Count)];

        var inGroup = nodes.Where(n => n.group == group).ToList();
        float sum = inGroup.Sum(n => n.weight);
        if (sum <= 0f) return inGroup[Random.Range(0, inGroup.Count)];
        float roll = Random.value * sum;
        foreach (var n in inGroup) { roll -= n.weight; if (roll <= 0f) return n; }
        return inGroup[inGroup.Count - 1];
    }

    static Prob Find(List<Prob> list, int streak)
    {
        if (list.Count == 0) return null;
        Prob best = list[0];
        foreach (var p in list) if (p.count <= streak) best = p;
        return best;
    }

    static bool Roll(float a, float b)
    {
        float sum = a + b;
        return sum <= 0f ? Random.value < 0.5f : Random.value * sum < a;
    }

    int PickSide(bool leftOk, bool rightOk)
    {
        if (!leftOk) return 1;
        if (!rightOk) return 0;
        var p = Find(sideProbs, sideStreak);
        if (lastSide < 0 || p == null) return Random.value < 0.5f ? 0 : 1;
        return Roll(p.a, p.b) ? lastSide : 1 - lastSide;
    }

    int PickLine(List<int> cand, bool sameSide)
    {
        if (cand.Count == 1) return cand[0];
        var p = Find(lineProbs, lineStreak);
        if (sameSide && lastLine >= 0 && cand.Contains(lastLine) && p != null)
        {
            if (Roll(p.a, p.b)) return lastLine;
            var others = cand.Where(x => x != lastLine).ToList();
            return others[Random.Range(0, others.Count)];
        }
        return cand[Random.Range(0, cand.Count)];
    }

    void Commit(int side, int line)
    {
        sideStreak = side == lastSide ? sideStreak + 1 : 1;
        lastSide = side;
        lineStreak = line == lastLine ? lineStreak + 1 : 1;
        lastLine = line;
    }
}