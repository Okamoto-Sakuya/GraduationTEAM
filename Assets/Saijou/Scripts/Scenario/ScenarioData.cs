using System.Collections.Generic;
/// <summary>
/// 1つのセリフのデータを管理するクラス
/// </summary>
public class ScenarioData
{
    private readonly Dictionary<string, string> fields;

    public ScenarioData(Dictionary<string, string> fields)
    {
        this.fields = fields;
    }

    // CSVの列名から値を取得する
    public string Get(string columnName, string defaultValue = "")
    {
        if (fields.TryGetValue(columnName, out string value))
        {
            return value;
        }

        return defaultValue;
    }

    // 数値として取得したい場合
    public int GetInt(string columnName, int defaultValue = 0)
    {
        return int.TryParse(Get(columnName), out int result)
            ? result
            : defaultValue;
    }

    // CSVに列が存在するか確認
    public bool HasColumn(string columnName)
    {
        return fields.ContainsKey(columnName);
    }
}
