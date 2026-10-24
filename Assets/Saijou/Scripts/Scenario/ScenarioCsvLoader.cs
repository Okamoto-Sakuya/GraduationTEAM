using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// CSVの読み込みと解析用Script（複数CSV対応）
/// </summary>
public class ScenarioCsvLoader : MonoBehaviour
{
    // インスペクターで自由に増やせる（1ストーリー = 1CSV）
    [SerializeField] private List<TextAsset> csvFiles = new();

    // ストーリー名(CSVのファイル名) → (ID → シナリオ)
    private readonly Dictionary<string, Dictionary<int, ScenarioData>> stories = new();

    // 現在選択中のストーリー
    private Dictionary<int, ScenarioData> current;
    public string CurrentStoryName { get; private set; }

    public int Count => current?.Count ?? 0;
    public IEnumerable<string> StoryNames => stories.Keys;

    // 全CSVを読み込む。読み込み後は最初のCSVが選択される
    public void Load()
    {
        stories.Clear();
        current = null;
        CurrentStoryName = null;

        if (csvFiles == null || csvFiles.Count == 0)
        {
            Debug.LogError("CSVファイルが設定されていません。");
            return;
        }

        foreach (TextAsset csvFile in csvFiles)
        {
            if (csvFile == null) continue;

            if (stories.ContainsKey(csvFile.name))
            {
                Debug.LogWarning($"CSV名 {csvFile.name} が重複しています。スキップします。");
                continue;
            }

            stories.Add(csvFile.name, LoadOne(csvFile));
        }

        // 最初のストーリーを選択状態にしておく
        foreach (var pair in stories)
        {
            SetStory(pair.Key);
            break;
        }
    }

    // ストーリー(CSV)を切り替える。ファイル名（拡張子なし）で指定
    public bool SetStory(string storyName)
    {
        if (!stories.TryGetValue(storyName, out var dict))
        {
            Debug.LogWarning($"ストーリー {storyName} が見つかりません。");
            return false;
        }

        current = dict;
        CurrentStoryName = storyName;
        return true;
    }

    // インスペクターのリストの順番(0始まり)で切り替える
    public bool SetStory(int index)
    {
        if (index < 0 || index >= csvFiles.Count || csvFiles[index] == null)
        {
            Debug.LogWarning($"ストーリー番号 {index} が正しくありません。");
            return false;
        }
        return SetStory(csvFiles[index].name);
    }

    // 現在のストーリーからIDでシナリオを取得
    public bool TryGetScenario(int id, out ScenarioData data)
    {
        data = null;
        return current != null && current.TryGetValue(id, out data);
    }

    // 現在のストーリーの最後のシナリオを取得
    public ScenarioData GetLastScenario()
    {
        if (current == null) return null;

        ScenarioData lastData = null;
        int maxId = int.MinValue;

        foreach (var entry in current)
        {
            if (entry.Key > maxId)
            {
                maxId = entry.Key;
                lastData = entry.Value;
            }
        }
        return lastData;
    }

    // CSV1つ分を読み込む
    private Dictionary<int, ScenarioData> LoadOne(TextAsset csvFile)
    {
        Dictionary<int, ScenarioData> result = new();

        string csv = csvFile.text.TrimStart('\uFEFF');
        List<List<string>> rows = ParseCsv(csv, csvFile.name);

        if (rows.Count < 2)
        {
            Debug.LogWarning($"[{csvFile.name}] シナリオデータがありません。");
            return result;
        }

        List<string> headers = rows[0];

        for (int i = 1; i < rows.Count; i++)
        {
            List<string> row = rows[i];

            if (row.Count == 1 && string.IsNullOrWhiteSpace(row[0]))
                continue;

            Dictionary<string, string> fields = new();

            for (int j = 0; j < headers.Count; j++)
            {
                string header = headers[j].Trim();
                if (string.IsNullOrEmpty(header)) continue;

                fields[header] = j < row.Count ? row[j] : "";
            }

            if (!fields.TryGetValue("id", out string idText) ||
                !int.TryParse(idText, out int id))
            {
                Debug.LogWarning($"[{csvFile.name}] {i + 1}行目: idが正しくありません。");
                continue;
            }

            if (result.ContainsKey(id))
            {
                Debug.LogWarning($"[{csvFile.name}] シナリオID {id} が重複しています。");
                continue;
            }

            result.Add(id, new ScenarioData(fields));
        }

        Debug.Log($"[{csvFile.name}] シナリオを {result.Count} 件読み込みました。");
        return result;
    }

    // CSVの解析（カンマ、引用符、改行に対応）
    private List<List<string>> ParseCsv(string csv, string fileName)
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
                if (inQuotes && i + 1 < csv.Length && csv[i + 1] == '"')
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
                if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                    i++;

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

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        if (inQuotes)
            Debug.LogWarning($"[{fileName}] CSVの引用符が閉じられていません。");

        return rows;
    }
}