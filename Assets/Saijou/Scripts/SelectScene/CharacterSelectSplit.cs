using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 左右2分割のキャラ選択。画面の左半分/右半分のどこをクリックしても、その側が全画面に広がり、反対側は端へ消える。
/// 初期位置はInspectorの設定をそのまま使い、起動時に書き換えない。
/// </summary>
public class CharacterSelectSplit : MonoBehaviour
{
    [System.Serializable]
    private class Side
    {
        public RectTransform root;
        public CanvasGroup detail;
        [HideInInspector] public CanvasGroup whole;
        [HideInInspector] public Graphic[] texts;       // 子のテキスト
        [HideInInspector] public float[] textAlphas;    // 元のアルファ
    }

    private struct RectState
    {
        public Vector2 aMin, aMax, oMin, oMax;

        public static RectState Of(RectTransform r) => new RectState
        {
            aMin = r.anchorMin,
            aMax = r.anchorMax,
            oMin = r.offsetMin,
            oMax = r.offsetMax
        };

        public static RectState Lerp(RectState a, RectState b, float t) => new RectState
        {
            aMin = Vector2.Lerp(a.aMin, b.aMin, t),
            aMax = Vector2.Lerp(a.aMax, b.aMax, t),
            oMin = Vector2.Lerp(a.oMin, b.oMin, t),
            oMax = Vector2.Lerp(a.oMax, b.oMax, t)
        };

        public void ApplyTo(RectTransform r)
        {
            r.anchorMin = aMin;
            r.anchorMax = aMax;
            r.offsetMin = oMin;
            r.offsetMax = oMax;
        }
    }

    [SerializeField] private Side left;    // 娘
    [SerializeField] private Side right;   // 母
    [SerializeField] private Button backButton;   // 広がった状態から戻る(任意)

    [Header("Timing")]
    [SerializeField] private float expandDuration = 0.6f;
    [SerializeField] private float detailFadeDuration = 0.3f;
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Expanded Offset")]
    [SerializeField] private float leftExpandedShiftX = 100f;     // 娘が広がったときの横ずれ(プラスで右)
    [SerializeField] private float rightExpandedShiftX = -100f;   // 母が広がったときの横ずれ(マイナスで左)

    [Header("Other Side Exit")]
    [SerializeField] private float exitSlideDistance = 600f;   // 消える側が外へ動く距離(px)

    private RectState leftRest;    // Inspectorで決めた元の位置
    private RectState rightRest;
    private bool touched;          // 位置を動かしたかどうか
    private Side current;          // 選択中の側(null = 未選択)
    private bool busy;

    private void Awake()
    {
        // 位置は書き換えず、元の値を記録するだけ
        leftRest = RectState.Of(left.root);
        rightRest = RectState.Of(right.root);

        left.whole = GetOrAddGroup(left.root.gameObject);
        right.whole = GetOrAddGroup(right.root.gameObject);

        // テキスト集める
        CollectTexts(left);
        CollectTexts(right);

        if (backButton != null) backButton.onClick.AddListener(Back);

       
    }

    private void OnEnable()
    {
        ResetImmediate();
    }

    private void OnDestroy()
    {
        if (backButton != null) backButton.onClick.RemoveListener(Back);
    }

    private void Update()
    {
        if (busy || current != null) return;

        if (TryGetPress(out Vector2 pos))
        {
            Select(pos.x < Screen.width * 0.5f ? left : right);
        }
    }

    // ---------- 入力 ----------

    private static bool TryGetPress(out Vector2 pos)
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            pos = mouse.position.ReadValue();
            return true;
        }

        var touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
        {
            pos = touch.primaryTouch.position.ReadValue();
            return true;
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            pos = Input.mousePosition;
            return true;
        }
