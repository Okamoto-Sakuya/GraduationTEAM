using UnityEngine;

public class WireDrawingController : MonoBehaviour
{
    [Header("接続点の検索")]
    public Camera targetCamera;
    public LayerMask linePointLayer = ~0;

    [Header("造線設定")]
    public Material wireMaterial;
    [Min(0.01f)]
    public float wireWidth = 0.08f;
    public Color wireColor = Color.white;

    [Header("造線ゲージ")]
    [Min(0.1f)]
    public float maxDrawingTime = 10f;

    [Tooltip("1秒間に消費するゲージ量。1秒間の描画でこの値だけ減少")]
    [Min(0.01f)]
    public float gaugeDrainPerSecond = 1f;

    [Header("プレビュー線")]
    [Min(0.01f)]
    public float previewWidth = 0.08f;
    public Color previewColor = Color.cyan;

    private float remainingDrawingTime;

    private bool isDrawing;
    private bool drawingDisabled;

    private LinePoint startPoint;
    private LineRenderer previewLine;

    private void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        remainingDrawingTime = maxDrawingTime;

        CreatePreviewLine();
        UpdateGaugeUI();
    }

    private void Update()
    {
        if (GameManager.Instance != null &&
            GameManager.Instance.IsGameOver)
        {
            CancelDrawing();
            return;
        }

        if (targetCamera == null || drawingDisabled)
        {
            return;
        }

        Vector3 mouseWorldPosition = GetMouseWorldPosition();

        // 左クリックで開始
        if (Input.GetMouseButtonDown(0) && !isDrawing)
        {
            LinePoint clickedPoint = GetPointAt(mouseWorldPosition);

            if (clickedPoint != null &&
                remainingDrawingTime > 0f)
            {
                BeginDrawing(clickedPoint);
            }
        }

        if (!isDrawing) return;

        // 描画中だけゲージを消費する
        remainingDrawingTime -=
            Time.deltaTime * gaugeDrainPerSecond;

        remainingDrawingTime = Mathf.Max(
            0f, remainingDrawingTime);

        UpdateGaugeUI();

        if (previewLine != null)
        {
            previewLine.SetPosition(
                0, startPoint.transform.position);

            previewLine.SetPosition(
                1, mouseWorldPosition);
        }

        // ゲージが0になったら描画禁止
        if (remainingDrawingTime <= 0f)
        {
            drawingDisabled = true;
            CancelDrawing();
            return;
        }

        // マウスを離した位置で造線を確定
        if (Input.GetMouseButtonUp(0))
        {
            LinePoint endPoint = GetPointAt(mouseWorldPosition);

            if (endPoint != null && endPoint != startPoint)
            {
                CreateWire(startPoint, endPoint);
            }

            CancelDrawing();
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePosition = Input.mousePosition;
        mousePosition.z = Mathf.Abs(
            targetCamera.transform.position.z);

        Vector3 worldPosition =
            targetCamera.ScreenToWorldPoint(mousePosition);

        worldPosition.z = 0f;

        return worldPosition;
    }

    private LinePoint GetPointAt(Vector3 worldPosition)
    {
        Collider2D hit = Physics2D.OverlapPoint(
            new Vector2(worldPosition.x, worldPosition.y),
            linePointLayer);

        if (hit == null) return null;

        return hit.GetComponent<LinePoint>();
    }

    private void BeginDrawing(LinePoint point)
    {
        startPoint = point;
        isDrawing = true;

        if (previewLine != null)
        {
            previewLine.enabled = true;

            previewLine.SetPosition(
                0, point.transform.position);

            previewLine.SetPosition(
                1, point.transform.position);
        }
    }

    private void CreateWire(LinePoint start, LinePoint end)
    {
        GameObject wireObject = new GameObject(
            $"DrawnWire_{start.name}_{end.name}");

        DrawnWire wire = wireObject.AddComponent<DrawnWire>();

        wire.Initialize(
            start,
            end,
            wireMaterial,
            wireWidth,
            wireColor);
    }

    private void CreatePreviewLine()
    {
        GameObject previewObject =
            new GameObject("WirePreview");

        previewLine =
            previewObject.AddComponent<LineRenderer>();

        previewLine.useWorldSpace = true;
        previewLine.positionCount = 2;
        previewLine.startWidth = previewWidth;
        previewLine.endWidth = previewWidth;
        previewLine.startColor = previewColor;
        previewLine.endColor = previewColor;
        previewLine.enabled = false;

        if (wireMaterial != null)
        {
            previewLine.material = wireMaterial;
        }
    }

    private void CancelDrawing()
    {
        isDrawing = false;
        startPoint = null;

        if (previewLine != null)
        {
            previewLine.enabled = false;
        }
    }

    private void UpdateGaugeUI()
    {
        if (UIManager.Instance == null) return;

        float normalizedValue = maxDrawingTime > 0f
            ? remainingDrawingTime / maxDrawingTime
            : 0f;

        UIManager.Instance.UpdateWireGauge(normalizedValue);
    }

    public float GetRemainingDrawingTime()
    {
        return remainingDrawingTime;
    }

    public bool IsDrawingDisabled()
    {
        return drawingDisabled;
    }
}