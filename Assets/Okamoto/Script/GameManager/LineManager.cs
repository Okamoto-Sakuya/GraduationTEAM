using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [SerializeField]
    private float lineSelectDistance = 0.25f;

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

    [Header("UI")]
    [SerializeField]
    private Slider lineGaugeSlider;

    [SerializeField]
    private TMP_Text lineGaugeText;

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

        // 左クリック開始
        if (Input.GetMouseButtonDown(0))
        {
            HandleLeftClick();
        }

        // 描画中の点線更新
        if (isDrawing)
        {
            UpdatePreviewLine();
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

    // 左クリック処理
    private void HandleLeftClick()
    {
        Vector2 mousePosition = GetMouseWorldPosition();

        // まだ始点がない場合
        if (!isDrawing)
        {
            BasicLine line = GetNearestBasicLine(mousePosition);

            if (line == null)
            {
                return;
            }

            StartDrawing(line, mousePosition);

            return;
        }

        // 描画中なら終点を探す
        BasicLine endLine = GetNearestBasicLine(mousePosition);

        if (endLine == null)
        {
            return;
        }

        // 同じ基本線への接続は不可
        if (endLine == selectedStartLine)
        {
            return;
        }

        Vector2 endPosition =
            endLine.GetClosestPoint(mousePosition);

        // 線の長さを確認
        float length = Vector2.Distance(
            startPosition,
            endPosition
        );

        if (length < minLineLength)
        {
            return;
        }

        // 既存の線を横切る場合は作成できない
        if (CrossesExistingLines(startPosition, endPosition, endLine))
        {
            Debug.Log("基本線または既存の造線を横切っています。");
            return;
        }

        // 必要なゲージ
        float cost = length * lineCostPerMeter;

        // ゲージ不足
        if (currentLineGauge < cost)
        {
            Debug.Log("造線ゲージが足りません。");
            return;
        }

        // 造線を確定
        CreateCreatedLine(
            startPosition,
            endPosition,
            cost
        );

        // 描画終了
        FinishDrawing();
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
    }

    // 点線を更新
    private void UpdatePreviewLine()
    {
        Vector2 mousePosition = GetMouseWorldPosition();

        previewLine.SetPosition(0, startPosition);
        previewLine.SetPosition(1, mousePosition);
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
    }

    // 描画確定後の後処理
    private void FinishDrawing()
    {
        isDrawing = false;

        selectedStartLine = null;

        previewLine.enabled = false;
    }

    // 最も近い基本線を取得
    private BasicLine GetNearestBasicLine(Vector2 position)
    {
        BasicLine nearestLine = null;

        float nearestDistance = lineSelectDistance;

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
        if (lineGaugeSlider != null)
        {
            lineGaugeSlider.maxValue = maxLineGauge;

            lineGaugeSlider.value = currentLineGauge;
        }

        if (lineGaugeText != null)
        {
            lineGaugeText.text =
                $"造線ゲージ {currentLineGauge:F0} / {maxLineGauge:F0}";
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