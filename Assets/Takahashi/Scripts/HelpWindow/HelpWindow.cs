#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
#define NEW_INPUT_ONLY
#endif
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if NEW_INPUT_ONLY
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// ヘルプウィンドウ（どのメインゲームからでも使える共通スクリプト）。
/// 空の GameObject にアタッチするだけで、画面の隅に「？」ボタンを出し、押すとヘルプが開きます。
/// 他のスクリプトへの依存なし。中身（ページ）は Inspector の pages で設定します。
///
/// ・開いている間はゲームを一時停止（Time.timeScale = 0）。閉じると元の速さに戻す
/// ・ページ送り：◀ ▶ ボタン / ← → キー
/// ・開く：? ボタン / H キー（開いている間は ? ボタンを消す）
/// ・H キーでも閉じられる
/// ・閉じる：× ボタン / Esc キー / ウィンドウの外をクリック
/// ・ゲーム側は HelpWindow.IsOpen を見て、開いている間の入力を無視する
///
/// 1ページ = 画像（任意）＋見出し＋本文。画像が無いページは本文を横いっぱいに表示します。
/// 画像は pages の image に直接入れるか、Assets/Resources/Help/ に置いて imageResource に名前を書く（初期ページは設定済み）。
/// 非再生中は Inspector の previewOpen でウィンドウの見た目を確認できます（シーンには保存されません）。
/// </summary>
[ExecuteAlways]
public class HelpWindow : MonoBehaviour
{
    [System.Serializable]
    public class HelpPage
    {
        public string title;                 // ページの見出し
        public Sprite image;                 // 説明画像（空欄なら imageResource → それも無ければ本文のみ）
        public string imageResource;         // Resources フォルダ内の画像名（例: Help/help_01_overview）。image が空欄のときに読み込む
        [TextArea(4, 12)] public string text;   // 本文（\n で改行）
    }

    /// <summary>ヘルプが開いているか。ゲーム側はこれが true の間、入力を無視する</summary>
    public static bool IsOpen { get; private set; }

    // ======================= Inspector =======================

    [Header("ページ（上から順に表示）")]
    public List<HelpPage> pages = DefaultPages();

    /// <summary>初期のページ内容（プロト仕様書より）。Inspector の ⋮ →「ページを初期内容に戻す」でも再設定できる</summary>
    public static List<HelpPage> DefaultPages() => new List<HelpPage>
    {
        new HelpPage
        {
            title = "ゲームの目的",
            imageResource = "Help/help_01_overview",
            text = "線を伝って流れてくるノードを、四肢の回収点（▲▼）へ正しく導くパズルゲームです。\n\n" +
                   "・青いノードを回収 → スコア\n" +
                   "・赤いノードを回収 → 部位が傷つき、SAN値も減る\n\n" +
                   "導き方を誤ると部位が欠損します。キャラクターを守りながら高得点を目指そう。"
        },
        new HelpPage
        {
            title = "仕様（ノードと体力）",
            imageResource = "Help/help_02_rules",
            text = "・ノードは画面外の基本線から現れ、回収点に届くと回収される\n" +
                   "・時間で 青⇔赤 に変化。周りのリングが変化までの残り\n" +
                   "・青は早く、赤は時間をかけて入れるほど高得点\n" +
                   "・体力が0の部位は欠損。腕を失うと造線ゲージの上限が減る\n" +
                   "・4部位すべて失うとゲームオーバー"
        },
        new HelpPage
        {
            title = "造線の引き方",
            imageResource = "Help/help_03_drawing",
            text = "基本線の上からドラッグして、別の基本線まで伸ばすと「造線」を引けます。\n\n" +
                   "・白い点線なら、離すと確定\n" +
                   "・赤い点線はまだ引けない\n" +
                   "・ほかの線はまたげない\n" +
                   "・端の赤い円からは引けない\n" +
                   "・右クリックで消す（ゲージは戻る）"
        },
        new HelpPage
        {
            title = "硬さと造線ゲージ",
            imageResource = "Help/help_04_hardness",
            text = "上の ● ボタンで、造線の「硬さ」を選びます。\n\n" +
                   "・硬さ＝ノードが通れる回数（1〜3回）\n" +
                   "・造線の真ん中の数字＝残りの回数\n" +
                   "・残り1回で点滅し、0で消える\n" +
                   "・どの硬さも消費ゲージは同じ\n" +
                   "下の造線ゲージが残量。オレンジ部分はこれから使う量です。"
        },
        new HelpPage
        {
            title = "ラッシュモード",
            text = "絶え間なく流れてくるノードを、リアルタイムでさばき切るモードです。\n\n" +
                   "・ノードは止まらずに流れ続ける。素早く造線を引いて道を作ろう\n" +
                   "・時間がたつほどレベルが上がり、ノードの出現が速くなる\n" +
                   "・決められた時間を耐えきるとステージクリア\n" +
                   "・4つの部位の体力がすべて 0 になるとゲームオーバー"
        },
        new HelpPage
        {
            title = "ロジックモード",
            imageResource = "Help/help_06_logic",
            text = "用意された問題を見て、道を考えるモードです。\n\n" +
                   "① 問題準備：来るノードと順番を確認\n" +
                   "② 経路フェーズ（1分）：造線を引く。▶で早めに実行\n" +
                   "③ 実行フェーズ：ノードが流れる（操作不可）\n\n" +
                   "最後の問題まで部位が残ればクリア。"
        },
    };

