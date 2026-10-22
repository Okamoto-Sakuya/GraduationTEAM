using System.Collections.Generic;
using UnityEngine;

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
            return;
        }

        UpdateCreatedLines();

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

        // 既存の造線の始点・終点の禁止範囲内には始点を置けない
        if (IsInForbiddenZone(line.GetClosestPoint(mousePosition)))
        {
            Debug.Log("開始禁止範囲内です。");
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

        // 基本線に届くまではマウスの位置まで伸び、届いたら基本線の上で止まる
        previewLine.SetPosition(0, startPosition);
        previewLine.SetPosition(1, endPosition);

        // 基本線に届いたのに確定できないときだけ、色を変える
        Color color = (reached && !valid)
            ? invalidPreviewColor
            : previewLineColor;

        previewLine.startColor = color;
        previewLine.endColor = color;

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