using System.Collections.Generic;
using System.Text;

/// <summary>
/// シンプルなCSVパーサー。
/// ・Excel の「CSV UTF-8」保存（BOM付き）に対応
/// ・"..." で囲んだセル（カンマ・改行・"" を含む）に対応
/// ・空行と、先頭セルが # で始まる行（コメント）は無視
/// </summary>
public static class CsvUtil
{
    public static List<string[]> Parse(string text)
    {
        var rows = new List<string[]>();
        if (string.IsNullOrEmpty(text)) return rows;
        if (text[0] == '\uFEFF') text = text.Substring(1);

        var row = new List<string>();
        var sb = new StringBuilder();
        bool inQuote = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuote)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuote = false;
                }
                else sb.Append(c);
                continue;
            }

            switch (c)
            {
                case '"':  inQuote = true; break;
                case ',':  row.Add(sb.ToString().Trim()); sb.Clear(); break;
                case '\r': break;
                case '\n':
                    row.Add(sb.ToString().Trim()); sb.Clear();
                    AddRow(rows, row);
                    row = new List<string>();
                    break;
                default:   sb.Append(c); break;
            }
        }
        row.Add(sb.ToString().Trim());
        AddRow(rows, row);
        return rows;
    }

    /// <summary>1行目を見出しとして、各行を「見出し(小文字) → 値」の辞書にする</summary>
    public static List<Dictionary<string, string>> ParseWithHeader(string text)
    {
        var result = new List<Dictionary<string, string>>();
        var rows = Parse(text);
        if (rows.Count == 0) return result;

        var header = rows[0];
        for (int r = 1; r < rows.Count; r++)
        {
            var dict = new Dictionary<string, string>();
            for (int c = 0; c < header.Length; c++)
                dict[header[c].Trim().ToLowerInvariant()] = c < rows[r].Length ? rows[r][c] : "";
            result.Add(dict);
        }
        return result;
    }

    public static string Get(this Dictionary<string, string> row, string key, string fallback = "")
        => row.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : fallback;

    static void AddRow(List<string[]> rows, List<string> row)
    {
        if (row.TrueForAll(string.IsNullOrEmpty)) return;
        if (row[0].StartsWith("#")) return;
        rows.Add(row.ToArray());
    }
}
