using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ロジックモード「確率出現」（マスター CSV 4 種から問題を抽選で作る）。
///
///   logic_randomproblem_master : stage, problem(ploblem), nodegroup, nodecount
///       … ステージの問題構成。その問題で nodegroup から nodecount 個を抽選する（2つ同時出現も 1 個と数える）
///   logic_randomnode_master    : id, nodegroup, nodetype[, nodekind][, nodelimb]
///       … グループごとのノードの抽選箱。1 行 = 1 枚のくじ（同じ種類の行が多いほど出やすい）
///         nodetype  1=良性 / 2=悪性 / 3=良性(2つ同時出現) / 4=悪性(2つ同時出現)
///                   5=四肢対応 / 6=足早 / 7=重い / 8=途中停止
///         nodekind  type5〜8 の良性/悪性（1・良性・青 / 2・悪性・赤）。空欄なら良性
///         nodelimb  type5 の対応部位（右腕・左腕・右足・左足 など）。空欄なら残っている部位から抽選
///   logic_randomside_master    : id, count, same, opposite
///       … 同じ側（左/右）に count 回続けて出たとき、次も同じ側に出る確率 : 反対側に出る確率
///         count 0 の行は最初の 1 個。"4!" のように書いた行は 4 回以上すべてに使う
///   logic_randomline_master    : id, count, sameline, differentline
///       … 同じ基本線に count 回続けて出たとき、次も同じ線 : 同じ側の別の線 の確率
///         （側が変わったときは、その側の線から等確率）
///
/// 連続回数はステージの最初（BeginStage）で 0 に戻り、問題をまたいで続く。
/// </summary>
public class LogicRandomSpawner
{
    public class ProblemDef { public int stage, problem, group, nodecount; }

    class NodeRow
    {
        public int id, group, type;
        public bool bad;
        public string limb = "";
    }

    struct Prob { public int count; public float same, other; }

    /// <summary>出現 1 体分</summary>
    public struct Spawn
    {
        public int line;      // 基本線の番号（LogicModeScreen の lines の index）
        public bool bad;      // true = 悪性
        public int order;     // 何番目に来るか（2つ同時出現は同じ番号）
        public float time;    // 実行開始から何秒後に出るか
        public int type;      // nodetype（1〜8）
        public string limb;   // type5 の対応部位（"RightArm" など。空 = 抽選）
    }

    readonly List<ProblemDef> problems = new List<ProblemDef>();
    readonly List<NodeRow> nodes = new List<NodeRow>();
    readonly List<Prob> sideProbs = new List<Prob>();
    readonly List<Prob> lineProbs = new List<Prob>();

    // 連続回数（ステージ内で続く）
    int lastSide;      // 0 = まだ無し / 1 = 左 / 2 = 右
    int sideStreak;    // 同じ側に何回続けて出たか
    int lastLine = -1;
    int lineStreak;    // 同じ基本線に何回続けて出たか

    // ======================= 読み込み =======================

