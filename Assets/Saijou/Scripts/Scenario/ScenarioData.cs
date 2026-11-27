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

    // ===== よく使う項目（列名を直接書かなくて済むようにしたもの） =====

    // データid
    public int Id => GetInt("id");

    // 話者（"A" "B" "A&B" など）
    public string Speaker => Get("speaker");

    // 表示テキスト
    public string Text => Get("text");

    // 左右の表示画像（1=表示 / 0=非表示 / 末尾b=暗く表示）
    public CharaImageSpec ImageA => CharaImageSpec.Parse(Get("A"));
    public CharaImageSpec ImageB => CharaImageSpec.Parse(Get("B"));

    // 次の表示idがあるか（空なら false）
    public bool HasNext => int.TryParse(Get("nextId"), out _);

    // 次の表示id。ない場合は -1
    public int NextId => int.TryParse(Get("nextId"), out int next) ? next : -1;

    // 特殊遷移（"intro" "stageEnd" など。ない場合は空文字）
    public string StoryType => Get("storyType");

    // ボイスファイル名（ない場合は空文字）
    public string Voice => Get("voice");

    // ===== 汎用の取得メソッド（今までどおり） =====

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