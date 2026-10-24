using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ロジックモード「指定出現」（マスター CSV 2 種から問題を作る）。
///
///   logic_fixedproblem_master : stage, problem, nodecount
///       … ステージの問題構成。その問題で出すノードの数（2つ同時出現も 1 個と数える）
///   logic_fixednode_master    : id, stage, problem(ploblem), nodegroup, nodetype, nodeorder, nodeposition1, nodeposition2, …
///       … 問題ごとのノード。同じ問題の中に nodegroup が複数あれば、そのうち 1 つを抽選して出す
///         nodetype  1=良性 / 2=悪性 / 3=良性(2つ同時出現) / 4=悪性(2つ同時出現)
///         nodeorder 何番目に出るか（同じ番号は同じ時刻に出る）
///         nodeposition 1=左上 2=左中 3=左下 4=右上 5=右中 6=右下（L1〜L3 / R1〜R3 でも可）
///             nodeposition1 から順に見て、出せる基本線が見つかった位置に出す（列はいくつ増やしてもよい）
///         2つ同時出現は連番の 2 行で書く。2 行目の stage / problem / nodegroup は空欄でよい（1 行目を引き継ぐ）
/// </summary>
public class LogicFixedSpawner
{
    public class ProblemDef { public int stage, problem, nodecount; }

    class NodeRow
    {
        public int id, stage, problem, group, type, order;
        public readonly List<int> positions = new List<int>();
    }

    /// <summary>出現 1 体分</summary>
    public struct Spawn
    {
        public int line;      // 基本線の番号（LogicModeScreen の lines の index）
        public bool bad;      // true = 悪性
        public int order;     // 何番目に来るか（2つ同時出現は同じ番号）
        public float time;    // 実行開始から何秒後に出るか
    }

    readonly List<ProblemDef> problems = new List<ProblemDef>();
    readonly List<NodeRow> nodes = new List<NodeRow>();

    /// <summary>位置番号 1〜6 → 基本線の名前</summary>
    public static readonly string[] PositionLineNames =
        { "", "TopLeft", "MiddleLeft", "BottomLeft", "TopRight", "MiddleRight", "BottomRight" };

    // ======================= 読み込み =======================

    /// <summary>node の CSV が読めたときだけ作る（problem の CSV は無くても node から問題を組み立てる）</summary>
    public static LogicFixedSpawner Load(string problemCsv, string nodeCsv)
    {
        if (string.IsNullOrEmpty(nodeCsv)) return null;
        var s = new LogicFixedSpawner();

        // --- ノード ---
        var table = Table(nodeCsv, new[] { "id", "nodetype", "nodeorder" }, "fixednode");
        if (table != null)
        {
            var h = table.header;
            int cId = Col(h, "id"), cStage = Col(h, "stage"), cProb = Col(h, "problem", "ploblem"),
                cGroup = Col(h, "nodegroup"), cType = Col(h, "nodetype"), cOrder = Col(h, "nodeorder");
            var posCols = new List<int>();
            for (int i = 0; i < h.Length; i++)
                if (h[i].StartsWith("nodeposition", System.StringComparison.OrdinalIgnoreCase)) posCols.Add(i);

            int stage = 0, prob = 0, group = 1;
            foreach (var r in table.rows)
            {
                // 空欄は直前の行を引き継ぐ（2つ同時出現の 2 行目など）
                if (Has(r, cStage)) stage = Int(r[cStage]);
                if (Has(r, cProb)) prob = Int(r[cProb]);
                if (Has(r, cGroup)) group = Int(r[cGroup]);

                var n = new NodeRow
                {
                    id = Int(Get(r, cId)),
                    stage = stage,
                    problem = prob,
                    group = group,
                    type = Int(Get(r, cType)),
                    order = Int(Get(r, cOrder))
                };
                foreach (int c in posCols)
                {
                    int p = Position(Get(r, c));
                    if (p > 0) n.positions.Add(p);
                }
                if (n.positions.Count == 0)
                {
                    Debug.LogWarning($"[指定出現:fixednode] id {n.id} に出現位置がありません → 読み飛ばします");
                    continue;
                }
                s.nodes.Add(n);
            }
        }

        // --- 問題 ---
        var pt = string.IsNullOrEmpty(problemCsv) ? null : Table(problemCsv, new[] { "stage", "nodecount" }, "fixedproblem");
        if (pt != null)
        {
            int cStage = Col(pt.header, "stage"), cProb = Col(pt.header, "problem", "ploblem"), cCount = Col(pt.header, "nodecount");
            foreach (var r in pt.rows)
                s.problems.Add(new ProblemDef { stage = Int(Get(r, cStage)), problem = Int(Get(r, cProb)), nodecount = Int(Get(r, cCount)) });
        }
        else
        {
            // problem の CSV が無ければ node から作る
            var seen = new HashSet<(int, int)>();
            foreach (var n in s.nodes)
                if (seen.Add((n.stage, n.problem)))
                    s.problems.Add(new ProblemDef { stage = n.stage, problem = n.problem, nodecount = -1 });
        }
        s.problems.Sort((a, b) => a.stage != b.stage ? a.stage.CompareTo(b.stage) : a.problem.CompareTo(b.problem));

        Debug.Log($"[指定出現] マスター読み込み：問題 {s.problems.Count} 行 / ノード {s.nodes.Count} 行");
        return s.nodes.Count > 0 && s.problems.Count > 0 ? s : null;
    }

