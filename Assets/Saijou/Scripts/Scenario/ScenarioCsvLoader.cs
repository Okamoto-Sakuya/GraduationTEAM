using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
/// <summary>
/// CSVの読み込みと解析用Script
/// </summary>
public class ScenarioCsvLoader : MonoBehaviour
{
    [SerializeField] private TextAsset csvFile;

    private readonly Dictionary<int, ScenarioData> scenarios = new();

    public int Count => scenarios.Count;

    // CSVを読み込む
    public void Load()
    {
        scenarios.Clear();

        if (csvFile == null)
        {
            Debug.LogError("CSVファイルが設定されていません。");
            return;
        }

        string csv = csvFile.text.TrimStart('\uFEFF');
        List<List<string>> rows = ParseCsv(csv);

        if (rows.Count < 2)
        {
            Debug.LogWarning("CSVにシナリオデータがありません。");
            return;
        }

        // 1行目をヘッダーとして扱う
        List<string> headers = rows[0];

        for (int i = 1; i < rows.Count; i++)
        {
            List<string> row = rows[i];

            // 空行をスキップ
            if (row.Count == 1 && string.IsNullOrWhiteSpace(row[0]))
                continue;

            Dictionary<string, string> fields = new();

            for (int j = 0; j < headers.Count; j++)
            {
                string header = headers[j].Trim();

                if (string.IsNullOrEmpty(header))
                    continue;

                string value = j < row.Count ? row[j] : "";
                fields[header] = value;
            }

            if (!fields.TryGetValue("id", out string idText) ||
                !int.TryParse(idText, out int id))
            {
                Debug.LogWarning(
                    $"CSV {i + 1}行目: idが正しくありません。");
                continue;
            }

            if (scenarios.ContainsKey(id))
            {
                Debug.LogWarning($"シナリオID {id} が重複しています。");
                continue;
            }

            scenarios.Add(id, new ScenarioData(fields));
        }

        Debug.Log($"シナリオを {scenarios.Count} 件読み込みました。");
    }

    // IDからシナリオを取得
    public bool TryGetScenario(int id, out ScenarioData data)
    {
        return scenarios.TryGetValue(id, out data);
    }

    // CSVの解析（カンマ、引用符、改行に対応）
    private List<List<string>> ParseCsv(string csv)
    {
        List<List<string>> rows = new();
        List<string> row = new();
        StringBuilder field = new();

        bool inQuotes = false;

        for (int i = 0; i < csv.Length; i++)
        {
            char c = csv[i];

            if (c == '"')
            {
                if (inQuotes &&
                    i + 1 < csv.Length &&
                    csv[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if ((c == '\n' || c == '\r') && !inQuotes)
            {
                if (c == '\r' &&
                    i + 1 < csv.Length &&
                    csv[i + 1] == '\n')
                {
                    i++;
                }

                row.Add(field.ToString());
                field.Clear();

                rows.Add(row);
                row = new List<string>();
            }
            else
            {
                field.Append(c);
            }
        }

        // 最終行を追加
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        if (inQuotes)
        {
            Debug.LogWarning("CSVの引用符が閉じられていません。");
        }

        return rows;
    }

    // 最後のシナリオのデータを取得する
    public ScenarioData GetLastScenario()
    {
        ScenarioData lastData = null;
        int maxId = int.MinValue;

        // 読み込んだシナリオの中から最大IDを探す
        foreach (var entry in scenarios)
        {
            if (entry.Key > maxId)
            {
                maxId = entry.Key;
                lastData = entry.Value;
            }
        }

        return lastData;
    }
}