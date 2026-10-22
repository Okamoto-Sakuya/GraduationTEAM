using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 造線ゲージのバー表示
//  ・灰色   : 使ったあとに残るゲージ
//  ・橙色   : 今引いている造線で消費する量
//  ・暗い部分: すでに使っている(空の)ゲージ
//  文字     : 「造線ゲージ 1176 / 2000 (-376)」(使用後の残量と消費量)
//
// 使い方: Canvasの下に空のUIオブジェクトを作ってこのスクリプトを付け、
//         大きさと位置を決める。バーの中身は自動で作られる。
[RequireComponent(typeof(Image))]
public class LineGaugeBar : MonoBehaviour
{
    [Header("文字")]
    [Tooltip("未設定なら自動で作る(自動で作る文字は日本語が表示されない場合があります)")]
    [SerializeField] private TMP_Text label;

    [Tooltip("{0}=使用後の残量、{1}=最大値")]
    [SerializeField] private string labelFormat = "造線ゲージ {0:F0} / {1:F0}";

    [Tooltip("造線を引いている間だけ付く。{0}=消費量")]
    [SerializeField] private string costFormat = " (-{0:F0})";

    [SerializeField] private Color textColor = Color.white;

    [Tooltip("ゲージが足りないときの文字の色")]
    [SerializeField] private Color shortageTextColor = new Color(1f, 0.45f, 0.4f, 1f);

    [Header("色")]
    [SerializeField] private Color borderColor = new Color(0.27f, 0.42f, 0.6f, 1f);
    [SerializeField] private Color backColor = new Color(0.05f, 0.07f, 0.1f, 1f);
    [SerializeField] private Color fillColor = new Color(0.72f, 0.72f, 0.72f, 1f);
    [SerializeField] private Color costColor = new Color(0.8f, 0.45f, 0.15f, 1f);

    [Tooltip("ゲージが足りないときの、消費予定部分の色")]
    [SerializeField] private Color shortageColor = new Color(0.85f, 0.2f, 0.2f, 1f);

    [Header("枠")]
    [SerializeField] private float borderThickness = 3f;

    private RectTransform fillRect;
    private RectTransform costRect;
    private Image costImage;
    private bool built;

    private void Awake()
    {
        Build();
    }

    private void Build()
    {
        if (built)
        {
            return;
        }

        built = true;

        // 自身のImageを枠にする
        Image borderImage = GetComponent<Image>();
        borderImage.color = borderColor;
        borderImage.raycastTarget = false;

        // 背景(暗い部分)
        RectTransform back = CreateChild("Back", backColor, transform);
        back.anchorMin = Vector2.zero;
        back.anchorMax = Vector2.one;
        back.offsetMin = new Vector2(borderThickness, borderThickness);
        back.offsetMax = new Vector2(-borderThickness, -borderThickness);

        // 消費予定(橙)→ 残量(灰)の順に重ねる
        costRect = CreateChild("Cost", costColor, back);
        costImage = costRect.GetComponent<Image>();
        fillRect = CreateChild("Fill", fillColor, back);

        // 文字
        if (label == null)
        {
            GameObject textObject = new GameObject("Label");
            textObject.transform.SetParent(transform, false);

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 12f;
            text.fontSizeMax = 36f;
            text.raycastTarget = false;

            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            label = text;
        }
    }

    private RectTransform CreateChild(string objectName, Color color, Transform parent)
    {
        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(parent, false);

        Image image = obj.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return image.rectTransform;
    }

    private static void SetRange(RectTransform rect, float from, float to)
    {
        rect.anchorMin = new Vector2(Mathf.Clamp01(from), 0f);
        rect.anchorMax = new Vector2(Mathf.Clamp01(to), 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // current: 今のゲージ / max: 最大 / pendingCost: 引いている造線の消費量(なければ0)
    public void SetValues(float current, float max, float pendingCost)
    {
        Build();

        max = Mathf.Max(1f, max);

        float cost = Mathf.Max(0f, pendingCost);
        bool shortage = cost > current;
        float after = Mathf.Max(0f, current - cost);

        // 灰色: 使用後の残量 / 橙色: 消費する分
        SetRange(fillRect, 0f, after / max);
        SetRange(costRect, after / max, current / max);

        costImage.color = shortage ? shortageColor : costColor;

        if (label != null)
        {
            string text = string.Format(labelFormat, after, max);

            if (cost > 0f)
            {
                text += string.Format(costFormat, cost);
            }

            label.text = text;
            label.color = shortage ? shortageTextColor : textColor;
        }
    }
}