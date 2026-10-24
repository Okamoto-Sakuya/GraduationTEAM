using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ロジックモード「確率出現」の抽選（マスター CSV 4 種から問題を作る）。
///
///   logic_randomproblem_master : stage, problem(ploblem), nodegroup, nodecount
///       … ステージの問題構成。その問題で出すノード数と、どのノードグループから出すか
///   logic_randomnode_master    : id, nodegroup, nodetype
///       … グループごとのノード。1 回出たノード（id）はそのステージ中は二度と出ない
///         nodetype 1=良性 / 2=悪性 / 3=良性(2つ同時出現) / 4=悪性(2つ同時出現)
///   logic_randomside_master    : id, count, same, opposite
///       … 左右どちらのサイドに出すか。同じサイドに count 回連続したときの確率（「4!」= 4 回以上）
///   logic_randomline_master    : id, count, sameline, differentline
///       … 選んだサイドのどの基本線に出すか。同じ基本線に count 回連続したときの確率
///         別の基本線が 2 本ある場合はどちらも等確率
///
/// 抽選の順番：サイド → 基本線。連続回数はステージを通して数える（問題をまたいでも続く）。
/// 2つ同時出現のノードは、同じ時刻に別々の基本線から出す（2 つ目も同じ抽選で決め、1 つ目と同じ線は除く）。
/// side / line のマスターが無い場合は等確率で抽選する。
/// </summary>
public class LogicRandomSpawner
{
    public class ProblemDef { public int stage, problem, nodegroup, nodecount; }
    public class NodeDef { public int id, nodegroup, nodetype; }
    class StreakRow { public int count; public bool orMore; public float same, other; }

    /// <summary>抽選結果 1 体分</summary>
    public struct Spawn
    {
        public int line;      // 基本線の番号（LogicModeScreen の lines の index）
        public bool bad;      // true = 悪性
        public int order;     // 何番目に来るか（2つ同時出現は同じ番号）
        public float time;    // 実行開始から何秒後に出るか
    }

    readonly List<ProblemDef> problems = new List<ProblemDef>();
    readonly List<NodeDef> nodes = new List<NodeDef>();
    readonly List<StreakRow> sideRows = new List<StreakRow>();
    readonly List<StreakRow> lineRows = new List<StreakRow>();

    // ステージ中の状態
    readonly HashSet<int> usedIds = new HashSet<int>();
    int lastSide = -1, sideStreak;     // 0=左 1=右
    int lastLine = -1, lineStreak;

    public bool HasSideTable => sideRows.Count > 0;
    public bool HasLineTable => lineRows.Count > 0;

    // ======================= 読み込み =======================

    /// <summary>problem と node の CSV が読めたときだけ作る（side / line は無くても可）</summary>
    public static LogicRandomSpawner Load(string problemCsv, string nodeCsv, string sideCsv, string lineCsv)
    {
        if (string.IsNullOrEmpty(problemCsv) || string.IsNullOrEmpty(nodeCsv)) return null;
        var s = new LogicRandomSpawner();

        foreach (var r in Rows(problemCsv, new[] { "stage", "problem|ploblem", "nodegroup", "nodecount" }, "randomproblem"))
            s.problems.Add(new ProblemDef { stage = Int(r[0]), problem = Int(r[1]), nodegroup = Int(r[2]), nodecount = Int(r[3]) });

        foreach (var r in Rows(nodeCsv, new[] { "id", "nodegroup", "nodetype" }, "randomnode"))
            s.nodes.Add(new NodeDef { id = Int(r[0]), nodegroup = Int(r[1]), nodetype = Int(r[2]) });

        if (!string.IsNullOrEmpty(sideCsv))
            foreach (var r in Rows(sideCsv, new[] { "count", "same", "opposite" }, "randomside"))
                s.sideRows.Add(Streak(r));

        if (!string.IsNullOrEmpty(lineCsv))
            foreach (var r in Rows(lineCsv, new[] { "count", "sameline", "differentline" }, "randomline"))
                s.lineRows.Add(Streak(r));

        s.problems.Sort((a, b) => a.stage != b.stage ? a.stage.CompareTo(b.stage) : a.problem.CompareTo(b.problem));
        Debug.Log($"[確率出現] マスター読み込み：問題 {s.problems.Count} 行 / ノード {s.nodes.Count} 行 / " +
                  $"サイド {s.sideRows.Count} 行 / 基本線 {s.lineRows.Count} 行");
        return s.problems.Count > 0 && s.nodes.Count > 0 ? s : null;
    }

