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
/// ノードは円に入ると時計回りに進む（左：中間→左腕 / 右：中間→右足）
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
    public Sprite centerSprite;
    public Vector2 centerSize = new Vector2(820, 820);
    public Vector2 centerOffset = new Vector2(0, 20);
    public bool preserveAspect = true;

    [Header("人体を囲む円（シーンビューでドラッグ編集可）")]
    public Vector2 circleCenter = new Vector2(0, 20);   // 円の中心（centerOffset と揃えるのが基本）
    public float circleRadius = 285f;
    [Range(0.1f, 89.9f)] public float armAngle = 36f;  // 右腕の角度（度。右=0、上が正）。左腕は左右反転
    [Range(-89.9f, -0.1f)] public float legAngle = -67f; // 右足の角度。左足は左右反転
    public int arcSegments = 96;                         // 円1周あたりの分割数（多いほど滑らか）

    [Header("フォント（未設定ならビルトインフォント）")]
    public Font uiFont;

    [Header("CSV（StreamingAssets優先 → TextAsset → どちらも無ければInspectorの値）")]
    public bool useStreamingAssets = true;
    public string paramsCsvName = "logic_params.csv";
    public string nodeTableCsvName = "node_table.csv";
    public string problemsCsvName = "logic_problems.csv";
    public TextAsset paramsCsv;
    public TextAsset nodeTableCsv;
    public TextAsset problemsCsv;

    [Header("参照数値：体力・SAN")]
    public float limbMaxHp = 25f;
    public float maxSan = 40f;

    [Header("参照数値：造線")]
    public float maxLineGauge = 2000f;
    public float armLostGaugePenalty = 500f;
    public float costPerMeter = 100f;
    public float pixelsPerMeter = 100f;          // 1m を何pxとみなすか（仮）
    public enum VanishMode { Count, Seconds }
    public VanishMode vanishMode = VanishMode.Count;   // ロジックは回数（＝硬さ）
    public float vanishSeconds = 7f;             // 秒間消滅（実行フェーズ中のみ進む）
    public float warnBeforeSeconds = 2f;         // 消滅〇秒前で消滅準備
    public int warnBeforeCount = 1;              // 残り〇回で消滅準備
    public float blinkSpeed = 1.5f;              // 点滅速度（往復/秒）
    [Range(0f, 1f)] public float vanishAlpha = 0.3f;

    [Header("参照数値：ノード・スコア")]
    public float routePhaseSeconds = 60f;
    public float nodeStartGauge = 100f;
    public float gaugePerSecond = 10f;
    public int[] goodScore = { 1000, 1500, 2000, 3000 };   // 良性 Lv1〜4
    public int[] badScore = { 500, 350, 200, 50 };        // 悪性 Lv1〜4
    public float[] badDamage = { 5, 7, 10, 15 };
    public float[] badSan = { 3, 5, 8, 10 };
    public int scoreDigits = 8;

    [Header("造線の操作")]
    public float snapRadius = 22f;               // 基本線をクリックできる距離
    public float forbiddenRadius = 45f;          // 始点・終点まわりの開始禁止範囲
    public float minLineLength = 30f;

    [Header("ノード移動・問題（プロト用ランダム生成）")]
    public float nodeSpeed = 160f;               // px/秒
    public int problemCount = 3;
    public int nodesPerProblem = 5;
    public float spawnInterval = 1.5f;
    public float previewRevealInterval = 0.4f;
    [Range(0f, 1f)] public float badRate = 0.4f;

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
    }

    class BasicLine
    {
        public string name;
        public Limb target;
        public bool alive = true;
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
            if (csvProblems.Count > 0) problemCount = csvProblems.Count;
            else csvProblems = null;
        }
        if (csvProblems == null) Debug.Log("[CSV] 問題CSVなし → ランダム出題");
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
        float rArm = armAngle, rLeg = legAngle;           // 右側の角度

        // 円弧の描画。経路にならない半分は「跨げない障害物」
        DrawCircleArc(lArm, 180f, "ArcLeft", false);
        DrawCircleArc(180f, lLeg, "ArcLeft", true);    // 左下半分は障害物
        DrawCircleArc(rArm, 0f, "ArcRight", true);    // 右上半分は障害物
        DrawCircleArc(0f, rLeg, "ArcRight", false);

        MakeLine("TopLeft", leftArm, new List<Vector2> { new Vector2(-960, 540), leftArm.pos });
        MakeLine("TopRight", rightArm, new List<Vector2> { new Vector2(960, 540), rightArm.pos });
        MakeLine("BottomLeft", leftLeg, new List<Vector2> { new Vector2(-960, -540), leftLeg.pos });
        MakeLine("BottomRight", rightLeg, new List<Vector2> { new Vector2(960, -540), rightLeg.pos });

        // 左右中間 → 円に当たったら時計回り（角度が減る方向）に進む
        //   左：180° → 左腕　／　右：0° → 右足
        var ml = new List<Vector2> { new Vector2(-960, circleCenter.y), CirclePt(180f) };
        AddArcPoints(ml, 180f, lArm);
        MakeLine("MiddleLeft", leftArm, ml, 1);

        var mr = new List<Vector2> { new Vector2(960, circleCenter.y), CirclePt(0f) };
        AddArcPoints(mr, 0f, rLeg);
        MakeLine("MiddleRight", rightLeg, mr, 1);
    }

    void MakeLine(string name, Limb target, List<Vector2> pts, int visibleSegments = -1)
    {
        var l = new BasicLine { name = name, target = target };
        l.pts.AddRange(pts);
        l.Bake();
        int segs = visibleSegments < 0 ? pts.Count - 1 : visibleSegments;
        for (int i = 0; i < segs; i++)
            l.visuals.Add(UIShapes.CreateLine(lineLayer, pts[i], pts[i + 1], LineThickness, LineColor, name));
        lines.Add(l);
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

        // CSVの問題
        if (csvProblems != null && problemIndex < csvProblems.Count)
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

        // CSVが無いときはランダム
        var alive = new List<int>();
        for (int i = 0; i < lines.Count; i++) if (lines[i].alive) alive.Add(i);
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
        if (limb.hp <= 0f) LoseLimb(limb);
    }

    void LoseLimb(Limb limb)
    {
        limb.lost = true;
        limb.root.gameObject.SetActive(false);

        if (limb.isArm)
        {
            currentMaxLineGauge = Mathf.Max(0f, currentMaxLineGauge - armLostGaugePenalty);
            lineGauge = Mathf.Min(lineGauge, currentMaxLineGauge);
            // TODO: 左腕欠損時「頭部側の基本線が追加」
        }
        else
        {
            KillLine(limb == leftLeg ? "MiddleLeft" : "MiddleRight");
            // TODO: 右足欠損時「足側の基本線が追加」
        }

        bool allLost = true;
        foreach (var l in limbs) if (!l.lost) allLost = false;
        if (allLost) GameOver();
    }

    void KillLine(string name)
    {
        foreach (var l in lines)
        {
            if (l.name != name) continue;
            l.alive = false;
            foreach (var v in l.visuals) v.gameObject.SetActive(false);
            // 繋がっていた造線も消滅
            for (int i = drawnLines.Count - 1; i >= 0; i--)
            {
                var d = drawnLines[i];
                if (d.lineA == l || d.lineB == l) Vanish(d);
            }
        }
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
            $"問題 {Mathf.Min(problemIndex + 1, problemCount)}/{problemCount}  {phase}\n" +
            $"SAN {san:0}/{maxSan:0}   硬さ {SelectedHardness}\n" +
            (CurrentPhase == Phase.Route ? "基本線をドラッグで造線 / 右クリックで削除" : "");
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
        static readonly Color ObstacleCol = new Color(1f, 0.35f, 0.3f, 0.9f);
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

            // 円の表示（経路になる部分＝水色 / 跨げない部分＝赤 / 線なし＝薄い白）
            float lArm = 180f - s.armAngle, lLeg = 180f - s.legAngle;
            DrawArc(s, W, lArm, 180f, CircleCol);
            DrawArc(s, W, 180f, lLeg, ObstacleCol);
            DrawArc(s, W, s.armAngle, 0f, ObstacleCol);
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