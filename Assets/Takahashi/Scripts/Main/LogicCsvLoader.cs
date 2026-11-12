using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>ロジックモードの問題CSV 1行分（＝ノード1個）… 旧形式 logic_problems.csv 用</summary>
public class CsvProblemNode
{
    public int problem;
    public int order;
    public string line;
    public ProtoNode.Kind kind;
    public float gauge = -1f;   // 出現時ゲージ（空欄なら nodeStartGauge）
    public float time = -1f;    // 実行開始から何秒後に出るか（空欄なら 順番×spawnInterval）
}

/// <summary>
/// ノードの種類（nodetype）… メイン共通仕様（11/12 版）
///   1 通常 / 2 同時出現 / 3 迅速 / 4 重圧 / 5 停滞
///   6 帰属(左腕) / 7 帰属(右腕) / 8 帰属(左足) / 9 帰属(右足)
///   10 交錯 / 11 造線
/// 良性・悪性は nodetype ではなく base 列（1 = 良性 / 2 = 悪性）で決まる。
/// </summary>
public static class LogicNodeTypes
{
    public const int Normal = 1, Double = 2, Fast = 3, Heavy = 4, Stop = 5;
    public const int LimbLeftArm = 6, LimbRightArm = 7, LimbLeftLeg = 8, LimbRightLeg = 9;
    public const int Cross = 10, AddLine = 11;
    public const int Max = 11;

    public static bool IsValid(int t) => t >= 1 && t <= Max;
    public static bool IsLimb(int t) => t >= LimbLeftArm && t <= LimbRightLeg;

    /// <summary>帰属ノードの対応部位（LogicModeScreen の Limb.name）。帰属以外は ""</summary>
    public static string LimbOf(int t) => t switch
    {
        LimbLeftArm => "LeftArm",
        LimbRightArm => "RightArm",
        LimbLeftLeg => "LeftLeg",
        LimbRightLeg => "RightLeg",
        _ => "",
    };

    /// <summary>1 回の抽選で何個出るか（同時出現は 2 個）</summary>
    public static int SpawnCount(int t) => t == Double ? 2 : 1;

    public static string Name(int t) => t switch
    {
        Normal => "通常",
        Double => "同時出現",
        Fast => "迅速",
        Heavy => "重圧",
        Stop => "停滞",
        LimbLeftArm => "帰属(左腕)",
        LimbRightArm => "帰属(右腕)",
        LimbLeftLeg => "帰属(左足)",
        LimbRightLeg => "帰属(右足)",
        Cross => "交錯",
        AddLine => "造線",
        _ => "type" + t,
    };

    /// <summary>base 列：1・良性・青・good → 良性 / 2・悪性・赤・bad → 悪性。読めたら true</summary>
    public static bool TryParseBase(string v, out bool bad)
    {
        bad = false;
        v = (v ?? "").Trim();
        if (v.Length == 0) return false;
        if (v == "1") return true;
        if (v == "2") { bad = true; return true; }
        if (LogicCsvLoader.TryParseKind(v, out var k)) { bad = k == ProtoNode.Kind.Bad; return true; }
        return false;
    }
}

/// <summary>
/// nodestatus_master の 1 行（ノードの種類ごとのステータス）。
/// ゲージ（属性値）は −100〜+100。0 以上が良性、マイナスが悪性。レベルは絶対値で決まる。
/// </summary>
public class NodeStatus
{
    public int id;
    public float hp = 100f;       // 出現時のゲージの大きさ（良性なら +hp、悪性なら −hp で出る）
    public float value = 10f;     // 1 秒あたりの増減量
    public float[] goodMin = { 0, 25, 50, 75 };   // 良性 Lv1〜4 の下限
    public float[] badMin = { 1, 25, 50, 75 };    // 悪性 Lv1〜4 の下限（絶対値。lv-1min = -1 → 1）
    public int[] goodScore = { 1000, 1500, 2000, 3000 };
    public int[] badScore = { 50, 200, 350, 500 };
    public float[] goodDamage = { 0, 0, 0, 0 };
    public float[] badDamage = { 5, 7, 10, 15 };
    public float[] goodSan = { 0, 0, 0, 0 };
    public float[] badSan = { 3, 5, 8, 10 };

