#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
#define NEW_INPUT_ONLY
#endif
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if NEW_INPUT_ONLY
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// 「ロジック」モード プロト。
/// 空の GameObject にアタッチして再生するだけで、Canvas 以下の画面一式をコードで構築します。
/// 非再生中もプレビュー画面を構築するので、ゲームビューで確認できます（シーンには保存されません）。
/// シーンビューでは円の中心・半径・回収点をハンドルでドラッグ編集できます（LogicModeScreenEditor）。
///
/// 画面構成（1920×1080 基準）
///   中央   : 人体図（UI Image。centerSprite に画像を設定）
///   円     : 人体を囲む円。回収点 4 つはこの円の上
///   四肢   : 回収点（三角）＋体力ゲージ
///   線     : 基本線 6 本 ＋ プレイヤーが描く造線
///   上中央 : 造線の硬さ 1/2/3（ノードが通れる回数）＋ 実行ボタン ▶
///   左上   : 経路フェーズの残り時間（仮）＋状態表示
///   右上   : スコア
///   下     : 造線ゲージ
///
/// フェーズ : 問題準備 → 経路(1分・造線を描く) → 実行(操作不可) → 次の問題 …
/// ノードは円の上では常に時計回りに進む
///   左中間 → 180° → 左腕 ／ 右中間 → 0° → 右足
///   左下の円弧（左足 → 180°）は左中間に合流、右上の円弧（右腕 → 0°）は右中間に合流
/// 欠損時
///   左腕欠損 → 上の円弧が完成し、左腕に届いたノードは頭上を回って右腕へ
///   右足欠損 → 下の円弧が完成し、右足に届いたノードは股下を回って左足へ
///   右腕欠損 → 右腕に届いたノードは円の右側を下って右足へ
///   左足欠損 → 左足に届いたノードは円の左側を上って左腕へ
///   流れ先も欠損していれば、さらに時計回りに次の部位へ
/// 出現モード（Inspector の spawnMode）: 自動 / 指定出現(logic_problems.csv) / 確率出現
/// 危険演出 : SAN が減るほど・部位が欠損するほど、画面の外周から内側へ赤くなる（ダメージ時は一瞬強まる）
///
/// 造線の操作（経路フェーズのみ）
///   基本線の上で左ドラッグ → 始点から伸ばした直線が最初に触れた別の基本線が終点
///   描画中は点線（白=確定可能 / 赤=不可）、離すと実線で確定
///   右クリック : 描画キャンセル / 造線の削除（ゲージ返却）※プロト用
/// </summary>
[ExecuteAlways]
public class LogicModeScreen : MonoBehaviour
{
    // ======================= Inspector =======================

    [Header("中央の人体図（UI Image）")]
    public Sprite centerSprite;                          // 中央に表示する人体図の画像。未設定なら仮の板＋文字を表示
    public Vector2 centerSize = new Vector2(820, 820);   // 人体図の表示サイズ（px。画面は 1920×1080 基準）
    public Vector2 centerOffset = new Vector2(0, 20);    // 画面中央からのずらし量（px。右・上が正）
    public bool preserveAspect = true;                   // true = 画像の縦横比を保って centerSize 内に収める

    [Header("人体を囲む円（シーンビューでドラッグ編集可）")]
    public Vector2 circleCenter = new Vector2(0, 20);    // 円の中心（centerOffset と揃えるのが基本）
    public float circleRadius = 285f;                    // 円の半径（px）。回収点（四肢の三角）はこの円の上に並ぶ
    [Range(0.1f, 89.9f)] public float armAngle = 36f;    // 右腕の角度（度。右=0、上が正）。左腕は左右反転した位置
    [Range(-89.9f, -0.1f)] public float legAngle = -67f; // 右足の角度（度。下がマイナス）。左足は左右反転した位置
    public int arcSegments = 96;                         // 円1周あたりの分割数（多いほど滑らか。円弧の当たり判定もこの細かさ）

    [Header("フォント（未設定ならビルトインフォント）")]
    public Font uiFont;                                  // UI の文字に使うフォント。日本語が化ける場合はここに日本語フォントを設定

    public enum SpawnMode
    {
        Auto,     // 問題CSVがあれば指定出現、無ければ確率出現
        Fixed,    // 指定出現：logic_problems.csv の通りに出す
        Random    // 確率出現：ランダム（今は nodesPerProblem / badRate を使用）
    }

    [Header("出現モード")]
    public SpawnMode spawnMode = SpawnMode.Auto;         // 指定出現 / 確率出現の切り替え

    [Header("CSV（StreamingAssets優先 → TextAsset → どちらも無ければInspectorの値）")]
    public bool useStreamingAssets = true;               // true = StreamingAssets フォルダの CSV を最優先で読む（ビルド後も差し替え可）
    public string paramsCsvName = "logic_params.csv";    // 参照数値の CSV ファイル名（StreamingAssets 内）。読めた値は下の Inspector 値を上書き
    public string nodeTableCsvName = "node_table.csv";   // ノード表（スコア・ダメージ等の Lv 別テーブル）の CSV ファイル名
    public string problemsCsvName = "logic_problems.csv";// 問題（指定出現のノードの並び）の CSV ファイル名
    public TextAsset paramsCsv;                          // StreamingAssets に無いときに使う参照数値 CSV
    public TextAsset nodeTableCsv;                       // StreamingAssets に無いときに使うノード表 CSV
    public TextAsset problemsCsv;                        // StreamingAssets に無いときに使う問題 CSV

    [Header("参照数値：体力・SAN")]
    public float limbMaxHp = 25f;                        // 四肢それぞれの最大体力。0 になるとその部位は欠損
    public bool killMiddleOnLegLost = false;             // true = 足が欠損したら同じ側の中間線（と円の右半分/左半分）を消す（旧仕様）
    public float maxSan = 40f;                           // 正気度の最大値。悪性ノード回収で減る（現状、0 になっても効果なし）

    [Header("参照数値：造線")]
    public float maxLineGauge = 2000f;                   // 造線ゲージの最大値（造線を引くと消費、消すと返却）
    public float armLostGaugePenalty = 500f;             // 腕が1本欠損するごとに造線ゲージの上限が減る量
    public float costPerMeter = 100f;                    // 造線 1m あたりのゲージ消費量
    public float pixelsPerMeter = 100f;                  // 1m を何 px とみなすか（仮）。コスト = 長さ(px) ÷ これ × costPerMeter
    public enum VanishMode { Count, Seconds }            // 造線の消え方：Count = ノード通過回数 / Seconds = 経過秒数
    public VanishMode vanishMode = VanishMode.Count;     // ロジックは回数（＝硬さ。硬さ1/2/3 = 通れる回数）
    public float vanishSeconds = 7f;                     // Seconds 時の造線の寿命（秒）。実行フェーズ中のみ減る
    public float warnBeforeSeconds = 2f;                 // Seconds 時、残りこの秒数以下で点滅（消滅準備）
    public int warnBeforeCount = 1;                      // Count 時、残り通過回数がこれ以下で点滅（消滅準備）
    public float blinkSpeed = 1.5f;                      // 消滅準備の点滅速度（往復/秒）
    [Range(0f, 1f)] public float vanishAlpha = 0.3f;     // 点滅の最低不透明度。消滅待ち（ノードが乗ったまま寿命切れ）の造線もこの濃さ

    [Header("参照数値：ノード・スコア")]
    public float routePhaseSeconds = 60f;                // 経路フェーズ（造線を描ける時間）の制限時間（秒）
    public float nodeStartGauge = 100f;                  // ノード出現時のゲージ量（問題 CSV で gauge 未指定のときに使用）
    public float gaugePerSecond = 10f;                   // ノードのゲージが 1 秒あたりに増減する量（0〜100 を往復、0 で良性⇔悪性が反転）
    public int[] goodScore = { 1000, 1500, 2000, 3000 }; // 良性ノード回収時のスコア（Lv1〜4）
    public int[] badScore = { 500, 350, 200, 50 };       // 悪性ノード回収時のスコア（Lv1〜4）
    public float[] badDamage = { 5, 7, 10, 15 };         // 悪性ノード回収時に、その部位が受けるダメージ（Lv1〜4）
    public float[] badSan = { 3, 5, 8, 10 };             // 悪性ノード回収時の SAN 減少量（Lv1〜4）
    public int scoreDigits = 8;                          // スコアの表示桁数（ゼロ埋め）。この桁の最大値で頭打ち

