using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ResultPanelの見た目を、コードで一から作る。
//  ・Prefabを用意していないときの簡易表示として、ゲーム中に使われる
//  ・メニュー Tools > Result > Create Result Prefab で、Prefabのひな形を作るときにも使う
public static class ResultPanelBuilder
{
    public static ResultPanel Build()
    {
        GameObject root = new GameObject("ResultPanel");

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        root.AddComponent<GraphicRaycaster>();

        ResultPanel panel = root.AddComponent<ResultPanel>();

        // 画面全体を覆う暗い背景
        GameObject background = new GameObject("Background");
        background.transform.SetParent(root.transform, false);

        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = new Color(0f, 0f, 0f, 0.8f);

        RectTransform backgroundRect = backgroundImage.rectTransform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        Transform parent = background.transform;

        // タイトル・スコア・ランク
        panel.titleText = CreateText(
            parent, "Title", "GAME OVER", 130f,
            new Vector2(0f, 380f), new Vector2(1400f, 160f),
            TextAlignmentOptions.Center, Color.white);

        panel.scoreText = CreateText(
            parent, "Score", "SCORE 0", 80f,
            new Vector2(0f, 250f), new Vector2(1400f, 110f),
            TextAlignmentOptions.Center, Color.white);

        panel.rankText = CreateText(
            parent, "Rank", "RANK D", 140f,
            new Vector2(0f, 100f), new Vector2(1400f, 180f),
            TextAlignmentOptions.Center, Color.white);

        // 取得ノード(良性・悪性それぞれレベル1?4)
        Color benignColor = new Color(0.45f, 0.7f, 1f, 1f);
        Color malignantColor = new Color(1f, 0.45f, 0.45f, 1f);

        BuildCountRow(
            parent, "Benign", "BENIGN", -60f,
            benignColor, panel.benignCountTexts);

        BuildCountRow(
            parent, "Malignant", "MALIGNANT", -170f,
            malignantColor, panel.malignantCountTexts);

        // リトライボタン
        GameObject buttonObject = new GameObject("RetryButton");
        buttonObject.transform.SetParent(parent, false);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.25f, 0.25f, 0.3f, 1f);

        RectTransform buttonRect = buttonImage.rectTransform;
        buttonRect.sizeDelta = new Vector2(360f, 100f);
        buttonRect.anchoredPosition = new Vector2(0f, -330f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;

        CreateText(
            buttonObject.transform, "Label", "RETRY", 52f,
            Vector2.zero, new Vector2(360f, 100f),
            TextAlignmentOptions.Center, Color.white);

        panel.retryButton = button;

        return panel;
    }

    // 「ラベル + Lv1?Lv4の4つの数」の1行を作る
    private static void BuildCountRow(
        Transform parent,
        string rowName,
        string label,
        float y,
        Color color,
        TMP_Text[] cells)
    {
        CreateText(
            parent, rowName + "Label", label, 48f,
            new Vector2(-440f, y), new Vector2(300f, 80f),
            TextAlignmentOptions.Left, color);

        float[] xs = { -180f, 40f, 260f, 480f };

        for (int i = 0; i < cells.Length; i++)
        {
            cells[i] = CreateText(
                parent, rowName + "Lv" + (i + 1), "Lv" + (i + 1) + "  x0", 44f,
                new Vector2(xs[i], y), new Vector2(220f, 80f),
                TextAlignmentOptions.Center, color);
        }
    }

    private static TMP_Text CreateText(
        Transform parent,
        string objectName,
        string text,
        float fontSize,
        Vector2 position,
        Vector2 size,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        label.enableWordWrapping = false;

        RectTransform rect = label.rectTransform;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        return label;
    }
}