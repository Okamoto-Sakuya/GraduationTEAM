using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// メニュー1項目分。選択中は文字が濃くなり、下線が右へ伸びて、少し左に寄る。
[RequireComponent(typeof(CanvasGroup))]
public class TitleMenuItem : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] TMP_Text label;
    [SerializeField] RectTransform underline;   // Pivot X=0 にしておく(右へ伸びる)
    [SerializeField] RectTransform body;        // 選択時に左へずらす対象(任意)
    [SerializeField] Color normalColor = new Color(0.86f, 0.62f, 0.67f, 0.75f);
    [SerializeField] Color selectedColor = new Color(0.55f, 0.06f, 0.16f, 1f);
    [SerializeField] float underlineWidth = 220f;
    [SerializeField] float selectedShiftX = -14f;
    [SerializeField] float selectSeconds = 0.18f;

    public Action<TitleMenuItem> Hovered;

    CanvasGroup group;
    public CanvasGroup Group => group ? group : (group = GetComponent<CanvasGroup>());

    float amount;      // 0=非選択 1=選択
    bool selected;
    Vector2 bodyRest;

    void Awake()
    {
        if (body) bodyRest = body.anchoredPosition;
        Apply();
    }

    public void SetSelected(bool value) => selected = value;

    void Update()
    {
        float target = selected ? 1f : 0f;
        if (Mathf.Approximately(amount, target)) return;
        amount = Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime / selectSeconds);
        Apply();
    }

    void Apply()
    {
        float e = amount * amount * (3f - 2f * amount);   // smoothstep
        if (label) label.color = Color.Lerp(normalColor, selectedColor, e);
        if (underline) underline.sizeDelta = new Vector2(underlineWidth * e, underline.sizeDelta.y);
        if (body) body.anchoredPosition = bodyRest + new Vector2(selectedShiftX * e, 0f);
    }

    public void OnPointerEnter(PointerEventData eventData) => Hovered?.Invoke(this);
}