    /// <summary>1〜6、または L1〜L3 / R1〜R3（上・中・下）を 1〜6 に</summary>
    static int Position(string v)
    {
        v = v.Trim().ToUpperInvariant();
        if (v.Length == 0) return 0;
        if (v.Length == 2 && (v[0] == 'L' || v[0] == 'R') && v[1] >= '1' && v[1] <= '3')
            return (v[0] == 'L' ? 0 : 3) + (v[1] - '0');
        int p = Int(v);
        return p >= 1 && p <= 6 ? p : 0;
    }

    // ======================= ステージ =======================

    /// <summary>そのステージの問題（problem 順）</summary>
    public List<ProblemDef> ProblemsOf(int stage) => problems.FindAll(p => p.stage == stage);

    /// <summary>
    /// 1 問分の出現を作る。同じ問題にグループが複数あれば 1 つを抽選する。
    /// resolveLine : 位置番号(1〜6) → 基本線の番号。今出せない線なら -1
    /// </summary>
    public List<Spawn> Generate(ProblemDef p, System.Func<int, int> resolveLine, float interval)
    {
        var result = new List<Spawn>();
        var rows = nodes.FindAll(n => n.stage == p.stage && n.problem == p.problem);
        if (rows.Count == 0)
        {
            Debug.LogWarning($"[指定出現] ステージ{p.stage} 問題{p.problem} のノードが fixednode にありません");
            return result;
        }

        // グループの抽選
        var groups = new List<int>();
        foreach (var n in rows) if (!groups.Contains(n.group)) groups.Add(n.group);
        int group = groups[Random.Range(0, groups.Count)];
        rows = rows.FindAll(n => n.group == group);
        rows.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : a.id.CompareTo(b.id));

        // nodecount との確認
        var orders = new HashSet<int>();
        foreach (var n in rows) orders.Add(n.order);
        if (p.nodecount > 0 && orders.Count != p.nodecount)
            Debug.LogWarning($"[指定出現] ステージ{p.stage} 問題{p.problem} グループ{group}：出現順の数 {orders.Count} が nodecount {p.nodecount} と違います");

        var log = new System.Text.StringBuilder();
        foreach (var n in rows)
        {
            // nodeposition1 から順に、出せる基本線を探す
            int line = -1, usedPos = 0;
            foreach (int pos in n.positions)
            {
                line = resolveLine(pos);
                if (line >= 0) { usedPos = pos; break; }
            }
            if (line < 0)
            {
                Debug.LogWarning($"[指定出現] id {n.id}：出現位置 {string.Join("/", n.positions)} がどれも出せないため出しません");
                continue;
            }

            bool bad = n.type == 2 || n.type == 4;
            result.Add(new Spawn { line = line, bad = bad, order = n.order, time = (n.order - 1) * interval });
            log.Append($" {n.order}{(bad ? "赤" : "青")}@{usedPos}");
        }

        Debug.Log($"[指定出現] ステージ{p.stage} 問題{p.problem}（グループ{group}を抽選・{orders.Count}個）：{log}");
        return result;
    }

    // ======================= CSV =======================

    class CsvTable { public string[] header; public List<string[]> rows = new List<string[]>(); }

    /// <summary>必要な列名が全部そろった行を見出しとして、それより下をデータとして読む（上に日本語の見出し行があっても OK）</summary>
    static CsvTable Table(string csv, string[] required, string label)
    {
        var lines = csv.Replace("\r", "").TrimStart('﻿').Split('\n');
        CsvTable t = null;
        foreach (var raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            var cells = line.Split(',');
            for (int i = 0; i < cells.Length; i++) cells[i] = cells[i].Trim().Trim('"', '﻿');

            if (t == null)
            {
                bool all = true;
                foreach (var name in required) if (Col(cells, name) < 0) { all = false; break; }
                if (all) t = new CsvTable { header = cells };
                continue;
            }

            bool empty = true;
            foreach (var c in cells) if (c.Length > 0) { empty = false; break; }
            if (!empty) t.rows.Add(cells);
        }
        if (t == null) Debug.LogWarning($"[指定出現:{label}] 見出し行（{string.Join(", ", required)}）が見つかりません");
        return t;
    }

    static int Col(string[] header, params string[] names)
    {
        foreach (var name in names)
            for (int i = 0; i < header.Length; i++)
                if (string.Equals(header[i], name, System.StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    static bool Has(string[] r, int c) => c >= 0 && c < r.Length && r[c].Length > 0;
    static string Get(string[] r, int c) => c >= 0 && c < r.Length ? r[c] : "";
    static int Int(string s) { int.TryParse(s.Trim(), out int v); return v; }
}