    static StreakRow Streak(string[] r)
    {
        string c = r[0].Trim();
        bool orMore = c.EndsWith("!") || c.EndsWith("！");
        return new StreakRow { count = Int(c.TrimEnd('!', '！')), orMore = orMore, same = Float(r[1]), other = Float(r[2]) };
    }

    /// <summary>
    /// CSV から指定列の値を行ごとに返す。列名の行を探して、それより下をデータとして読む
    /// （Excel の表のように上に日本語の見出し行があっても OK）。"a|b" はどちらの列名でも可。
    /// </summary>
    static IEnumerable<string[]> Rows(string csv, string[] cols, string label)
    {
        var lines = csv.Replace("\r", "").TrimStart('﻿').Split('\n');
        int[] idx = null;
        for (int li = 0; li < lines.Length; li++)
        {
            string line = lines[li].Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            var cells = line.Split(',');
            for (int i = 0; i < cells.Length; i++) cells[i] = cells[i].Trim().Trim('"', '﻿');

            if (idx == null)
            {
                var found = new int[cols.Length];
                bool all = true;
                for (int c = 0; c < cols.Length && all; c++)
                {
                    found[c] = -1;
                    foreach (var name in cols[c].Split('|'))
                        for (int i = 0; i < cells.Length && found[c] < 0; i++)
                            if (string.Equals(cells[i], name, System.StringComparison.OrdinalIgnoreCase)) found[c] = i;
                    all = found[c] >= 0;
                }
                if (all) idx = found;
                continue;
            }

            var row = new string[cols.Length];
            bool empty = true;
            for (int c = 0; c < cols.Length; c++)
            {
                row[c] = idx[c] < cells.Length ? cells[idx[c]] : "";
                if (row[c].Length > 0) empty = false;
            }
            if (!empty) yield return row;
        }
        if (idx == null) Debug.LogWarning($"[確率出現:{label}] 見出し行（{string.Join(", ", cols)}）が見つかりません");
    }

    static int Int(string s) { int.TryParse(s.Trim(), out int v); return v; }
    static float Float(string s) { float.TryParse(s.Trim(), out float v); return v; }

    // ======================= ステージ =======================

    /// <summary>そのステージの問題（problem 順）</summary>
    public List<ProblemDef> ProblemsOf(int stage) => problems.FindAll(p => p.stage == stage);

    /// <summary>ステージ開始時に呼ぶ（使用済みノードと連続回数をリセット）</summary>
    public void BeginStage()
    {
        usedIds.Clear();
        lastSide = -1; sideStreak = 0;
        lastLine = -1; lineStreak = 0;
    }

