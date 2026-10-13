using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class WireDrawingController : MonoBehaviour
{
    [Header("カメラ・レイヤー")]
    public Camera targetCamera;
    public LayerMask linePointLayer = ~0;

    [Header("造線設定")]
    public Material wireMaterial;
    public float wireWidth = 0.08f;
    public Color wireColor = Color.white;

    [Header("ゲージ設定")]
    public float maxDrawingGauge = 100f;
    public float gaugeCostPerMeter = 1f;

    [Header("プレビュー設定")]
    public float previewWidth = 0.08f;
    public Color previewColor = Color.cyan;
    public float pointSearchDistance = 0.2f;
    public float lineSearchDistance = 0.25f;

    [Header("警告設定")]
    public float warningSeconds = 3f;

    private float remainingGauge;
    private bool isDrawing;
    private LinePoint startPoint;
    private LineRenderer previewLine;

    private void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        remainingGauge = maxDrawingGauge;

        CreatePreviewLine();
        UpdateGaugeUI();
    }

    private void Update()
    {
        if (Mouse.current == null || targetCamera == null)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector3 mouseWorld = GetMouseWorldPosition();

            LinePoint point = GetOrCreatePointAt(mouseWorld);

            if (point != null && remainingGauge > 0f)
            {
                startPoint = point;
                isDrawing = true;

                previewLine.enabled = true;
                UpdatePreview(mouseWorld);
            }
        }

        if (isDrawing && Mouse.current.leftButton.isPressed)
        {
            UpdatePreview(GetMouseWorldPosition());
        }

        if (isDrawing && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            Vector3 mouseWorld = GetMouseWorldPosition();
            LinePoint endPoint = GetOrCreatePointAt(mouseWorld);

            if (endPoint != null && endPoint != startPoint)
                TryCreateWire(startPoint, endPoint);

            CancelDrawing();
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector2 screenPosition = Mouse.current.position.ReadValue();

        Vector3 screenPoint = new Vector3(
            screenPosition.x,
            screenPosition.y,
            Mathf.Abs(targetCamera.transform.position.z));

        Vector3 world = targetCamera.ScreenToWorldPoint(screenPoint);
        world.z = 0f;

        return world;
    }

    private void CreatePreviewLine()
    {
        GameObject previewObject = new GameObject("WirePreview");

        previewLine = previewObject.AddComponent<LineRenderer>();
        previewLine.useWorldSpace = true;
        previewLine.positionCount = 2;
        previewLine.startWidth = previewWidth;
        previewLine.endWidth = previewWidth;
        previewLine.startColor = previewColor;
        previewLine.endColor = previewColor;

        if (wireMaterial != null)
            previewLine.material = wireMaterial;

        previewLine.enabled = false;
    }

    private void UpdatePreview(Vector3 mouseWorld)
    {
        if (previewLine == null || startPoint == null)
            return;

        previewLine.SetPosition(0, startPoint.transform.position);
        previewLine.SetPosition(1, mouseWorld);
    }

    private void CancelDrawing()
    {
        isDrawing = false;
        startPoint = null;

        if (previewLine != null)
            previewLine.enabled = false;
    }

    /// <summary>
    /// クリック位置にあるLinePointを取得する。
    /// なければ、基本線の途中かどうかを調べて分岐点を作る。
    /// </summary>
    private LinePoint GetOrCreatePointAt(Vector3 worldPosition)
    {
        Collider2D[] colliders = Physics2D.OverlapPointAll(
            worldPosition, linePointLayer);

        // 回収点が重なっている場合は回収点を優先
        foreach (Collider2D col in colliders)
        {
            if (col == null) continue;

            LinePoint point = col.GetComponent<LinePoint>();

            if (point != null && point.isRecoveryPoint)
                return point;
        }

        // 既存の端点を探す
        LinePoint nearestPoint = null;
        float nearestDistance = pointSearchDistance;

        foreach (Collider2D col in colliders)
        {
            if (col == null) continue;

            LinePoint point = col.GetComponent<LinePoint>();

            if (point == null) continue;

            float distance = Vector2.Distance(
                worldPosition, point.transform.position);

            if (distance <= nearestDistance)
            {
                nearestDistance = distance;
                nearestPoint = point;
            }
        }

        if (nearestPoint != null)
            return nearestPoint;

        // 基本線の途中を探す
        BasicLine[] lines = FindObjectsByType<BasicLine>(
            FindObjectsSortMode.None);

        BasicLine closestLine = null;
        Vector3 closestPosition = Vector3.zero;
        float closestLineDistance = lineSearchDistance;

        foreach (BasicLine line in lines)
        {
            if (line == null ||
                line.pointA == null ||
                line.pointB == null)
            {
                continue;
            }

            Vector3 closest = BasicLine.GetClosestPointOnSegment(
                worldPosition,
                line.pointA.transform.position,
                line.pointB.transform.position);

            float distance = Vector2.Distance(
                worldPosition, closest);

            if (distance <= closestLineDistance)
            {
                closestLineDistance = distance;
                closestLine = line;
                closestPosition = closest;
            }
        }

        if (closestLine != null)
        {
            return closestLine.SplitAt(closestPosition);
        }

        // 造線上の途中は、新しい分岐点にする処理を
        // 今後追加できるよう、ここでは未対応とする。
        return null;
    }

    private void TryCreateWire(LinePoint start, LinePoint end)
    {
        if (start == null || end == null || start == end)
            return;

        float length = Vector2.Distance(
            start.transform.position,
            end.transform.position);

        float cost = length * gaugeCostPerMeter;

        if (cost > remainingGauge)
            return;

        if (CrossesExistingLine(start, end))
            return;

        GameObject wireObject = new GameObject("DrawnWire");

        DrawnWire wire = wireObject.AddComponent<DrawnWire>();

        wire.lifetimeSeconds = 15f;
        wire.warningSeconds = warningSeconds;

        wire.Initialize(
            start,
            end,
            wireMaterial,
            wireWidth,
            wireColor,
            this,
            cost);

        remainingGauge -= cost;
        remainingGauge = Mathf.Max(0f, remainingGauge);

        UpdateGaugeUI();
    }

    /// <summary>
    /// 既存の線を横切る造線は拒否する。
    /// 始点・終点として接続する線は許可する。
    /// </summary>
    private bool CrossesExistingLine(
        LinePoint start,
        LinePoint end)
    {
        TrackEdge[] edges = FindObjectsByType<TrackEdge>(
            FindObjectsSortMode.None);

        Vector2 a = start.transform.position;
        Vector2 b = end.transform.position;

        foreach (TrackEdge edge in edges)
        {
            if (edge == null ||
                edge.pointA == null ||
                edge.pointB == null)
            {
                continue;
            }

            if (edge is DrawnWire drawn &&
                drawn.State == DrawnWire.WireState.Removed)
            {
                continue;
            }

            // 端点を共有する線は接続として許可
            if (edge.pointA == start || edge.pointB == start ||
                edge.pointA == end || edge.pointB == end)
            {
                continue;
            }

            Vector2 c = edge.pointA.transform.position;
            Vector2 d = edge.pointB.transform.position;

            if (SegmentsIntersect(a, b, c, d))
                return true;
        }

        return false;
    }

    private bool SegmentsIntersect(
        Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        return Mathf.Sign(Cross(b - a, c - a)) !=
               Mathf.Sign(Cross(b - a, d - a)) &&
               Mathf.Sign(Cross(d - c, a - c)) !=
               Mathf.Sign(Cross(d - c, b - c));
    }

    private float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    public void RefundGauge(float amount)
    {
        remainingGauge = Mathf.Clamp(
            remainingGauge + amount, 0f, maxDrawingGauge);

        UpdateGaugeUI();
    }

    public float GetRemainingDrawingTime()
    {
        return remainingGauge;
    }

    public bool IsDrawingDisabled()
    {
        return remainingGauge <= 0f;
    }

    private void UpdateGaugeUI()
    {
        if (UIManager.Instance != null)
        {
            float normalized = maxDrawingGauge > 0f
                ? remainingGauge / maxDrawingGauge
                : 0f;

            UIManager.Instance.UpdateWireGauge(normalized);
        }
    }
}