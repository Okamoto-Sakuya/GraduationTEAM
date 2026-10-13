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

    [Header("éıñΩê›íË")]
    public LifetimeMode lifetimeMode = LifetimeMode.Seconds;
    public float lifetimeSeconds = 15f;
    public int maximumPassCount = 5;

    [Header("è¡ñ≈åxçê")]
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
    /// ÉmÅ[ÉhÇ™Ç±ÇÃë¢ê¸Ç÷ì¸Ç¡ÇΩÇ∆Ç´Ç…åƒÇ‘ÅB
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
}