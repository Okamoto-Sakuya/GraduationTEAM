using UnityEngine;

public class DrawnWire : TrackEdge
{
    public enum LifetimeMode
    {
        Seconds,
        PassCount
    }

    public enum WireState
    {
        Active,
        Warning,
        WaitingForNodes,
        Removed
    }

    [Header("寿命設定")]
    public LifetimeMode lifetimeMode = LifetimeMode.Seconds;
    public float lifetimeSeconds = 15f;
    public int maximumPassCount = 5;

    [Header("消滅警告")]
    public float warningSeconds = 3f;
    public int warningPassCount = 1;
    public float blinkSpeed = 3f;
    [Range(0f, 1f)] public float warningAlpha = 0.3f;
    [Range(0f, 1f)] public float waitingAlpha = 0.3f;

    private float elapsedTime;
    private int passCount;
    private Color originalStartColor;
    private Color originalEndColor;
    private WireDrawingController owner;
    private float reservedGauge;
    private WireState state = WireState.Active;
    private bool initialized;


    /// <summary>
    /// 造線が完成しているか
    /// </summary>
    public bool IsCompleted => initialized && !isPreviewing && state != WireState.Removed;


    // 描画中の仮線かどうか
    private bool isPreviewing;

    public bool IsPreviewing => isPreviewing;

    private LinePoint previewEndPoint;

    public WireState State => state;

    public void Initialize(
        LinePoint start,
        LinePoint end,
        Material material,
        float width,
        Color color,
        WireDrawingController wireOwner = null,
        float gaugeCost = 0f)
    {
        pointA = start;
        pointB = end;

        owner = wireOwner;
        reservedGauge = gaugeCost;

        lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;

        if (material != null)
            lineRenderer.material = material;

        originalStartColor = lineRenderer.startColor;
        originalEndColor = lineRenderer.endColor;

        state = WireState.Active;
        initialized = true;

        RegisterPoints();
    }