    public int Score(bool bad, int lv) => (bad ? badScore : goodScore)[Mathf.Clamp(lv, 1, 4) - 1];
    public float Damage(bool bad, int lv) => (bad ? badDamage : goodDamage)[Mathf.Clamp(lv, 1, 4) - 1];
    public float San(bool bad, int lv) => (bad ? badSan : goodSan)[Mathf.Clamp(lv, 1, 4) - 1];
}

/// <summary>
/// CSV の読み込みと LogicModeScreen への反映。
///   置き場所（CSVの置き方 仕様）… Resources/MasterData/Main/Common（共通）・Resources/MasterData/Main/Logic（ロジックのみ）
///     {ファイル名}_debug があれば、useDebugCsv のときそちらを優先
///   nodestatus_master   : id, hp, value, lv〇min/max, lv〇score, lv〇damage, lv〇san … ノードの種類ごとのステータス
///   playerstatus_master : id, value … 1 左腕hp / 2 左足hp / 3 右腕hp / 4 右足hp / 5 最大SAN / 6 造線ゲージ最大量 / 7 1m毎の造線消費量
///   miscparams_master   : key, value … LogicModeScreen の public 変数名をキーに（形式が決まったら合わせる）
///   logic_params.csv    : key, value … 旧形式の参照数値
///   node_table.csv      : kind, level, damage, san, score … 旧形式のレベル表
/// どの表も「必要な列名がそろった行」を見出しとして読むので、上に日本語の見出し行があっても OK。
/// </summary>
public static class LogicCsvLoader
{
    // ======================= ファイルを探す =======================

    /// <summary>
    /// CSV の中身を探して返す（無ければ null）。順番：
    ///   ① Resources/{folder}/{名前}_debug（preferDebug のとき）
    ///   ② Resources/{folder}/{名前}
    ///   ③ StreamingAssets/{ファイル名}（allowStreaming のとき）
    ///   ④ Inspector の TextAsset
    /// </summary>
    public static string LoadText(TextAsset asset, string fileName, bool allowStreaming, string resourcesFolder = null, bool preferDebug = false)
    {
        string label = string.IsNullOrEmpty(fileName) ? (asset != null ? asset.name : "?") : fileName;
        string baseName = string.IsNullOrEmpty(fileName) ? "" : Path.GetFileNameWithoutExtension(fileName);

        if (!string.IsNullOrEmpty(resourcesFolder) && baseName != "")
        {
            string folder = resourcesFolder.Trim('/');
            if (preferDebug)
            {
                var dbg = Resources.Load<TextAsset>(folder + "/" + baseName + "_debug");
                if (dbg != null) { Debug.Log($"[CSV] {label} ← Resources/{folder}/{baseName}_debug"); return dbg.text; }
            }
            var res = Resources.Load<TextAsset>(folder + "/" + baseName);
            if (res != null) { Debug.Log($"[CSV] {label} ← Resources/{folder}/{baseName}"); return res.text; }
        }

        if (allowStreaming && baseName != "")
        {
            string path = Path.Combine(Application.streamingAssetsPath, fileName);
            if (File.Exists(path)) { Debug.Log($"[CSV] {label} ← StreamingAssets"); return File.ReadAllText(path, Encoding.UTF8); }
        }

        if (asset != null) { Debug.Log($"[CSV] {label} ← TextAsset {asset.name}"); return asset.text; }
        return null;
    }

    // ======================= 見出し行を探して読む表 =======================

    public class Table
    {
        public string[] header;
        public readonly List<string[]> rows = new List<string[]>();