    [Header("危険演出（画面の外周から内側へ赤くなる）")]
    public Color dangerColor = new Color(0.85f, 0f, 0f, 0.85f); // 赤さの色。α が最大時の濃さ
    [Range(0f, 1f)] public float lostLimbDanger = 0.25f;       // 部位が1か所欠損するごとに加算される赤さ（SAN分と合計、最大1）
    [Range(0f, 1f)] public float damageFlash = 0.35f;          // ダメージを受けた瞬間に一時的に強まる量（欠損時はこの2倍）
    public float dangerFadeSpeed = 1.5f;                       // 赤さが目標値へ追従する速さ（/秒）
    public float flashDecay = 2f;                              // 一時的な強まりが引く速さ（/秒）
    [Range(1f, 4f)] public float dangerStartScale = 2.2f;      // 赤さ0のときの赤枠の大きさ（画面比）。大きいほど赤が外から入ってくる距離が長い
    [Range(0f, 1f)] public float previewDanger = 0f;           // 非再生中のプレビュー用。見た目の確認・調整に使う

    [Header("造線の操作")]
    public float snapRadius = 22f;                       // 基本線をクリックできる距離（px）。終点判定の伸ばし分にも使用
    public float forbiddenRadius = 45f;                  // 既存の造線の両端からこの距離（px）以内では新しい造線を始められない
    public float minLineLength = 30f;                    // 造線の最短長（px）。これより短いと確定できない

    [Header("ノード移動・問題（確率出現用）")]
    public float nodeSpeed = 160f;                       // ノードの移動速度（px/秒）
    public int problemCount = 3;                         // 確率出現時の問題数（指定出現では問題 CSV の問題数を使用）
    public int nodesPerProblem = 5;                      // 確率出現時、1問あたりのノード数
    public float spawnInterval = 1.5f;                   // ノードの出現間隔（秒）。確率出現時、または CSV で time 未指定のとき
    public float previewRevealInterval = 0.4f;           // 問題準備フェーズで、出題ノードを1つずつ見せる間隔（秒）
    [Range(0f, 1f)] public float badRate = 0.4f;         // 確率出現時に悪性ノードになる確率（0〜1）

    // ======================= 公開状態 =======================

    public enum Phase { Preview, Route, Execute, Clear, GameOver }
    public Phase CurrentPhase { get; private set; }
    public int SelectedHardness { get; private set; } = 1;
    public int Score { get; private set; }

    /// <summary>画面のルート（エディタのハンドルが座標変換に使用）</summary>
    public RectTransform CanvasRoot => root;

    // ======================= 内部データ =======================

    class Limb
    {
        public string name;
        public Vector2 pos;
        public bool isArm;
        public float hp;
        public bool lost;
        public RectTransform root;
        public Image hpFill;
        public BasicLine overflow;   // 欠損後、ここに届いたノードが流れる先（円弧）
    }

    class BasicLine
    {
        public string name;
        public Limb target;
        public bool alive = true;
        public bool spawnable = true;   // false = ノードの出現元にしない（円弧用）
        public BasicLine next;          // 終点まで来たら合流する線（null なら回収点）
        public float nextDist;          // 合流先の線上の位置
        public readonly List<Vector2> pts = new List<Vector2>();
        public readonly List<float> cum = new List<float>();
        public readonly List<Graphic> visuals = new List<Graphic>();
        public float length;

        public void Bake()
        {
            cum.Clear(); cum.Add(0f); length = 0f;
            for (int i = 1; i < pts.Count; i++) { length += Vector2.Distance(pts[i - 1], pts[i]); cum.Add(length); }
        }

        public Vector2 Sample(float d)
        {
            d = Mathf.Clamp(d, 0f, length);
            for (int i = 1; i < pts.Count; i++)
            {
                if (d <= cum[i])
                {
                    float seg = cum[i] - cum[i - 1];
                    return Vector2.Lerp(pts[i - 1], pts[i], seg > 0.0001f ? (d - cum[i - 1]) / seg : 0f);
                }
            }
            return pts[pts.Count - 1];
        }

        /// <summary>点 p に最も近い線上の点と、その点の始点からの距離</summary>
        public float Closest(Vector2 p, out Vector2 point, out float along)
        {
            float best = float.MaxValue; point = pts[0]; along = 0f;
            for (int i = 1; i < pts.Count; i++)
            {
                Vector2 a = pts[i - 1], b = pts[i], ab = b - a;
                float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                Vector2 q = a + ab * t;
                float d = Vector2.Distance(p, q);
                if (d < best) { best = d; point = q; along = cum[i - 1] + ab.magnitude * t; }
            }
            return best;
        }
    }

    enum DrawnState { Solid, Warning, VanishPending }

    class DrawnLine
    {
        public Vector2 a, b;
        public BasicLine lineA, lineB;
        public float distA, distB, length, cost;
        public int remaining;
        public float lifeLeft;
        public DrawnState state;
        public int occupants;
        public RectTransform root;
        public Image body;
        public Text label;
        public GameObject forbiddenGroup;
    }

    struct ProblemNode { public int line; public ProtoNode.Kind kind; public float gauge; public float time; }

    List<List<CsvProblemNode>> csvProblems;
    bool useFixedSpawn;   // 実際に指定出現で動いているか（spawnMode と CSV の有無から決定）

    class MovingNode
    {
        public ProtoNode node;
        public BasicLine line;
        public float dist;
        public DrawnLine onDrawn;
        public bool fromA;
        public float drawnPos;
    }

    struct DrawEval
    {
        public bool valid;
        public Vector2 end;
        public BasicLine endLine;
        public float endDist, length, cost;
        public string reason;
    }

    // 色
    static readonly Color LineColor = Color.white;
    static readonly Color DrawnColor = new Color(0.75f, 0.92f, 1f);
    static readonly Color InvalidCol = new Color(1f, 0.35f, 0.3f);
    static readonly Color LimbFill = new Color(0.94f, 0.66f, 0.55f);
    static readonly Color LimbOutline = new Color(0.10f, 0.16f, 0.22f);
    static readonly Color BtnOff = new Color(0.12f, 0.33f, 0.45f);
    static readonly Color BtnOn = new Color(0.25f, 0.62f, 0.85f);
    const float LineThickness = 4f;
    const float DrawnThickness = 6f;

    const string RuntimeCanvasName = "LogicModeCanvas";
    const string PreviewCanvasName = "LogicModePreview";

    Font font;
    RectTransform root, lineLayer, drawnLayer, limbLayer, nodeLayer, uiLayer;
    Image centerImage;
    Text scoreText, timerText, statusText, overlayText, gaugeText, hintText;
    Image gaugeFill, gaugePreviewFill, gaugeLostFill;
    readonly List<Button> hardnessButtons = new List<Button>();
    Button executeButton;

    Limb leftArm, rightArm, leftLeg, rightLeg;
    readonly List<Limb> limbs = new List<Limb>();
    readonly List<BasicLine> lines = new List<BasicLine>();
    readonly List<Vector2[]> obstacles = new List<Vector2[]>();   // 経路にならない円弧部分（跨げない）
    readonly List<DrawnLine> drawnLines = new List<DrawnLine>();
    readonly List<MovingNode> moving = new List<MovingNode>();

    // 描画中
    bool drawing;
    BasicLine drawStartLine;
    float drawStartDist;
    Vector2 drawStart;
    readonly List<Image> dashPool = new List<Image>();
    Image startMarker;

    // 危険演出
    Image dangerVignette;
    float dangerLevel, dangerFlash;
    static Sprite vignetteSprite;

    float san, currentMaxLineGauge, lineGauge, timeLeft;
    int problemIndex;
    bool executeRequested;

    // ======================= ライフサイクル =======================

    void Start()
    {
        if (!Application.isPlaying) return;

        DestroyPreview();
        ClearBuiltState();

        Time.timeScale = 1f;
        font = ResolveFont();
        LoadCsv();
        san = maxSan;
        currentMaxLineGauge = maxLineGauge;
        lineGauge = maxLineGauge;

        EnsureEventSystem();
        BuildScreen(RuntimeCanvasName);
        StartCoroutine(GameLoop());
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        if (CurrentPhase == Phase.Route) HandleDrawingInput();
        else if (drawing) CancelDrawing();

        UpdateDrawnVisuals();
        UpdateStatus();
        UpdateDanger(Time.unscaledDeltaTime);   // ゲームオーバー中（timeScale=0）も動かす

        if ((CurrentPhase == Phase.GameOver || CurrentPhase == Phase.Clear) && RestartPressed())
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    // ======================= 編集用プレビュー（非再生中） =======================

#if UNITY_EDITOR
    void OnEnable()
    {
        if (!Application.isPlaying) SchedulePreviewRebuild();
    }

    void OnDisable()
    {
        if (!Application.isPlaying) DestroyPreview();
    }

    void OnValidate()
    {
        if (!Application.isPlaying) SchedulePreviewRebuild();
    }

    void SchedulePreviewRebuild()
    {
        // OnValidate / OnEnable 中は生成・破棄ができないので次フレームで行う
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && isActiveAndEnabled && !Application.isPlaying) RebuildPreview();
        };
    }
