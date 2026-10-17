using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class LinePoint : MonoBehaviour
{
    [Header("回収点設定LinePoint ")]
    public bool isRecoveryPoint;
    public RecoveryPoint recoveryPoint;

    [Header("分岐点設定")]
    public bool isBranchPoint;

    [Header("造線の設定")]
    [Tooltip("チェックすると、この地点から造線を引けなくなる")]
    public bool preventWireDrawing = false;

    private readonly List<TrackEdge> connectedEdges =
        new List<TrackEdge>();

    public IReadOnlyList<TrackEdge> ConnectedEdges => connectedEdges;

    public void RegisterEdge(TrackEdge edge)
    {
        if (edge != null && !connectedEdges.Contains(edge))
        {
            connectedEdges.Add(edge);
        }
    }

    public void UnregisterEdge(TrackEdge edge)
    {
        if (edge != null)
        {
            connectedEdges.Remove(edge);
        }
    }

    private void OnDrawGizmos()
    {
        if (isRecoveryPoint)
            Gizmos.color = Color.yellow;
        else if (isBranchPoint)
            Gizmos.color = Color.cyan;
        else
            Gizmos.color = Color.white;

        Gizmos.DrawSphere(transform.position, 0.12f);
    }
}