    /// <summary>problem と node の CSV が読めたときだけ作る（side / line が無ければ 50:50）</summary>
    public static LogicRandomSpawner Load(string problemCsv, string nodeCsv, string sideCsv, string lineCsv)
    {
        if (string.IsNullOrEmpty(problemCsv) || string.IsNullOrEmpty(nodeCsv))
        {
            Debug.LogWarning("[確率出現] randomproblem / randomnode の CSV が見つかりません");
            return null;
        }
        var s = new LogicRandomSpawner();

        // --- 問題 ---
        var pt = Table(problemCsv, new[] { "stage", "nodegroup", "nodecount" }, "randomproblem");
        if (pt != null)
        {
            int cStage = Col(pt.header, "stage"), cProb = Col(pt.header, "problem", "ploblem"),
                cGroup = Col(pt.header, "nodegroup"), cCount = Col(pt.header, "nodecount");
            foreach (var r in pt.rows)
                s.problems.Add(new ProblemDef
                {
                    stage = Int(Get(r, cStage)),
                    problem = Int(Get(r, cProb)),
                    group = Int(Get(r, cGroup)),
                    nodecount = Int(Get(r, cCount))
                });
            s.problems.Sort((a, b) => a.stage != b.stage ? a.stage.CompareTo(b.stage) : a.problem.CompareTo(b.problem));
        }

        // --- ノード（抽選箱） ---
        var nt = Table(nodeCsv, new[] { "nodegroup", "nodetype" }, "randomnode");
        if (nt != null)
        {
            var h = nt.header;
            int cId = Col(h, "id"), cGroup = Col(h, "nodegroup"), cType = Col(h, "nodetype"),
                cKind = Col(h, "nodekind", "kind"), cLimb = Col(h, "nodelimb", "limb");
            foreach (var r in nt.rows)
            {
                var n = new NodeRow { id = Int(Get(r, cId)), group = Int(Get(r, cGroup)), type = Int(Get(r, cType)) };
                if (n.type < 1 || n.type > 8)
                {
                    Debug.LogWarning($"[確率出現:randomnode] id {n.id} の nodetype {Get(r, cType)} は不明 → 良性(1)として扱います");
                    n.type = 1;
                }
                n.bad = n.type == 2 || n.type == 4 || (n.type >= 5 && Kind(Get(r, cKind)) == 2);
                if (n.type == 5) n.limb = Limb(Get(r, cLimb), n.id);
                s.nodes.Add(n);
            }
        }

        // --- 左右・基本線の確率 ---
        ReadProbs(sideCsv, "same", "opposite", "randomside", s.sideProbs);
        ReadProbs(lineCsv, "sameline", "differentline", "randomline", s.lineProbs);

        Debug.Log($"[確率出現] マスター読み込み：問題 {s.problems.Count} 行 / ノード {s.nodes.Count} 行 / 左右 {s.sideProbs.Count} 行 / 基本線 {s.lineProbs.Count} 行");
        return s.problems.Count > 0 && s.nodes.Count > 0 ? s : null;
    }

    static void ReadProbs(string csv, string sameCol, string otherCol, string label, List<Prob> list)
    {
        if (string.IsNullOrEmpty(csv))
        {
            Debug.LogWarning($"[確率出現:{label}] CSV が見つかりません → 常に 50:50 で抽選します");
            return;
        }
        var t = Table(csv, new[] { "count", sameCol, otherCol }, label);
        if (t == null) return;
        int cCount = Col(t.header, "count"), cSame = Col(t.header, sameCol), cOther = Col(t.header, otherCol);
        foreach (var r in t.rows)
        {
            // "4!" などは数字だけ取り出す（それ以上はこの行を使う）
            string digits = "";
            foreach (char ch in Get(r, cCount)) if (char.IsDigit(ch)) digits += ch;
            list.Add(new Prob { count = Int(digits), same = Float(Get(r, cSame)), other = Float(Get(r, cOther)) });
        }
        list.Sort((a, b) => a.count.CompareTo(b.count));
    }

    // ======================= ステージ =======================

    /// <summary>そのステージの問題（problem 順）</summary>
    public List<ProblemDef> ProblemsOf(int stage) => problems.FindAll(p => p.stage == stage);

    /// <summary>ステージ開始時に連続回数をリセット</summary>
    public void BeginStage()
    {
        lastSide = 0; sideStreak = 0;
        lastLine = -1; lineStreak = 0;
    }

    /// <summary>
    /// 1 問分の出現を作る。nodegroup の抽選箱から nodecount 回くじを引き、
    /// 1 個ごとに 左右 → 基本線 の順で出現位置を抽選する。
    /// leftLines / rightLines : 今出せる基本線の番号（左側・右側）
    /// </summary>
    public List<Spawn> Generate(ProblemDef p, List<int> leftLines, List<int> rightLines, float interval)
    {
        var result = new List<Spawn>();
        var pool = nodes.FindAll(n => n.group == p.group);
        if (pool.Count == 0)
        {
            Debug.LogWarning($"[確率出現] ステージ{p.stage} 問題{p.problem}：nodegroup {p.group} のノードが randomnode にありません");
            return result;
        }
        if (leftLines.Count == 0 && rightLines.Count == 0)
        {
            Debug.LogWarning($"[確率出現] ステージ{p.stage} 問題{p.problem}：出せる基本線がありません");
            return result;
        }

        var log = new System.Text.StringBuilder();
        for (int order = 1; order <= p.nodecount; order++)
        {
            var row = pool[Random.Range(0, pool.Count)];
            int count = row.type == 3 || row.type == 4 ? 2 : 1;   // 2つ同時出現
            int first = -1;
            for (int c = 0; c < count; c++)
            {
                int line = PickLine(leftLines, rightLines, first);
                if (line < 0) continue;
                if (c == 0) first = line;
                result.Add(new Spawn
                {
                    line = line,
                    bad = row.bad,
                    order = order,
                    time = (order - 1) * interval,
                    type = row.type,
                    limb = row.limb
                });
                log.Append($" {order}{(row.bad ? "赤" : "青")}{(row.type >= 5 ? "(type" + row.type + ")" : "")}@{(leftLines.Contains(line) ? "L" : "R")}{line}");
            }
        }

        Debug.Log($"[確率出現] ステージ{p.stage} 問題{p.problem}（グループ{p.group}・{p.nodecount}個）：{log}");
        return result;
    }