        /// <summary>列番号（どれかの名前に一致した列。無ければ -1）</summary>
        public int Col(params string[] names)
        {
            foreach (var name in names)
                for (int i = 0; i < header.Length; i++)
                    if (string.Equals(header[i], name, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        public static string Get(string[] r, int c) => c >= 0 && c < r.Length ? r[c].Trim() : "";
    }

    /// <summary>必要な列名が全部そろった行を見出しとして、それより下をデータとして読む</summary>
    public static Table ReadTable(string csv, string[] required, string label)
    {
        if (string.IsNullOrEmpty(csv)) return null;
        Table t = null;
        foreach (var row in CsvUtil.Parse(csv))
        {
            var cells = row.Select(c => c.Trim().Trim('﻿')).ToArray();
            if (t == null)
            {
                var probe = new Table { header = cells };
                if (required.All(n => probe.Col(n) >= 0)) t = probe;
                continue;
            }
            if (cells.Any(c => c.Length > 0)) t.rows.Add(cells);
        }
        if (t == null) Debug.LogWarning($"[CSV:{label}] 見出し行（{string.Join(", ", required)}）が見つかりません");
        return t;
    }

    // ======================= nodestatus_master =======================

    /// <summary>id（nodetype）→ ステータス</summary>
    public static Dictionary<int, NodeStatus> LoadNodeStatus(string csv)
    {
        var result = new Dictionary<int, NodeStatus>();
        var t = ReadTable(csv, new[] { "id", "hp", "value" }, "nodestatus");
        if (t == null) return result;

        int cId = t.Col("id"), cHp = t.Col("hp"), cValue = t.Col("value");
        foreach (var r in t.rows)
        {
            if (!int.TryParse(Table.Get(r, cId), out int id)) continue;
            var s = new NodeStatus { id = id };
            s.hp = Mathf.Abs(ParseFloat(Table.Get(r, cHp), s.hp));
            s.value = Mathf.Abs(ParseFloat(Table.Get(r, cValue), s.value));
            for (int lv = 1; lv <= 4; lv++)
            {
                int i = lv - 1;
                s.goodMin[i] = Mathf.Abs(ParseFloat(Table.Get(r, t.Col($"lv{lv}min")), s.goodMin[i]));
                s.badMin[i] = Mathf.Abs(ParseFloat(Table.Get(r, t.Col($"lv-{lv}min")), s.badMin[i]));
                s.goodScore[i] = ParseInt(Table.Get(r, t.Col($"lv{lv}score")), s.goodScore[i]);
                s.badScore[i] = ParseInt(Table.Get(r, t.Col($"lv-{lv}score")), s.badScore[i]);
                s.goodDamage[i] = ParseFloat(Table.Get(r, t.Col($"lv{lv}damage")), s.goodDamage[i]);
                s.badDamage[i] = ParseFloat(Table.Get(r, t.Col($"lv-{lv}damage")), s.badDamage[i]);
                s.goodSan[i] = ParseFloat(Table.Get(r, t.Col($"lv{lv}san")), s.goodSan[i]);
                s.badSan[i] = ParseFloat(Table.Get(r, t.Col($"lv-{lv}san")), s.badSan[i]);
            }
            result[id] = s;
        }
        Debug.Log($"[CSV:nodestatus] {result.Count} 種類を読み込み（id {string.Join(",", result.Keys)}）");
        return result;
    }

    // ======================= playerstatus_master =======================

    /// <summary>id → LogicModeScreen の変数名</summary>
    static readonly Dictionary<int, string> PlayerStatusKeys = new Dictionary<int, string>
    {
        { 1, nameof(LogicModeScreen.hpLeftArm) },
        { 2, nameof(LogicModeScreen.hpLeftLeg) },
        { 3, nameof(LogicModeScreen.hpRightArm) },
        { 4, nameof(LogicModeScreen.hpRightLeg) },
        { 5, nameof(LogicModeScreen.maxSan) },
        { 6, nameof(LogicModeScreen.maxLineGauge) },
        { 7, nameof(LogicModeScreen.costPerMeter) },
    };

    public static void ApplyPlayerStatus(LogicModeScreen s, string csv)
    {
        var t = ReadTable(csv, new[] { "id", "value" }, "playerstatus");
        if (t == null) return;
        int cId = t.Col("id"), cValue = t.Col("value"), applied = 0;
        foreach (var r in t.rows)
        {
            if (!int.TryParse(Table.Get(r, cId), out int id)) continue;
            if (!PlayerStatusKeys.TryGetValue(id, out var key))
            {
                Debug.LogWarning($"[CSV:playerstatus] 不明な id: {id}");
                continue;
            }
            if (SetField(s, key, Table.Get(r, cValue), "playerstatus")) applied++;
        }
        Debug.Log($"[CSV:playerstatus] {applied} 項目を反映");
    }

    // ======================= key,value 形式（logic_params / miscparams） =======================

    /// <summary>key 列と同じ名前の public 変数に value を入れる（float / int / bool / enum / string）</summary>
    public static void ApplyParams(object target, string csv, string sourceName = "params")
    {
        var t = ReadTable(csv, new[] { "key", "value" }, sourceName);
        if (t == null) return;
        int cKey = t.Col("key"), cValue = t.Col("value"), applied = 0;
        foreach (var r in t.rows)
        {
            string key = Table.Get(r, cKey);
            if (key == "") continue;
            if (SetField(target, key, Table.Get(r, cValue), sourceName)) applied++;
        }
        Debug.Log($"[CSV:{sourceName}] {applied} 項目を反映");
    }

    static bool SetField(object target, string key, string value, string sourceName)
    {
        var field = target.GetType().GetField(key, BindingFlags.Public | BindingFlags.Instance);
        if (field == null) { Debug.LogWarning($"[CSV:{sourceName}] 不明なキー: {key}"); return false; }
        try
        {
            field.SetValue(target, Convert(value, field.FieldType));
            return true;
        }
        catch (Exception)
        {
            Debug.LogWarning($"[CSV:{sourceName}] {key} の値 \"{value}\" を {field.FieldType.Name} に変換できません");
            return false;
        }
    }

    static object Convert(string s, Type t)
    {
        if (t == typeof(float)) return float.Parse(s, CultureInfo.InvariantCulture);
        if (t == typeof(int)) return int.Parse(s, CultureInfo.InvariantCulture);
        if (t == typeof(bool)) return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        if (t == typeof(string)) return s;
        if (t.IsEnum) return Enum.Parse(t, s, true);
        throw new NotSupportedException();
    }

    // ======================= 旧形式：ノードのレベル表 =======================

    public static void ApplyNodeTable(LogicModeScreen s, string csv)
    {
        int applied = 0;
        foreach (var row in CsvUtil.ParseWithHeader(csv))
        {
            if (!TryParseKind(row.Get("kind"), out var kind)) continue;
            if (!int.TryParse(row.Get("level"), out int lv) || lv < 1 || lv > 4) continue;
            int i = lv - 1;

            int score = ParseInt(row.Get("score", "0"), 0);
            if (kind == ProtoNode.Kind.Good)
            {
                s.goodScore[i] = score;
            }
            else
            {
                s.badScore[i] = score;
                s.badDamage[i] = ParseFloat(row.Get("damage", "0"), 0f);
                s.badSan[i] = ParseFloat(row.Get("san", "0"), 0f);
            }
            applied++;
        }
        Debug.Log($"[CSV:node_table] {applied} 行を反映");
    }

    // ======================= 旧形式：問題データ =======================

    /// <summary>problem 番号ごとにまとめ、order 順に並べて返す</summary>
    public static List<List<CsvProblemNode>> LoadProblems(string csv)
    {
        var all = new List<CsvProblemNode>();
        int lineNo = 1;
        foreach (var row in CsvUtil.ParseWithHeader(csv))
        {
            lineNo++;
            if (!int.TryParse(row.Get("problem"), out int p)) continue;
            if (!TryParseKind(row.Get("kind"), out var kind))
            {
                Debug.LogWarning($"[CSV:problems] {lineNo}行目 kind が不正: {row.Get("kind")}");
                continue;
            }
            all.Add(new CsvProblemNode
            {
                problem = p,
                order = ParseInt(row.Get("order", "0"), 0),
                line = row.Get("line"),
                kind = kind,
                gauge = row.Get("gauge") == "" ? -1f : ParseFloat(row.Get("gauge"), -1f),
                time = row.Get("time") == "" ? -1f : ParseFloat(row.Get("time"), -1f),
            });
        }

        var result = all.GroupBy(n => n.problem)
                        .OrderBy(g => g.Key)
                        .Select(g => g.OrderBy(n => n.order).ToList())
                        .ToList();
        Debug.Log($"[CSV:problems] {result.Count} 問 / ノード {all.Count} 個を読み込み");
        return result;
    }

    // ======================= 共通 =======================

    public static bool TryParseKind(string s, out ProtoNode.Kind kind)
    {
        switch ((s ?? "").Trim().ToLowerInvariant())
        {
            case "good":
            case "g":
            case "blue":
            case "青":
            case "良性":
                kind = ProtoNode.Kind.Good; return true;
            case "bad":
            case "b":
            case "red":
            case "赤":
            case "悪性":
                kind = ProtoNode.Kind.Bad; return true;
        }
        kind = ProtoNode.Kind.Good;
        return false;
    }

    static int ParseInt(string s, int fallback) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;
    static float ParseFloat(string s, float fallback) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;
}