#endif
        pos = default;
        return false;
    }

    // ---------- 操作 ----------

    private void Select(Side chosen)
    {
        if (busy || current != null) return;
        StartCoroutine(OpenRoutine(chosen));
    }

    private void Back()
    {
        if (busy || current == null) return;
        StartCoroutine(CloseRoutine());
    }

    // ---------- 演出 ----------

    private IEnumerator OpenRoutine(Side chosen)
    {
        busy = true;
        current = chosen;
        touched = true;

        Side other = chosen == left ? right : left;

        RectState chosenFrom = RectState.Of(chosen.root);
        RectState otherFrom = RectState.Of(other.root);
        RectState chosenTo = FullScreen(chosen == right ? rightExpandedShiftX : leftExpandedShiftX);
        RectState otherTo = SlideOut(otherFrom, other == left ? -exitSlideDistance : exitSlideDistance);

        for (float t = 0f; t < expandDuration; t += Time.unscaledDeltaTime)
        {
            float k = ease.Evaluate(t / expandDuration);
            RectState.Lerp(chosenFrom, chosenTo, k).ApplyTo(chosen.root);
            RectState.Lerp(otherFrom, otherTo, k).ApplyTo(other.root);
            other.whole.alpha = 1f - k;
            SetTextAlpha(chosen, 1f - k);   // 選んだ側のテキストが消えていく
            yield return null;
        }
        chosenTo.ApplyTo(chosen.root);
        otherTo.ApplyTo(other.root);
        other.whole.alpha = 0f;
        SetTextAlpha(chosen, 0f);

        // 説明文と進むボタンをフェードイン
        yield return Fade(chosen.detail, 1f, detailFadeDuration);
        chosen.detail.interactable = true;
        chosen.detail.blocksRaycasts = true;

        if (backButton != null) backButton.gameObject.SetActive(true);
        busy = false;
    }

    private IEnumerator CloseRoutine()
    {
        busy = true;
        Side chosen = current;
        Side other = chosen == left ? right : left;

        chosen.detail.interactable = false;
        chosen.detail.blocksRaycasts = false;
        if (backButton != null) backButton.gameObject.SetActive(false);

        yield return Fade(chosen.detail, 0f, detailFadeDuration);

        RectState chosenFrom = RectState.Of(chosen.root);
        RectState otherFrom = RectState.Of(other.root);
        RectState chosenTo = RestOf(chosen);
        RectState otherTo = RestOf(other);

        for (float t = 0f; t < expandDuration; t += Time.unscaledDeltaTime)
        {
            float k = ease.Evaluate(t / expandDuration);
            RectState.Lerp(chosenFrom, chosenTo, k).ApplyTo(chosen.root);
            RectState.Lerp(otherFrom, otherTo, k).ApplyTo(other.root);
            other.whole.alpha = k;
            SetTextAlpha(chosen, k);  // 戻るにつれて元の透明度へ
            yield return null;
        }
        chosenTo.ApplyTo(chosen.root);
        otherTo.ApplyTo(other.root);
        other.whole.alpha = 1f;
        SetTextAlpha(chosen, 1f);

        touched = false;
        current = null;
        busy = false;
    }

    private IEnumerator Fade(CanvasGroup group, float to, float time)
    {
        float from = group.alpha;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, t / time);
            yield return null;
        }
        group.alpha = to;
    }

    // ---------- 内部処理 ----------

    private RectState RestOf(Side side) => side == left ? leftRest : rightRest;

    private static RectState FullScreen(float shiftX = 0f) => new RectState
    {
        aMin = Vector2.zero,
        aMax = Vector2.one,
        oMin = new Vector2(shiftX, 0f),
        oMax = new Vector2(shiftX, 0f)
    };

    // x の位置(0=左端 / 1=右端)で幅0につぶれた状態
    private static RectState Collapsed(float x) => new RectState
    {
        aMin = new Vector2(x, 0f),
        aMax = new Vector2(x, 1f),
        oMin = Vector2.zero,
        oMax = Vector2.zero
    };

    private void ResetImmediate()
    {
        StopAllCoroutines();
        busy = false;
        current = null;

        // 位置を動かしていたときだけ、元の位置に戻す(初回起動では何も書き換えない)
        if (touched)
        {
            leftRest.ApplyTo(left.root);
            rightRest.ApplyTo(right.root);
            touched = false;
        }

        foreach (var side in new[] { left, right })
        {
            if (side.whole == null) side.whole = GetOrAddGroup(side.root.gameObject);
            side.whole.alpha = 1f;
            side.detail.alpha = 0f;
            side.detail.interactable = false;
            side.detail.blocksRaycasts = false;
            if (side.texts != null) SetTextAlpha(side, 1f);
        }
        if (backButton != null) backButton.gameObject.SetActive(false);
    }

    private static CanvasGroup GetOrAddGroup(GameObject go)
    {
        var g = go.GetComponent<CanvasGroup>();
        if (g == null) g = go.AddComponent<CanvasGroup>();
        return g;
    }

    // 今の大きさのまま、横にずらした状態
    private static RectState SlideOut(RectState from, float dx) => new RectState
    {
        aMin = from.aMin,
        aMax = from.aMax,
        oMin = from.oMin + new Vector2(dx, 0f),
        oMax = from.oMax + new Vector2(dx, 0f)
    };

    // root の子のテキストを集める(detail の中身は別でフェードするので除外)
    private static void CollectTexts(Side side)
    {
        var list = new System.Collections.Generic.List<Graphic>();
        foreach (var g in side.root.GetComponentsInChildren<Graphic>(true))
        {
            bool isText = g is Text || g is TMPro.TMP_Text;
            if (!isText) continue;
            if (side.detail != null && g.transform.IsChildOf(side.detail.transform)) continue;
            list.Add(g);
        }
        side.texts = list.ToArray();
        side.textAlphas = new float[side.texts.Length];
        for (int i = 0; i < side.texts.Length; i++)
            side.textAlphas[i] = side.texts[i].color.a;
    }

    private static void SetTextAlpha(Side side, float rate)
    {
        for (int i = 0; i < side.texts.Length; i++)
        {
            var c = side.texts[i].color;
            c.a = side.textAlphas[i] * rate;
            side.texts[i].color = c;
        }
    }
}