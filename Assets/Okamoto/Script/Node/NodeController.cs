
using UnityEngine;

public class NodeController : MonoBehaviour
{
    public enum NodeType
    {
        Good,
        Bad
    }

    [Header("ノード設定")]
    public NodeType nodeType = NodeType.Good;
    public float moveSpeed = 2f;

    [Header("種類変更時間")]
    public float goodToBadTime = 10f;
    public float badToGoodTime = 15f;

    [Header("見た目")]
    public SpriteRenderer spriteRenderer;
    public Color goodColor = Color.blue;
    public Color badColor = Color.red;
    public Color greenGoodColor = Color.green;
    public bool useGreenForGood;

    [Header("ルート選択")]
    [Tooltip("接続されている造線を優先して進む")]
    public bool prioritizeDrawnWire = true;

    private TrackEdge currentEdge;
    private LinePoint fromPoint;
    private LinePoint targetPoint;
    private TrackEdge previousEdge;

    private float progress;
    private float stateTimer;

    private bool initialized;
    private bool recovered;

    private float wireWarningAlpha = 1f;

    public void Initialize(TrackEdge startingEdge, NodeType type)
    {
        if (startingEdge == null ||
            startingEdge.pointA == null ||
            startingEdge.pointB == null)
        {
            Debug.LogWarning(
                "NodeController: 開始線または接続点が設定されていません。",
                this);
            return;
        }

        currentEdge = startingEdge;
        previousEdge = null;

        fromPoint = startingEdge.pointA;
        targetPoint = startingEdge.pointB;

        nodeType = type;
        progress = 0f;
        stateTimer = 0f;
        wireWarningAlpha = 1f;

        initialized = true;
        recovered = false;

        transform.position = fromPoint.transform.position;

        UpdateAppearance();
    }

    private void Update()
    {
        if (!initialized || recovered)
            return;

        if (GameManager.Instance != null &&
            GameManager.Instance.IsGameOver)
        {
            return;
        }

        UpdateNodeType();
        MoveAlongEdge();
    }

    // =====================================================
    // ノードの種類変更
    // =====================================================

    private void UpdateNodeType()
    {
        stateTimer += Time.deltaTime;

        if (nodeType == NodeType.Good &&
            stateTimer >= goodToBadTime)
        {
            SetType(NodeType.Bad);
        }
        else if (nodeType == NodeType.Bad &&
                 stateTimer >= badToGoodTime)
        {
            SetType(NodeType.Good);
        }
    }

    // =====================================================
    // 現在の線に沿って移動
    // =====================================================

    private void MoveAlongEdge()
    {
        if (currentEdge == null ||
            fromPoint == null ||
            targetPoint == null)
        {
            return;
        }

        Vector3 start = fromPoint.transform.position;
        Vector3 end = targetPoint.transform.position;

        float distance = Vector3.Distance(start, end);

        if (distance <= 0.001f)
        {
            ArriveAtPoint();
            return;
        }

        progress += moveSpeed * Time.deltaTime / distance;
        progress = Mathf.Clamp01(progress);

        transform.position = Vector3.Lerp(start, end, progress);

        if (progress >= 1f)
            ArriveAtPoint();
    }

    // =====================================================
    // 接続点に到着
    // =====================================================

    private void ArriveAtPoint()
    {
        if (targetPoint == null)
            return;

        transform.position = targetPoint.transform.position;

        // 回収点を最優先
        if (targetPoint.isRecoveryPoint)
        {
            RecoveryPoint recovery = targetPoint.recoveryPoint;

            if (recovery == null)
                recovery = targetPoint.GetComponent<RecoveryPoint>();

            if (recovery != null)
            {
                recovery.Recover(this);
                return;
            }
        }

        ChooseNextEdge(targetPoint);
    }

    // =====================================================
    // 次に進む線を選択
    // 造線 → 基本線の順で優先する
    // =====================================================

