
using UnityEngine;
using UnityEngine.UI;

public class SanityBlackout : MonoBehaviour
{
    [Header("SAN値管理")]
    public GameManager gameManager;

    [Header("赤い円の透明度")]
    [Range(0f, 1f)]
    public float maxDarkness = 0.4f;

    [Tooltip("SAN値が0のときに残す視界の半径")]
    [Range(0.05f, 1f)]
    public float minimumVisionRadius = 0.1f;

    [Tooltip("SAN値が最大のときの視界の半径")]
    public float maximumVisionRadius = 1.25f;

    [Header("変化速度")]
    public float fadeSpeed = 2f;

    [Tooltip("円の境界をぼかす幅")]
    [Range(0.01f, 0.2f)]
    public float edgeSoftness = 0.05f;

    private float currentRadius;
    private float lastRenderedRadius = -1f;

    private const int TextureSize = 512;

    private RawImage overlay;
    private Texture2D maskTexture;
    private Color[] pixels;
    private Canvas overlayCanvas;

    private int lastScreenWidth;
    private int lastScreenHeight;

    private void Start()
    {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();

        CreateOverlay();

        currentRadius = maximumVisionRadius;
        UpdateMaskTexture();
    }

    private void CreateOverlay()
    {
        // 画面全体に表示するCanvasを作成
        GameObject canvasObject = new GameObject(
            "SanityRedOverlayCanvas",
            typeof(Canvas),
            typeof(CanvasScaler)
        );

        overlayCanvas = canvasObject.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 32767;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // 赤い円状の演出を表示するRawImage
        GameObject imageObject = new GameObject(
            "SanityRedOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage)
        );

        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        overlay = imageObject.GetComponent<RawImage>();
        overlay.color = Color.white;
        overlay.raycastTarget = false;

        // 円形のマスク画像を作成
        maskTexture = new Texture2D(
            TextureSize,
            TextureSize,
            TextureFormat.RGBA32,
            false
        );

        maskTexture.filterMode = FilterMode.Bilinear;
        maskTexture.wrapMode = TextureWrapMode.Clamp;

        pixels = new Color[TextureSize * TextureSize];

        overlay.texture = maskTexture;

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }

    private void Update()
    {
        if (gameManager == null || overlay == null)
            return;

        // SAN値の割合を計算
        float sanityRate = gameManager.initialSanity > 0
            ? Mathf.Clamp01(
                (float)gameManager.Sanity / gameManager.initialSanity)
            : 0f;

        // SAN値が減るほど、中央の視界を狭くする
        float targetRadius = Mathf.Lerp(
            minimumVisionRadius,
            maximumVisionRadius,
            sanityRate
        );

        currentRadius = Mathf.MoveTowards(
            currentRadius,
            targetRadius,
            fadeSpeed * Time.deltaTime
        );

        // 画面サイズ変更にも対応
        bool screenChanged =
            lastScreenWidth != Screen.width ||
            lastScreenHeight != Screen.height;

        // 毎フレーム画像を作り直さず、変化があったときだけ更新
        if (Mathf.Abs(currentRadius - lastRenderedRadius) > 0.005f
            || screenChanged)
        {
            UpdateMaskTexture();

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
        }
    }

    private void UpdateMaskTexture()
    {
        float aspect = (float)Screen.width /
                       Mathf.Max(1, Screen.height);

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float u = (float)x / (TextureSize - 1);
                float v = (float)y / (TextureSize - 1);

                // 画面の中央からの距離
                float dx = (u - 0.5f) * aspect;
                float dy = v - 0.5f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                // 円の外側ほど赤くする
                float t = Mathf.InverseLerp(
                    currentRadius - edgeSoftness,
                    currentRadius + edgeSoftness,
                    distance
                );

                float alpha = Mathf.SmoothStep(0f, maxDarkness, t);

                int index = y * TextureSize + x;

                // 黒ではなく赤色にする
                pixels[index] = new Color(1f, 0f, 0f, alpha);
            }
        }

        maskTexture.SetPixels(pixels);
        maskTexture.Apply(false, false);

        lastRenderedRadius = currentRadius;
    }

    private void OnDestroy()
    {
        if (maskTexture != null)
            Destroy(maskTexture);

        if (overlayCanvas != null)
            Destroy(overlayCanvas.gameObject);
    }
}
