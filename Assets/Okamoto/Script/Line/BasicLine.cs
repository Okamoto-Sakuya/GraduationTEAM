
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class BasicLine : MonoBehaviour
{
    [Header("基本線の始点・終点")]
    public Transform startPoint;
    public Transform endPoint;

    [Header("基本線の設定")]
    public bool isActive = true;

    [Header("曲線設定")]
    [Tooltip("曲線の曲がり具合")]
    [SerializeField] private float curveAmount = 0.5f;

    [Tooltip("曲線を描画する分割数。大きいほど滑らか")]
    [SerializeField] private int curveSegments = 30;

    [Header("線の表示設定")]
    [SerializeField] private float lineWidth = 0.05f;
    [SerializeField] private Color lineColor = Color.white;
    [SerializeField] private Material lineMaterial;
    [SerializeField] private int sortingOrder = 5;

    private LineRenderer lineRenderer;

    // 描画した曲線の座標を保存する
    private readonly List<Vector2> curvePoints = new List<Vector2>();

    public Vector2 StartPosition
    {
        get { return startPoint.position; }
    }

    public Vector2 EndPosition
    {
        get { return endPoint.position; }
    }

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();

        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;

        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;

        lineRenderer.sortingOrder = sortingOrder;

        if (lineMaterial != null)
        {
            lineRenderer.material = lineMaterial;
        }
        else
        {
            lineRenderer.material = new Material(
                Shader.Find("Sprites/Default")
            );
        }

        lineRenderer.numCapVertices = 8;
        lineRenderer.numCornerVertices = 8;
    }

    private void Start()
    {
        UpdateLine();
    }

    private void Update()
    {
        UpdateLine();
    }

    // 曲線を描画し、判定用の座標も更新する
    private void UpdateLine()
    {
        if (startPoint == null || endPoint == null)
        {
            lineRenderer.enabled = false;
            curvePoints.Clear();
            return;
        }

        lineRenderer.enabled = isActive;

        if (!isActive)
        {
            curvePoints.Clear();
            return;
        }

        int segments = Mathf.Max(2, curveSegments);

        Vector2 start = startPoint.position;
        Vector2 end = endPoint.position;

        Vector2 direction = end - start;

        // 始点と終点を結ぶ方向に対して垂直な向き
        Vector2 perpendicular =
            new Vector2(-direction.y, direction.x).normalized;

        // 曲線の制御点
        Vector2 controlPoint =
            (start + end) * 0.5f
            + perpendicular * curveAmount;

        curvePoints.Clear();

        lineRenderer.positionCount = segments + 1;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;

            // 二次ベジェ曲線
            Vector2 point =
                (1f - t) * (1f - t) * start
                + 2f * (1f - t) * t * controlPoint
                + t * t * end;

            // 描画と判定で同じ座標を使用する
            curvePoints.Add(point);

            lineRenderer.SetPosition(i, point);
        }
    }

    // 曲がった線上で最も近い位置を取得
    public Vector2 GetClosestPoint(Vector2 position)
    {
        if (curvePoints.Count == 0)
        {
            return startPoint != null
                ? (Vector2)startPoint.position
                : position;
        }

        Vector2 closestPoint = curvePoints[0];
        float closestDistanceSquared = float.MaxValue;

        // 曲線を構成する各区間を調べる
        for (int i = 0; i < curvePoints.Count - 1; i++)
        {
            Vector2 a = curvePoints[i];
            Vector2 b = curvePoints[i + 1];

            Vector2 segment = b - a;
            float lengthSquared = segment.sqrMagnitude;

            float t = 0f;

            if (lengthSquared > 0.0001f)
            {
                t = Mathf.Clamp01(
                    Vector2.Dot(position - a, segment)
                    / lengthSquared
                );
            }

            Vector2 pointOnSegment = a + segment * t;

            float distanceSquared =
                (position - pointOnSegment).sqrMagnitude;

            if (distanceSquared < closestDistanceSquared)
            {
                closestDistanceSquared = distanceSquared;
                closestPoint = pointOnSegment;
            }
        }

        return closestPoint;
    }

    // 曲がった線までの最短距離を取得
    public float GetDistance(Vector2 position)
    {
        return Vector2.Distance(
            position,
            GetClosestPoint(position)
        );
    }

    // 曲線上にあるかどうかを距離で判定
    public bool IsPointOnLine(Vector2 position, float tolerance)
    {
        return isActive && GetDistance(position) <= tolerance;
    }
    // 進行度（0～1）に対応する曲線上の位置を取得
    public Vector2 GetPointOnCurve(float t)
    {
        if (startPoint == null || endPoint == null)
        {
            return transform.position;
        }

        t = Mathf.Clamp01(t);

        Vector2 start = startPoint.position;
        Vector2 end = endPoint.position;

        Vector2 direction = end - start;
        Vector2 perpendicular =
            new Vector2(-direction.y, direction.x).normalized;

        Vector2 controlPoint =
            (start + end) * 0.5f + perpendicular * curveAmount;

        return
            (1f - t) * (1f - t) * start +
            2f * (1f - t) * t * controlPoint +
            t * t * end;
    }

    // 曲線上の位置を取得するための移動距離を概算
    public float GetCurveLength()
    {
        if (startPoint == null || endPoint == null)
        {
            return 0f;
        }

        float length = 0f;
        Vector2 previous = GetPointOnCurve(0f);

        int segments = Mathf.Max(2, curveSegments);

        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector2 current = GetPointOnCurve(t);

            length += Vector2.Distance(previous, current);
            previous = current;
        }

        return length;
    }
    // 曲線を折れ線として取得(経路・交差判定用)
    public List<Vector2> GetPolyline(int samples)
    {
        samples = Mathf.Max(2, samples);
        var list = new List<Vector2>(samples + 1);
        for (int i = 0; i <= samples; i++)
        {
            list.Add(GetPointOnCurve((float)i / samples));
        }
        return list;
    }
}