#endif

    /// <summary>非再生中のプレビュー画面を作り直す（シーンには保存されない）</summary>
    public void RebuildPreview()
    {
        if (Application.isPlaying) return;
        DestroyPreview();
        ClearBuiltState();

        font = ResolveFont();
        san = maxSan;
        currentMaxLineGauge = maxLineGauge;
        lineGauge = maxLineGauge;
        CurrentPhase = Phase.Preview;

        BuildScreen(PreviewCanvasName);
        UpdateStatus();
        dangerLevel = previewDanger;   // Inspector のスライダーで見た目を確認
        ApplyDanger();

        var canvas = transform.Find(PreviewCanvasName);
        if (canvas != null)
            foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSave;
    }

    void DestroyPreview()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i);
            if (c.name != PreviewCanvasName) continue;
            if (Application.isPlaying) Destroy(c.gameObject);
            else DestroyImmediate(c.gameObject);
        }
        root = null;
    }

    void ClearBuiltState()
    {
        hardnessButtons.Clear();
        limbs.Clear();
        lines.Clear();
        obstacles.Clear();
        drawnLines.Clear();
        moving.Clear();
        dashPool.Clear();
        drawing = false;
    }

    void LoadCsv()
    {
        string t = LogicCsvLoader.LoadText(paramsCsv, paramsCsvName, useStreamingAssets);
        if (t != null) LogicCsvLoader.ApplyParams(this, t, "logic_params");

        t = LogicCsvLoader.LoadText(nodeTableCsv, nodeTableCsvName, useStreamingAssets);
        if (t != null) LogicCsvLoader.ApplyNodeTable(this, t);

        t = LogicCsvLoader.LoadText(problemsCsv, problemsCsvName, useStreamingAssets);
        if (t != null)
        {
            csvProblems = LogicCsvLoader.LoadProblems(t);
            if (csvProblems.Count == 0) csvProblems = null;
        }

        // 出現モードの決定
        useFixedSpawn = spawnMode == SpawnMode.Fixed ||
                        (spawnMode == SpawnMode.Auto && csvProblems != null);
        if (useFixedSpawn && csvProblems == null)
        {
            Debug.LogWarning("[CSV] 指定出現が選ばれていますが問題CSVがありません → 確率出現で動かします");
            useFixedSpawn = false;
        }

        if (useFixedSpawn) problemCount = csvProblems.Count;   // 問題数はCSVに合わせる
        Debug.Log(useFixedSpawn ? "[出現] 指定出現（問題CSV）" : $"[出現] 確率出現（{problemCount}問）");
    }

    /// <summary>CSVの line 列 → 基本線。英名・日本語どちらでも可</summary>
    int FindLineIndex(string name)
    {
        string key = name.Trim();
        switch (key)
        {
            case "左上": key = "TopLeft"; break;
            case "右上": key = "TopRight"; break;
            case "左中": case "左中間": key = "MiddleLeft"; break;
            case "右中": case "右中間": key = "MiddleRight"; break;
            case "左下": key = "BottomLeft"; break;
            case "右下": key = "BottomRight"; break;
        }
        for (int i = 0; i < lines.Count; i++)
            if (string.Equals(lines[i].name, key, System.StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    public void SetCenterSprite(Sprite s)
    {
        centerSprite = s;
        if (centerImage != null)
        {
            centerImage.sprite = s;
            centerImage.color = s != null ? Color.white : new Color(0.93f, 0.87f, 0.72f);
        }
    }

    // ======================= 画面構築 =======================

    void BuildScreen(string canvasName)
    {
        var canvasGo = new GameObject(canvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        root = UIShapes.CreateRect(canvasGo.transform, "GameScreen", Vector2.zero, new Vector2(1920, 1080));
        root.gameObject.AddComponent<RectMask2D>();
        UIShapes.CreateImage(root, "Background", Vector2.zero, new Vector2(1920, 1080), UIShapes.White, Color.black);

        centerImage = UIShapes.CreateImage(root, "CenterImage", centerOffset, centerSize, centerSprite, Color.white);
        centerImage.preserveAspect = preserveAspect;
        if (centerSprite == null)
        {
            centerImage.sprite = UIShapes.White;
            centerImage.color = new Color(0.93f, 0.87f, 0.72f);
            UIShapes.CreateText(centerImage.transform, "Placeholder", "Center Image\n(centerSprite を設定)", font, 32,
                                new Color(0.4f, 0.32f, 0.2f), TextAnchor.MiddleCenter, Vector2.zero, centerSize, new Vector2(0.5f, 0.5f));
        }

        // 危険演出（人体図の上、線・ノード・UI の下）
        dangerVignette = UIShapes.CreateImage(root, "DangerVignette", Vector2.zero, new Vector2(1920, 1080), GetVignetteSprite(), dangerColor);
        dangerVignette.raycastTarget = false;
        dangerLevel = 0f;
        dangerFlash = 0f;
        ApplyDanger();

        lineLayer = UIShapes.CreateRect(root, "Lines", Vector2.zero, new Vector2(1920, 1080));
        drawnLayer = UIShapes.CreateRect(root, "DrawnLines", Vector2.zero, new Vector2(1920, 1080));
        limbLayer = UIShapes.CreateRect(root, "Limbs", Vector2.zero, new Vector2(1920, 1080));
        nodeLayer = UIShapes.CreateRect(root, "Nodes", Vector2.zero, new Vector2(1920, 1080));
        uiLayer = UIShapes.CreateRect(root, "UI", Vector2.zero, new Vector2(1920, 1080));

        BuildLimbs();
        BuildLines();
        BuildUI();
    }

    void BuildLimbs()
    {
        // 回収点は円の上。画面上の左右で命名（プロト）
        leftArm = CreateLimb("LeftArm", CirclePt(180f - armAngle), true);
        rightArm = CreateLimb("RightArm", CirclePt(armAngle), true);
        leftLeg = CreateLimb("LeftLeg", CirclePt(180f - legAngle), false);
        rightLeg = CreateLimb("RightLeg", CirclePt(legAngle), false);
    }

    Limb CreateLimb(string name, Vector2 pos, bool isArm)
    {
        var limb = new Limb { name = name, pos = pos, isArm = isArm, hp = limbMaxHp };
        limb.root = UIShapes.CreateRect(limbLayer, name, pos, new Vector2(80, 80));

        Sprite tri = isArm ? UIShapes.TriDown : UIShapes.TriUp;
        UIShapes.CreateImage(limb.root, "Outline", Vector2.zero, new Vector2(78, 78), tri, LimbOutline);
        UIShapes.CreateImage(limb.root, "Fill", Vector2.zero, new Vector2(62, 62), tri, LimbFill);

        Vector2 barPos = new Vector2(0, isArm ? 52 : -52);
        UIShapes.CreateImage(limb.root, "HpBg", barPos, new Vector2(74, 10), UIShapes.White, new Color(0, 0, 0, 0.7f));
        limb.hpFill = UIShapes.CreateImage(limb.root, "HpFill", barPos, new Vector2(70, 6), UIShapes.White, new Color(0.3f, 0.9f, 0.4f));
        limb.hpFill.type = Image.Type.Filled;
        limb.hpFill.fillMethod = Image.FillMethod.Horizontal;

        limbs.Add(limb);
        return limb;
    }

    void BuildLines()
    {
        float lArm = 180f - armAngle, lLeg = 180f - legAngle;   // 左側の角度
        float rArm = armAngle, rLeg = legAngle;                 // 右側の角度

        MakeLine("TopLeft", leftArm, new List<Vector2> { new Vector2(-960, 540), leftArm.pos });
        MakeLine("TopRight", rightArm, new List<Vector2> { new Vector2(960, 540), rightArm.pos });
        MakeLine("BottomLeft", leftLeg, new List<Vector2> { new Vector2(-960, -540), leftLeg.pos });
        MakeLine("BottomRight", rightLeg, new List<Vector2> { new Vector2(960, -540), rightLeg.pos });

        // 左右中間 → 円に当たったら時計回り（角度が減る方向）に進む
        //   左：180° → 左腕　／　右：0° → 右足
        var ml = new List<Vector2> { new Vector2(-960, circleCenter.y), CirclePt(180f) };
        AddArcPoints(ml, 180f, lArm);
        var middleLeft = MakeLine("MiddleLeft", leftArm, ml);

        var mr = new List<Vector2> { new Vector2(960, circleCenter.y), CirclePt(0f) };
        AddArcPoints(mr, 0f, rLeg);
        var middleRight = MakeLine("MiddleRight", rightLeg, mr);

        // 円の残り（左下・右上）も基本線。時計回りに進んで中間線に合流
        var lowLeft = new List<Vector2> { CirclePt(lLeg) };
        AddArcPoints(lowLeft, lLeg, 180f);                      // 左足 → 180°
        var arcLL = MakeLine("ArcLowerLeft", leftArm, lowLeft);
        arcLL.spawnable = false;
        arcLL.next = middleLeft;
        arcLL.nextDist = middleLeft.cum[1];                     // 180° の点

        var upRight = new List<Vector2> { CirclePt(rArm) };
        AddArcPoints(upRight, rArm, 0f);                        // 右腕 → 0°
        var arcUR = MakeLine("ArcUpperRight", rightLeg, upRight);
        arcUR.spawnable = false;
        arcUR.next = middleRight;
        arcUR.nextDist = middleRight.cum[1];                    // 0° の点

        // 欠損時の流れ先（時計回りで次の部位へ）
        //   右腕欠損 → 右上の円弧 → 右中間の円弧 → 右足
        //   左足欠損 → 左下の円弧 → 左中間の円弧 → 左腕
        //   左腕・右足は欠損時に上下の円弧を追加（LoseLimb）
        rightArm.overflow = arcUR;
        leftLeg.overflow = arcLL;
    }

    BasicLine MakeLine(string name, Limb target, List<Vector2> pts, int visibleSegments = -1)
    {
        var l = new BasicLine { name = name, target = target };
        l.pts.AddRange(pts);
        l.Bake();
        int segs = visibleSegments < 0 ? pts.Count - 1 : visibleSegments;
        for (int i = 0; i < segs; i++)
            l.visuals.Add(UIShapes.CreateLine(lineLayer, pts[i], pts[i + 1], LineThickness, LineColor, name));
        lines.Add(l);
        return l;
    }

    /// <summary>欠損した部位 from から時計回りに to まで続く円弧の基本線を追加</summary>
    void AddOverflowArc(Limb from, Limb to, float fromDeg, float toDeg, string name)
    {
        var pts = new List<Vector2> { CirclePt(fromDeg) };
        AddArcPoints(pts, fromDeg, toDeg);
        var l = MakeLine(name, to, pts);
        l.spawnable = false;
        from.overflow = l;
    }

    /// <summary>円周上の点（角度は度。右=0、反時計回りが正）</summary>
    public Vector2 CirclePt(float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        return circleCenter + new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * circleRadius;
    }

    int ArcSegCount(float spanDeg) => Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(spanDeg) / 360f * Mathf.Max(8, arcSegments)));

    /// <summary>from → to の円弧上の点を追加（from 自体は含まない）</summary>
    void AddArcPoints(List<Vector2> list, float fromDeg, float toDeg)
    {
        int n = ArcSegCount(toDeg - fromDeg);
        for (int i = 1; i <= n; i++) list.Add(CirclePt(Mathf.Lerp(fromDeg, toDeg, (float)i / n)));
    }

    void DrawCircleArc(float fromDeg, float toDeg, string name, bool obstacle)
    {
        int n = ArcSegCount(toDeg - fromDeg);
        for (int i = 0; i < n; i++)
        {
            Vector2 p0 = CirclePt(Mathf.Lerp(fromDeg, toDeg, (float)i / n));
            Vector2 p1 = CirclePt(Mathf.Lerp(fromDeg, toDeg, (float)(i + 1) / n));
            UIShapes.CreateLine(lineLayer, p0, p1, LineThickness, LineColor, name);
            if (obstacle) obstacles.Add(new[] { p0, p1 });
        }
    }

    void BuildUI()
    {
        // 右上：スコア
        scoreText = UIShapes.CreateText(uiLayer, "Score", "", font, 40, Color.white, TextAnchor.UpperRight,
                                        new Vector2(700, 525), new Vector2(400, 50), new Vector2(1f, 1f));

        // 左上：残り時間（仮）＋状態
        timerText = UIShapes.CreateText(uiLayer, "Timer", "", font, 40, Color.white, TextAnchor.UpperLeft,
                                        new Vector2(-940, 528), new Vector2(500, 50), new Vector2(0f, 1f));
        statusText = UIShapes.CreateText(uiLayer, "Status", "", font, 22, new Color(1, 1, 1, 0.8f), TextAnchor.UpperLeft,
                                         new Vector2(-940, 476), new Vector2(600, 120), new Vector2(0f, 1f));

        // 上中央：硬さボタン 1 / 2 / 3
        for (int i = 0; i < 3; i++)
        {
            int hardness = i + 1;
            var btn = CreateButton("Hardness" + hardness, new Vector2(-110 + i * 110, 512), new Vector2(100, 38), BtnOff);
            for (int d = 0; d < hardness; d++)
            {
                float x = (d - (hardness - 1) * 0.5f) * 22f;
                UIShapes.CreateImage(btn.transform, "Dot", new Vector2(x, 0), new Vector2(16, 16), UIShapes.Circle, Color.white);
            }
            btn.onClick.AddListener(() => SelectHardness(hardness));
            hardnessButtons.Add(btn);
        }

        // ② 実行ボタン
        executeButton = CreateButton("Execute", new Vector2(0, 466), new Vector2(70, 38), BtnOff);
        UIShapes.CreateImage(executeButton.transform, "Play", new Vector2(2, 0), new Vector2(24, 24), UIShapes.TriRight, new Color(0.45f, 0.9f, 0.5f));
        executeButton.onClick.AddListener(() => { if (CurrentPhase == Phase.Route) executeRequested = true; });

        // 下：造線ゲージ
        Vector2 barPos = new Vector2(0, -505);
        Vector2 barSize = new Vector2(1000, 38);
        UIShapes.CreateImage(uiLayer, "GaugeBorder", barPos, barSize + new Vector2(8, 8), UIShapes.White, new Color(0.35f, 0.65f, 0.9f));
        UIShapes.CreateImage(uiLayer, "GaugeBg", barPos, barSize, UIShapes.White, new Color(0.08f, 0.1f, 0.14f));
        gaugePreviewFill = CreateFill("GaugePreview", barPos, barSize, new Color(1f, 0.6f, 0.2f), Image.OriginHorizontal.Left);
        gaugeFill = CreateFill("GaugeFill", barPos, barSize, Color.white, Image.OriginHorizontal.Left);
        gaugeLostFill = CreateFill("GaugeLost", barPos, barSize, new Color(0.45f, 0.05f, 0.05f), Image.OriginHorizontal.Right);
        gaugeText = UIShapes.CreateText(uiLayer, "GaugeText", "", font, 26, new Color(0.1f, 0.1f, 0.1f), TextAnchor.MiddleCenter,
                                        barPos, barSize, new Vector2(0.5f, 0.5f));
        gaugeText.gameObject.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.9f);

        // 描画中の補助
        startMarker = UIShapes.CreateImage(uiLayer, "DrawStart", Vector2.zero, new Vector2(16, 16), UIShapes.Circle, Color.white);
        startMarker.gameObject.SetActive(false);
        hintText = UIShapes.CreateText(uiLayer, "Hint", "", font, 22, InvalidCol, TextAnchor.LowerLeft,
                                       Vector2.zero, new Vector2(400, 30), new Vector2(0f, 0f));
        hintText.gameObject.AddComponent<Outline>().effectColor = Color.black;

        overlayText = UIShapes.CreateText(uiLayer, "Overlay", "", font, 96, Color.white, TextAnchor.MiddleCenter,
                                          Vector2.zero, new Vector2(1600, 300), new Vector2(0.5f, 0.5f));
        overlayText.gameObject.AddComponent<Outline>().effectColor = Color.black;

        SelectHardness(1);
        SetRouteInteractable(false);
        UpdateGaugeUI(0f);
        UpdateScore();
    }

    Image CreateFill(string name, Vector2 pos, Vector2 size, Color c, Image.OriginHorizontal origin)
    {
        var img = UIShapes.CreateImage(uiLayer, name, pos, size, UIShapes.White, c);
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)origin;
        return img;
    }

    Button CreateButton(string name, Vector2 pos, Vector2 size, Color color)
    {
        var img = UIShapes.CreateImage(uiLayer, name, pos, size, UIShapes.White, color);
        img.raycastTarget = true;
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
        btn.colors = cb;
        return btn;
    }

    // ======================= ゲーム進行 =======================

    IEnumerator GameLoop()
    {
        for (problemIndex = 0; problemIndex < problemCount; problemIndex++)
        {
            var problem = GenerateProblem();

            // --- 問題準備フェーズ ---
            CurrentPhase = Phase.Preview;
            var previews = new List<ProtoNode>();
            var perLine = new int[lines.Count];
            for (int i = 0; i < problem.Count; i++)
            {
                var line = lines[problem[i].line];
                if (!line.alive) continue;
                int k = perLine[problem[i].line]++;
                var n = ProtoNode.Create(nodeLayer, problem[i].kind, problem[i].gauge, font);
                n.SetOrder(i + 1);
                n.Rect.anchoredPosition = line.Sample(45f + k * 64f);
                previews.Add(n);
                yield return new WaitForSeconds(previewRevealInterval);
            }

            // --- 経路フェーズ（硬さ選択・造線） ---
            CurrentPhase = Phase.Route;
            executeRequested = false;
            timeLeft = routePhaseSeconds;
            SetRouteInteractable(true);
            while (timeLeft > 0f && !executeRequested)
            {
                timeLeft = Mathf.Max(0f, timeLeft - Time.deltaTime);
                yield return null;
            }
            SetRouteInteractable(false);
            foreach (var p in previews) Destroy(p.gameObject);

            // --- 実行フェーズ（操作不可） ---
            CurrentPhase = Phase.Execute;
            int spawned = 0;
            float t = 0f;
            while (spawned < problem.Count || moving.Count > 0)
            {
                if (CurrentPhase == Phase.GameOver) yield break;
                t += Time.deltaTime;
                while (spawned < problem.Count && t >= problem[spawned].time)
                {
                    SpawnMoving(problem[spawned]);
                    spawned++;
                }
                UpdateMoving(Time.deltaTime);
                yield return null;
            }
            if (CurrentPhase == Phase.GameOver) yield break;
            yield return new WaitForSeconds(0.8f);
        }

        CurrentPhase = Phase.Clear;
        overlayText.text = "STAGE CLEAR\n<size=36>R でリスタート</size>";
    }

    List<ProblemNode> GenerateProblem()
    {
        var list = new List<ProblemNode>();

        // 指定出現
        if (useFixedSpawn && problemIndex < csvProblems.Count)
        {
            int i = 0;
            foreach (var c in csvProblems[problemIndex])
            {
                int li = FindLineIndex(c.line);
                if (li < 0) { Debug.LogWarning($"[CSV:problems] 問題{c.problem} 不明な line: {c.line}"); continue; }
                list.Add(new ProblemNode
                {
                    line = li,
                    kind = c.kind,
                    gauge = c.gauge >= 0f ? c.gauge : nodeStartGauge,
                    time = c.time >= 0f ? c.time : i * spawnInterval
                });
                i++;
            }
            // 出現時刻順（同時刻なら order 順のまま）
            return System.Linq.Enumerable.ToList(System.Linq.Enumerable.OrderBy(list, n => n.time));
        }

        // 確率出現（今はランダム。円弧からは出現しない）
        var alive = new List<int>();
        for (int i = 0; i < lines.Count; i++) if (lines[i].alive && lines[i].spawnable) alive.Add(i);
        for (int i = 0; i < nodesPerProblem && alive.Count > 0; i++)
            list.Add(new ProblemNode
            {
                line = alive[Random.Range(0, alive.Count)],
                kind = Random.value < badRate ? ProtoNode.Kind.Bad : ProtoNode.Kind.Good,
                gauge = nodeStartGauge,
                time = i * spawnInterval
            });
        return list;
    }

    // ======================= 造線の描画 =======================

    void HandleDrawingInput()
    {
        Vector2 p = MouseLocal();

        if (RightDown())
        {
            if (drawing) CancelDrawing();
            else TryDeleteDrawn(p);
            return;
        }

        if (!drawing && LeftDown() && !PointerOverUI())
            TryBeginDraw(p);

        if (!drawing) return;

        var e = EvaluateDraw(p);
        ShowPreview(e);
        if (LeftUp())
        {
            if (e.valid) CommitDraw(e);
            CancelDrawing();
        }
    }

    void TryBeginDraw(Vector2 p)
    {
        BasicLine best = null;
        float bestD = snapRadius, bestAlong = 0f;
        Vector2 bestPt = Vector2.zero;
        foreach (var l in lines)
        {
            if (!l.alive) continue;
            float d = l.Closest(p, out var pt, out var along);
            if (d <= bestD) { bestD = d; best = l; bestPt = pt; bestAlong = along; }
        }
        if (best == null) return;

        // 開始禁止範囲
        foreach (var d in drawnLines)
        {
            if (Vector2.Distance(bestPt, d.a) < forbiddenRadius || Vector2.Distance(bestPt, d.b) < forbiddenRadius)
            {
                FlashHint(bestPt, "開始禁止範囲です");
                return;
            }
        }

        drawing = true;
        drawStartLine = best;
        drawStartDist = bestAlong;
        drawStart = bestPt;
        startMarker.gameObject.SetActive(true);
        startMarker.rectTransform.anchoredPosition = bestPt;
    }

    /// <summary>始点から mouse 方向へ伸ばした直線が最初に触れる線を調べる</summary>
    DrawEval EvaluateDraw(Vector2 mouse)
    {
        var e = new DrawEval { end = mouse, valid = false };
        Vector2 s = drawStart;
        Vector2 dir = mouse - s;
        float len = dir.magnitude;
        if (len < 1f) { e.end = s; e.reason = ""; return e; }

        Vector2 n = dir / len;
        float reach = len + snapRadius;
        Vector2 far = s + n * reach;
        const float ignoreNearStart = 6f;

        float bestAlong = float.MaxValue;
        int hitKind = 0;               // 0=なし 1=基本線 2=跨げない
        BasicLine hitLine = null;
        float hitLineDist = 0f;

        foreach (var l in lines)
        {
            if (!l.alive) continue;
            for (int i = 1; i < l.pts.Count; i++)
            {
                if (!SegSeg(s, far, l.pts[i - 1], l.pts[i], out float t, out float u)) continue;
                float along = t * reach;
                if (along < ignoreNearStart || along >= bestAlong) continue;
                bestAlong = along; hitKind = 1; hitLine = l;
                hitLineDist = l.cum[i - 1] + (l.cum[i] - l.cum[i - 1]) * u;
            }
        }
        foreach (var o in obstacles)
        {
            if (!SegSeg(s, far, o[0], o[1], out float t, out _)) continue;
            float along = t * reach;
            if (along < ignoreNearStart || along >= bestAlong) continue;
            bestAlong = along; hitKind = 2; hitLine = null;
        }
        foreach (var d in drawnLines)
        {
            if (!SegSeg(s, far, d.a, d.b, out float t, out _)) continue;
            float along = t * reach;
            if (along < ignoreNearStart || along >= bestAlong) continue;
            bestAlong = along; hitKind = 2; hitLine = null;
        }

        if (hitKind == 0) { e.end = mouse; e.reason = "別の基本線まで伸ばしてください"; return e; }

        e.end = s + n * bestAlong;
        e.length = bestAlong;
        e.cost = bestAlong / pixelsPerMeter * costPerMeter;

        if (hitKind == 2) { e.reason = "線を跨いで書けません"; return e; }
        if (hitLine == drawStartLine) { e.reason = "同じ基本線には繋げません"; return e; }
        if (bestAlong < minLineLength) { e.reason = "短すぎます"; return e; }
        if (e.cost > lineGauge + 0.001f) { e.reason = "造線ゲージが足りません"; return e; }

        e.valid = true;
        e.endLine = hitLine;
        e.endDist = hitLineDist;
        e.reason = "";
        return e;
    }

    void ShowPreview(DrawEval e)
    {
        Color c = e.valid ? Color.white : InvalidCol;
        DrawDashed(drawStart, e.end, c);
        startMarker.color = c;

        hintText.text = e.valid ? $"-{e.cost:0}" : e.reason;
        hintText.color = e.valid ? new Color(1f, 0.75f, 0.4f) : InvalidCol;
        hintText.rectTransform.anchoredPosition = e.end + new Vector2(14, 10);

        UpdateGaugeUI(e.valid ? e.cost : 0f);
    }

    void CommitDraw(DrawEval e)
    {
        var d = new DrawnLine
        {
            a = drawStart,
            b = e.end,
            lineA = drawStartLine,
            lineB = e.endLine,
            distA = drawStartDist,
            distB = e.endDist,
            length = e.length,
            cost = e.cost,
            remaining = SelectedHardness,
            lifeLeft = vanishSeconds,
            state = DrawnState.Solid
        };

        d.root = UIShapes.CreateRect(drawnLayer, "DrawnLine", Vector2.zero, Vector2.zero);

        // 開始禁止範囲の表示（経路フェーズ中のみ）
        d.forbiddenGroup = new GameObject("Forbidden", typeof(RectTransform));
        d.forbiddenGroup.transform.SetParent(d.root, false);
        foreach (var pt in new[] { d.a, d.b })
        {
            UIShapes.CreateImage(d.forbiddenGroup.transform, "Area", pt, Vector2.one * forbiddenRadius * 2f, UIShapes.Circle, new Color(0.8f, 0.3f, 0.3f, 0.12f));
            UIShapes.CreateImage(d.forbiddenGroup.transform, "Ring", pt, Vector2.one * forbiddenRadius * 2f, UIShapes.Ring, new Color(0.85f, 0.4f, 0.4f, 0.5f));
        }

        d.body = UIShapes.CreateLine(d.root, d.a, d.b, DrawnThickness, DrawnColor, "Body");

        Vector2 mid = (d.a + d.b) * 0.5f;
        UIShapes.CreateImage(d.root, "LabelBg", mid, new Vector2(30, 30), UIShapes.Circle, new Color(0.1f, 0.2f, 0.3f, 0.9f));
        d.label = UIShapes.CreateText(d.root, "Label", "", font, 20, Color.white, TextAnchor.MiddleCenter,
                                      mid, new Vector2(30, 30), new Vector2(0.5f, 0.5f));

        lineGauge -= e.cost;
        drawnLines.Add(d);
        RefreshDrawnState(d);
    }

    void CancelDrawing()
    {
        drawing = false;
        startMarker.gameObject.SetActive(false);
        hintText.text = "";
        foreach (var img in dashPool) img.gameObject.SetActive(false);
        UpdateGaugeUI(0f);
    }

    void TryDeleteDrawn(Vector2 p)
    {
        for (int i = drawnLines.Count - 1; i >= 0; i--)
        {
            var d = drawnLines[i];
            if (DistPointSeg(p, d.a, d.b) <= 14f) { RemoveDrawn(d); return; }
        }
    }

    void DrawDashed(Vector2 a, Vector2 b, Color c)
    {
        const float dash = 14f, gap = 9f;
        float len = Vector2.Distance(a, b);
        Vector2 n = len > 0.001f ? (b - a) / len : Vector2.zero;
        int count = Mathf.CeilToInt(len / (dash + gap));
        while (dashPool.Count < count)
            dashPool.Add(UIShapes.CreateLine(uiLayer, Vector2.zero, Vector2.right, 4f, Color.white, "Dash"));

        for (int i = 0; i < dashPool.Count; i++)
        {
            var img = dashPool[i];
            if (i >= count) { img.gameObject.SetActive(false); continue; }
            float s0 = i * (dash + gap);
            float s1 = Mathf.Min(s0 + dash, len);
            img.gameObject.SetActive(true);
            img.color = c;
            UIShapes.SetLine(img, a + n * s0, a + n * s1, 4f, false);
        }
    }

    Coroutine hintRoutine;
    void FlashHint(Vector2 at, string msg)
    {
        if (hintRoutine != null) StopCoroutine(hintRoutine);
        hintRoutine = StartCoroutine(FlashHintCo(at, msg));
    }

    IEnumerator FlashHintCo(Vector2 at, string msg)
    {
        hintText.text = msg;
        hintText.color = InvalidCol;
        hintText.rectTransform.anchoredPosition = at + new Vector2(14, 10);
        yield return new WaitForSeconds(1f);
        if (!drawing) hintText.text = "";
    }

    // ======================= 造線の状態・消滅 =======================

    void RefreshDrawnState(DrawnLine d)
    {
        if (d.state == DrawnState.VanishPending) return;
        bool warn = vanishMode == VanishMode.Count
            ? d.remaining <= warnBeforeCount
            : d.lifeLeft <= warnBeforeSeconds;
        d.state = warn ? DrawnState.Warning : DrawnState.Solid;
    }

    void TickDrawnLifetimes(float dt)
    {
        for (int i = drawnLines.Count - 1; i >= 0; i--)
        {
            var d = drawnLines[i];
            if (d.state == DrawnState.VanishPending) continue;
            d.lifeLeft -= dt;
            if (d.lifeLeft <= 0f) Vanish(d);
            else RefreshDrawnState(d);
        }
    }

    /// <summary>消滅判定：上にノードが無ければ即削除、あれば α30 で固定し、通過後に削除</summary>
    void Vanish(DrawnLine d)
    {
        if (d.occupants <= 0) RemoveDrawn(d);
        else d.state = DrawnState.VanishPending;
    }

    void RemoveDrawn(DrawnLine d)
    {
        if (!drawnLines.Remove(d)) return;
        if (d.root != null) Destroy(d.root.gameObject);
        lineGauge = Mathf.Min(currentMaxLineGauge, lineGauge + d.cost);   // ゲージ返却
    }

    float BlinkAlpha()
    {
        float w = (Mathf.Cos(Time.time * blinkSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        return Mathf.Lerp(vanishAlpha, 1f, w);
    }

    float AlphaOf(DrawnLine d) =>
        d.state == DrawnState.Warning ? BlinkAlpha() :
        d.state == DrawnState.VanishPending ? vanishAlpha : 1f;

    void UpdateDrawnVisuals()
    {
        bool showForbidden = CurrentPhase == Phase.Route;
        foreach (var d in drawnLines)
        {
            float a = AlphaOf(d);
            var c = DrawnColor; c.a = a;
            d.body.color = c;
            d.label.text = vanishMode == VanishMode.Count ? d.remaining.ToString() : Mathf.CeilToInt(d.lifeLeft).ToString();
            d.forbiddenGroup.SetActive(showForbidden);
        }
        foreach (var m in moving)
            m.node.SetAlpha(m.onDrawn != null ? AlphaOf(m.onDrawn) : 1f);

        if (!drawing) UpdateGaugeUI(0f);
    }

    // ======================= ノード移動 =======================

    void SpawnMoving(ProblemNode pn)
    {
        var line = lines[pn.line];
        if (!line.alive) return;   // 消滅した基本線からは出現しない
        var n = ProtoNode.Create(nodeLayer, pn.kind, pn.gauge, font);
        n.Rect.anchoredPosition = line.Sample(0f);
        moving.Add(new MovingNode { node = n, line = line, dist = 0f });
    }

    void UpdateMoving(float dt)
    {
        if (vanishMode == VanishMode.Seconds) TickDrawnLifetimes(dt);

        for (int i = moving.Count - 1; i >= 0; i--)
        {
            var m = moving[i];
            m.node.TickGauge(dt, gaugePerSecond);
            float step = nodeSpeed * dt;

            // 造線の上
            if (m.onDrawn != null)
            {
                var d = m.onDrawn;
                m.drawnPos += step;
                Vector2 from = m.fromA ? d.a : d.b, to = m.fromA ? d.b : d.a;
                if (m.drawnPos >= d.length)
                {
                    // 反対側の基本線へ。そこからは回収点（中心）方向へ進む
                    m.line = m.fromA ? d.lineB : d.lineA;
                    m.dist = m.fromA ? d.distB : d.distA;
                    ExitDrawn(m);
                    m.node.Rect.anchoredPosition = m.line.Sample(m.dist);
                }
                else
                {
                    m.node.Rect.anchoredPosition = Vector2.Lerp(from, to, m.drawnPos / d.length);
                }
                continue;
            }

            // 基本線の上：通過区間に造線の端点があれば乗り換える
            float prev = m.dist, next = prev + step;
            var j = FindJunction(m.line, prev, next, out bool atA);
            if (j != null)
            {
                EnterDrawn(m, j, atA);
                m.node.Rect.anchoredPosition = atA ? j.a : j.b;
                continue;
            }

            m.dist = next;
            m.node.Rect.anchoredPosition = m.line.Sample(m.dist);
            if (m.dist >= m.line.length)
            {
                // 円弧の終点 → 中間線に合流
                if (m.line.next != null)
                {
                    var cur = m.line;
                    if (!cur.next.alive)
                    {
                        // 合流先が消えている → 消滅（スコア・ダメージなし）
                        Destroy(m.node.gameObject);
                        moving.RemoveAt(i);
                        continue;
                    }
                    m.line = cur.next;
                    m.dist = cur.nextDist;
                    m.node.Rect.anchoredPosition = m.line.Sample(m.dist);
                    continue;
                }

                // 欠損部位に届いたら、完成した円弧へ乗り換えて時計回りに進む
                var limb = m.line.target;
                if (limb.lost && limb.overflow != null && limb.overflow.alive)
                {
                    m.line = limb.overflow;
                    m.dist = 0f;
                    m.node.Rect.anchoredPosition = m.line.Sample(0f);
                    continue;
                }

                Collect(m);
                Destroy(m.node.gameObject);
                moving.RemoveAt(i);
                if (CurrentPhase == Phase.GameOver) return;
            }
        }
    }

    /// <summary>
    /// 基本線上の (prev, next] にある造線の端点を探す。
    /// 同じ位置に複数ある場合は、反対側の端点が中心に近い方を優先（中心の方向）。
    /// 消滅判定中（ノードが乗っている）の造線には移動できない。
    /// </summary>
    DrawnLine FindJunction(BasicLine line, float prev, float next, out bool atA)
    {
        DrawnLine best = null;
        float bestDist = float.MaxValue, bestCenter = float.MaxValue;
        atA = true;
        foreach (var d in drawnLines)
        {
            if (d.state == DrawnState.VanishPending) continue;
            if (!d.lineA.alive || !d.lineB.alive) continue;
            for (int side = 0; side < 2; side++)
            {
                bool isA = side == 0;
                var l = isA ? d.lineA : d.lineB;
                float jd = isA ? d.distA : d.distB;
                if (l != line || jd <= prev || jd > next) continue;
                float center = ((isA ? d.b : d.a) - circleCenter).magnitude;
                bool better = jd < bestDist - 0.5f || (Mathf.Abs(jd - bestDist) <= 0.5f && center < bestCenter);
                if (!better) continue;
                best = d; bestDist = jd; bestCenter = center; atA = isA;
            }
        }
        return best;
    }

    void EnterDrawn(MovingNode m, DrawnLine d, bool atA)
    {
        m.onDrawn = d;
        m.fromA = atA;
        m.drawnPos = 0f;
        d.occupants++;
        if (vanishMode == VanishMode.Count)
        {
            d.remaining--;
            if (d.remaining <= 0) d.state = DrawnState.VanishPending;   // 通り終わったら消える
            else RefreshDrawnState(d);
        }
    }

    void ExitDrawn(MovingNode m)
    {
        var d = m.onDrawn;
        m.onDrawn = null;
        m.node.SetAlpha(1f);
        d.occupants--;
        if (d.state == DrawnState.VanishPending && d.occupants <= 0) RemoveDrawn(d);
    }

    void Collect(MovingNode m)
    {
        var limb = m.line.target;
        if (limb.lost) return;   // TODO: 欠損済み部位に届いた場合の仕様確認

        int lv = m.node.Level - 1;
        if (m.node.CurrentKind == ProtoNode.Kind.Good)
        {
            AddScore(goodScore[lv]);
        }
        else
        {
            AddScore(badScore[lv]);
            san = Mathf.Max(0f, san - badSan[lv]);
            Damage(limb, badDamage[lv]);
        }
        StartCoroutine(Pop(limb.root));
    }

    void Damage(Limb limb, float dmg)
    {
        limb.hp = Mathf.Max(0f, limb.hp - dmg);
        limb.hpFill.fillAmount = limb.hp / limbMaxHp;
        limb.hpFill.color = Color.Lerp(new Color(0.95f, 0.25f, 0.2f), new Color(0.3f, 0.9f, 0.4f), limb.hp / limbMaxHp);
        dangerFlash = Mathf.Max(dangerFlash, damageFlash);   // ダメージで一瞬赤くなる
        if (limb.hp <= 0f) LoseLimb(limb);
    }

    void LoseLimb(Limb limb)
    {
        limb.lost = true;
        limb.root.gameObject.SetActive(false);
        dangerFlash = Mathf.Min(1f, Mathf.Max(dangerFlash, damageFlash * 2f));   // 欠損は強めに脈打つ

        if (limb.isArm)
        {
            currentMaxLineGauge = Mathf.Max(0f, currentMaxLineGauge - armLostGaugePenalty);
            lineGauge = Mathf.Min(lineGauge, currentMaxLineGauge);

            // 左腕欠損 → 上の円弧が完成。左腕 → 頭上 → 右腕（時計回り）
            if (limb == leftArm)
                AddOverflowArc(leftArm, rightArm, 180f - armAngle, armAngle, "TopArc");
        }
        else
        {
            if (killMiddleOnLegLost)
                KillLine(limb == leftLeg ? "MiddleLeft" : "MiddleRight");

            // 右足欠損 → 下の円弧が完成。右足 → 股下 → 左足（時計回り）
            if (limb == rightLeg)
                AddOverflowArc(rightLeg, leftLeg, legAngle, -180f - legAngle, "BottomArc");
        }

        bool allLost = true;
        foreach (var l in limbs) if (!l.lost) allLost = false;
        if (allLost) GameOver();
    }

    void KillLine(string name)
    {
        foreach (var l in lines.ToArray())
        {
            if (l.name != name) continue;
            KillLine(l);
        }
    }

    void KillLine(BasicLine l)
    {
        if (!l.alive) return;
        l.alive = false;
        foreach (var v in l.visuals) v.gameObject.SetActive(false);
        // 繋がっていた造線も消滅
        for (int i = drawnLines.Count - 1; i >= 0; i--)
        {
            var d = drawnLines[i];
            if (d.lineA == l || d.lineB == l) Vanish(d);
        }
        // この線に合流する円弧も消滅
        foreach (var other in lines.ToArray())
            if (other.next == l) KillLine(other);
    }

    void GameOver()
    {
        CurrentPhase = Phase.GameOver;
        SetRouteInteractable(false);
        overlayText.text = "GAME OVER\n<size=36>R でリスタート</size>";
        Time.timeScale = 0f;
    }

    // ======================= UI更新 =======================

    void SelectHardness(int h)
    {
        SelectedHardness = h;
        for (int i = 0; i < hardnessButtons.Count; i++)
            ((Image)hardnessButtons[i].targetGraphic).color = (i + 1 == h) ? BtnOn : BtnOff;
    }

    void SetRouteInteractable(bool on)
    {
        foreach (var b in hardnessButtons) b.interactable = on;
        if (executeButton != null) executeButton.interactable = on;
    }

    void UpdateGaugeUI(float previewCost)
    {
        if (gaugeFill == null) return;
        gaugePreviewFill.fillAmount = lineGauge / maxLineGauge;
        gaugeFill.fillAmount = Mathf.Max(0f, lineGauge - previewCost) / maxLineGauge;
        gaugeLostFill.fillAmount = (maxLineGauge - currentMaxLineGauge) / maxLineGauge;
        gaugeText.text = previewCost > 0f
            ? $"造線ゲージ {lineGauge - previewCost:0} / {currentMaxLineGauge:0}  (-{previewCost:0})"
            : $"造線ゲージ {lineGauge:0} / {currentMaxLineGauge:0}";
    }

    void AddScore(int v)
    {
        int max = scoreDigits >= 9 ? int.MaxValue : (int)Mathf.Pow(10, scoreDigits) - 1;
        Score = (int)System.Math.Min((long)Score + v, (long)max);
        UpdateScore();
    }

    void UpdateScore() => scoreText.text = Score.ToString("D" + scoreDigits);

    void UpdateStatus()
    {
        if (statusText == null) return;
        timerText.text = CurrentPhase == Phase.Route ? $"残り {timeLeft:0.0}" : "残り --.-";
        timerText.color = CurrentPhase == Phase.Route && timeLeft <= 10f ? InvalidCol : Color.white;

        string phase = CurrentPhase switch
        {
            Phase.Preview => "問題準備フェーズ",
            Phase.Route => "経路フェーズ",
            Phase.Execute => "実行フェーズ",
            Phase.Clear => "クリア",
            _ => "ゲームオーバー",
        };
        statusText.text =
            $"問題 {Mathf.Min(problemIndex + 1, problemCount)}/{problemCount}  {phase}  [{(useFixedSpawn ? "指定出現" : "確率出現")}]\n" +
            $"SAN {san:0}/{maxSan:0}   硬さ {SelectedHardness}\n" +
            (CurrentPhase == Phase.Route ? "基本線をドラッグで造線 / 右クリックで削除" : "");
    }

    // ======================= 危険演出 =======================

    /// <summary>赤さの目標値（0〜1）= SAN の減り具合 ＋ 欠損数 × lostLimbDanger</summary>
    float DangerTarget()
    {
        float sanPart = maxSan > 0f ? 1f - Mathf.Clamp01(san / maxSan) : 0f;
        int lost = 0;
        foreach (var l in limbs) if (l.lost) lost++;
        return Mathf.Clamp01(sanPart + lost * lostLimbDanger);
    }

    void UpdateDanger(float dt)
    {
        if (dangerVignette == null) return;
        dangerLevel = Mathf.MoveTowards(dangerLevel, DangerTarget(), dangerFadeSpeed * dt);
        dangerFlash = Mathf.MoveTowards(dangerFlash, 0f, flashDecay * dt);
        ApplyDanger();
    }

    /// <summary>
    /// 赤枠の見た目を反映。
    /// 赤さが小さいときは赤枠を画面より大きくして（外周が画面外に出る）薄く、
    /// 赤さが増えるほど画面サイズまで縮めて濃くする → 外から内へ赤が入ってくる
    /// </summary>
    void ApplyDanger()
    {
        if (dangerVignette == null) return;
        float v = Mathf.Clamp01(dangerLevel + dangerFlash);
        dangerVignette.enabled = v > 0.001f;
        dangerVignette.rectTransform.sizeDelta = new Vector2(1920, 1080) * Mathf.Lerp(dangerStartScale, 1f, v);
        var c = dangerColor;
        c.a = dangerColor.a * Mathf.Clamp01(v * 1.5f);
        dangerVignette.color = c;
    }

    /// <summary>中心が透明・外周ほど不透明な白のビネット画像（色は Image.color で付ける）</summary>
    static Sprite GetVignetteSprite()
    {
        if (vignetteSprite != null) return vignetteSprite;
        const int W = 256, H = 144;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };
        var px = new Color32[W * H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W * 2f - 1f;
                float v = (y + 0.5f) / H * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v) / 1.41421f;           // 中心 0 〜 四隅 1
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.95f, r));
                px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        vignetteSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f));
        vignetteSprite.hideFlags = HideFlags.DontSave;
        return vignetteSprite;
    }

    IEnumerator Pop(RectTransform rt)
    {
        float t = 0f;
        while (t < 0.2f && rt != null)
        {
            t += Time.deltaTime;
            float s = 1f + 0.3f * Mathf.Sin(t / 0.2f * Mathf.PI);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;
    }

    // ======================= 幾何 =======================

    static bool SegSeg(Vector2 p, Vector2 p2, Vector2 q, Vector2 q2, out float t, out float u)
    {
        Vector2 r = p2 - p, s = q2 - q;
        float den = r.x * s.y - r.y * s.x;
        t = u = 0f;
        if (Mathf.Abs(den) < 1e-6f) return false;
        Vector2 qp = q - p;
        t = (qp.x * s.y - qp.y * s.x) / den;
        u = (qp.x * r.y - qp.y * r.x) / den;
        return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
    }

    static float DistPointSeg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    // ======================= 入力・ユーティリティ =======================

    Vector2 MouseLocal()
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, MouseScreen(), null, out var lp);
        return lp;
    }

