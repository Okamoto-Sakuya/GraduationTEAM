using UnityEngine;

public class DrawnWire : TrackEdge
{
    private bool initialized;

    public override bool CanDepartFrom(LinePoint point)
    {
        return initialized &&
               (point == pointA || point == pointB);
    }

    public void Initialize(
        LinePoint start,
        LinePoint end,
        Material material,
        float width,
        Color color)
    {
        pointA = start;
        pointB = end;

        lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;

        if (material != null)
        {
            lineRenderer.material = material;
        }

        initialized = true;
        RegisterPoints();
    }

    private void OnDestroy()
    {
        UnregisterPoints();
    }
}