    private void ChooseNextEdge(LinePoint arrivedPoint)
    {
        if (arrivedPoint == null)
            return;

        TrackEdge nextEdge = null;
        LinePoint nextPoint = null;

        // -------------------------------------------------
        // 優先1：接続されている造線
        // -------------------------------------------------

        if (prioritizeDrawnWire)
        {
            foreach (TrackEdge edge in arrivedPoint.ConnectedEdges)
            {
                if (!(edge is DrawnWire))
                    continue;

                if (!CanUseEdge(edge, arrivedPoint))
                    continue;

                LinePoint otherPoint = edge.GetOtherPoint(arrivedPoint);

                if (otherPoint == null)
                    continue;

                nextEdge = edge;
                nextPoint = otherPoint;
                break;
            }
        }

        // -------------------------------------------------
        // 優先2：基本線
        // -------------------------------------------------

        if (nextEdge == null)
        {
            foreach (TrackEdge edge in arrivedPoint.ConnectedEdges)
            {
                if (!(edge is BasicLine))
                    continue;

                if (!CanUseEdge(edge, arrivedPoint))
                    continue;

                LinePoint otherPoint = edge.GetOtherPoint(arrivedPoint);

                if (otherPoint == null)
                    continue;

                nextEdge = edge;
                nextPoint = otherPoint;
                break;
            }
        }

        // -------------------------------------------------
        // 移動先がない場合
        // -------------------------------------------------

        if (nextEdge == null || nextPoint == null)
        {
            Debug.LogWarning(
                "ノードの移動先がありません: " +
                arrivedPoint.name,
                arrivedPoint);

            return;
        }

        // -------------------------------------------------
        // 選択した線へ移動
        // -------------------------------------------------

        previousEdge = currentEdge;
        currentEdge = nextEdge;

        fromPoint = arrivedPoint;
        targetPoint = nextPoint;

        progress = 0f;

        if (currentEdge is DrawnWire drawnWire)
        {
            wireWarningAlpha = 1f;

            // 造線へ入ったタイミングで通過回数を加算
            drawnWire.RegisterPass();
        }
        else
        {
            wireWarningAlpha = 1f;
        }

        UpdateAppearance();
    }

    // =====================================================
    // 線が利用できるか確認
    // =====================================================

    private bool CanUseEdge(
        TrackEdge edge,
        LinePoint arrivedPoint)
    {
        if (edge == null)
            return false;

        // 今いる線へそのまま戻ることはしない
        if (edge == currentEdge)
            return false;

        // 消滅待ちの造線には入らない
        if (!edge.CanEnter())
            return false;

        // 基本線はA→B方向のみ
        // 造線は両端から進める
        if (!edge.CanDepartFrom(arrivedPoint))
            return false;

        if (edge.GetOtherPoint(arrivedPoint) == null)
            return false;

        return true;
    }

    // =====================================================
    // ノードの種類・色
    // =====================================================

    public void SetType(NodeType newType)
    {
        nodeType = newType;
        stateTimer = 0f;

        UpdateAppearance();
    }

    private void UpdateAppearance()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
            return;

        Color color;

        if (nodeType == NodeType.Good)
        {
            color = useGreenForGood
                ? greenGoodColor
                : goodColor;
        }
        else
        {
            color = badColor;
        }

        color.a *= wireWarningAlpha;
        spriteRenderer.color = color;
    }

    // =====================================================
    // 造線の消滅処理から参照
    // =====================================================

    public bool IsOnEdge(TrackEdge edge)
    {
        return initialized &&
               !recovered &&
               currentEdge == edge;
    }

    public void SetWireWarningAlpha(float alpha)
    {
        wireWarningAlpha = Mathf.Clamp01(alpha);
        UpdateAppearance();
    }

    // =====================================================
    // 回収処理
    // =====================================================

    public void MarkRecovered()
    {
        recovered = true;
        initialized = false;

        Destroy(gameObject);
    }
}