    [ContextMenu("ページを初期内容に戻す")]
    void ResetPagesToDefault()
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "ページを初期内容に戻す");
#endif
        pages = DefaultPages();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        if (!Application.isPlaying) RebuildPreview();
#endif
    }

    [Header("？ボタン（画面の隅）")]
    public bool showHelpButton = true;
    public Vector2 helpButtonPos = new Vector2(230, 505);   // 画面中央基準（px、1920×1080）。初期値は硬さボタンとスコアの間（四隅はノードの出現位置と重なるため避ける）
    public float helpButtonSize = 64f;
    public Sprite helpButtonSprite;                         // 空欄なら丸に「？」

    [Header("ウィンドウ")]
    public string windowTitle = "HELP";
    public Vector2 windowSize = new Vector2(1720, 940);   // 画面(1920×1080)に収まる最大近く。画像を大きく見せるため
    public bool pauseWhileOpen = true;                      // true = 開いている間 Time.timeScale = 0
    public bool openOnStart = false;                        // true = 再生開始時に自動で開く
    public bool closeOnOutsideClick = true;                 // true = ウィンドウの外をクリックで閉じる
    public Sprite windowSprite;                             // ウィンドウ背景（空欄なら単色）

    [Header("見た目")]
    public Font font;                                       // 未設定ならビルトインフォント
    public Color accentColor = new Color(0.55f, 0.85f, 1f);
    public Color windowColor = new Color(0.04f, 0.07f, 0.13f, 0.97f);
    public Color dimColor = new Color(0f, 0f, 0f, 0.65f);   // 後ろのゲーム画面を暗くする色
    public int titleFontSize = 44;
    public int textFontSize = 30;
    public int sortingOrder = 90;                           // リザルト画面（100）より奥、ゲーム画面より手前

    [Header("キー操作")]
    public bool toggleWithKey = true;                       // true = H キーでも開閉できる（？ボタンが見えないときの保険）

    // 設定の版。古い版のまま保存されていたら、位置・大きさを新しい初期値に自動で直す
    [SerializeField, HideInInspector] int settingsVersion = 0;   // 古いデータには無い項目なので 0 から始まり、自動更新が一度だけ走る
    const int CurrentSettingsVersion = 2;

    [Header("編集用プレビュー（非再生中のみ）")]
    public bool previewOpen = false;                        // true = ウィンドウを開いた状態でプレビュー
    public int previewPage = 0;

    // ======================= 内部 =======================

    const string RuntimeCanvasName = "HelpCanvas";
    const string PreviewCanvasName = "HelpPreview";

    GameObject canvasGo;
    GameObject windowRoot;
    GameObject helpButtonGo;
    Canvas runtimeCanvas;
    float diagTimer = -1f;
    Text pageTitle, pageText, pageCounter;
    Image pageImage;
    RectTransform textRect;
    Button prevButton, nextButton;
    int page;
    float savedTimeScale = 1f;
    bool open;

    static Sprite circleSprite, triSprite;

    // ======================= 公開API =======================

    public void Open()
    {
        if (open || windowRoot == null) return;
        open = true;
        IsOpen = true;
        windowRoot.SetActive(true);
        if (helpButtonGo != null) helpButtonGo.SetActive(false);   // 開いている間は ？ボタンを消す
        ShowPage(page);
        if (pauseWhileOpen && Application.isPlaying)
        {
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
    }

    public void Close()
    {
        if (!open) return;
        open = false;
        IsOpen = false;
        if (windowRoot != null) windowRoot.SetActive(false);
        if (helpButtonGo != null) helpButtonGo.SetActive(true);    // 閉じたら ？ボタンを戻す
        if (pauseWhileOpen && Application.isPlaying) Time.timeScale = savedTimeScale;
    }

    public void Toggle() { if (open) Close(); else Open(); }

    public void NextPage() => ShowPage(page + 1);
    public void PrevPage() => ShowPage(page - 1);

    // ======================= ライフサイクル =======================

    /// <summary>古い設定値（ボタンが右上の隅・小さいウィンドウ・4ページ）を新しい初期値に直す</summary>
    void MigrateSettings()
    {
        if (settingsVersion >= CurrentSettingsVersion) return;
        helpButtonPos = new Vector2(230, 505);
        windowSize = new Vector2(1720, 940);
        if (sortingOrder <= 0) sortingOrder = 90;
        showHelpButton = true;
        if (pages == null || pages.Count < 6) pages = DefaultPages();
        settingsVersion = CurrentSettingsVersion;
        Debug.Log("[Help] 設定を新しい初期値に更新しました（？ボタン位置 230,505 / ウィンドウ 1720×940）");
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    void Awake() => MigrateSettings();

    void Start()
    {
        if (!Application.isPlaying) return;
        DestroyCanvas(PreviewCanvasName);
        IsOpen = false;
        EnsureEventSystem();
        Build(RuntimeCanvasName);
        windowRoot.SetActive(false);
        if (openOnStart) Open();
        diagTimer = 1f;   // 1 秒後に描画順の再確認と状態ログ
        Debug.Log(showHelpButton
            ? $"[Help] ？ボタンを作成しました（位置 {helpButtonPos.x:0}, {helpButtonPos.y:0} / Sorting Order {sortingOrder}）。H キーでも開けます"
            : "[Help] Show Help Button が OFF のため ？ボタンは出していません。H キーで開けます");
    }

    void OnDestroy()
    {
        if (Application.isPlaying && canvasGo != null && canvasGo.transform.parent == null) Destroy(canvasGo);
        if (Application.isPlaying && open)
        {
            IsOpen = false;
            if (pauseWhileOpen) Time.timeScale = savedTimeScale;
        }
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        // 何かの理由で画面が消えていたら作り直す
        if (canvasGo == null)
        {
            open = false; IsOpen = false;
            Build(RuntimeCanvasName);
            windowRoot.SetActive(false);
        }

        // ゲーム画面ができた後に描画順を再確認し、状態を Console に出す
        if (diagTimer > 0f)
        {
            diagTimer -= Time.unscaledDeltaTime;
            if (diagTimer <= 0f) RecheckAndLog();
        }

        // ヘルプを開いている間・リザルト画面が出ている間は ？ボタンを隠す
        if (helpButtonGo != null)
        {
            bool resultShowing = GameObject.Find("ResultCanvas") != null;   // ResultScreen が表示中に作る Canvas の名前
            bool visible = !open && !resultShowing;
            if (helpButtonGo.activeSelf != visible) helpButtonGo.SetActive(visible);
        }

        if (toggleWithKey && TogglePressed()) { Toggle(); return; }
        if (!open) return;
        if (EscPressed()) Close();
        else if (RightPressed()) NextPage();
        else if (LeftPressed()) PrevPage();
    }

#if UNITY_EDITOR
    void OnEnable() { if (!Application.isPlaying) SchedulePreview(); }
    void OnDisable() { if (!Application.isPlaying) DestroyCanvas(PreviewCanvasName); }
    void OnValidate() { MigrateSettings(); if (!Application.isPlaying) SchedulePreview(); }

    void SchedulePreview()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && isActiveAndEnabled && !Application.isPlaying) RebuildPreview();
        };
    }
