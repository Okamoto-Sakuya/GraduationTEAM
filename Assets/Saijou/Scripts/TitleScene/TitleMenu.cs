using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// container の直下にある TitleMenuItem を自動で集めて、
/// 斜めに並べ、右から順番にスライドインさせる。
/// 項目を増やすときは、項目をコピーして container の子に足すだけ。
public class TitleMenu : MonoBehaviour
{
    [SerializeField] RectTransform container;
    [SerializeField] float spacing = 64f;          // 項目の縦間隔
    [SerializeField] float diagonalX = -12f;       // 下の項目ほどX方向にずらす量
    [SerializeField] float slideDistance = 480f;   // 右からスライドしてくる距離
    [SerializeField] float duration = 0.4f;        // 1項目あたりの所要時間
    [SerializeField] float stagger = 0.05f;        // 項目ごとの開始ずれ

    [Header("開き方")]
    [SerializeField] bool openOnLeftClick = true;  // 画面のどこでも左クリックで開く
    [SerializeField] bool clickEnabled = true;     // ロゴ演出が終わるまで false にしておくことも可能

    readonly List<TitleMenuItem> items = new List<TitleMenuItem>();
    readonly List<Vector2> rest = new List<Vector2>();
    int selected = -1;
    bool isOpen;
    Coroutine playing;
    CanvasGroup containerGroup;

    void Awake()
    {
        // container ごと隠す(TitleMenuItem を付け忘れた子も含めて、開くまで一切見えなくする)
        containerGroup = container.GetComponent<CanvasGroup>();
        if (containerGroup == null) containerGroup = container.gameObject.AddComponent<CanvasGroup>();
        containerGroup.alpha = 0f;
        containerGroup.blocksRaycasts = false;

        foreach (Transform child in container)
        {
            var item = child.GetComponent<TitleMenuItem>();
            if (!item) continue;

            int index = items.Count;
            items.Add(item);
            rest.Add(new Vector2(index * diagonalX, -index * spacing));
            item.Hovered = x => Select(items.IndexOf(x));
        }
        if (items.Count == 0)
            Debug.LogWarning("TitleMenu: container の直下に TitleMenuItem が見つかりません。各項目に TitleMenuItem を付けてください。", this);

        SetTime(0f);
        SetInteractable(false);
    }

    void Update()
    {
        if (openOnLeftClick && clickEnabled && !isOpen && LeftClicked())
            Open();
    }

    static bool LeftClicked()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    // ロゴ演出の onFinished などから呼ぶと、演出が終わってからクリックを受け付けられる
    public void SetClickEnabled(bool value) { clickEnabled = value; }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        Play(true);
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        Play(false);
    }

    void Play(bool open)
    {
        if (playing != null) StopCoroutine(playing);
        playing = StartCoroutine(Animate(open));
    }

    IEnumerator Animate(bool open)
    {
        SetInteractable(false);
        containerGroup.alpha = 1f;
        containerGroup.blocksRaycasts = true;
        float total = duration + stagger * Mathf.Max(0, items.Count - 1);

        for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
        {
            SetTime(open ? t : total - t);
            yield return null;
        }
        SetTime(open ? total : 0f);

        if (open) { SetInteractable(true); Select(0); }
        else
        {
            Select(-1);
            containerGroup.alpha = 0f;
            containerGroup.blocksRaycasts = false;
        }
    }

    void SetTime(float time)
    {
        for (int i = 0; i < items.Count; i++)
        {
            float p = Mathf.Clamp01((time - i * stagger) / duration);
            float e = 1f - (1f - p) * (1f - p) * (1f - p);   // ease-out cubic
            ((RectTransform)items[i].transform).anchoredPosition =
                rest[i] + new Vector2(slideDistance * (1f - e), 0f);
            items[i].Group.alpha = e;
        }
    }

    void SetInteractable(bool value)
    {
        foreach (var item in items)
        {
            item.Group.interactable = value;
            item.Group.blocksRaycasts = value;
        }
    }

    public void Select(int index)
    {
        if (index == selected) return;
        selected = index;
        for (int i = 0; i < items.Count; i++) items[i].SetSelected(i == index);
    }
}