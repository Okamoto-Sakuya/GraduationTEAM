using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LineManager : MonoBehaviour
{
    // ノード・PathGraphから参照するための造線情報
    public struct CreatedLineInfo
    {
        public int Id;
        public Vector2 Start;
        public Vector2 End;
        public bool IsBlocked;   // 消滅待ち(新しいノードは入れない)

        public CreatedLineInfo(int id, Vector2 start, Vector2 end, bool blocked)
        {
            Id = id;
            Start = start;
            End = end;
            IsBlocked = blocked;
        }
    }

    // 造線の追加・削除・状態変化を通知(PathGraphが購読)
    public event System.Action LinesChanged;

    [Header("基本線")]
    [SerializeField]
    private BasicLine[] basicLines;

    // PathGraphなど、他のスクリプトから基本線を参照するための公開プロパティ
    public BasicLine[] BasicLines => basicLines;

    [Header("造線の設定")]
    [SerializeField]
    private float maxLineGauge = 2000f;

    [SerializeField]
    private float lineCostPerMeter = 100f;

    [Tooltip("右クリックで造線を消すときに、線を選べる距離")]
    [SerializeField]
    private float lineSelectDistance = 0.25f;

    [Tooltip("引いている線の先が、基本線にこの距離まで近づいたら、基本線に届いたとみなす")]
    [SerializeField]
    private float endReachTolerance = 0.3f;

    [Tooltip("造線を引き始めるとき、基本線からこの距離以内をクリックすれば始点にできる")]
    [SerializeField]
    private float startSelectDistance = 0.6f;

    [SerializeField]
    private float minLineLength = 0.1f;

    [Tooltip("基本線との交差判定に使う曲線の分割数")]
    [SerializeField]
    private int basicLineCrossSamples = 30;

    [Header("線の表示")]
    [SerializeField]
    private Material lineMaterial;

    [SerializeField]
    private float lineWidth = 0.05f;

    [SerializeField]
    private Color basicLineColor = Color.white;

    [SerializeField]
    private Color createdLineColor = Color.white;

    [SerializeField]
    private Color previewLineColor = Color.white;

    [Tooltip("引いている線が確定できないとき(基本線に届かない、交差している、ゲージ不足など)の色")]
    [SerializeField]
    private Color invalidPreviewColor = new Color(1f, 0.35f, 0.3f, 1f);

    [Header("開始禁止範囲(造線の始点・終点の周囲)")]
    [Tooltip("この半径の中には、新しい造線の始点を置けない")]
    [SerializeField]
    private float forbiddenRadius = 0.5f;

    [SerializeField]
    private Color forbiddenFillColor = new Color(0.35f, 0.1f, 0.1f, 0.45f);

    [SerializeField]
    private Color forbiddenBorderColor = new Color(0.9f, 0.45f, 0.45f, 1f);

    [SerializeField]
    private float forbiddenBorderWidth = 0.03f;

    [Tooltip("中心から円周へ引く細い線(半径の表示)")]
    [SerializeField]
    private Color forbiddenRadiusLineColor = new Color(0.9f, 0.45f, 0.45f, 1f);

    [Tooltip("始点・終点の丸い点")]
    [SerializeField]
    private Color endpointDotColor = new Color(0.45f, 0.65f, 0.85f, 1f);

    [SerializeField]
    private float endpointDotSize = 0.14f;

    [Header("禁止ルールに触れたときの点滅表示")]
    [SerializeField]
    private Color violationColor = new Color(1f, 0.15f, 0.15f, 1f);

    [SerializeField]
    private float violationBlinkSpeed = 12f;

    [Tooltip("始点を置けなかったとき(禁止ルール5)に、禁止範囲を点滅させる秒数")]
    [SerializeField]
    private float startBlockedFlashSeconds = 1f;

    [Header("設置禁止範囲(禁止ルール2・3)")]
    [Tooltip("中央の円の設置禁止範囲が未設定(半径0)のとき、基本線⑦⑧から自動で設定する")]
    [SerializeField] private bool autoSetCenterZone = true;

    [Tooltip("中央の円をつくる基本線(⑦⑧)の、basicLines内の番号(0始まり)")]
    [SerializeField] private int centerLineIndexA = 6;
    [SerializeField] private int centerLineIndexB = 7;

    [Tooltip("ルール2: 中央の円の中心。Sceneビューで赤い円が出る(LineManagerを選択中)")]
    [SerializeField] private Vector2 centerZoneCenter;

    [Tooltip("ルール2: 中央の円の半径。0ならルール2は無効")]
    [SerializeField] private float centerZoneRadius = 0f;

    [Tooltip("円の縁に始点・終点があっても、すぐ禁止にならないための余裕")]
    [SerializeField] private float centerZoneTolerance = 0.15f;

    [Tooltip("ルール2の例外: 黄色い枠(上)。基本線⑨が追加されたら禁止範囲になる。幅0なら自動")]
    [SerializeField] private Rect yellowZoneTop;
    [SerializeField] private BasicLine yellowLineTop;

    [Tooltip("ルール2の例外: 黄色い枠(下)。基本線⑩が追加されたら禁止範囲になる。幅0なら自動")]
    [SerializeField] private Rect yellowZoneBottom;
    [SerializeField] private BasicLine yellowLineBottom;

    [Tooltip("ルール3: 画面の端からこの距離の範囲には、始点を置けない")]
    [SerializeField] private float outerMargin = 0.6f;

    [Tooltip("造線を引けない(届いていない・禁止)ときの、線と終点の色")]
    [SerializeField] private Color impossibleColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    [Header("マウスカーソル(空なら、コードで作った仮の画像を使う)")]
    [Tooltip("画像を割り当てるときは、Texture TypeをCursor、Read/Writeを有効にする")]
    [SerializeField] private Texture2D cursorNormal;
    [SerializeField] private Texture2D cursorStartable;
    [SerializeField] private Texture2D cursorPenImpossible;
    [SerializeField] private Texture2D cursorPenReady;
    [SerializeField] private Texture2D cursorForbidden;

    [Tooltip("割り当てた画像の、クリックされる位置(左上からのピクセル)。仮の画像では使わない")]
    [SerializeField] private Vector2 cursorHotspot = Vector2.zero;

    [Header("UI")]
    [Tooltip("造線ゲージのバー表示(LineGaugeBar)")]
    [SerializeField]
    private LineGaugeBar lineGaugeBar;

    [Header("カメラ")]
    [SerializeField]
    private Camera targetCamera;

    [Header("造線の寿命")]
    [SerializeField] private float lineLifetime = 7f;

    [Tooltip("消滅何秒前から点滅させるか")]
    [SerializeField] private float warningTime = 2f;

    [Header("点滅設定")]
    [SerializeField] private float blinkSpeed = 4f;
    [SerializeField] private float minAlpha = 0.3f;

    // 現在の造線ゲージ
    private float currentLineGauge;

    // 現在の始点となっている基本線
    private BasicLine selectedStartLine;

    // 造線の始点
    private Vector2 startPosition;

    // マウス操作中か
    private bool isDrawing;

    // 造線ID採番用
    private int nextLineId;

    // 作成した造線
    private readonly List<CreatedLine> createdLines =
        new List<CreatedLine>();

    // 点線のプレビュー
    private LineRenderer previewLine;

    // 描画中の始点に表示する禁止範囲
    private GameObject previewZone;

    // 今引いている造線の消費予定量(ゲージ表示用)
    private float pendingCost;

    // 禁止範囲の表示用キャッシュ
    private Mesh discMesh;
    private Material zoneFillMaterial;
    private Material dashedMaterial;
    private Material solidMaterial;

    // ===== 禁止ルールの点滅表示 =====
    private BasicLine lastHitLine;

    private bool blinkCenter;
    private bool blinkYellowTop;
    private bool blinkYellowBottom;
    private bool startBlockedByFrame;
    private LineRenderer previewEndDot;
    private Mesh quadMesh;
    private GameObject flashCenter, flashYellowTop, flashYellowBottom;
    private readonly GameObject[] flashFrame = new GameObject[4];

    // カーソル表示用
    private enum CursorState { None, Normal, Startable, PenImpossible, PenReady, Forbidden }
    private CursorState cursorState = CursorState.None;
    private bool previewReached;
    private bool previewValid;
    private Texture2D genNormal, genStartable, genPenImpossible, genPenReady, genForbidden;

    private readonly HashSet<BasicLine> blinkBasic = new HashSet<BasicLine>();
    private readonly HashSet<CreatedLine> blinkCreated = new HashSet<CreatedLine>();
    private readonly List<Vector2> blinkZones = new List<Vector2>();
    private bool blinkGauge;

    private readonly List<Vector2> startBlockedZones = new List<Vector2>();
    private float startBlockedUntil;

    private readonly Dictionary<BasicLine, Color> basicOriginalColors =
        new Dictionary<BasicLine, Color>();
    private readonly HashSet<CreatedLine> shownBlinkCreated = new HashSet<CreatedLine>();
    private readonly Dictionary<Graphic, Color> gaugeOriginalColors =
        new Dictionary<Graphic, Color>();
    private readonly List<GameObject> zoneFlashPool = new List<GameObject>();
    private Material zoneFlashMaterial;

    // 造線データ
    private class CreatedLine
    {
        public int id;

        public LineRenderer renderer;

        public Vector2 start;
        public Vector2 end;

        public float cost;

        public bool isActive = true;

        public float elapsedTime;
        public bool isWarning;
        public bool isWaitingForNode;
    }

    private void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        currentLineGauge = maxLineGauge;

        CreatePreviewLine();

        previewEndDot = CreateZoneLine(
            transform, "PreviewEndDot", 2, endpointDotSize,
            endpointDotColor, GetSolidMaterial(), 11);
        previewEndDot.numCapVertices = 12;
        previewEndDot.enabled = false;

        AutoSetCenterZone();

        UpdateGaugeUI();
    }

    private void Update()
    {
        if (targetCamera == null)
        {
            return;
        }

        // ゲームオーバー中は造線を操作できない
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
        {
            SetCursorState(CursorState.Normal);
            return;
        }

        UpdateCreatedLines();
        ApplyViolationDisplay();

        // 右クリック
        if (Input.GetMouseButtonDown(1))
        {
            if (isDrawing)
            {
                // 描画中ならキャンセル
                CancelDrawing();
            }
            else
            {
                // 完成済みの造線なら削除
                DeleteCreatedLine();
            }
        }

        // 左ボタンを押したところから、造線を引き始める
        if (Input.GetMouseButtonDown(0))
        {
            TryBeginDraw();
        }

        // 押している間は点線を更新し、離したところで造線を確定する
        if (isDrawing)
        {
            UpdatePreviewLine();

            if (Input.GetMouseButtonUp(0))
            {
                CompleteDraw();
            }
        }

        UpdateCursor();
    }

    private void OnDisable()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        cursorState = CursorState.None;
    }

    // ===== マウスカーソルの表示 =====
    private void UpdateCursor()
    {
        CursorState state;

        if (Time.unscaledTime < startBlockedUntil)
        {
            // 始点を置けなかった(禁止ルール3,5): 1秒だけ×
            state = CursorState.Forbidden;
        }
        else if (isDrawing)
        {
            if (previewReached && !previewValid)
            {
                state = CursorState.Forbidden;
            }
            else if (previewReached && previewValid)
            {
                state = CursorState.PenReady;
            }
            else
            {
                state = CursorState.PenImpossible;
            }
        }
        else
        {
            Vector2 mouse = GetMouseWorldPosition();
            BasicLine near = GetNearestBasicLine(mouse, startSelectDistance);

            bool canStart =
                near != null && !IsInForbiddenZone(near.GetClosestPoint(mouse));

            state = canStart ? CursorState.Startable : CursorState.Normal;
        }

        SetCursorState(state);
    }

    private void SetCursorState(CursorState state)
    {
        if (state == cursorState)
        {
            return;
        }

        cursorState = state;

        Texture2D tex;
        Vector2 hotspot;

        switch (state)
        {
            case CursorState.Startable:
                tex = cursorStartable != null ? cursorStartable
                    : (genStartable = genStartable != null ? genStartable : CursorImages.ArrowStart());
                hotspot = cursorStartable != null ? cursorHotspot : CursorImages.ArrowHotspot;
                break;

            case CursorState.PenImpossible:
                tex = cursorPenImpossible != null ? cursorPenImpossible
                    : (genPenImpossible = genPenImpossible != null ? genPenImpossible : CursorImages.Pen());
                hotspot = cursorPenImpossible != null ? cursorHotspot : CursorImages.PenHotspot;
                break;

            case CursorState.PenReady:
                tex = cursorPenReady != null ? cursorPenReady
                    : (genPenReady = genPenReady != null ? genPenReady : CursorImages.PenReady());
                hotspot = cursorPenReady != null ? cursorHotspot : CursorImages.PenHotspot;
                break;

            case CursorState.Forbidden:
                tex = cursorForbidden != null ? cursorForbidden
                    : (genForbidden = genForbidden != null ? genForbidden : CursorImages.Cross());
                hotspot = cursorForbidden != null ? cursorHotspot : CursorImages.CrossHotspot;
                break;

            default:
                tex = cursorNormal != null ? cursorNormal
                    : (genNormal = genNormal != null ? genNormal : CursorImages.Arrow());
                hotspot = cursorNormal != null ? cursorHotspot : CursorImages.ArrowHotspot;
                break;
        }

        Cursor.SetCursor(tex, hotspot, CursorMode.Auto);
    }

    // 点線プレビューを作成
    private void CreatePreviewLine()
    {
        GameObject obj = new GameObject("LinePreview");

        obj.transform.SetParent(transform);

        previewLine = obj.AddComponent<LineRenderer>();

        SetupLineRenderer(previewLine);

        previewLine.positionCount = 2;

        previewLine.startColor = previewLineColor;
        previewLine.endColor = previewLineColor;

        previewLine.enabled = false;

        // 点線用マテリアル
        Material dottedMaterial = new Material(
            Shader.Find("Sprites/Default")
        );

        Texture2D texture = new Texture2D(
            16,
            1,
            TextureFormat.RGBA32,
            false
        );

        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Repeat;

        for (int i = 0; i < 16; i++)
        {
            // 8ピクセル表示、8ピクセル透明
            Color color = i < 8
                ? Color.white
                : new Color(1, 1, 1, 0);

            texture.SetPixel(i, 0, color);
        }

        texture.Apply();

        dottedMaterial.mainTexture = texture;

        dottedMaterial.mainTextureScale = new Vector2(1, 1);

        previewLine.material = dottedMaterial;

        previewLine.textureMode = LineTextureMode.Tile;

        previewLine.startColor = previewLineColor;
        previewLine.endColor = previewLineColor;
    }

    // LineRendererの共通設定
    private void SetupLineRenderer(LineRenderer lr)
    {
        lr.useWorldSpace = true;

        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;

        lr.numCapVertices = 4;

        lr.positionCount = 2;

        if (lineMaterial != null)
        {
            lr.material = lineMaterial;
        }
        else
        {
            lr.material = new Material(
                Shader.Find("Sprites/Default")
            );
        }

        lr.sortingOrder = 10;
    }

    // マウス座標をワールド座標に変換
    private Vector2 GetMouseWorldPosition()
    {
        Vector3 mousePosition = Input.mousePosition;

        mousePosition.z = -targetCamera.transform.position.z;

        Vector3 worldPosition =
            targetCamera.ScreenToWorldPoint(mousePosition);

        return new Vector2(
            worldPosition.x,
            worldPosition.y
        );
    }

    // 左ボタンを押したとき: 基本線の近くなら、そこを始点にして引き始める
    private void TryBeginDraw()
    {
        Vector2 mousePosition = GetMouseWorldPosition();

        BasicLine line = GetNearestBasicLine(mousePosition, startSelectDistance);

        if (line == null)
        {
            return;
        }

        // 禁止ルール3: 画面の端の範囲には始点を置けない
        if (!GetInnerScreenRect().Contains(line.GetClosestPoint(mousePosition)))
        {
            Debug.Log("画面端の設置禁止範囲です。");

            startBlockedZones.Clear();
            startBlockedByFrame = true;
            startBlockedUntil = Time.unscaledTime + startBlockedFlashSeconds;
            return;
        }

        // 既存の造線の始点・終点の禁止範囲内には始点を置けない
        if (IsInForbiddenZone(line.GetClosestPoint(mousePosition)))
        {
            Debug.Log("開始禁止範囲内です。");

            // 禁止ルール5: 触れた禁止範囲を点滅させる
            startBlockedZones.Clear();
            startBlockedByFrame = false;
            CollectZoneCenters(line.GetClosestPoint(mousePosition), startBlockedZones);
            startBlockedUntil = Time.unscaledTime + startBlockedFlashSeconds;
            return;
        }

        StartDrawing(line, mousePosition);
    }

    // 左ボタンを離したとき: 造線を引けるなら確定、引けないならキャンセル
    private void CompleteDraw()
    {
        Vector2 mousePosition = GetMouseWorldPosition();

        if (EvaluateEnd(
            mousePosition,
            out Vector2 endPosition,
            out bool reached,
            out string reason))
        {
            float length = Vector2.Distance(startPosition, endPosition);

            CreateCreatedLine(
                startPosition,
                endPosition,
                length * lineCostPerMeter
            );

            FinishDrawing();
        }
        else
        {
            Debug.Log(reason);

            CancelDrawing();
        }
    }

    // 始点からマウスの方向へ線を伸ばす。
    //  ・マウスがまだ基本線に届いていない間は、マウスの位置まで線が伸びる
    //  ・マウスが最初の基本線に届く(または超える)と、その基本線の上で止まり、終点になる
    //    (マウスは基本線の上になくてよい。奥まで動かしてもよい)
    // end: 線の先の位置 / reached: 基本線に届いたか
    // 戻り値: 今ここで離したら、造線を確定できるか
    private bool EvaluateEnd(
        Vector2 mouse,
        out Vector2 end,
        out bool reached,
        out string reason)
    {
        end = mouse;
        reached = false;
        reason = null;
        lastHitLine = null;

        Vector2 direction = mouse - startPosition;
        float mouseDistance = direction.magnitude;

        if (mouseDistance < 0.0001f)
        {
            reason = "線が短すぎます。";
            return false;
        }

        direction /= mouseDistance;

        BasicLine hitLine = FindFirstBasicLineHit(
            startPosition,
            direction,
            out Vector2 hitPoint
        );

        if (hitLine == null)
        {
            reason = "その方向に、つなげる基本線がありません。";
            return false;
        }

        lastHitLine = hitLine;

        float hitDistance = Vector2.Distance(startPosition, hitPoint);

        // まだ基本線に届いていない: マウスの位置まで線を伸ばす
        if (mouseDistance < hitDistance - endReachTolerance)
        {
            reason = "基本線に届いていません。";
            return false;
        }

        end = hitPoint;
        reached = true;

        // 同じ基本線への接続は不可
        if (hitLine == selectedStartLine)
        {
            reason = "同じ基本線にはつなげられません。";
            return false;
        }

        float length = Vector2.Distance(startPosition, end);

        if (length < minLineLength)
        {
            reason = "線が短すぎます。";
            return false;
        }

        // 終点が、自分の始点の禁止範囲に重なる(近すぎる)場合は作成できない
        if (length < forbiddenRadius)
        {
            reason = "始点に近すぎます(開始禁止範囲)。";
            return false;
        }

        // 終点も、既存の造線の始点・終点の禁止範囲内には置けない
        if (IsInForbiddenZone(end))
        {
            reason = "終点が開始禁止範囲内です。";
            return false;
        }

        // 既存の線を横切る場合は作成できない
        if (CrossesExistingLines(startPosition, end, hitLine))
        {
            reason = "基本線または既存の造線を横切っています。";
            return false;
        }

        // 禁止ルール2: 中央の設置禁止範囲を通る場合は作成できない
        if (HitsCenterZone(startPosition, end, out _, out _, out _))
        {
            reason = "中央の設置禁止範囲を通っています。";
            return false;
        }

        // ゲージ不足
        if (currentLineGauge < length * lineCostPerMeter)
        {
            reason = "造線ゲージが足りません。";
            return false;
        }

        return true;
    }

    // origin から direction へ伸ばした線が、最初に触れる基本線とその位置
    // (始点の基本線も含める。手前に触れた線がなければnull)
    private BasicLine FindFirstBasicLineHit(
        Vector2 origin,
        Vector2 direction,
        out Vector2 hitPoint)
    {
        BasicLine hitLine = null;
        float nearest = float.MaxValue;

        // 始点のすぐそばは、始点の基本線自身なので除外する
        const float minDistance = 0.02f;

        foreach (BasicLine line in basicLines)
        {
            if (line == null || !line.isActive)
            {
                continue;
            }

            List<Vector2> poly = line.GetPolyline(basicLineCrossSamples);

            for (int i = 0; i < poly.Count - 1; i++)
            {
                Vector2 a = poly[i];
                Vector2 e = poly[i + 1] - a;

                float denominator = Cross(direction, e);

                // 平行
                if (Mathf.Abs(denominator) < 0.000001f)
                {
                    continue;
                }

                Vector2 ap = a - origin;

                float t = Cross(ap, e) / denominator;
                float u = Cross(ap, direction) / denominator;

                if (t > minDistance && u >= 0f && u <= 1f && t < nearest)
                {
                    nearest = t;
                    hitLine = line;
                }
            }
        }

        hitPoint = hitLine != null
            ? origin + direction * nearest
            : origin;

        return hitLine;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    // 描画開始
    private void StartDrawing(
        BasicLine line,
        Vector2 mousePosition)
    {
        selectedStartLine = line;

        startPosition =
            line.GetClosestPoint(mousePosition);

        isDrawing = true;

        previewLine.enabled = true;

        previewLine.SetPosition(0, startPosition);
        previewLine.SetPosition(1, startPosition);

        DestroyPreviewZone();
        previewZone = CreateEndpointVisual(transform, startPosition, -45f);
    }

    // 点線を更新
    private void UpdatePreviewLine()
    {
        Vector2 mousePosition = GetMouseWorldPosition();

        bool valid = EvaluateEnd(
            mousePosition,
            out Vector2 endPosition,
            out bool reached,
            out string reason
        );

        UpdateViolations(endPosition, reached);

        previewReached = reached;
        previewValid = valid;

        // 基本線に届くまではマウスの位置まで伸び、届いたら基本線の上で止まる
        previewLine.SetPosition(0, startPosition);
        previewLine.SetPosition(1, endPosition);

        // 造線を引ける(基本線に届き、確定できる)ときだけ通常の色。
        // 届いていない・禁止のときは、線と終点を灰色にする
        bool ready = reached && valid;

        Color color = ready ? previewLineColor : impossibleColor;

        previewLine.startColor = color;
        previewLine.endColor = color;

        if (previewEndDot != null)
        {
            Color dotColor = ready ? endpointDotColor : impossibleColor;

            previewEndDot.startColor = dotColor;
            previewEndDot.endColor = dotColor;
            previewEndDot.SetPosition(0, new Vector3(endPosition.x, endPosition.y, 0f));
            previewEndDot.SetPosition(1, new Vector3(endPosition.x + 0.0001f, endPosition.y, 0f));
            previewEndDot.enabled = true;
        }

        // 消費予定量(ゲージ表示用): 伸ばした長さに応じて増える
        float newCost =
            Vector2.Distance(startPosition, endPosition) * lineCostPerMeter;

        if (!Mathf.Approximately(newCost, pendingCost))
        {
            pendingCost = newCost;
            UpdateGaugeUI();
        }
    }

    // 造線を作成
    private void CreateCreatedLine(
        Vector2 start,
        Vector2 end,
        float cost)
    {
        GameObject obj = new GameObject("CreatedLine");

        obj.transform.SetParent(transform);

        LineRenderer lr = obj.AddComponent<LineRenderer>();

        SetupLineRenderer(lr);

        lr.positionCount = 2;

        lr.SetPosition(0, start);
        lr.SetPosition(1, end);

        lr.startColor = createdLineColor;
        lr.endColor = createdLineColor;

        // 始点・終点の開始禁止範囲(造線と一緒に消える)
        CreateEndpointVisual(obj.transform, start, -45f);
        CreateEndpointVisual(obj.transform, end, -135f);

        // ゲージ消費
        currentLineGauge -= cost;

        currentLineGauge = Mathf.Max(
            0,
            currentLineGauge
        );

        CreatedLine data = new CreatedLine();

        data.id = nextLineId++;
        data.renderer = lr;
        data.start = start;
        data.end = end;
        data.cost = cost;

        createdLines.Add(data);

        UpdateGaugeUI();

        LinesChanged?.Invoke();

        Debug.Log(
            $"造線を作成しました。長さ：{Vector2.Distance(start, end):F2} " +
            $"消費ゲージ：{cost:F0}"
        );
    }

    // 描画キャンセル
    private void CancelDrawing()
    {
        isDrawing = false;

        selectedStartLine = null;

        previewLine.enabled = false;

        DestroyPreviewZone();
        ClearViolations();

        if (previewEndDot != null)
        {
            previewEndDot.enabled = false;
        }

        pendingCost = 0f;
        UpdateGaugeUI();
    }

    // 描画確定後の後処理
    private void FinishDrawing()
    {
        isDrawing = false;

        selectedStartLine = null;

        previewLine.enabled = false;

        DestroyPreviewZone();
        ClearViolations();

        if (previewEndDot != null)
        {
            previewEndDot.enabled = false;
        }

        pendingCost = 0f;
        UpdateGaugeUI();
    }

    private void DestroyPreviewZone()
    {
        if (previewZone != null)
        {
            Destroy(previewZone);
            previewZone = null;
        }
    }

    // ===== 開始禁止範囲 =====

    // 点が、既存の造線の始点・終点の禁止範囲内か
    private bool IsInForbiddenZone(Vector2 point)
    {
        foreach (CreatedLine line in createdLines)
        {
            if (line == null || !line.isActive)
            {
                continue;
            }

            if (Vector2.Distance(point, line.start) <= forbiddenRadius ||
                Vector2.Distance(point, line.end) <= forbiddenRadius)
            {
                return true;
            }
        }

        return false;
    }

    // 禁止範囲の表示(塗り・点線の円・半径の線・中心の点)
    // radiusAngle: 半径の線を引く角度(度)。始点は-45、終点は-135
    private GameObject CreateEndpointVisual(
        Transform parent,
        Vector2 center,
        float radiusAngle)
    {
        GameObject root = new GameObject("EndpointZone");
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(center.x, center.y, 0f);

        // 塗りつぶし(暗い赤)
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(root.transform, false);
        fill.transform.localScale =
            new Vector3(forbiddenRadius, forbiddenRadius, 1f);

        fill.AddComponent<MeshFilter>().sharedMesh = GetDiscMesh();

        MeshRenderer meshRenderer = fill.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = GetZoneFillMaterial();
        meshRenderer.sortingOrder = 8;

        // 点線の円
        const int segments = 96;

        LineRenderer circle = CreateZoneLine(
            root.transform,
            "Border",
            segments + 1,
            forbiddenBorderWidth,
            forbiddenBorderColor,
            GetDashedMaterial(),
            9
        );

        circle.textureMode = LineTextureMode.Stretch;

        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;

            circle.SetPosition(
                i,
                new Vector3(
                    center.x + Mathf.Cos(angle) * forbiddenRadius,
                    center.y + Mathf.Sin(angle) * forbiddenRadius,
                    0f
                )
            );
        }

        // 中心から円周への細い線
        float rad = radiusAngle * Mathf.Deg2Rad;

        LineRenderer radiusLine = CreateZoneLine(
            root.transform,
            "RadiusLine",
            2,
            forbiddenBorderWidth * 0.6f,
            forbiddenRadiusLineColor,
            GetSolidMaterial(),
            9
        );

        radiusLine.SetPosition(
            0,
            new Vector3(center.x, center.y, 0f)
        );

        radiusLine.SetPosition(
            1,
            new Vector3(
                center.x + Mathf.Cos(rad) * forbiddenRadius,
                center.y + Mathf.Sin(rad) * forbiddenRadius,
                0f
            )
        );

        // 中心の丸い点(太い線の両端を丸くして円にする)
        LineRenderer dot = CreateZoneLine(
            root.transform,
            "Dot",
            2,
            endpointDotSize,
            endpointDotColor,
            GetSolidMaterial(),
            11
        );

        dot.numCapVertices = 12;

        dot.SetPosition(0, new Vector3(center.x, center.y, 0f));
        dot.SetPosition(1, new Vector3(center.x + 0.0001f, center.y, 0f));

        return root;
    }

    private LineRenderer CreateZoneLine(
        Transform parent,
        string objectName,
        int positionCount,
        float width,
        Color color,
        Material material,
        int sortingOrder)
    {
        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(parent, false);

        LineRenderer lr = obj.AddComponent<LineRenderer>();

        lr.useWorldSpace = true;
        lr.positionCount = positionCount;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.startColor = color;
        lr.endColor = color;
        lr.sharedMaterial = material;
        lr.sortingOrder = sortingOrder;
        lr.numCapVertices = 0;

        return lr;
    }

    // 半径1の円盤メッシュ
    private Mesh GetDiscMesh()
    {
        if (discMesh != null)
        {
            return discMesh;
        }

        const int segments = 48;

        Vector3[] vertices = new Vector3[segments + 1];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;

        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;

            vertices[i + 1] = new Vector3(
                Mathf.Cos(angle),
                Mathf.Sin(angle),
                0f
            );

            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % segments + 1;
        }

        discMesh = new Mesh();
        discMesh.name = "ZoneDisc";
        discMesh.vertices = vertices;
        discMesh.triangles = triangles;
        discMesh.RecalculateBounds();

        return discMesh;
    }

    private Material GetZoneFillMaterial()
    {
        if (zoneFillMaterial == null)
        {
            zoneFillMaterial = new Material(Shader.Find("Sprites/Default"));
            zoneFillMaterial.color = forbiddenFillColor;
        }

        return zoneFillMaterial;
    }

    private Material GetSolidMaterial()
    {
        if (solidMaterial == null)
        {
            solidMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        return solidMaterial;
    }

    // 円周に等間隔で並ぶ細かい点線(24個の破線)
    private Material GetDashedMaterial()
    {
        if (dashedMaterial != null)
        {
            return dashedMaterial;
        }

        const int width = 96;

        Texture2D texture = new Texture2D(
            width,
            1,
            TextureFormat.RGBA32,
            false
        );

        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int i = 0; i < width; i++)
        {
            // 2ピクセル表示、2ピクセル透明を繰り返す
            Color color = (i % 4) < 2
                ? Color.white
                : new Color(1f, 1f, 1f, 0f);

            texture.SetPixel(i, 0, color);
        }

        texture.Apply();

        dashedMaterial = new Material(Shader.Find("Sprites/Default"));
        dashedMaterial.mainTexture = texture;

        return dashedMaterial;
    }

    // 最も近い基本線を取得(maxDistance以内)
    private BasicLine GetNearestBasicLine(Vector2 position, float maxDistance)
    {
        BasicLine nearestLine = null;

        float nearestDistance = maxDistance;

        foreach (BasicLine line in basicLines)
        {
            if (line == null || !line.isActive)
            {
                continue;
            }

            float distance = line.GetDistance(position);

            if (distance <= nearestDistance)
            {
                nearestDistance = distance;

                nearestLine = line;
            }
        }

        return nearestLine;
    }

    // 既存の線と交差するか
    // 始点側・終点側の基本線は端で接続するため判定から除外する
    private bool CrossesExistingLines(
        Vector2 start,
        Vector2 end,
        BasicLine endLine)
    {
        // 基本線との交差判定(曲線を折れ線に分割して判定)
        foreach (BasicLine line in basicLines)
        {
            if (line == null || !line.isActive)
            {
                continue;
            }

            if (line == selectedStartLine || line == endLine)
            {
                continue;
            }

            List<Vector2> poly = line.GetPolyline(basicLineCrossSamples);

            for (int i = 0; i < poly.Count - 1; i++)
            {
                if (SegmentsCross(start, end, poly[i], poly[i + 1]))
                {
                    return true;
                }
            }
        }

        // 既存の造線との交差判定
        foreach (CreatedLine line in createdLines)
        {
            if (line == null || !line.isActive)
            {
                continue;
            }

            if (SegmentsCross(
                start,
                end,
                line.start,
                line.end))
            {
                return true;
            }
        }

        return false;
    }

    // 2本の線分が交差するか
    private bool SegmentsCross(
        Vector2 a,
        Vector2 b,
        Vector2 c,
        Vector2 d)
    {
        float denominator =
            (b.x - a.x) * (d.y - c.y)
            - (b.y - a.y) * (d.x - c.x);

        // 平行または同一直線上
        if (Mathf.Abs(denominator) < 0.0001f)
        {
            return false;
        }

        float t =
            ((c.x - a.x) * (d.y - c.y)
            - (c.y - a.y) * (d.x - c.x))
            / denominator;

        float u =
            ((c.x - a.x) * (b.y - a.y)
            - (c.y - a.y) * (b.x - a.x))
            / denominator;

        // 線分の内部で交差する場合
        // 端点同士の接続は許可
        const float epsilon = 0.001f;

        bool insideT =
            t > epsilon && t < 1f - epsilon;

        bool insideU =
            u > epsilon && u < 1f - epsilon;

        return insideT && insideU;
    }

    // ===== 線追加ノード用: ランダムな造線を1本追加する =====
    // ゲージは消費しない(cost=0)。禁止ルールに触れない線が見つかったときだけ追加する。
    //  maxAngleDegrees: 基本線に垂直な向きからの、ずれの最大角度(度)
    //  maxDistance    : 造線の最大の長さ(0以下なら制限なし)
    public bool AddRandomLine(float maxAngleDegrees, float maxDistance)
    {
        const int maxTries = 40;

        List<BasicLine> candidates = new List<BasicLine>();

        foreach (BasicLine line in basicLines)
        {
            if (line != null && line.isActive)
            {
                candidates.Add(line);
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        float angleLimit = Mathf.Clamp(maxAngleDegrees, 0f, 89f);

        BasicLine savedStartLine = selectedStartLine;

        for (int i = 0; i < maxTries; i++)
        {
            BasicLine startLine = candidates[Random.Range(0, candidates.Count)];

            float t = Random.Range(0.05f, 0.95f);

            Vector2 start = startLine.GetPointOnCurve(t);

            // 曲線の接線に垂直な向きを基準にする
            Vector2 tangent =
                startLine.GetPointOnCurve(Mathf.Min(1f, t + 0.01f)) -
                startLine.GetPointOnCurve(Mathf.Max(0f, t - 0.01f));

            if (tangent.sqrMagnitude < 0.000001f)
            {
                continue;
            }

            Vector2 normal = new Vector2(-tangent.y, tangent.x).normalized;

            if (Random.value < 0.5f)
            {
                normal = -normal;
            }

            float angle = Random.Range(-angleLimit, angleLimit);

            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * normal;

            BasicLine hitLine = FindFirstBasicLineHit(
                start, direction, out Vector2 end);

            if (hitLine == null || hitLine == startLine)
            {
                continue;
            }

            float length = Vector2.Distance(start, end);

            if (length < minLineLength || length < forbiddenRadius)
            {
                continue;
            }

            if (maxDistance > 0f && length > maxDistance)
            {
                continue;
            }

            if (IsInForbiddenZone(start) || IsInForbiddenZone(end))
            {
                continue;
            }

            if (HitsCenterZone(start, end, out _, out _, out _))
            {
                continue;
            }

            // 始点側の基本線は交差判定から除外される
            selectedStartLine = startLine;

            bool crosses = CrossesExistingLines(start, end, hitLine);

            selectedStartLine = savedStartLine;

            if (crosses)
            {
                continue;
            }

            CreateCreatedLine(start, end, 0f);

            return true;
        }

        selectedStartLine = savedStartLine;

        Debug.Log("線追加ノード: 置ける場所が見つからず、造線を追加できませんでした。");

        return false;
    }

    // ===== 禁止ルールの点滅表示 =====

    // 点の周りの、既存の造線の始点・終点の禁止範囲の中心を集める
    private void CollectZoneCenters(Vector2 point, List<Vector2> result)
    {
        foreach (CreatedLine line in createdLines)
        {
            if (line == null || !line.isActive)
            {
                continue;
            }

            if (Vector2.Distance(point, line.start) <= forbiddenRadius)
            {
                result.Add(line.start);
            }

            if (Vector2.Distance(point, line.end) <= forbiddenRadius)
            {
                result.Add(line.end);
            }
        }
    }

    // 今引いている線が触れている禁止ルールを調べる(基本線に届いているときだけ)
    //  ルール1: 跨いだ基本線 / ルール4: 跨いだ造線
    //  ルール5: 終点が入った禁止範囲 / ルール6: ゲージ不足
    private void UpdateViolations(Vector2 end, bool reached)
    {
        blinkBasic.Clear();
        blinkCreated.Clear();
        blinkZones.Clear();
        blinkGauge = false;
        blinkCenter = false;
        blinkYellowTop = false;
        blinkYellowBottom = false;

        if (!isDrawing || !reached || lastHitLine == null)
        {
            return;
        }

        foreach (BasicLine line in basicLines)
        {
            if (line == null || !line.isActive ||
                line == selectedStartLine || line == lastHitLine)
            {
                continue;
            }

            List<Vector2> poly = line.GetPolyline(basicLineCrossSamples);

            for (int i = 0; i < poly.Count - 1; i++)
            {
                if (SegmentsCross(startPosition, end, poly[i], poly[i + 1]))
                {
                    blinkBasic.Add(line);
                    break;
                }
            }
        }

        foreach (CreatedLine line in createdLines)
        {
            if (line == null || !line.isActive)
            {
                continue;
            }

            if (SegmentsCross(startPosition, end, line.start, line.end))
            {
                blinkCreated.Add(line);
            }
        }

        CollectZoneCenters(end, blinkZones);

        HitsCenterZone(
            startPosition, end,
            out blinkCenter, out blinkYellowTop, out blinkYellowBottom);

        float length = Vector2.Distance(startPosition, end);

        blinkGauge = currentLineGauge < length * lineCostPerMeter;
    }

    private void ClearViolations()
    {
        blinkBasic.Clear();
        blinkCreated.Clear();
        blinkZones.Clear();
        blinkGauge = false;
        blinkCenter = false;
        blinkYellowTop = false;
        blinkYellowBottom = false;
    }

    // 毎フレーム、点滅の表示を反映する(触れなくなったものは元に戻す)
    private void ApplyViolationDisplay()
    {
        float k = (Mathf.Sin(Time.unscaledTime * violationBlinkSpeed) + 1f) * 0.5f;

        // 基本線
        List<BasicLine> restore = null;

        foreach (var pair in basicOriginalColors)
        {
            if (!blinkBasic.Contains(pair.Key))
            {
                if (restore == null) restore = new List<BasicLine>();
                restore.Add(pair.Key);
            }
        }

        if (restore != null)
        {
            foreach (BasicLine line in restore)
            {
                if (line != null)
                {
                    LineRenderer lr = line.GetComponent<LineRenderer>();

                    if (lr != null)
                    {
                        lr.startColor = basicOriginalColors[line];
                        lr.endColor = basicOriginalColors[line];
                    }
                }

                basicOriginalColors.Remove(line);
            }
        }

        foreach (BasicLine line in blinkBasic)
        {
            if (line == null) continue;

            LineRenderer lr = line.GetComponent<LineRenderer>();

            if (lr == null) continue;

            if (!basicOriginalColors.ContainsKey(line))
            {
                basicOriginalColors[line] = lr.startColor;
            }

            Color c = Color.Lerp(basicOriginalColors[line], violationColor, k);
            lr.startColor = c;
            lr.endColor = c;
        }

        // 造線
        foreach (CreatedLine line in shownBlinkCreated)
        {
            if (!blinkCreated.Contains(line) && line != null &&
                line.isActive && line.renderer != null)
            {
                Color c = createdLineColor;
                c.a = line.isWaitingForNode ? minAlpha : 1f;
                line.renderer.startColor = c;
                line.renderer.endColor = c;
            }
        }

        shownBlinkCreated.Clear();

        foreach (CreatedLine line in blinkCreated)
        {
            if (line == null || !line.isActive || line.renderer == null) continue;

            Color c = Color.Lerp(createdLineColor, violationColor, k);
            line.renderer.startColor = c;
            line.renderer.endColor = c;
            shownBlinkCreated.Add(line);
        }

        // 禁止範囲(始点・終点)
        List<Vector2> zones = new List<Vector2>(blinkZones);

        if (Time.unscaledTime < startBlockedUntil)
        {
            zones.AddRange(startBlockedZones);
        }

        ShowZoneFlashes(zones, k);
        ShowAreaFlashes();

        // 造線ゲージ
        ApplyGaugeBlink(k);
    }

    private void ShowZoneFlashes(List<Vector2> zones, float k)
    {
        if (zoneFlashMaterial == null)
        {
            zoneFlashMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        Color c = violationColor;
        c.a = Mathf.Lerp(0.15f, 0.8f, k);
        zoneFlashMaterial.color = c;

        while (zoneFlashPool.Count < zones.Count)
        {
            GameObject obj = new GameObject("ViolationZoneFlash");
            obj.transform.SetParent(transform, false);
            obj.transform.localScale =
                new Vector3(forbiddenRadius, forbiddenRadius, 1f);

            obj.AddComponent<MeshFilter>().sharedMesh = GetDiscMesh();

            MeshRenderer mr = obj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = zoneFlashMaterial;
            mr.sortingOrder = 12;

            zoneFlashPool.Add(obj);
        }

        for (int i = 0; i < zoneFlashPool.Count; i++)
        {
            GameObject obj = zoneFlashPool[i];

            if (obj == null) continue;

            if (i < zones.Count)
            {
                obj.transform.position = new Vector3(zones[i].x, zones[i].y, 0f);
                obj.transform.localScale =
                    new Vector3(forbiddenRadius, forbiddenRadius, 1f);
                obj.SetActive(true);
            }
            else if (obj.activeSelf)
            {
                obj.SetActive(false);
            }
        }
    }

    // 禁止ルール6: 造線ゲージのUIを赤く点滅させる
    private void ApplyGaugeBlink(float k)
    {
        if (lineGaugeBar == null)
        {
            return;
        }

        if (blinkGauge)
        {
            foreach (Graphic g in lineGaugeBar.GetComponentsInChildren<Graphic>(true))
            {
                if (g == null) continue;

                if (!gaugeOriginalColors.ContainsKey(g))
                {
                    gaugeOriginalColors[g] = g.color;
                }

                Color target = violationColor;
                target.a = gaugeOriginalColors[g].a;

                g.color = Color.Lerp(gaugeOriginalColors[g], target, k);
            }
        }
        else if (gaugeOriginalColors.Count > 0)
        {
            foreach (var pair in gaugeOriginalColors)
            {
                if (pair.Key != null)
                {
                    pair.Key.color = pair.Value;
                }
            }

            gaugeOriginalColors.Clear();
        }
    }

    // ===== 設置禁止範囲(禁止ルール2・3) =====

    // 画面の中で、始点を置いてよい範囲(画面の端から outerMargin を除いた範囲)
    private Rect GetInnerScreenRect()
    {
        if (targetCamera == null)
        {
            return new Rect(-1000f, -1000f, 2000f, 2000f);
        }

        float h = targetCamera.orthographicSize;
        float w = h * targetCamera.aspect;
        Vector2 c = targetCamera.transform.position;

        return new Rect(
            c.x - w + outerMargin,
            c.y - h + outerMargin,
            Mathf.Max(0f, 2f * (w - outerMargin)),
            Mathf.Max(0f, 2f * (h - outerMargin)));
    }

    // 中央の円と黄色い枠を、基本線⑦⑧などから自動で設定する
    [ContextMenu("中央の設置禁止範囲を基本線から自動設定")]
    private void AutoSetCenterZone()
    {
        if (basicLines == null)
        {
            return;
        }

        if (autoSetCenterZone && centerZoneRadius <= 0f &&
            centerLineIndexA >= 0 && centerLineIndexB >= 0 &&
            centerLineIndexA < basicLines.Length &&
            centerLineIndexB < basicLines.Length &&
            basicLines[centerLineIndexA] != null &&
            basicLines[centerLineIndexB] != null)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            foreach (int index in new[] { centerLineIndexA, centerLineIndexB })
            {
                foreach (Vector2 p in basicLines[index].GetPolyline(basicLineCrossSamples))
                {
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
            }

            centerZoneCenter = (min + max) * 0.5f;
            centerZoneRadius = ((max.x - min.x) + (max.y - min.y)) * 0.25f;

            Debug.Log(
                $"中央の設置禁止範囲を自動設定しました。中心={centerZoneCenter} " +
                $"半径={centerZoneRadius:F2}(違う場合はInspectorで直してください)");
        }

        if (centerZoneRadius <= 0f)
        {
            return;
        }

        float r = centerZoneRadius;
        Vector2 c = centerZoneCenter;

        if (yellowZoneTop.width <= 0f)
        {
            yellowZoneTop = new Rect(c.x - r * 0.5f, c.y + r * 0.7f, r, r * 0.5f);
        }

        if (yellowZoneBottom.width <= 0f)
        {
            yellowZoneBottom = new Rect(c.x - r * 0.5f, c.y - r * 1.2f, r, r * 0.5f);
        }

        if (yellowLineTop == null && basicLines.Length > 8)
        {
            yellowLineTop = basicLines[8];
        }

        if (yellowLineBottom == null && basicLines.Length > 9)
        {
            yellowLineBottom = basicLines[9];
        }
    }

    // 線分が、中央の設置禁止範囲(円・追加済みの黄色い枠)を通るか
    private bool HitsCenterZone(
        Vector2 a, Vector2 b,
        out bool circle, out bool yellowTop, out bool yellowBottom)
    {
        circle = false;
        yellowTop = false;
        yellowBottom = false;

        if (centerZoneRadius <= 0f)
        {
            return false;
        }

        float distance = Vector2.Distance(
            centerZoneCenter,
            GetClosestPointOnSegment(centerZoneCenter, a, b));

        circle = distance < centerZoneRadius - centerZoneTolerance;

        // 黄色い枠は、「欠損」で基本線⑨⑩が追加されたあとだけ禁止範囲になる
        yellowTop =
            yellowLineTop != null && yellowLineTop.isActive &&
            SegmentHitsRect(a, b, yellowZoneTop);

        yellowBottom =
            yellowLineBottom != null && yellowLineBottom.isActive &&
            SegmentHitsRect(a, b, yellowZoneBottom);

        return circle || yellowTop || yellowBottom;
    }

    private static bool SegmentHitsRect(Vector2 a, Vector2 b, Rect rect)
    {
        if (rect.width <= 0f || rect.height <= 0f)
        {
            return false;
        }

        // Liang-Barsky
        float t0 = 0f, t1 = 1f;
        Vector2 d = b - a;

        float[] p = { -d.x, d.x, -d.y, d.y };
        float[] q =
        {
            a.x - rect.xMin, rect.xMax - a.x,
            a.y - rect.yMin, rect.yMax - a.y
        };

        for (int i = 0; i < 4; i++)
        {
            if (Mathf.Abs(p[i]) < 0.000001f)
            {
                if (q[i] < 0f) return false;
                continue;
            }

            float t = q[i] / p[i];

            if (p[i] < 0f) { if (t > t1) return false; if (t > t0) t0 = t; }
            else { if (t < t0) return false; if (t < t1) t1 = t; }
        }

        return true;
    }

    private Mesh GetQuadMesh()
    {
        if (quadMesh != null)
        {
            return quadMesh;
        }

        quadMesh = new Mesh();
        quadMesh.name = "ZoneQuad";
        quadMesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f)
        };
        quadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        quadMesh.RecalculateBounds();

        return quadMesh;
    }

    private GameObject SetAreaFlash(
        GameObject obj, Mesh mesh, bool show, Vector2 center, Vector2 scale)
    {
        if (obj == null)
        {
            if (!show)
            {
                return null;
            }

            obj = new GameObject("ViolationAreaFlash");
            obj.transform.SetParent(transform, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer mr = obj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = zoneFlashMaterial;
            mr.sortingOrder = 12;
        }

        if (show)
        {
            obj.transform.position = new Vector3(center.x, center.y, 0f);
            obj.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        }

        if (obj.activeSelf != show)
        {
            obj.SetActive(show);
        }

        return obj;
    }

    // 禁止ルール2(中央の円・黄色い枠)、禁止ルール3(画面端の枠)の点滅
    private void ShowAreaFlashes()
    {
        if (zoneFlashMaterial == null)
        {
            return;
        }

        flashCenter = SetAreaFlash(
            flashCenter, GetDiscMesh(), blinkCenter,
            centerZoneCenter, new Vector2(centerZoneRadius, centerZoneRadius));

        flashYellowTop = SetAreaFlash(
            flashYellowTop, GetQuadMesh(), blinkYellowTop,
            yellowZoneTop.center, yellowZoneTop.size);

        flashYellowBottom = SetAreaFlash(
            flashYellowBottom, GetQuadMesh(), blinkYellowBottom,
            yellowZoneBottom.center, yellowZoneBottom.size);

        // 画面端の枠(内側の範囲の外側4辺)
        bool showFrame =
            startBlockedByFrame && Time.unscaledTime < startBlockedUntil;

        if (targetCamera != null)
        {
            float h = targetCamera.orthographicSize;
            float w = h * targetCamera.aspect;
            Vector2 c = targetCamera.transform.position;
            float m = outerMargin;

            flashFrame[0] = SetAreaFlash(flashFrame[0], GetQuadMesh(), showFrame,
                new Vector2(c.x, c.y + h - m * 0.5f), new Vector2(2f * w, m));
            flashFrame[1] = SetAreaFlash(flashFrame[1], GetQuadMesh(), showFrame,
                new Vector2(c.x, c.y - h + m * 0.5f), new Vector2(2f * w, m));
            flashFrame[2] = SetAreaFlash(flashFrame[2], GetQuadMesh(), showFrame,
                new Vector2(c.x - w + m * 0.5f, c.y), new Vector2(m, 2f * h - 2f * m));
            flashFrame[3] = SetAreaFlash(flashFrame[3], GetQuadMesh(), showFrame,
                new Vector2(c.x + w - m * 0.5f, c.y), new Vector2(m, 2f * h - 2f * m));
        }
    }

    // Sceneビューで、設置禁止範囲を確認する(LineManagerを選択中に表示)
    private void OnDrawGizmosSelected()
    {
        if (centerZoneRadius > 0f)
        {
            Gizmos.color = Color.red;

            Vector3 prev = Vector3.zero;

            for (int i = 0; i <= 64; i++)
            {
                float angle = i / 64f * Mathf.PI * 2f;

                Vector3 p = new Vector3(
                    centerZoneCenter.x + Mathf.Cos(angle) * centerZoneRadius,
                    centerZoneCenter.y + Mathf.Sin(angle) * centerZoneRadius, 0f);

                if (i > 0) Gizmos.DrawLine(prev, p);

                prev = p;
            }
        }

        Gizmos.color = Color.yellow;
        DrawRectGizmo(yellowZoneTop);
        DrawRectGizmo(yellowZoneBottom);

        Camera cam = targetCamera != null ? targetCamera : Camera.main;

        if (cam != null && cam.orthographic)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 1f);

            float h = cam.orthographicSize;
            float w = h * cam.aspect;
            Vector2 c = cam.transform.position;

            DrawRectGizmo(new Rect(
                c.x - w + outerMargin, c.y - h + outerMargin,
                2f * (w - outerMargin), 2f * (h - outerMargin)));
        }
    }

    private static void DrawRectGizmo(Rect r)
    {
        if (r.width <= 0f || r.height <= 0f) return;

        Vector3 a = new Vector3(r.xMin, r.yMin, 0f);
        Vector3 b = new Vector3(r.xMin, r.yMax, 0f);
        Vector3 c = new Vector3(r.xMax, r.yMax, 0f);
        Vector3 d = new Vector3(r.xMax, r.yMin, 0f);

        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d);
        Gizmos.DrawLine(d, a);
    }

    // 右クリックした造線を削除
    private void DeleteCreatedLine()
    {
        Vector2 mousePosition = GetMouseWorldPosition();

        CreatedLine nearestLine = null;

        float nearestDistance = lineSelectDistance;

        foreach (CreatedLine line in createdLines)
        {
            if (line == null || !line.isActive)
            {
                continue;
            }

            float distance = Vector2.Distance(
                mousePosition,
                GetClosestPointOnSegment(
                    mousePosition,
                    line.start,
                    line.end
                )
            );

            if (distance <= nearestDistance)
            {
                nearestDistance = distance;

                nearestLine = line;
            }
        }

        if (nearestLine == null)
        {
            return;
        }

        // ノードが乗っている間は消さず、消滅待ちにする(ノードが固まるのを防ぐ)
        if (IsNodeOnLine(nearestLine))
        {
            nearestLine.elapsedTime =
                Mathf.Max(nearestLine.elapsedTime, lineLifetime);

            if (!nearestLine.isWaitingForNode)
            {
                nearestLine.isWaitingForNode = true;
                LinesChanged?.Invoke();
            }

            if (nearestLine.renderer != null)
            {
                Color waitColor = createdLineColor;
                waitColor.a = minAlpha;

                nearestLine.renderer.startColor = waitColor;
                nearestLine.renderer.endColor = waitColor;
            }

            return;
        }

        // ゲージ返却
        currentLineGauge += nearestLine.cost;

        currentLineGauge = Mathf.Min(
            currentLineGauge,
            maxLineGauge
        );

        nearestLine.isActive = false;

        if (nearestLine.renderer != null)
        {
            Destroy(nearestLine.renderer.gameObject);
        }

        createdLines.Remove(nearestLine);

        UpdateGaugeUI();

        LinesChanged?.Invoke();

        Debug.Log(
            $"造線を削除しました。返却ゲージ：{nearestLine.cost:F0}"
        );
    }

    // 線分上の最も近い点
    private Vector2 GetClosestPointOnSegment(
        Vector2 position,
        Vector2 start,
        Vector2 end)
    {
        Vector2 direction = end - start;

        float lengthSquared = direction.sqrMagnitude;

        if (lengthSquared <= 0.0001f)
        {
            return start;
        }

        float t = Vector2.Dot(
            position - start,
            direction
        ) / lengthSquared;

        t = Mathf.Clamp01(t);

        return start + direction * t;
    }

    // ゲージUI更新
    private void UpdateGaugeUI()
    {
        if (lineGaugeBar != null)
        {
            lineGaugeBar.SetValues(
                currentLineGauge,
                maxLineGauge,
                isDrawing ? pendingCost : 0f
            );
        }
    }

    // 最大ゲージを外部から変更する場合
    public void SetMaxLineGauge(float value)
    {
        maxLineGauge = Mathf.Max(0, value);

        currentLineGauge = Mathf.Min(
            currentLineGauge,
            maxLineGauge
        );

        UpdateGaugeUI();
    }

    // ステージ開始時: 最大ゲージを設定して、満タンにする
    public void InitLineGauge(float value)
    {
        maxLineGauge = Mathf.Max(0, value);
        currentLineGauge = maxLineGauge;

        UpdateGaugeUI();
    }

    // 現在のゲージを取得
    public float GetCurrentLineGauge()
    {
        return currentLineGauge;
    }

    // 最大ゲージを取得
    public float GetMaxLineGauge()
    {
        return maxLineGauge;
    }

    // ノードが移動経路として利用できる造線を取得
    public List<CreatedLineInfo> GetActiveCreatedLines()
    {
        List<CreatedLineInfo> result =
            new List<CreatedLineInfo>();

        foreach (CreatedLine line in createdLines)
        {
            if (line == null || !line.isActive)
            {
                continue;
            }

            if (line.renderer == null)
            {
                continue;
            }

            result.Add(
                new CreatedLineInfo(
                    line.id,
                    line.start,
                    line.end,
                    line.isWaitingForNode
                )
            );
        }

        return result;
    }

    private void UpdateCreatedLines()
    {
        for (int i = createdLines.Count - 1; i >= 0; i--)
        {
            CreatedLine line = createdLines[i];

            if (line == null || !line.isActive)
            {
                continue;
            }

            line.elapsedTime += Time.deltaTime;

            float remainingTime =
                lineLifetime - line.elapsedTime;

            // 消滅準備
            if (remainingTime <= warningTime)
            {
                line.isWarning = true;

                float alpha = Mathf.Lerp(
                    minAlpha,
                    1f,
                    (Mathf.Sin(Time.time * blinkSpeed) + 1f) / 2f
                );

                Color color = createdLineColor;
                color.a = alpha;

                if (line.renderer != null)
                {
                    line.renderer.startColor = color;
                    line.renderer.endColor = color;
                }
            }

            // 寿命到達
            if (line.elapsedTime >= lineLifetime)
            {
                // ノードがいない場合は削除
                if (!IsNodeOnLine(line))
                {
                    RemoveCreatedLine(line);
                    continue;
                }

                // ノードがいる場合は消滅待ち(新しいノードは入れない)
                if (!line.isWaitingForNode)
                {
                    line.isWaitingForNode = true;
                    LinesChanged?.Invoke();
                }

                if (line.renderer != null)
                {
                    Color color = createdLineColor;
                    color.a = minAlpha;

                    line.renderer.startColor = color;
                    line.renderer.endColor = color;
                }
            }

            // 消滅待ちの線はノードが離れたら削除
            if (line.isWaitingForNode && !IsNodeOnLine(line))
            {
                RemoveCreatedLine(line);
            }
        }
    }

    // ノードが造線上にいるか確認
    private bool IsNodeOnLine(CreatedLine line)
    {
        if (line == null)
        {
            return false;
        }

        NodeController[] nodes =
            FindObjectsByType<NodeController>(FindObjectsSortMode.None);

        foreach (NodeController node in nodes)
        {
            if (node == null || !node.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector2 nodePosition = node.transform.position;

            Vector2 closestPoint = GetClosestPointOnSegment(
                nodePosition,
                line.start,
                line.end
            );

            float distance = Vector2.Distance(
                nodePosition,
                closestPoint
            );

            // 造線から0.15以内なら線上にいると判定
            if (distance <= 0.15f)
            {
                return true;
            }
        }

        return false;
    }

    // 造線を削除してゲージを返却
    private void RemoveCreatedLine(CreatedLine line)
    {
        if (line == null || !line.isActive)
        {
            return;
        }

        line.isActive = false;

        // 消費したゲージを返却
        currentLineGauge += line.cost;
        currentLineGauge = Mathf.Min(
            currentLineGauge,
            maxLineGauge
        );

        if (line.renderer != null)
        {
            Destroy(line.renderer.gameObject);
        }

        createdLines.Remove(line);

        UpdateGaugeUI();

        LinesChanged?.Invoke();

        Debug.Log($"造線が消滅しました。返却ゲージ：{line.cost:F0}");
    }
}