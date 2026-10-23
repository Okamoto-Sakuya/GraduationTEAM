#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
#define NEW_INPUT_ONLY
#endif
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if NEW_INPUT_ONLY
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// ゲームクリア / ゲームオーバーのリザルト画面（どのメインゲームからでも使える共通スクリプト）。
/// 空の GameObject にアタッチしておき、ゲーム側から ShowClear / ShowGameOver を呼ぶだけで、
/// Canvas 以下の画面一式をコードで作って表示します（他のスクリプトへの依存なし）。
///
/// 画面構成（1920×1080 基準）
///   全面   : 暗い背景（ゲーム画面の上に重ねる）
///   左     : イラスト（clearImage / gameOverImage）
///   右上   : タイトル（STAGE CLEAR / GAME OVER）
///   右中   : スコア、残った部位（残存 / 欠損）
///   右下   : ボタン（クリア＝次のステージへ / ゲームオーバー＝リトライ）
///
/// 呼び出し例
///   var parts = new List&lt;ResultScreen.PartStatus&gt; {
///       new ResultScreen.PartStatus("左腕", true), new ResultScreen.PartStatus("右足", false) };
///   resultScreen.ShowClear(score, 8, parts);      // クリア
///   resultScreen.ShowGameOver(score, 8, parts);   // ゲームオーバー
///   部位が無いゲームでは parts に null を渡すと「残った部位」欄ごと表示しない
///
/// ボタン
///   次のステージへ : onNextStage を実行（空なら何もしない）
///   リトライ       : onRetry を実行（空なら今のシーンを読み直す）
///
/// 非再生中は Inspector の previewMode でプレビューを表示できます（シーンには保存されません）。
/// Time.timeScale = 0 の状態でも動くように、演出は unscaled time で動かしています。
/// </summary>
[ExecuteAlways]
public class ResultScreen : MonoBehaviour
{
    [System.Serializable]
    public struct PartStatus
    {
        public string name;   // 表示名（例：左腕）
        public bool alive;    // true = 残存 / false = 欠損
        public PartStatus(string name, bool alive) { this.name = name; this.alive = alive; }
    }

    public enum PreviewMode { None, Clear, GameOver }

    // ======================= Inspector =======================

    [Header("編集用プレビュー（非再生中のみ。ゲームビューで見た目を確認）")]
    public PreviewMode previewMode = PreviewMode.None;
    public int previewScore = 12500;                     // プレビュー用のスコア

    [Header("イラスト（左側）")]
    public Sprite clearImage;                            // クリア時のイラスト
    public Sprite gameOverImage;                         // ゲームオーバー時のイラスト（空欄なら clearImage を使う）
    public Vector2 imagePos = new Vector2(-470, 0);      // イラストの位置（画面中央基準 px）
    public Vector2 imageSize = new Vector2(760, 1000);   // イラストの表示枠（縦横比は保つ）

    [Header("文言")]
    public string clearTitle = "STAGE CLEAR";
    public string gameOverTitle = "GAME OVER";
    public string scoreLabel = "SCORE";
    public string partsLabel = "残った部位";
    public string aliveText = "残存";
    public string lostText = "欠損";
    public string nextButtonText = "次のステージへ";
    public string retryButtonText = "リトライ";

    [Header("見た目")]
    public Font font;                                    // 未設定ならビルトインフォント
    public Color clearColor = new Color(0.55f, 0.85f, 1f);       // クリア時のアクセント色
    public Color gameOverColor = new Color(0.9f, 0.22f, 0.2f);   // ゲームオーバー時のアクセント色
    public Color backgroundColor = new Color(0f, 0.016f, 0.059f, 1f); // 背景。イラストの地の色に合わせると枠が目立たない（α を下げるとゲーム画面が透ける）
    public float panelX = 430f;                          // 右側の文字・ボタンの中心 X（px）
    public float fadeSeconds = 0.6f;                     // 表示時のフェードイン時間（秒）
    public int sortingOrder = 100;                       // 他の Canvas より手前に出すための順番

    [Header("ボタンの処理")]
    public UnityEvent onNextStage;                       // 次のステージへ（空なら何もしない）
    public UnityEvent onRetry;                           // リトライ（空なら今のシーンを読み直す）

    // ======================= 公開状態 =======================

    /// <summary>リザルト画面を表示中か（再生中）</summary>
    public bool IsShowing { get; private set; }

    // ======================= 内部 =======================

    const string RuntimeCanvasName = "ResultCanvas";
    const string PreviewCanvasName = "ResultPreview";

    GameObject canvasGo;
    CanvasGroup group;
    RectTransform illust;
    static Sprite circleSprite;

    // ======================= 公開API =======================

    public void ShowClear(int score, int scoreDigits, IList<PartStatus> parts) => Show(true, score, scoreDigits, parts);
    public void ShowGameOver(int score, int scoreDigits, IList<PartStatus> parts) => Show(false, score, scoreDigits, parts);

