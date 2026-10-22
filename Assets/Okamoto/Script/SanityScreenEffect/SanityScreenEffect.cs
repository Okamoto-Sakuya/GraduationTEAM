using UnityEngine;
using UnityEngine.UI;

// SAN値が減るほど、画面全体が徐々に赤くなる演出
//  ・画面の最前面に、赤い半透明の画像を重ねる(スコアなどのUIより後ろ)
//  ・SAN値が最大のときは透明、0に近づくほど赤くなる
//  GameManagerと同じオブジェクトに付ける(付けていなければ自動で追加される)
public class SanityScreenEffect : MonoBehaviour
{
    [Header("色")]
    [SerializeField] private Color tintColor = new Color(0.85f, 0.05f, 0.05f, 1f);

    [Tooltip("SAN値が0のときの、赤の濃さの上限(0?1)")]
    [Range(0f, 1f)]
    [SerializeField] private float maxAlpha = 0.55f;

    [Tooltip("画面の端ほど赤くする強さ(0なら画面全体が均一に赤くなる)")]
    [Range(0f, 1f)]
    [SerializeField] private float vignette = 0.6f;

    [Header("変化のしかた")]
    [Tooltip("横軸: SANの減少割合(0=満タン、1=0)、縦軸: 赤さ(0?1)")]
    [SerializeField]
    private AnimationCurve intensityBySanityLoss = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Tooltip("赤さが変化する速さ(大きいほど素早く追従する)")]
    [SerializeField] private float smoothSpeed = 2f;

    [Header("表示")]
    [Tooltip("スコアなどのUI(100)より小さくして、UIの後ろに重ねる")]
    [SerializeField] private int sortingOrder = 50;

    private Image overlay;
    private float current;

    private void Awake()
    {
        BuildOverlay();
    }

    private void Update()
    {
        float target = 0f;

        GameManager gm = GameManager.Instance;

        if (gm != null && gm.maxSanity > 0f)
        {
            float sanityRatio = Mathf.Clamp01(gm.currentSanity / gm.maxSanity);

            target = Mathf.Clamp01(
                intensityBySanityLoss.Evaluate(1f - sanityRatio)
            );
        }

        // ゲームオーバー中(時間停止中)も滑らかに変化させる
        current = Mathf.MoveTowards(
            current,
            target,
            smoothSpeed * Time.unscaledDeltaTime
        );

        Color color = tintColor;
        color.a = maxAlpha * current;

        overlay.color = color;
    }

    private void BuildOverlay()
    {
        GameObject canvasObject = new GameObject("SanityOverlayCanvas");

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        GameObject imageObject = new GameObject("Overlay");
        imageObject.transform.SetParent(canvasObject.transform, false);

        overlay = imageObject.AddComponent<Image>();
        overlay.sprite = CreateVignetteSprite();
        overlay.raycastTarget = false;
        overlay.color = new Color(tintColor.r, tintColor.g, tintColor.b, 0f);

        RectTransform rect = overlay.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // 中央が薄く、端が濃い画像を作る(vignetteが0なら全面が同じ濃さ)
    private Sprite CreateVignetteSprite()
    {
        const int size = 128;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;

                // 中心0、四隅が約1.4
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                float t = Mathf.Clamp01(distance / 1.2f);

                float alpha = Mathf.Lerp(1f - vignette, 1f, t * t);

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f)
        );
    }
}