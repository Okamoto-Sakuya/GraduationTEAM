using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>ロジックモードの問題CSV 1行分（＝ノード1個）</summary>
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
/// CSV の読み込みと LogicModeScreen への反映。
///   logic_params.csv   : key,value       … 参照数値（LogicModeScreen の public 変数名をそのままキーに）
///   node_table.csv     : kind,level,damage,san,score … ノードのレベル表
///   logic_problems.csv : problem,order,line,kind[,gauge][,time] … 問題データ
/// </summary>
public static class LogicCsvLoader
{
    /// <summary>StreamingAssets のファイル → TextAsset の順で探す。どちらも無ければ null</summary>
    public static string LoadText(TextAsset asset, string streamingFileName, bool preferStreaming)
    {
        if (preferStreaming && !string.IsNullOrEmpty(streamingFileName))
        {
            string path = Path.Combine(Application.streamingAssetsPath, streamingFileName);
            if (File.Exists(path)) return File.ReadAllText(path, Encoding.UTF8);
        }
        return asset != null ? asset.text : null;
    }

    // ---------------- 参照数値 ----------------

    /// <summary>key 列と同じ名前の public 変数に value を入れる（float / int / bool / enum / string）</summary>
    public static void ApplyParams(object target, string csv, string sourceName = "params")
    {
        var type = target.GetType();
        int applied = 0;
        foreach (var row in CsvUtil.ParseWithHeader(csv))
        {
            string key = row.Get("key");
            string value = row.Get("value");
            if (key == "") continue;

            var field = type.GetField(key, BindingFlags.Public | BindingFlags.Instance);
            if (field == null) { Debug.LogWarning($"[CSV:{sourceName}] 不明なキー: {key}"); continue; }

            try
            {
                field.SetValue(target, Convert(value, field.FieldType));
                applied++;
            }
            catch (Exception)
            {
                Debug.LogWarning($"[CSV:{sourceName}] {key} の値 \"{value}\" を {field.FieldType.Name} に変換できません");
            }
        }
        Debug.Log($"[CSV:{sourceName}] {applied} 項目を反映");
    }

    static object Convert(string s, Type t)
    {
        if (t == typeof(float))  return float.Parse(s, CultureInfo.InvariantCulture);
        if (t == typeof(int))    return int.Parse(s, CultureInfo.InvariantCulture);
        if (t == typeof(bool))   return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        if (t == typeof(string)) return s;
        if (t.IsEnum)            return Enum.Parse(t, s, true);
        throw new NotSupportedException();
    }

    // ---------------- ノードのレベル表 ----------------

    public static void ApplyNodeTable(LogicModeScreen s, string csv)
    {
        int applied = 0;
        foreach (var row in CsvUtil.ParseWithHeader(csv))
        {
            if (!TryParseKind(row.Get("kind"), out var kind)) continue;
            if (!int.TryParse(row.Get("level"), out int lv) || lv < 1 || lv > 4) continue;
            int i = lv - 1;

            int score = ParseInt(row.Get("score", "0"));
            if (kind == ProtoNode.Kind.Good)
            {
                s.goodScore[i] = score;
            }
            else
            {
                s.badScore[i] = score;
                s.badDamage[i] = ParseFloat(row.Get("damage", "0"));
                s.badSan[i] = ParseFloat(row.Get("san", "0"));
            }
            applied++;
        }
        Debug.Log($"[CSV:node_table] {applied} 行を反映");
    }

    // ---------------- 問題データ ----------------

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
                order = ParseInt(row.Get("order", "0")),
                line = row.Get("line"),
                kind = kind,
                gauge = row.Get("gauge") == "" ? -1f : ParseFloat(row.Get("gauge")),
                time  = row.Get("time")  == "" ? -1f : ParseFloat(row.Get("time")),
            });
        }

        var result = all.GroupBy(n => n.problem)
                        .OrderBy(g => g.Key)
                        .Select(g => g.OrderBy(n => n.order).ToList())
                        .ToList();
        Debug.Log($"[CSV:problems] {result.Count} 問 / ノード {all.Count} 個を読み込み");
        return result;
    }

    // ---------------- 共通 ----------------

    public static bool TryParseKind(string s, out ProtoNode.Kind kind)
    {
        switch (s.Trim().ToLowerInvariant())
        {
            case "good": case "g": case "blue": case "青": case "良性":
                kind = ProtoNode.Kind.Good; return true;
            case "bad": case "b": case "red": case "赤": case "悪性":
                kind = ProtoNode.Kind.Bad; return true;
        }
        kind = ProtoNode.Kind.Good;
        return false;
    }

    static int ParseInt(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
    static float ParseFloat(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
}
