using UnityEngine;

/// <summary>
/// CSVの表示画像の指定（"1" "0" "1b" など）を読み取った結果。
///  数字 : 画像の番号（1以上で表示）
///  0    : 非表示
///  末尾b: 暗く表示
/// </summary>
public readonly struct CharaImageSpec
{
    public readonly int Number;   // 画像番号。0なら非表示
    public readonly bool Dark;    // 暗く表示するか

    public bool Visible => Number > 0;

    private CharaImageSpec(int number, bool dark)
    {
        Number = number;
        Dark = dark;
    }

    public static CharaImageSpec Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new CharaImageSpec(0, false);

        text = text.Trim();

        bool dark = false;
        if (text.EndsWith("b") || text.EndsWith("B"))
        {
            dark = true;
            text = text.Substring(0, text.Length - 1);
        }

        if (!int.TryParse(text, out int number) || number < 0)
        {
            Debug.LogWarning($"表示画像の指定が正しくありません: {text}");
            return new CharaImageSpec(0, false);
        }

        // 0 は非表示（暗くする意味がないので dark も外す）
        return new CharaImageSpec(number, number > 0 && dark);
    }
}