    private void Update()
    {
        if (!initialized || state == WireState.Removed)
            return;

        // 描画中の仮線は寿命カウントを開始しない
        if (isPreviewing)
        {
            UpdateLine();
            return;
        }

        if (state == WireState.WaitingForNodes)
        {
            SetWireAlpha(waitingAlpha);
            TryRemoveWhenEmpty();
            return;
        }

        if (lifetimeMode == LifetimeMode.Seconds)
        {
            elapsedTime += Time.deltaTime;

            if (lifetimeSeconds - elapsedTime <= warningSeconds)
                BeginWarning();

            if (elapsedTime >= lifetimeSeconds)
                BeginRemoval();
        }

        if (lifetimeMode == LifetimeMode.PassCount &&
            maximumPassCount - passCount <= warningPassCount)
        {
            BeginWarning();
        }

        if (state == WireState.Warning)
        {
            float t = (Mathf.Sin(Time.time * blinkSpeed) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(warningAlpha, 1f, t);

            SetWireAlpha(alpha);
            UpdateNodesAlpha(alpha);

            if (lifetimeMode == LifetimeMode.Seconds &&
                elapsedTime >= lifetimeSeconds)
            {
                BeginRemoval();
            }
            else if (lifetimeMode == LifetimeMode.PassCount &&
                     passCount >= maximumPassCount)
            {
                BeginRemoval();
            }
        }
    }


    public override bool CanDepartFrom(LinePoint point)
    {
        return initialized &&
               state != WireState.Removed &&
               (point == pointA || point == pointB);
    }

    public override bool CanEnter()
    {
        return initialized &&
               state != WireState.WaitingForNodes &&
               state != WireState.Removed;
    }

    /// <summary>
    /// ノードがこの造線へ入ったときに呼ぶ。
    /// </summary>
    public void RegisterPass()
    {
        if (state == WireState.Removed)
            return;

        passCount++;

        if (lifetimeMode == LifetimeMode.PassCount)
        {
            if (passCount >= maximumPassCount)
                BeginRemoval();
            else if (maximumPassCount - passCount <= warningPassCount)
                BeginWarning();
        }
    }

    private void BeginWarning()
    {
        if (state != WireState.Active)
            return;

        state = WireState.Warning;
    }

    private void BeginRemoval()
    {
        if (state == WireState.Removed ||
            state == WireState.WaitingForNodes)
        {
            return;
        }

        if (HasNodeOnWire())
        {
            state = WireState.WaitingForNodes;

            SetWireAlpha(waitingAlpha);
            UpdateNodesAlpha(waitingAlpha);
        }
        else
        {
            RemoveWire();
        }
    }

    private bool HasNodeOnWire()
    {
        NodeController[] nodes =
            FindObjectsByType<NodeController>(
                FindObjectsSortMode.None);

        foreach (NodeController node in nodes)
        {
            if (node != null && node.IsOnEdge(this))
                return true;
        }

        return false;
    }

    private void TryRemoveWhenEmpty()
    {
        if (!HasNodeOnWire())
            RemoveWire();
    }

    private void UpdateNodesAlpha(float alpha)
    {
        NodeController[] nodes =
            FindObjectsByType<NodeController>(
                FindObjectsSortMode.None);

        foreach (NodeController node in nodes)
        {
            if (node != null && node.IsOnEdge(this))
                node.SetWireWarningAlpha(alpha);
        }
    }

    private void SetWireAlpha(float alpha)
    {
        if (lineRenderer == null)
            return;

        Color start = originalStartColor;
        Color end = originalEndColor;

        start.a *= alpha;
        end.a *= alpha;

        lineRenderer.startColor = start;
        lineRenderer.endColor = end;
    }

    private void RemoveWire()
    {
        if (state == WireState.Removed)
            return;

        state = WireState.Removed;

        UnregisterPoints();

        if (owner != null)
            owner.RefundGauge(reservedGauge);

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        UnregisterPoints();
    }

    // =====================================================
    // 描画中の仮線を初期化
    // =====================================================
    public void InitializePreview(
        LinePoint start,
        LinePoint temporaryEnd,
        Material material,
        float width,
        Color color,
        WireDrawingController wireOwner)
    {
        if (start == null || temporaryEnd == null)
            return;

        previewEndPoint = temporaryEnd;

        Initialize(
            start,
            temporaryEnd,
            material,
            width,
            color,
            wireOwner,
            0f
        );

        isPreviewing = true;
    }

    // =====================================================
    // 描画中の終点をリアルタイム更新
    // =====================================================
    public void UpdatePreviewEnd(Vector3 position)
    {
        if (!isPreviewing || previewEndPoint == null)
            return;

        previewEndPoint.transform.position = position;

        if (lineRenderer != null)
        {
            lineRenderer.SetPosition(0, pointA.transform.position);
            lineRenderer.SetPosition(1, position);
        }
    }

    // =====================================================
    // 仮線を確定して通常の造線にする
    // =====================================================
    public void CompletePreview(LinePoint finalEndPoint, float gaugeCost)
    {
        if (!isPreviewing ||
            finalEndPoint == null ||
            pointA == null ||
            finalEndPoint == pointA)
        {
            return;
        }

        LinePoint oldPreviewEnd = previewEndPoint;

        // ノードが使用している接続情報を更新
        UnregisterPoints();

        pointB = finalEndPoint;
        reservedGauge = gaugeCost;
        elapsedTime = 0f;
        passCount = 0;
        state = WireState.Active;

        RegisterPoints();

        // ノードの移動先が仮の終点だった場合、確定先へ更新
        NodeController[] nodes =
            FindObjectsByType<NodeController>(
                FindObjectsSortMode.None);

        foreach (NodeController node in nodes)
        {
            if (node != null)
            {
                node.HandleWirePreviewCompleted(
                    this,
                    oldPreviewEnd,
                    finalEndPoint);
            }
        }

        isPreviewing = false;
        previewEndPoint = null;

        if (lineRenderer != null)
        {
            lineRenderer.SetPosition(0, pointA.transform.position);
            lineRenderer.SetPosition(1, finalEndPoint.transform.position);
        }

        if (oldPreviewEnd != null)
            Destroy(oldPreviewEnd.gameObject);
    }

    // =====================================================
    // 描画キャンセル時に仮線を削除
    // =====================================================
    public void CancelPreview()
    {
        if (!isPreviewing)
            return;

        isPreviewing = false;
        state = WireState.Removed;

        UnregisterPoints();

        if (previewEndPoint != null)
        {
            Destroy(previewEndPoint.gameObject);
            previewEndPoint = null;
        }

        Destroy(gameObject);
    }
}