#endif

    /// <summary>非再生中のプレビューを作り直す</summary>
    public void RebuildPreview()
    {
        if (Application.isPlaying) return;
        DestroyCanvas(PreviewCanvasName);
        Build(PreviewCanvasName);
        windowRoot.SetActive(previewOpen);
        if (helpButtonGo != null) helpButtonGo.SetActive(!previewOpen);
        ShowPage(previewPage);
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

    void Build(string canvasName)
    {
        Font f = ResolveFont();

        canvasGo = new GameObject(canvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        // 再生中はシーンの一番上に独立して作る（HelpWindow の Transform の位置・大きさに影響されないように）
        if (!Application.isPlaying) canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.targetDisplay = 0;
        canvas.sortingLayerID = 0;                      // Default
        canvas.sortingOrder = Application.isPlaying ? FrontSortingOrder() : sortingOrder;
        runtimeCanvas = canvas;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var root = NewRect(canvasGo.transform, "Help", Vector2.zero, new Vector2(1920, 1080));

        // ウィンドウ（開いている間だけ表示）
        windowRoot = NewRect(root, "Window", Vector2.zero, new Vector2(1920, 1080)).gameObject;
        var wr = windowRoot.transform;

        var dim = Img(wr, "Dim", Vector2.zero, new Vector2(1920, 1080), null, dimColor);
        dim.raycastTarget = true;   // 後ろのゲームをクリックさせない
        if (closeOnOutsideClick)
        {
            var db = dim.gameObject.AddComponent<Button>();
            db.transition = Selectable.Transition.None;
            db.onClick.AddListener(Close);
        }

        Vector2 ws = windowSize;
        var frame = Img(wr, "Frame", Vector2.zero, ws + new Vector2(6, 6), null, new Color(accentColor.r, accentColor.g, accentColor.b, 0.8f));
        frame.raycastTarget = true; // ウィンドウ内のクリックで閉じないように
        var win = Img(wr, "Panel", Vector2.zero, ws, windowSprite, windowSprite != null ? Color.white : windowColor);
        win.raycastTarget = true;

        float top = ws.y * 0.5f, left = -ws.x * 0.5f;

        // ヘッダー：HELP ＋ ページ見出し
        Txt(wr, "WindowTitle", windowTitle, f, 26, accentColor, TextAnchor.MiddleLeft,
            new Vector2(left + 60 + 300, top - 50), new Vector2(600, 40)).fontStyle = FontStyle.Bold;
        pageTitle = Txt(wr, "PageTitle", "", f, titleFontSize, Color.white, TextAnchor.MiddleLeft,
                        new Vector2(left + 60 + 550, top - 100), new Vector2(1100, 60));
        pageTitle.fontStyle = FontStyle.Bold;
        Img(wr, "HeaderLine", new Vector2(0, top - 145), new Vector2(ws.x - 120, 2), null,
            new Color(accentColor.r, accentColor.g, accentColor.b, 0.5f));

        // ×ボタン
        var close = Img(wr, "CloseButton", new Vector2(ws.x * 0.5f - 55, top - 55), new Vector2(60, 60), null, new Color(1, 1, 1, 0.06f));
        close.raycastTarget = true;
        var bar1 = Img(close.transform, "X1", Vector2.zero, new Vector2(34, 5), null, Color.white);
        bar1.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        var bar2 = Img(close.transform, "X2", Vector2.zero, new Vector2(34, 5), null, Color.white);
        bar2.rectTransform.localRotation = Quaternion.Euler(0, 0, -45);
        var cb = close.gameObject.AddComponent<Button>();
        cb.targetGraphic = close;
        cb.onClick.AddListener(Close);

        // 本文エリア：左に画像、右に文章（画像なしなら文章を全幅）
        float bodyTop = top - 170, bodyBottom = -top + 120;
        float bodyH = bodyTop - bodyBottom, bodyY = (bodyTop + bodyBottom) * 0.5f;
        float half = (ws.x - 120 - 40) * 0.5f;

        pageImage = Img(wr, "PageImage", new Vector2(-(half + 40) * 0.5f, bodyY), new Vector2(half, bodyH), null, Color.white);
        pageImage.preserveAspect = true;

        pageText = Txt(wr, "PageText", "", f, textFontSize, new Color(1, 1, 1, 0.92f), TextAnchor.UpperLeft,
                       Vector2.zero, Vector2.zero);
        pageText.horizontalOverflow = HorizontalWrapMode.Wrap;
        pageText.lineSpacing = 1.25f;
        textRect = pageText.rectTransform;
        textRect.anchoredPosition = new Vector2(0, bodyY);
        textRect.sizeDelta = new Vector2(ws.x - 120, bodyH);

        // フッター：◀ 1/4 ▶
        float footY = -top + 60;
        prevButton = ArrowButton(wr, "PrevButton", new Vector2(-180, footY), false);
        prevButton.onClick.AddListener(PrevPage);
        nextButton = ArrowButton(wr, "NextButton", new Vector2(180, footY), true);
        nextButton.onClick.AddListener(NextPage);
        pageCounter = Txt(wr, "PageCounter", "", f, 30, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter,
                          new Vector2(0, footY), new Vector2(200, 50));

        // ？ボタン（ヘルプを開いている間は非表示）
        if (showHelpButton)
        {
            var bimg = Img(root, "HelpButton", helpButtonPos, Vector2.one * helpButtonSize,
                           helpButtonSprite != null ? helpButtonSprite : CircleSprite(),
                           helpButtonSprite != null ? Color.white : accentColor);
            bimg.raycastTarget = true;
            if (helpButtonSprite == null)
            {
                Img(bimg.transform, "Inner", Vector2.zero, Vector2.one * (helpButtonSize - 6f), CircleSprite(), windowColor);
                Txt(bimg.transform, "Mark", "?", f, Mathf.RoundToInt(helpButtonSize * 0.6f), Color.white,
                    TextAnchor.MiddleCenter, Vector2.zero, Vector2.one * helpButtonSize).fontStyle = FontStyle.Bold;
            }
            var b = bimg.gameObject.AddComponent<Button>();
            b.targetGraphic = bimg;
            b.onClick.AddListener(Toggle);
            helpButtonGo = bimg.gameObject;
        }
        else helpButtonGo = null;

        // すべて UI レイヤーに
        foreach (var t in canvasGo.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 5;

        // 本文エリアの横幅を記録（画像の有無で切り替える）
        bodyFullWidth = ws.x - 120;
        bodyHalfWidth = half;
        bodyCenterY = bodyY;
        bodyHeight = bodyH;
    }

    float bodyFullWidth, bodyHalfWidth, bodyCenterY, bodyHeight;

    /// <summary>
    /// ゲーム画面の Canvas より必ず手前になる描画順を返す。
    /// sortingOrder より手前の Canvas があれば、その 1 つ上にする（リザルト画面は除く）。
    /// </summary>
    int FrontSortingOrder()
    {
        int order = sortingOrder;
        var sb = new System.Text.StringBuilder("[Help] 画面（Canvas）の描画順：");
#if UNITY_2023_1_OR_NEWER
        var all = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
#else
        var all = FindObjectsOfType<Canvas>();
#endif
        foreach (var c in all)
        {
            if (c == null || !c.isRootCanvas || c.gameObject == canvasGo) continue;
            sb.Append($" {c.name}={c.sortingOrder}({c.renderMode})");
            if (c.name == "ResultCanvas") continue;          // リザルト画面より手前には出さない
            if (c.sortingOrder >= order) order = c.sortingOrder + 1;
        }
        sb.Append($" → HelpCanvas={order}");
        Debug.Log(sb.ToString());
        return order;
    }

    /// <summary>ゲーム画面ができた後に描画順を上げ直し、ヘルプ画面の状態をログに出す</summary>
    void RecheckAndLog()
    {
        if (runtimeCanvas == null) return;
        runtimeCanvas.sortingOrder = FrontSortingOrder();
        var rt = (RectTransform)runtimeCanvas.transform;
        string btn = helpButtonGo != null
            ? $"？ボタン 表示={helpButtonGo.activeInHierarchy} 画面上の位置={RectTransformUtility.WorldToScreenPoint(null, helpButtonGo.transform.position)}"
            : "？ボタン なし";
        Debug.Log($"[Help] 状態：Canvas 有効={runtimeCanvas.isActiveAndEnabled} 描画順={runtimeCanvas.sortingOrder} " +
                  $"表示先=Display{runtimeCanvas.targetDisplay + 1} 拡大率={runtimeCanvas.scaleFactor:0.00} " +
                  $"Canvas の大きさ={rt.rect.size} 画面サイズ={Screen.width}x{Screen.height} / {btn}");
    }

    Button ArrowButton(Transform parent, string name, Vector2 pos, bool right)
    {
        var bg = Img(parent, name, pos, new Vector2(90, 56), null, new Color(1, 1, 1, 0.06f));
        bg.raycastTarget = true;
        var tri = Img(bg.transform, "Arrow", Vector2.zero, new Vector2(26, 30), TriSprite(), accentColor);
        if (!right) tri.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
        var b = bg.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        var cb = b.colors;
        cb.disabledColor = new Color(1, 1, 1, 0.25f);
        b.colors = cb;
        return b;
    }

    void ShowPage(int index)
    {
        if (pageText == null) return;
        int count = pages != null ? pages.Count : 0;

        if (count == 0)
        {
            page = 0;
            pageTitle.text = "（ページ未設定）";
            pageText.text = "Inspector の pages にページを追加してください。";
            pageImage.gameObject.SetActive(false);
            SetTextWidth(false);
            pageCounter.text = "0 / 0";
            prevButton.interactable = nextButton.interactable = false;
            return;
        }

        page = Mathf.Clamp(index, 0, count - 1);
        var p = pages[page];
        pageTitle.text = p.title;
        pageText.text = p.text != null ? p.text.Replace("\\n", "\n") : "";

        Sprite img = p.image != null ? p.image : LoadResourceSprite(p.imageResource);
        bool hasImage = img != null;
        pageImage.gameObject.SetActive(hasImage);
        pageImage.sprite = img;
        SetTextWidth(hasImage);

        pageCounter.text = $"{page + 1} / {count}";
        prevButton.interactable = page > 0;
        nextButton.interactable = page < count - 1;
    }

    readonly Dictionary<string, Sprite> resourceCache = new Dictionary<string, Sprite>();

    /// <summary>Resources フォルダから画像を読み込む（Texture Type は Sprite (2D and UI) にしておく）</summary>
    Sprite LoadResourceSprite(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (resourceCache.TryGetValue(path, out var cached) && cached != null) return cached;
        var sp = Resources.Load<Sprite>(path);
        if (sp == null)
        {
            // Sprite 以外で取り込まれていた場合の保険（Texture2D から作る）
            var tex = Resources.Load<Texture2D>(path);
            if (tex != null) sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        if (sp != null) resourceCache[path] = sp;
        return sp;
    }

    void SetTextWidth(bool withImage)
    {
        if (withImage)
        {
            textRect.sizeDelta = new Vector2(bodyHalfWidth, bodyHeight);
            textRect.anchoredPosition = new Vector2((bodyHalfWidth + 40) * 0.5f, bodyCenterY);
        }
        else
        {
            textRect.sizeDelta = new Vector2(bodyFullWidth, bodyHeight);
            textRect.anchoredPosition = new Vector2(0, bodyCenterY);
        }
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
        circleSprite = MakeSprite(64, (x, y, n) =>
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
            return Mathf.Clamp01(n / 2f - d);
        });
        return circleSprite;
    }

    /// <summary>右向きの三角</summary>
    static Sprite TriSprite()
    {
        if (triSprite != null) return triSprite;
        triSprite = MakeSprite(64, (x, y, n) =>
        {
            float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
            float halfH = 0.5f * (1f - u);           // 右に行くほど細くなる
            return Mathf.Clamp01((halfH - Mathf.Abs(v - 0.5f)) * n);
        });
        return triSprite;
    }

    static Sprite MakeSprite(int n, System.Func<int, int, int, float> alpha)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                px[y * n + x] = new Color32(255, 255, 255, (byte)(alpha(x, y, n) * 255f));
        tex.SetPixels32(px);
        tex.Apply();
        var s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        s.hideFlags = HideFlags.DontSave;
        return s;
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

    // ======================= 入力 =======================

#if NEW_INPUT_ONLY
    static bool EscPressed()   => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    static bool TogglePressed() => Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame;
    static bool RightPressed() => Keyboard.current != null && Keyboard.current.rightArrowKey.wasPressedThisFrame;
    static bool LeftPressed()  => Keyboard.current != null && Keyboard.current.leftArrowKey.wasPressedThisFrame;
#else
    static bool EscPressed() => Input.GetKeyDown(KeyCode.Escape);
    static bool TogglePressed() => Input.GetKeyDown(KeyCode.H);
    static bool RightPressed() => Input.GetKeyDown(KeyCode.RightArrow);
    static bool LeftPressed() => Input.GetKeyDown(KeyCode.LeftArrow);
#endif
}