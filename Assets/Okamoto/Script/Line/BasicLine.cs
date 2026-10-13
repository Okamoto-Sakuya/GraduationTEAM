using UnityEngine;

public class BasicLine : TrackEdge
{
    [Header("出現確率")]
    [Min(0.01f)]
    public float spawnWeight = 1f;

    [Header("基本線の識別名")]
    public string lineName;

    [Header("ノード生成設定")]
    public bool isSpawnSource = true;

    private bool registered;

    private void Start()
    {
        RegisterLine();
    }

    private void RegisterLine()
    {
        if (registered) return;

        RegisterPoints();
        registered = true;
    }

    private void OnDestroy()
    {
        UnregisterPoints();
    }

    // BasicLineはA→B方向に進む
    public override bool CanDepartFrom(LinePoint point)
    {
        return point == pointA && pointB != null;
    }

    /// <summary>
    /// この基本線の途中に分岐点を作る。
    /// 元の線を前半として残し、後半の線を新しく作る。
    /// </summary>
    public LinePoint SplitAt(Vector3 worldPosition)
    {
        if (pointA == null || pointB == null)
            return null;

        Vector3 a = pointA.transform.position;
        Vector3 b = pointB.transform.position;

        Vector3 closest = GetClosestPointOnSegment(
            worldPosition, a, b);

        // 端点に近すぎる場合は分割しない
        const float endpointTolerance = 0.05f;

        if (Vector3.Distance(closest, a) < endpointTolerance)
            return pointA;

        if (Vector3.Distance(closest, b) < endpointTolerance)
            return pointB;

        // 分割前の終点を保存
        LinePoint oldEnd = pointB;

        // 分岐点を作成
        GameObject branchObject = new GameObject(
            "BranchPoint_" + lineName);

        branchObject.transform.position = closest;

        CircleCollider2D branchCollider =
            branchObject.AddComponent<CircleCollider2D>();

        branchCollider.isTrigger = true;
        branchCollider.radius = 0.2f;

        LinePoint branchPoint =
            branchObject.AddComponent<LinePoint>();

        branchPoint.isBranchPoint = true;

        // 元の基本線を前半にする
        UnregisterPoints();

        pointB = branchPoint;

        RegisterPoints();
        registered = true;

        // 後半の基本線を作る
        GameObject continuationObject =
            new GameObject("BasicLineContinuation_" + lineName);

        continuationObject.transform.position = Vector3.zero;

        LineRenderer continuationRenderer =
            continuationObject.AddComponent<LineRenderer>();

        if (lineRenderer != null)
        {
            continuationRenderer.useWorldSpace = true;
            continuationRenderer.startWidth = lineRenderer.startWidth;
            continuationRenderer.endWidth = lineRenderer.endWidth;
            continuationRenderer.startColor = lineRenderer.startColor;
            continuationRenderer.endColor = lineRenderer.endColor;
            continuationRenderer.material = lineRenderer.material;
            continuationRenderer.sortingLayerID =
                lineRenderer.sortingLayerID;
            continuationRenderer.sortingOrder =
                lineRenderer.sortingOrder;
            continuationRenderer.numCapVertices =
                lineRenderer.numCapVertices;
            continuationRenderer.numCornerVertices =
                lineRenderer.numCornerVertices;
        }

        BasicLine continuation =
            continuationObject.AddComponent<BasicLine>();

        continuation.pointA = branchPoint;
        continuation.pointB = oldEnd;
        continuation.lineRenderer = continuationRenderer;

        continuation.lineName = lineName + "_Continuation";
        continuation.spawnWeight = 0f;
        continuation.isSpawnSource = false;

        continuation.RegisterLine();

        return branchPoint;
    }

    public static Vector3 GetClosestPointOnSegment(
        Vector3 position,
        Vector3 a,
        Vector3 b)
    {
        Vector3 direction = b - a;

        float lengthSquared = direction.sqrMagnitude;

        if (lengthSquared < 0.0001f)
            return a;

        float t = Vector3.Dot(position - a, direction)
                  / lengthSquared;

        t = Mathf.Clamp01(t);

        return a + direction * t;
    }
}