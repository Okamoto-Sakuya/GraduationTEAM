using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class FakeCursor : MonoBehaviour
{
    [Header("カーソル設定")]
    [SerializeField] RectTransform cursor;
    [SerializeField] Canvas canvas;

    EventSystem eventSystem;
    PointerEventData pointerData;

    readonly List<RaycastResult> results = new List<RaycastResult>();

    GameObject currentHit;
    GameObject currentHover;

    GameObject pressedTarget;
    GameObject dragTarget;
    GameObject clickTarget;

    RaycastResult currentRaycast;

    Vector2 previousPointerPosition;

    bool isDragging;

    void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        eventSystem = EventSystem.current;

        if (eventSystem == null)
        {
            Debug.LogError("EventSystemが見つかりません。");
            enabled = false;
            return;
        }

        pointerData = new PointerEventData(eventSystem);
    }

    void Update()
    {
        // マウス移動
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        Vector2 moveAmount = new Vector2(mouseX, mouseY) * OptionManager.mouseSens;

        cursor.anchoredPosition += moveAmount;

        // Canvas内に制限
        RectTransform canvasRect = canvas.transform as RectTransform;

        Vector2 halfSize = canvasRect.rect.size * 0.5f;

        Vector2 pos = cursor.anchoredPosition;

        pos.x = Mathf.Clamp(pos.x, -halfSize.x, halfSize.x);
        pos.y = Mathf.Clamp(pos.y, -halfSize.y, halfSize.y);

        cursor.anchoredPosition = pos;

        // UI判定を更新
        CheckUI();

        // 左クリック開始
        if (Input.GetMouseButtonDown(0))
        {
            PressUI();
        }

        // ドラッグ中
        if (Input.GetMouseButton(0) && isDragging)
        {
            DragUI();
        }

        // 左クリック終了
        if (Input.GetMouseButtonUp(0))
        {
            ReleaseUI();
        }
    }

    // UI判定
    void CheckUI()
    {
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;

        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(
            cam,
            cursor.position
        );

        pointerData.position = screenPos;
        pointerData.delta = screenPos - previousPointerPosition;

        previousPointerPosition = screenPos;

        results.Clear();

        eventSystem.RaycastAll(pointerData, results);

        currentHit = null;
        currentRaycast = new RaycastResult();

        foreach (RaycastResult result in results)
        {
            Selectable selectable =
                result.gameObject.GetComponentInParent<Selectable>();

            if (selectable != null && selectable.IsInteractable())
            {
                currentHit = result.gameObject;
                currentRaycast = result;
                break;
            }
        }

        pointerData.pointerCurrentRaycast = currentRaycast;

        // Hover判定
        GameObject newHover = currentHit != null
            ? ExecuteEvents.GetEventHandler<IPointerEnterHandler>(currentHit)
            : null;

        if (currentHover != newHover)
        {
            if (currentHover != null)
            {
                ExecuteEvents.Execute(
                    currentHover,
                    pointerData,
                    ExecuteEvents.pointerExitHandler
                );
            }

            if (newHover != null)
            {
                ExecuteEvents.Execute(
                    newHover,
                    pointerData,
                    ExecuteEvents.pointerEnterHandler
                );
            }

            currentHover = newHover;
        }

        pointerData.pointerEnter = currentHover;
    }

    // UIを押したとき
    void PressUI()
    {
        if (currentHit == null) return;

        pointerData.pressPosition = pointerData.position;
        pointerData.pointerPressRaycast = currentRaycast;
        pointerData.eligibleForClick = true;
        pointerData.dragging = false;

        // PointerDown
        pressedTarget = ExecuteEvents.ExecuteHierarchy(
            currentHit,
            pointerData,
            ExecuteEvents.pointerDownHandler
        );

        pointerData.pointerPress = pressedTarget;
        pointerData.rawPointerPress = currentHit;

        // ドラッグ対象を取得
        dragTarget = ExecuteEvents.GetEventHandler<IDragHandler>(
            currentHit
        );

        pointerData.pointerDrag = dragTarget;

        // Sliderなどのドラッグ初期化
        if (dragTarget != null)
        {
            ExecuteEvents.Execute(
                dragTarget,
                pointerData,
                ExecuteEvents.initializePotentialDrag
            );

            isDragging = true;

            // 押した位置を即座に反映
            DragUI();
        }

        // Buttonのクリック対象
        clickTarget = ExecuteEvents.GetEventHandler<IPointerClickHandler>(
            currentHit
        );
    }

    // ドラッグ処理
    void DragUI()
    {
        if (dragTarget == null) return;

        pointerData.position = RectTransformUtility.WorldToScreenPoint(
            canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera,
            cursor.position
        );

        // ドラッグ開始
        if (!pointerData.dragging)
        {
            ExecuteEvents.Execute(
                dragTarget,
                pointerData,
                ExecuteEvents.beginDragHandler
            );

            pointerData.dragging = true;
        }

        // 毎フレームドラッグイベントを送る
        ExecuteEvents.Execute(
            dragTarget,
            pointerData,
            ExecuteEvents.dragHandler
        );
    }

    // UIを離したとき
    void ReleaseUI()
    {
        if (pressedTarget != null)
        {
            ExecuteEvents.Execute(
                pressedTarget,
                pointerData,
                ExecuteEvents.pointerUpHandler
            );
        }

        // ドラッグ終了
        if (dragTarget != null && pointerData.dragging)
        {
            ExecuteEvents.Execute(
                dragTarget,
                pointerData,
                ExecuteEvents.endDragHandler
            );
        }

        // Buttonクリック判定
        if (clickTarget != null && currentHit != null)
        {
            GameObject currentClickTarget =
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(
                    currentHit
                );

            if (clickTarget == currentClickTarget)
            {
                ExecuteEvents.Execute(
                    clickTarget,
                    pointerData,
                    ExecuteEvents.pointerClickHandler
                );
            }
        }

        // 初期化
        pressedTarget = null;
        dragTarget = null;
        clickTarget = null;

        isDragging = false;

        pointerData.pointerPress = null;
        pointerData.rawPointerPress = null;
        pointerData.pointerDrag = null;
        pointerData.dragging = false;
        pointerData.eligibleForClick = false;
    }

    public void SetSensitivity(float value)
    {
        OptionManager.mouseSens = value;
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pointerData != null)
        {
            if (currentHover != null)
            {
                ExecuteEvents.Execute(
                    currentHover,
                    pointerData,
                    ExecuteEvents.pointerExitHandler
                );
            }

            if (pressedTarget != null)
            {
                ExecuteEvents.Execute(
                    pressedTarget,
                    pointerData,
                    ExecuteEvents.pointerUpHandler
                );
            }

            if (dragTarget != null && pointerData.dragging)
            {
                ExecuteEvents.Execute(
                    dragTarget,
                    pointerData,
                    ExecuteEvents.endDragHandler
                );
            }
        }
    }
}
