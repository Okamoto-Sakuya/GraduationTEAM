
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

    [Header("出現制御")]
    [Tooltip("最初から表示する基本線。破壊時に生成する線はOFFにする")]
    public bool showOnStart = true;

    [Header("破壊時に生成された基本線")]
    public bool isGeneratedRoute = false;

    private bool hasAppeared;
    private bool registered;

    [Header("B側のLinePoint削除設定")]
    [Tooltip("実行時にB側のLinePointだけを削除する")]
    public bool deletePointBOnly = false;

    private void Start()
    {
        RegisterLine();

        if (!showOnStart)
        {
            SetLineVisible(false);
        }
        else
        {
            hasAppeared = true;
        }
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
    /// B側のLinePointだけを削除する。
    /// A側とBasicLine自体は残す。
    /// 他の線がB側を使用している場合は削除しない。
    /// </summary>
    public bool DeletePointBOnly()
    {
        if (pointA == null || pointB == null)
        {
            Debug.LogWarning(
                $"[{name}] B側のLinePointが設定されていません。");
            return false;
        }

        LinePoint pointToDelete = pointB;

        // A側と同じLinePointは削除しない
        if (pointToDelete == pointA)
        {
            Debug.LogWarning(
                $"[{name}] A側とB側が同じLinePointのため削除できません。");
            return false;
        }

        // 他のBasicLineやDrawnWireが使っている場合は削除しない
        TrackEdge[] edges =
            FindObjectsByType<TrackEdge>(FindObjectsSortMode.None);

        foreach (TrackEdge edge in edges)
        {
            if (edge == null || edge == this)
                continue;

            if (edge.pointA == pointToDelete ||
                edge.pointB == pointToDelete)
            {
                Debug.LogWarning(
                    $"[{name}] B側のLinePointは他の線 " +
                    $"[{edge.name}] と接続されているため削除しません。");

                return false;
            }
        }

        // このBasicLineの接続登録を解除
        UnregisterPoints();
        registered = false;

        // B側だけ参照を解除する
        pointB = null;

        // 線の登録をやり直し、A側の接続を維持する
        RegisterLine();

        // B側のLinePointだけ削除
        Destroy(pointToDelete.gameObject);

        // BasicLineは残すが、B側がないため描画を無効化
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer != null)
            lineRenderer.enabled = false;

        Debug.Log(
            $"[{name}] B側のLinePointだけを削除しました。");

        return true;
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

        // 分割前の始点と終点を保存
        LinePoint oldStart = pointA;
        LinePoint oldEnd = pointB;

        // 分割位置を基本線上の割合で保存
        Vector3 originalDirection =
            oldEnd.transform.position - oldStart.transform.position;

        float originalLengthSquared = originalDirection.sqrMagnitude;

        float splitT = originalLengthSquared > 0.0001f
            ? Vector3.Dot(
                closest - oldStart.transform.position,
                originalDirection) / originalLengthSquared
            : 0f;

        splitT = Mathf.Clamp01(splitT);

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

        // =====================================================
        // 分割前の基本線を移動していたノードへ通知
        // 分割後の正しい線と移動先に更新する
        // =====================================================
        NodeController[] nodes =
            FindObjectsByType<NodeController>(
                FindObjectsSortMode.None);

        foreach (NodeController node in nodes)
        {
            if (node == null)
                continue;

            node.HandleEdgeSplit(
                this,
                branchPoint,
                oldStart,
                oldEnd,
                this,
                continuation,
                splitT);
        }

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

    // =====================================================
    // 実行中に線を登録する
    // =====================================================
    public void RegisterPointsForRuntime()
    {
        if (registered)
        {
            UpdateLine();
            return;
        }

        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        RegisterPoints();
        registered = true;

        UpdateLine();
    }

    [Header("出現演出")]
    [Tooltip("透明から元の色に戻るまでの時間")]
    [Min(0.01f)]
    public float appearDuration = 0.2f;

    private Coroutine appearCoroutine;

    /// <summary>
    /// 基本線を透明から元の色へフェードインさせる
    /// </summary>
    public void PlayAppearEffect()
    {
        if (hasAppeared)
            return;

        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer == null)
            return;

        hasAppeared = true;
        lineRenderer.enabled = true;

        if (appearCoroutine != null)
            StopCoroutine(appearCoroutine);

        appearCoroutine = StartCoroutine(AppearCoroutine());
    }

    private System.Collections.IEnumerator AppearCoroutine()
    {
        Color originalStart = lineRenderer.startColor;
        Color originalEnd = lineRenderer.endColor;

        Color transparentStart = originalStart;
        Color transparentEnd = originalEnd;

        transparentStart.a = 0f;
        transparentEnd.a = 0f;

        lineRenderer.startColor = transparentStart;
        lineRenderer.endColor = transparentEnd;

        float elapsed = 0f;

        while (elapsed < appearDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / appearDuration);

            Color startColor = originalStart;
            Color endColor = originalEnd;

            startColor.a = Mathf.Lerp(0f, originalStart.a, t);
            endColor.a = Mathf.Lerp(0f, originalEnd.a, t);

            lineRenderer.startColor = startColor;
            lineRenderer.endColor = endColor;

            yield return null;
        }

        lineRenderer.startColor = originalStart;
        lineRenderer.endColor = originalEnd;

        appearCoroutine = null;
    }

    public void SetLineVisible(bool visible)
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer != null)
            lineRenderer.enabled = visible;
    }
    // =====================================================
    // 基本線のON/OFF
    // ON：表示してノードが通れるようにする
    // OFF：非表示にしてノードが通れないようにする
    // =====================================================
    public void SetLineState(bool active)
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (active)
        {
            // 有効化
            enabled = true;

            // Start()で意図せず非表示にならないようにする
            showOnStart = true;

            // LinePointへの登録を保証する
            RegisterPointsForRuntime();

            if (lineRenderer != null)
            {
                lineRenderer.enabled = true;
                UpdateLine();
            }

            Debug.Log($"[基本線ON] {lineName} / 移動可能");
        }
        else
        {
            // LinePointから接続登録を解除する
            UnregisterPoints();
            registered = false;

            // 非表示
            if (lineRenderer != null)
                lineRenderer.enabled = false;

            // ノードの移動対象から外す
            enabled = false;

            Debug.Log($"[基本線OFF] {lineName} / 移動不可");
        }
    }
}