    public void Hide()
    {
        DestroyCanvas(RuntimeCanvasName);
        IsShowing = false;
    }

    void Show(bool clear, int score, int scoreDigits, IList<PartStatus> parts)
    {
        if (!Application.isPlaying) return;
        StopAllCoroutines();
        DestroyCanvas(RuntimeCanvasName);
        EnsureEventSystem();
        Build(RuntimeCanvasName, clear, score, scoreDigits, parts, true);
        IsShowing = true;
        StartCoroutine(FadeIn());
    }

    // ======================= 編集用プレビュー =======================

#if UNITY_EDITOR
    void OnEnable() { if (!Application.isPlaying) SchedulePreview(); }
    void OnDisable() { if (!Application.isPlaying) DestroyCanvas(PreviewCanvasName); }
    void OnValidate() { if (!Application.isPlaying) SchedulePreview(); }

    void SchedulePreview()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && isActiveAndEnabled && !Application.isPlaying) RebuildPreview();
        };
    }
#endif

    void Start()
    {
        if (Application.isPlaying) DestroyCanvas(PreviewCanvasName);
    }

    /// <summary>非再生中のプレビューを作り直す（previewMode が None なら消すだけ）</summary>
    public void RebuildPreview()
    {
        if (Application.isPlaying) return;
        DestroyCanvas(PreviewCanvasName);
        if (previewMode == PreviewMode.None) return;

        var sample = new List<PartStatus>
        {
            new PartStatus("左腕", true), new PartStatus("右腕", true),
            new PartStatus("左足", false), new PartStatus("右足", true),
        };
        Build(PreviewCanvasName, previewMode == PreviewMode.Clear, previewScore, 8, sample, false);
        foreach (var t in canvasGo.GetComponentsInChildren<Transform>(true))
            t.gameObject.hideFlags = HideFlags.DontSave;
    }

    void DestroyCanvas(string name)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i);
            if (c.name != name) continue;
            if (Application.isPlaying) Destroy(c.gameObject);
            else DestroyImmediate(c.gameObject);
        }
    }

    // ======================= 画面構築 =======================

    void Build(string canvasName, bool clear, int score, int scoreDigits, IList<PartStatus> parts, bool interactive)
    {
        Font f = ResolveFont();
        Color accent = clear ? clearColor : gameOverColor;

        canvasGo = new GameObject(canvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        group = canvasGo.GetComponent<CanvasGroup>();

        var root = NewRect(canvasGo.transform, "Result", Vector2.zero, new Vector2(1920, 1080));

        // 背景（ゲーム画面の上に重ね、クリックも下に通さない）
        var bg = Img(root, "Background", Vector2.zero, new Vector2(1920, 1080), null, backgroundColor);
        bg.raycastTarget = true;

        // 左：イラスト
        Sprite art = clear ? clearImage : (gameOverImage != null ? gameOverImage : clearImage);
        if (art != null)
        {
            var img = Img(root, "Illustration", imagePos, imageSize, art, Color.white);
            img.preserveAspect = true;
            illust = img.rectTransform;
        }
        else
        {
            var ph = Img(root, "Illustration", imagePos, imageSize, null, new Color(1, 1, 1, 0.06f));
            Txt(ph.transform, "Placeholder", "イラスト\n(clearImage / gameOverImage を設定)", f, 30,
                new Color(1, 1, 1, 0.5f), TextAnchor.MiddleCenter, Vector2.zero, imageSize);
            illust = ph.rectTransform;
        }

        // 右：タイトル
        var title = Txt(root, "Title", clear ? clearTitle : gameOverTitle, f, 110, accent, TextAnchor.MiddleCenter,
                        new Vector2(panelX, 340), new Vector2(900, 140));
        title.fontStyle = FontStyle.Bold;
        var glow = title.gameObject.AddComponent<Outline>();
        glow.effectColor = new Color(accent.r, accent.g, accent.b, 0.35f);
        glow.effectDistance = new Vector2(3, -3);
        Img(root, "TitleLine", new Vector2(panelX, 262), new Vector2(640, 3), null, new Color(accent.r, accent.g, accent.b, 0.6f));

        // スコア
        Txt(root, "ScoreLabel", scoreLabel, f, 30, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter,
            new Vector2(panelX, 200), new Vector2(600, 40));
        string scoreStr = scoreDigits > 0 ? Mathf.Max(0, score).ToString("D" + scoreDigits) : score.ToString();
        Txt(root, "Score", scoreStr, f, 92, Color.white, TextAnchor.MiddleCenter,
            new Vector2(panelX, 128), new Vector2(800, 110));

        // 残った部位（parts が null なら欄ごと出さない）
        if (parts != null && parts.Count > 0)
        {
            int alive = 0;
            foreach (var p in parts) if (p.alive) alive++;
            Txt(root, "PartsLabel", $"{partsLabel}  {alive} / {parts.Count}", f, 30, new Color(1, 1, 1, 0.6f),
                TextAnchor.MiddleCenter, new Vector2(panelX, 38), new Vector2(600, 40));

            const float colW = 260f, rowH = 72f;
            for (int i = 0; i < parts.Count; i++)
            {
                int col = i % 2, row = i / 2;
                Vector2 c = new Vector2(panelX + (col == 0 ? -colW * 0.5f : colW * 0.5f), -32 - row * rowH);
                var p = parts[i];
                Color dot = p.alive ? accent : new Color(0.35f, 0.35f, 0.38f);

                var cell = NewRect(root, "Part_" + p.name, c, new Vector2(240, 60));
                Img(cell, "Bg", Vector2.zero, new Vector2(240, 60), null, new Color(1, 1, 1, p.alive ? 0.07f : 0.03f));
                Img(cell, "Dot", new Vector2(-92, 0), new Vector2(20, 20), CircleSprite(), dot);
                Txt(cell, "Name", p.name, f, 32, p.alive ? Color.white : new Color(1, 1, 1, 0.4f), TextAnchor.MiddleLeft,
                    new Vector2(-8, 0), new Vector2(130, 50));
                Txt(cell, "State", p.alive ? aliveText : lostText, f, 24, p.alive ? accent : new Color(0.75f, 0.3f, 0.3f),
                    TextAnchor.MiddleRight, new Vector2(70, 0), new Vector2(90, 50));
            }
        }

        // ボタン
        var btn = MakeButton(root, clear ? "NextStageButton" : "RetryButton", clear ? nextButtonText : retryButtonText,
                             f, accent, new Vector2(panelX, -330), new Vector2(460, 92));
        btn.interactable = interactive;
        if (clear) btn.onClick.AddListener(OnNextStageClicked);
        else btn.onClick.AddListener(OnRetryClicked);
    }

    Button MakeButton(Transform parent, string name, string label, Font f, Color accent, Vector2 pos, Vector2 size)
    {
        var frame = Img(parent, name, pos, size, null, accent);
        frame.raycastTarget = true;
        Img(frame.transform, "Inner", Vector2.zero, size - new Vector2(6, 6), null, new Color(0.03f, 0.05f, 0.1f, 0.95f));
        Txt(frame.transform, "Label", label, f, 38, Color.white, TextAnchor.MiddleCenter, Vector2.zero, size);

        var btn = frame.gameObject.AddComponent<Button>();
        btn.targetGraphic = frame;
        var cb = btn.colors;
        cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        cb.disabledColor = Color.white;   // プレビュー時も同じ見た目
        btn.colors = cb;
        return btn;
    }

    // ======================= ボタン処理 =======================

    void OnNextStageClicked()
    {
        if (onNextStage != null) onNextStage.Invoke();   // まだ何もしない（後で処理を登録）
    }

    void OnRetryClicked()
    {
        if (onRetry != null && onRetry.GetPersistentEventCount() > 0)
        {
            onRetry.Invoke();
            return;
        }
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ======================= 演出 =======================

    IEnumerator FadeIn()
    {
        float t = 0f;
        Vector2 basePos = illust != null ? illust.anchoredPosition : Vector2.zero;
        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.01f, fadeSeconds)));
            if (group != null) group.alpha = k;
            if (illust != null) illust.anchoredPosition = basePos + new Vector2(-40f * (1f - k), 0f);   // 左から少しスライド
            yield return null;
        }
        if (group != null) group.alpha = 1f;
        if (illust != null) illust.anchoredPosition = basePos;
    }

    // ======================= UIヘルパー（他スクリプトに依存しない） =======================

    static RectTransform NewRect(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static Image Img(Transform parent, string name, Vector2 pos, Vector2 size, Sprite sprite, Color color)
    {
        var rt = NewRect(parent, name, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Text Txt(Transform parent, string name, string text, Font font, int size, Color color, TextAnchor anchor, Vector2 pos, Vector2 box)
    {
        var rt = NewRect(parent, name, pos, box);
        var t = rt.gameObject.AddComponent<Text>();
        t.text = text;
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static Sprite CircleSprite()
    {
        if (circleSprite != null) return circleSprite;
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear };
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N / 2f, N / 2f));
                byte a = (byte)(Mathf.Clamp01(N / 2f - d) * 255f);
                px[y * N + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
        circleSprite.hideFlags = HideFlags.DontSave;
        return circleSprite;
    }

    Font ResolveFont()
    {
        if (font != null) return font;
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
        return f;
    }

    static void EnsureEventSystem()
    {
#if UNITY_2023_1_OR_NEWER
        if (FindFirstObjectByType<EventSystem>() != null) return;
#else
        if (FindObjectOfType<EventSystem>() != null) return;
#endif
        var go = new GameObject("EventSystem", typeof(EventSystem));
#if NEW_INPUT_ONLY
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }
}
