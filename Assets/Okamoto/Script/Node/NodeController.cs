using System.Collections.Generic;
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

    [Header("移動速度")]
    [Min(0.01f)]
    public float moveSpeed = 2f;

    [Header("属性変化時間")]
    [Min(0.1f)]
    public float goodToBadTime = 10f;

    [Min(0.1f)]
    public float badToGoodTime = 15f;

    [Header("見た目")]
    public SpriteRenderer spriteRenderer;
    public Color goodColor = Color.blue;
    public Color badColor = Color.red;
    public Color greenColor = Color.green;

    [Tooltip("良性ノードを緑色で表示する")]
    public bool useGreenForGood;

    private TrackEdge currentEdge;
    private LinePoint fromPoint;
    private LinePoint targetPoint;
    private TrackEdge previousEdge;

    private float progress;
    private float stateTimer;
    private bool initialized;
    private bool recovered;

    public void Initialize(TrackEdge startingEdge, NodeType type)
    {
        currentEdge = startingEdge;
        nodeType = type;

        if (currentEdge == null ||
            currentEdge.pointA == null ||
            currentEdge.pointB == null)
        {
            Destroy(gameObject);
            return;
        }

        fromPoint = currentEdge.pointA;
        targetPoint = currentEdge.pointB;
        progress = 0f;
        stateTimer = 0f;
        initialized = true;

        transform.position = fromPoint.transform.position;

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        UpdateAppearance();
    }

    private void Update()
    {
        if (!initialized || recovered) return;

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
            nodeType = NodeType.Bad;
            stateTimer = 0f;
            UpdateAppearance();
        }
        else if (nodeType == NodeType.Bad &&
                 stateTimer >= badToGoodTime)
        {
            nodeType = NodeType.Good;
            stateTimer = 0f;
            UpdateAppearance();
        }
    }

    private void UpdateAppearance()
    {
        if (spriteRenderer == null) return;

        if (nodeType == NodeType.Bad)
        {
            spriteRenderer.color = badColor;
        }
        else
        {
            spriteRenderer.color =
                useGreenForGood ? greenColor : goodColor;
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

        float distance = Vector3.Distance(
            fromPoint.transform.position,
            targetPoint.transform.position);

        if (distance <= 0.001f)
        {
            ArriveAtPoint();
            return;
        }

        progress += moveSpeed * Time.deltaTime / distance;
        progress = Mathf.Clamp01(progress);

        transform.position = Vector3.Lerp(
            fromPoint.transform.position,
            targetPoint.transform.position,
            progress);

        if (progress >= 1f)
        {
            ArriveAtPoint();
        }
    }

    private void ArriveAtPoint()
    {
        transform.position = targetPoint.transform.position;

        // 四肢の回収点に到着
        if (targetPoint.isRecoveryPoint &&
            targetPoint.recoveryPoint != null)
        {
            targetPoint.recoveryPoint.Recover(this);
            return;
        }

        ChooseNextEdge();
    }

    private void ChooseNextEdge()
    {
        LinePoint arrivedPoint = targetPoint;
        TrackEdge nextEdge = null;

        IReadOnlyList<TrackEdge> edges =
            arrivedPoint.ConnectedEdges;

        // 造線を優先する
        foreach (TrackEdge edge in edges)
        {
            if (edge == null ||
                edge == currentEdge ||
                edge == previousEdge ||
                !(edge is DrawnWire))
            {
                continue;
            }

            if (edge.CanDepartFrom(arrivedPoint) &&
                edge.GetOtherPoint(arrivedPoint) != null)
            {
                nextEdge = edge;
                break;
            }
        }

        // 造線がなければ基本線の進行方向へ
        if (nextEdge == null)
        {
            foreach (TrackEdge edge in edges)
            {
                if (edge == null ||
                    edge == currentEdge ||
                    edge == previousEdge ||
                    !(edge is BasicLine))
                {
                    continue;
                }

                if (edge.CanDepartFrom(arrivedPoint) &&
                    edge.GetOtherPoint(arrivedPoint) != null)
                {
                    nextEdge = edge;
                    break;
                }
            }
        }

        if (nextEdge == null)
        {
            // 行き先がない場合は停止する
            return;
        }

        previousEdge = currentEdge;
        currentEdge = nextEdge;
        fromPoint = arrivedPoint;
        targetPoint = nextEdge.GetOtherPoint(arrivedPoint);
        progress = 0f;
    }

    public void SetType(NodeType newType)
    {
        nodeType = newType;
        stateTimer = 0f;
        UpdateAppearance();
    }

    public void MarkRecovered()
    {
        if (recovered) return;

        recovered = true;

        if (currentEdge != null &&
            currentEdge is BasicLine basicLine)
        {
            // 出現確率の更新はSpawner側で管理する
        }

        Destroy(gameObject);
    }
}