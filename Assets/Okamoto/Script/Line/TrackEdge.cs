using UnityEngine;

public abstract class TrackEdge : MonoBehaviour
{
    [Header("線の接続先")]
    public LinePoint pointA;
    public LinePoint pointB;

    [Header("線の表示")]
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
    // =====================================================
    // 接続情報を解除する
    // =====================================================
    public void UnregisterFromPoints()
    {
        UnregisterPoints();
    }
    // =====================================================
    // BasicLineを有効化して接続を登録
    // =====================================================
    public void ActivateLine()
    {
        if (pointA == null || pointB == null)
        {
            Debug.LogWarning(
                $"{name}: pointA または pointB が設定されていません。",
                this
            );
            return;
        }

        enabled = true;

        if (this is BasicLine basicLine)
        {
            basicLine.RegisterPointsForRuntime();
        }
        else
        {
            RegisterPoints();
        }

        // 線を表示
        if (lineRenderer != null)
        {
            lineRenderer.enabled = true;
            UpdateLine();
        }
    }

    // =====================================================
    // 2本のBasicLineを同時に有効化
    // =====================================================
    public static void ActivateTwoBasicLines(
        BasicLine line1,
        BasicLine line2)
    {
        if (line1 != null)
        {
            line1.ActivateLine();
        }

        if (line2 != null)
        {
            line2.ActivateLine();
        }
    }
}