#if NEW_INPUT_ONLY
    static Vector2 MouseScreen() => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
    static bool LeftDown()  => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    static bool LeftUp()    => Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
    static bool RightDown() => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
    static bool RestartPressed() => Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
    static Vector2 MouseScreen() => Input.mousePosition;
    static bool LeftDown() => Input.GetMouseButtonDown(0);
    static bool LeftUp() => Input.GetMouseButtonUp(0);
    static bool RightDown() => Input.GetMouseButtonDown(1);
    static bool RestartPressed() => Input.GetKeyDown(KeyCode.R);
#endif

    static bool PointerOverUI() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    Font ResolveFont()
    {
        if (uiFont != null) return uiFont;
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
        return f;
    }

    static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem));
#if NEW_INPUT_ONLY
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }
}

// ======================= シーンビュー編集（エディタ専用） =======================
// 別ファイル＋Editorフォルダだとアセンブリ定義(asmdef)次第で参照できないため、同じファイルに置いています。
#if UNITY_EDITOR
namespace LogicModeEditorInternal
{
    using UnityEditor;

    /// <summary>
    ///   黄色の四角 : 円の中心（ドラッグで移動）
    ///   水色の丸   : 半径（円の真上。ドラッグで拡大・縮小）
    ///   橙の丸     : 腕の位置（左右どちらを動かしても左右対称に反映）
    ///   緑の丸     : 足の位置（同上）
    /// </summary>
    [CustomEditor(typeof(LogicModeScreen))]
    public class LogicModeScreenEditor : Editor
    {
        static readonly Color CircleCol = new Color(0.3f, 0.9f, 1f, 0.9f);
        static readonly Color CenterCol = new Color(1f, 0.9f, 0.2f);
        static readonly Color ArmCol = new Color(1f, 0.6f, 0.2f);
        static readonly Color LegCol = new Color(0.4f, 1f, 0.5f);

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("プレビューを再構築"))
                    ((LogicModeScreen)target).RebuildPreview();
            }
            EditorGUILayout.HelpBox("シーンビューのハンドルで円の中心・半径・腕／足の位置をドラッグできます。", MessageType.Info);
        }

        void OnSceneGUI()
        {
            var s = (LogicModeScreen)target;
            var root = s.CanvasRoot;
            if (root == null) return;   // プレビュー（または再生中の画面）がまだ無い

            Vector3 W(Vector2 p) => root.TransformPoint(p);
            Vector2 L(Vector3 w) => root.InverseTransformPoint(w);

            // 円の表示（経路になる部分＝水色 / 線なし（欠損で完成する部分）＝薄い白）
            float lArm = 180f - s.armAngle, lLeg = 180f - s.legAngle;
            DrawArc(s, W, lArm, 180f, CircleCol);
            DrawArc(s, W, 180f, lLeg, CircleCol);
            DrawArc(s, W, s.armAngle, 0f, CircleCol);
            DrawArc(s, W, 0f, s.legAngle, CircleCol);
            DrawArc(s, W, s.armAngle, lArm, new Color(1f, 1f, 1f, 0.25f));
            DrawArc(s, W, s.legAngle, -180f - s.legAngle, new Color(1f, 1f, 1f, 0.25f));

            bool changed = false;

            // 中心
            EditorGUI.BeginChangeCheck();
            Vector3 c = Drag(W(s.circleCenter), CenterCol, Handles.RectangleHandleCap, 0.07f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(s, "円の中心を移動");
                s.circleCenter = L(c);
                changed = true;
            }

            // 半径（真上）
            EditorGUI.BeginChangeCheck();
            Vector3 r = Drag(W(s.CirclePt(90f)), CircleCol, Handles.CircleHandleCap, 0.06f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(s, "円の半径を変更");
                s.circleRadius = Mathf.Max(10f, Vector2.Distance(L(r), s.circleCenter));
                changed = true;
            }

            // 腕・足（左右対称）
            changed |= AngleHandle(s, W, L, s.armAngle, false, true, ArmCol, "右腕");
            changed |= AngleHandle(s, W, L, 180f - s.armAngle, true, true, ArmCol, "左腕");
            changed |= AngleHandle(s, W, L, s.legAngle, false, false, LegCol, "右足");
            changed |= AngleHandle(s, W, L, 180f - s.legAngle, true, false, LegCol, "左足");

            if (changed)
            {
                EditorUtility.SetDirty(s);
                if (!Application.isPlaying) s.RebuildPreview();
            }
        }

        static bool AngleHandle(LogicModeScreen s, System.Func<Vector2, Vector3> W, System.Func<Vector3, Vector2> L,
                                float deg, bool mirrored, bool isArm, Color col, string label)
        {
            Vector3 pos = W(s.CirclePt(deg));
            Handles.color = col;
            Handles.Label(pos + Vector3.up * HandleUtility.GetHandleSize(pos) * 0.25f, label);

            EditorGUI.BeginChangeCheck();
            Vector3 np = Drag(pos, col, Handles.CircleHandleCap, 0.06f);
            if (!EditorGUI.EndChangeCheck()) return false;

            Vector2 v = L(np) - s.circleCenter;
            float a = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            if (mirrored) a = 180f - a;
            a = Mathf.DeltaAngle(0f, a);

            Undo.RecordObject(s, label + "の位置を変更");
            if (isArm) s.armAngle = Mathf.Clamp(a, 0.1f, 89.9f);
            else s.legAngle = Mathf.Clamp(a, -89.9f, -0.1f);
            return true;
        }

        static void DrawArc(LogicModeScreen s, System.Func<Vector2, Vector3> W, float from, float to, Color col)
        {
            int n = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(to - from) / 3f));
            var pts = new Vector3[n + 1];
            for (int i = 0; i <= n; i++) pts[i] = W(s.CirclePt(Mathf.Lerp(from, to, (float)i / n)));
            Handles.color = col;
            Handles.DrawAAPolyLine(4f, pts);
        }

        static Vector3 Drag(Vector3 pos, Color col, Handles.CapFunction cap, float scale)
        {
            Handles.color = col;
            float size = HandleUtility.GetHandleSize(pos) * scale;
#if UNITY_2022_1_OR_NEWER
            return Handles.FreeMoveHandle(pos, size, Vector3.zero, cap);
#else
            return Handles.FreeMoveHandle(pos, Quaternion.identity, size, Vector3.zero, cap);
#endif
        }
    }
}
#endif