using UnityEngine;

public abstract class TrackEdge : MonoBehaviour
{
    [Header("ê¸ÇÃê⁄ë±êÊ")]
    public LinePoint pointA;
    public LinePoint pointB;

    [Header("ê¸ÇÃï\é¶")]
    public LineRenderer lineRenderer;

    public abstract bool CanDepartFrom(LinePoint point);

    public virtual bool CanEnter()
    {
        return true;
    }

    public virtual LinePoint GetOtherPoint(LinePoint point)
    {
        if (point == pointA) return pointB;
        if (point == pointB) return pointA;

        return null;
    }

    protected void RegisterPoints()
    {
        if (pointA != null) pointA.RegisterEdge(this);
        if (pointB != null) pointB.RegisterEdge(this);

        UpdateLine();
    }

    protected void UnregisterPoints()
    {
        if (pointA != null) pointA.UnregisterEdge(this);
        if (pointB != null) pointB.UnregisterEdge(this);
    }

    public void UpdateLine()
    {
        if (lineRenderer == null ||
            pointA == null ||
            pointB == null)
        {
            return;
        }

        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;

        lineRenderer.SetPosition(0, pointA.transform.position);
        lineRenderer.SetPosition(1, pointB.transform.position);
    }
}