    /// <summary>左右 → 基本線 の順に抽選。exclude はその線を避ける（2つ同時出現の 2 個目が重ならないように）</summary>
    int PickLine(List<int> left, List<int> right, int exclude)
    {
        // --- 左右 ---
        int side;
        if (lastSide == 0) side = Random.value < 0.5f ? 1 : 2;
        else side = Roll(Lookup(sideProbs, sideStreak)) ? lastSide : 3 - lastSide;
        if (Lines(side, left, right).Count == 0) side = 3 - side;   // その側に線が無ければ反対側
        var list = Lines(side, left, right);
        if (list.Count == 0) return -1;

        // --- 基本線 ---
        var others = list.FindAll(l => l != exclude);
        if (others.Count == 0) others = list;
        int line;
        if (side == lastSide && others.Contains(lastLine))
        {
            var diff = others.FindAll(l => l != lastLine);
            bool same = diff.Count == 0 || Roll(Lookup(lineProbs, lineStreak));
            line = same ? lastLine : diff[Random.Range(0, diff.Count)];
        }
        else line = others[Random.Range(0, others.Count)];

        // --- 連続回数の更新 ---
        sideStreak = side == lastSide ? sideStreak + 1 : 1;
        lineStreak = line == lastLine ? lineStreak + 1 : 1;
        lastSide = side;
        lastLine = line;
        return line;
    }

    static List<int> Lines(int side, List<int> left, List<int> right) => side == 1 ? left : right;

    /// <summary>count 以下で一番大きい行（無ければ 50:50）</summary>
    static Prob Lookup(List<Prob> list, int count)
    {
        var p = new Prob { same = 50, other = 50 };
        foreach (var x in list) if (x.count <= count) p = x;
        return p;
    }

    /// <summary>true = 同じ（same の確率）</summary>
    static bool Roll(Prob p)
    {
        float total = p.same + p.other;
        if (total <= 0f) return Random.value < 0.5f;
        return Random.value * total < p.same;
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
        if (t == null) Debug.LogWarning($"[確率出現:{label}] 見出し行（{string.Join(", ", required)}）が見つかりません");
        return t;
    }

    static int Col(string[] header, params string[] names)
    {
        foreach (var name in names)
            for (int i = 0; i < header.Length; i++)
                if (string.Equals(header[i], name, System.StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    static string Get(string[] r, int c) => c >= 0 && c < r.Length ? r[c] : "";
    static int Int(string s) { int.TryParse(s.Trim(), out int v); return v; }
    static float Float(string s)
    {
        float.TryParse(s.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v);
        return v;
    }

    /// <summary>nodekind：1・良性・青・good → 1 / 2・悪性・赤・bad → 2 / 空欄・不明 → 0</summary>
    static int Kind(string v)
    {
        v = v.Trim();
        if (v.Length == 0) return 0;
        if (v == "1") return 1;
        if (v == "2") return 2;
        return LogicCsvLoader.TryParseKind(v, out var k) ? (k == ProtoNode.Kind.Bad ? 2 : 1) : 0;
    }

    /// <summary>nodelimb → 部位名（空欄・不明は "" ＝抽選）</summary>
    static string Limb(string v, int id)
    {
        switch (v.Trim().ToUpperInvariant())
        {
            case "": return "";
            case "1": case "右腕": case "RA": case "RIGHTARM": return "RightArm";
            case "2": case "左腕": case "LA": case "LEFTARM": return "LeftArm";
            case "3": case "右足": case "RL": case "RIGHTLEG": return "RightLeg";
            case "4": case "左足": case "LL": case "LEFTLEG": return "LeftLeg";
        }
        Debug.LogWarning($"[確率出現:randomnode] id {id} の nodelimb \"{v}\" は不明 → 抽選にします");
        return "";
    }
}