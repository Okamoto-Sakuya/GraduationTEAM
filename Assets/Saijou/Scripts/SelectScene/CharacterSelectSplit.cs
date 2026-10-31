using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 左右2分割のキャラ選択。選んだ側が全画面に広がり、反対側は端へ消える。
/// 初期位置はInspectorの設定をそのまま使い、起動時に書き換えない。
/// </summary>
public class CharacterSelectSplit : MonoBehaviour
{
    [System.Serializable]
    private class Side
    {
        public RectTransform root;     // 半分のエリア
        public Button selectButton;    // 半分全体のクリック領域
        public CanvasGroup detail;     // 説明文 + 進むボタンの親
        [HideInInspector] public CanvasGroup whole;
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

        left.selectButton.onClick.AddListener(() => Select(left));
        right.selectButton.onClick.AddListener(() => Select(right));
        if (backButton != null) backButton.onClick.AddListener(Back);
    }

    private void OnEnable()
    {
        ResetImmediate();
    }

    private void OnDestroy()
    {
        left.selectButton.onClick.RemoveAllListeners();
        right.selectButton.onClick.RemoveAllListeners();
        if (backButton != null) backButton.onClick.RemoveAllListeners();
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
        SetSelectable(false);

        Side other = chosen == left ? right : left;

        RectState chosenFrom = RectState.Of(chosen.root);
        RectState otherFrom = RectState.Of(other.root);
        RectState chosenTo = FullScreen();
        RectState otherTo = Collapsed(other == left ? 0f : 1f);   // 外側の端へ幅0に

        for (float t = 0f; t < expandDuration; t += Time.unscaledDeltaTime)
        {
            float k = ease.Evaluate(t / expandDuration);
            RectState.Lerp(chosenFrom, chosenTo, k).ApplyTo(chosen.root);
            RectState.Lerp(otherFrom, otherTo, k).ApplyTo(other.root);
            other.whole.alpha = 1f - k;
            yield return null;
        }
        chosenTo.ApplyTo(chosen.root);
        otherTo.ApplyTo(other.root);
        other.whole.alpha = 0f;

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
            yield return null;
        }
        chosenTo.ApplyTo(chosen.root);
        otherTo.ApplyTo(other.root);
        other.whole.alpha = 1f;

        touched = false;
        current = null;
        SetSelectable(true);
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

    private static RectState FullScreen() => new RectState
    {
        aMin = Vector2.zero,
        aMax = Vector2.one,
        oMin = Vector2.zero,
        oMax = Vector2.zero
    };

    // x の位置(0=左端 / 1=右端)で幅0につぶれた状態
    private static RectState Collapsed(float x) => new RectState
    {
        aMin = new Vector2(x, 0f),
        aMax = new Vector2(x, 1f),
        oMin = Vector2.zero,
        oMax = Vector2.zero
    };

    private void SetSelectable(bool value)
    {
        left.selectButton.interactable = value;
        right.selectButton.interactable = value;
    }

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
        }
        SetSelectable(true);
        if (backButton != null) backButton.gameObject.SetActive(false);
    }

    private static CanvasGroup GetOrAddGroup(GameObject go)
    {
        var g = go.GetComponent<CanvasGroup>();
        if (g == null) g = go.AddComponent<CanvasGroup>();
        return g;
    }
}