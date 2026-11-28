using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CSVの指定に合わせて、キャラ画像A・Bを表示するクラス。
/// 画像は Resources/Portraits/ から "A_1" "B_1" のような名前で自動に読み込む。
/// </summary>
public class PortraitView : MonoBehaviour
{
    [Header("表示するImage")]
    [SerializeField] private Image imageA;
    [SerializeField] private Image imageB;

    [Header("画像の場所と名前")]
    [Tooltip("Resourcesフォルダの中のフォルダ名")]
    [SerializeField] private string folder = "Portraits";
    [SerializeField] private string prefixA = "A";
    [SerializeField] private string prefixB = "B";

    [Header("位置")]
    [SerializeField] private float leftX = -350f;
    [SerializeField] private float rightX = 350f;

    [Header("暗く表示するときの色")]
    [SerializeField] private Color darkColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    // 読み込み済みの画像（同じ画像を何度も読まないための入れ物）
    private readonly Dictionary<string, Sprite> cache = new();

    // テスト用：実行中にInspectorの歯車メニュー（?）から呼べる
    [ContextMenu("テスト：A=1 B=1b")]
    private void TestDark()
    {
        Debug.Log("[Portrait] テスト開始");
        Apply(imageA, prefixA, CharaImageSpec.Parse("1"), leftX);
        Apply(imageB, prefixB, CharaImageSpec.Parse("1b"), rightX);
    }

    /// <summary>
    /// 1つのセリフに合わせて、AとBの表示を更新する。
    /// </summary>
    public void Show(ScenarioData data)
    {
        CharaImageSpec a = data.ImageA;
        CharaImageSpec b = data.ImageB;

        // 片方だけ表示されるときは、そちらを中央（x=0）に寄せる
        float xA = (a.Visible && !b.Visible) ? 0f : leftX;
        float xB = (b.Visible && !a.Visible) ? 0f : rightX;

        Apply(imageA, prefixA, a, xA);
        Apply(imageB, prefixB, b, xB);
    }

    private void Apply(Image img, string prefix, CharaImageSpec spec, float x)
    {
        if (!spec.Visible)
        {
            img.gameObject.SetActive(false);
            return;
        }

        Sprite sprite = LoadSprite($"{prefix}_{spec.Number}");
        if (sprite == null)
        {
            // 画像が見つからないときは非表示にする
            img.gameObject.SetActive(false);
            return;
        }

        img.gameObject.SetActive(true);
        img.sprite = sprite;
        img.color = spec.Dark ? darkColor : Color.white;

        Vector2 pos = img.rectTransform.anchoredPosition;
        img.rectTransform.anchoredPosition = new Vector2(x, pos.y);
    }

    // 名前から画像を読み込む（一度読んだものは使い回す）
    private Sprite LoadSprite(string spriteName)
    {
        if (cache.TryGetValue(spriteName, out Sprite cached))
            return cached;

        string path = $"{folder}/{spriteName}";
        Sprite sprite = Resources.Load<Sprite>(path);

        if (sprite == null)
            Debug.LogWarning($"画像が見つかりません: Resources/{path}");

        cache[spriteName] = sprite;
        return sprite;
    }
}