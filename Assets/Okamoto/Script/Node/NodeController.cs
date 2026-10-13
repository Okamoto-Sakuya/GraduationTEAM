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

    private TrackEdge currentEdge;
    private LinePoint fromPoint;
    private LinePoint targetPoint;
    private TrackEdge previousEdge;

    private float progress;
    private float stateTimer;

    private bool initialized;
    private bool recovered;

    // 造線の警告表示用。通常時は1。
    private float wireWarningAlpha = 1f;

    public void Initialize(TrackEdge startingEdge, NodeType type)
    {
        if (startingEdge == null ||
            startingEdge.pointA == null ||
            startingEdge.pointB == null)
        {
            Debug.LogWarning("Nodeの開始線または接続点が設定されていません。");
            return;
        }

        currentEdge = startingEdge;
        fromPoint = startingEdge.pointA;
        targetPoint = startingEdge.pointB;

        nodeType = type;
        progress = 0f;
        stateTimer = 0f;
        initialized = true;
        recovered = false;
        wireWarningAlpha = 1f;

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

    private void ChooseNextEdge(LinePoint arrivedPoint)
    {
        if (arrivedPoint == null)
            return;

        TrackEdge nextEdge = null;
        LinePoint nextPoint = null;

        // 優先1：接続可能な造線
        foreach (TrackEdge edge in arrivedPoint.ConnectedEdges)
        {
            if (edge == null ||
                edge == currentEdge ||
                edge == previousEdge ||
                edge is not DrawnWire)
            {
                continue;
            }

            if (!edge.CanDepartFrom(arrivedPoint) ||
                !edge.CanEnter())
            {
                continue;
            }

            LinePoint other = edge.GetOtherPoint(arrivedPoint);

            if (other == null)
                continue;

            nextEdge = edge;
            nextPoint = other;
            break;
        }

        // 優先2：基本線
        // 造線が使えなければ、基本線の続きへ進む
        if (nextEdge == null)
        {
            foreach (TrackEdge edge in arrivedPoint.ConnectedEdges)
            {
                if (edge == null ||
                    edge == currentEdge ||
                    edge == previousEdge)
                {
                    continue;
                }

                if (!(edge is BasicLine))
                    continue;

                if (!edge.CanDepartFrom(arrivedPoint) ||
                    !edge.CanEnter())
                {
                    continue;
                }

                LinePoint other = edge.GetOtherPoint(arrivedPoint);

                if (other == null)
                    continue;

                nextEdge = edge;
                nextPoint = other;
                break;
            }
        }

        if (nextEdge == null || nextPoint == null)
        {
            // 移動先がない場合は現在位置で停止
            progress = 0f;
            return;
        }

        previousEdge = currentEdge;
        currentEdge = nextEdge;
        fromPoint = arrivedPoint;
        targetPoint = nextPoint;
        progress = 0f;

        // 造線に入ったタイミングで通過回数を加算
        if (currentEdge is DrawnWire drawnWire)
        {
            wireWarningAlpha = 1f;
            drawnWire.RegisterPass();
        }
        else
        {
            wireWarningAlpha = 1f;
        }

        UpdateAppearance();
    }

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
            color = useGreenForGood ? greenGoodColor : goodColor;
        }
        else
        {
            color = badColor;
        }

        color.a *= wireWarningAlpha;
        spriteRenderer.color = color;
    }

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

    public void MarkRecovered()
    {
        recovered = true;
        initialized = false;

        Destroy(gameObject);
    }
}