    /// <summary>
    /// 1 問分の出現を抽選する。
    /// leftLines / rightLines : 今ノードを出せる基本線（生きていて出現元になれる線）の番号
    /// </summary>
    public List<Spawn> Generate(ProblemDef p, IList<int> leftLines, IList<int> rightLines, float interval)
    {
        var result = new List<Spawn>();

        // このステージでまだ出ていない、同じグループのノードから nodecount 個を抽選
        var pool = nodes.FindAll(n => n.nodegroup == p.nodegroup && !usedIds.Contains(n.id));
        if (pool.Count < p.nodecount)
            Debug.LogWarning($"[確率出現] ステージ{p.stage} 問題{p.problem}：グループ{p.nodegroup} の残りが {pool.Count} 個しかありません（必要 {p.nodecount} 個）");
        Shuffle(pool);
        int take = Mathf.Min(p.nodecount, pool.Count);

        for (int i = 0; i < take; i++)
        {
            var n = pool[i];
            usedIds.Add(n.id);
            bool bad = n.nodetype == 2 || n.nodetype == 4;
            bool pair = n.nodetype == 3 || n.nodetype == 4;
            float time = i * interval;

            int first = PickLine(leftLines, rightLines, -1);
            if (first < 0) break;   // 出せる線が無い
            result.Add(new Spawn { line = first, bad = bad, order = i + 1, time = time });

            if (pair)
            {
                int second = PickLine(leftLines, rightLines, first);
                if (second >= 0) result.Add(new Spawn { line = second, bad = bad, order = i + 1, time = time });
            }
        }

        string log = string.Join(" ", result.ConvertAll(r => $"{r.order}{(r.bad ? "赤" : "青")}@{r.line}"));
        Debug.Log($"[確率出現] ステージ{p.stage} 問題{p.problem}（グループ{p.nodegroup}・{take}個）：{log}");
        return result;
    }

    // ======================= 抽選 =======================

    /// <summary>サイド → 基本線 の順で抽選。exclude の線は選ばない（2つ同時出現の 2 つ目用）</summary>
    int PickLine(IList<int> leftLines, IList<int> rightLines, int exclude)
    {
        var left = Without(leftLines, exclude);
        var right = Without(rightLines, exclude);
        if (left.Count == 0 && right.Count == 0) return -1;

        // --- サイド ---
        int side;
        if (left.Count == 0) side = 1;
        else if (right.Count == 0) side = 0;
        else if (lastSide < 0 || sideStreak <= 0) side = Random.value < 0.5f ? 0 : 1;   // まだ出ていない
        else
        {
            var row = Lookup(sideRows, sideStreak);
            float same = row != null ? Pct(row.same, row.other) : 0.5f;
            side = Random.value < same ? lastSide : 1 - lastSide;
        }
        if (side == lastSide) sideStreak++;
        else { lastSide = side; sideStreak = 1; }

        // --- 基本線 ---
        var cand = side == 0 ? left : right;
        int line;
        if (lastLine >= 0 && lineStreak > 0 && cand.Contains(lastLine))
        {
            var others = cand.FindAll(l => l != lastLine);
            var row = Lookup(lineRows, lineStreak);
            float same = row != null ? Pct(row.same, row.other) : 1f / cand.Count;
            line = others.Count == 0 || Random.value < same ? lastLine : others[Random.Range(0, others.Count)];
        }
        else line = cand[Random.Range(0, cand.Count)];   // 直前の線がこのサイドに無い → 等確率

        if (line == lastLine) lineStreak++;
        else { lastLine = line; lineStreak = 1; }
        return line;
    }

    /// <summary>連続回数に合う行（同じ count の行、無ければ「〇!」で 〇 以下の行、それも無ければ一番近い行）</summary>
    static StreakRow Lookup(List<StreakRow> rows, int count)
    {
        if (rows.Count == 0) return null;
        StreakRow exact = null, orMore = null, nearest = null;
        foreach (var r in rows)
        {
            if (r.count == count && !r.orMore) exact = r;
            if (r.orMore && count >= r.count && (orMore == null || r.count > orMore.count)) orMore = r;
            if (r.count <= count && (nearest == null || r.count > nearest.count)) nearest = r;
        }
        return exact ?? orMore ?? nearest ?? rows[0];
    }

    /// <summary>same : other を割合に（合計が 0 なら 0.5）</summary>
    static float Pct(float same, float other)
    {
        float sum = same + other;
        return sum > 0f ? same / sum : 0.5f;
    }

    static List<int> Without(IList<int> src, int exclude)
    {
        var list = new List<int>();
        foreach (var v in src) if (v != exclude) list.Add(v);
        return list;
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
