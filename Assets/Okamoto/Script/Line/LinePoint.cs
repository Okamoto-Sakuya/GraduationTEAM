using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class LinePoint : MonoBehaviour
{
    [Header("âÒé˚ì_ê›íË")]
    public bool isRecoveryPoint;
    public RecoveryPoint recoveryPoint;

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
        Gizmos.color = isRecoveryPoint ? Color.yellow : Color.white;
        Gizmos.DrawSphere(transform.position, 0.12f);
    }
}