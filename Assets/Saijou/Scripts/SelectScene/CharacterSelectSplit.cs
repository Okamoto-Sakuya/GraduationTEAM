using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 左右2分割のキャラ選択。
/// Root の大きさは変えず、RectMask2D の Padding だけで「見える範囲」を動かす。
/// 選ぶと、選んだ側のマスクが画面全体まで開き、反対側は大きさを変えずに横へ移動して消える。
/// </summary>
public class CharacterSelectSplit : MonoBehaviour
{
    [System.Serializable]
    private class Side
    {
        public RectTransform root;     // 半分サイズでも全画面でもOK(大きさは変えない)
        public CanvasGroup detail;
        [HideInInspector] public RectMask2D mask;
        [HideInInspector] public CanvasGroup whole;
        [HideInInspector] public Graphic[] texts;
        [HideInInspector] public float[] textAlphas;
        [HideInInspector] public Vector2 restPos;   // 元の位置

        public RectTransform characterImage;        // 空なら、Root直下の「CharacterImage」を自動で探す
        [HideInInspector] public Vector2 imageRestPos;
    }

    [SerializeField] private Side left;    // 娘
    [SerializeField] private Side right;   // 母
    [SerializeField] private Button backButton;

    [Header("Timing")]
    [SerializeField] private float expandDuration = 0.6f;
    [SerializeField] private float detailFadeDuration = 0.3f;
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Expanded Offset")]
    [SerializeField] private float leftExpandedShiftX = 0f;     // 娘が広がったときの横ずれ(プラスで右)
    [SerializeField] private float rightExpandedShiftX = 0f;    // 母が広がったときの横ずれ(マイナスで左)

    [Header("Character Image Shift (Rootとは別に、絵だけ動かす量)")]
    [SerializeField] private float leftImageShiftX = 0f;     // 娘を選んだとき、娘の絵をずらす量(プラスで右)
    [SerializeField] private float rightImageShiftX = 80f;   // 母を選んだとき、母の絵をずらす量(プラスで右)

    [Header("Other Side Exit")]
    [SerializeField] private float exitSlideDistance = 600f;    // 消える側が外へ動く距離

    private Side current;
    private bool busy;
    private RectTransform canvasRect;
    private readonly Vector3[] corners = new Vector3[4];

    private void Awake()
    {
        // マスクの基準にする画面(Canvas)
        canvasRect = (RectTransform)left.root.GetComponentInParent<Canvas>().rootCanvas.transform;

        Setup(left);
        Setup(right);

        if (backButton != null) backButton.onClick.AddListener(Back);
    }

    private void Setup(Side side)
    {
        side.restPos = side.root.anchoredPosition;

        if (side.characterImage == null)
            side.characterImage = side.root.Find("CharacterImage") as RectTransform;
        if (side.characterImage != null)
            side.imageRestPos = side.characterImage.anchoredPosition;
        side.whole = GetOrAdd<CanvasGroup>(side.root.gameObject);
        side.mask = GetOrAdd<RectMask2D>(side.root.gameObject);
        CollectTexts(side);
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
        if (busy) return;

        if (current == null)
        {
            // 待機中は毎フレーム「半分だけ見せる」状態を保つ(画面サイズが変わっても追従)
            SetMask(left, 0f);
            SetMask(right, 0f);

            if (TryGetPress(out Vector2 pos))
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

        Side other = chosen == left ? right : left;
        float otherDir = other == left ? -1f : 1f;
        float chosenShift = chosen == right ? rightExpandedShiftX : leftExpandedShiftX;

        for (float t = 0f; t < expandDuration; t += Time.unscaledDeltaTime)
        {
            float k = ease.Evaluate(t / expandDuration);
            Apply(chosen, other, k, otherDir, chosenShift);
            yield return null;
        }
        Apply(chosen, other, 1f, otherDir, chosenShift);

        // フェードアウトが終わってから、選ばれなかった側のRectMask2Dを外す(戻るときに復活させる)
        other.mask.enabled = false;

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
        float otherDir = other == left ? -1f : 1f;
        float chosenShift = chosen == right ? rightExpandedShiftX : leftExpandedShiftX;

        chosen.detail.interactable = false;
        chosen.detail.blocksRaycasts = false;
        if (backButton != null) backButton.gameObject.SetActive(false);

        yield return Fade(chosen.detail, 0f, detailFadeDuration);

        // 反対側はまだ透明なので、ここでマスクを戻しても見た目は変わらない
        other.mask.enabled = true;

        for (float t = 0f; t < expandDuration; t += Time.unscaledDeltaTime)
        {
            float k = 1f - ease.Evaluate(t / expandDuration);   // 逆再生
            Apply(chosen, other, k, otherDir, chosenShift);
            yield return null;
        }
        Apply(chosen, other, 0f, otherDir, chosenShift);

        current = null;
        busy = false;
    }

    // k: 0 = 初期状態(半分表示) / 1 = 選んだ側が全表示
    private void Apply(Side chosen, Side other, float k, float otherDir, float chosenShift)
    {
        // 選んだ側：マスクを開く＋(必要なら)横ずれ。Rootの大きさは変えない
        SetMask(chosen, k);
        chosen.root.anchoredPosition = chosen.restPos + new Vector2(chosenShift * k, 0f);
        SetTextAlpha(chosen, 1f - k);

        // 絵だけ、Rootとは別にもう少し横へずらす
        if (chosen.characterImage != null)
        {
            float imageShift = chosen == right ? rightImageShiftX : leftImageShiftX;
            chosen.characterImage.anchoredPosition = chosen.imageRestPos + new Vector2(imageShift * k, 0f);
        }

        // 反対側：大きさはそのまま、位置だけ外へ動かして消す
        other.root.anchoredPosition = other.restPos + new Vector2(otherDir * exitSlideDistance * k, 0f);
        other.whole.alpha = 1f - k;
    }

    // open: 0 = 半分だけ見せる / 1 = 全部見せる
    // Rootの大きさには依存せず、「画面の左半分 / 右半分」を基準に見える範囲を決める。
    // Paddingはマイナスにすると外側へ広がるので、半分サイズのRootでもそのまま全画面まで開ける。
    private void SetMask(Side side, float open)
    {
        // 画面の左端・右端を、このRootのローカル座標に変換
        canvasRect.GetWorldCorners(corners);   // 0:左下 2:右上
        float canvasL = side.root.InverseTransformPoint(corners[0]).x;
        float canvasR = side.root.InverseTransformPoint(corners[2]).x;
        float mid = (canvasL + canvasR) * 0.5f;
        Rect r = side.root.rect;

        // Vector4(left, bottom, right, top)
        if (side == left)
        {
            // 娘：左端?中央 → 左端?右端
            float visibleRight = Mathf.Lerp(mid, canvasR, open);
            side.mask.padding = new Vector4(0f, 0f, r.xMax - visibleRight, 0f);
        }
        else
        {
            // 母：中央?右端 → 左端?右端
            float visibleLeft = Mathf.Lerp(mid, canvasL, open);
            side.mask.padding = new Vector4(visibleLeft - r.xMin, 0f, 0f, 0f);
        }
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

    private void ResetImmediate()
    {
        StopAllCoroutines();
        busy = false;
        current = null;

        foreach (var side in new[] { left, right })
        {
            if (side.whole == null) Setup(side);   // Awake前に呼ばれた場合の保険

            side.root.anchoredPosition = side.restPos;
            if (side.characterImage != null)
                side.characterImage.anchoredPosition = side.imageRestPos;
            side.whole.alpha = 1f;
            side.mask.enabled = true;
            side.detail.alpha = 0f;
            side.detail.interactable = false;
            side.detail.blocksRaycasts = false;
            SetMask(side, 0f);
            SetTextAlpha(side, 1f);
        }
        if (backButton != null) backButton.gameObject.SetActive(false);
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null) c = go.AddComponent<T>();
        return c;
    }

    // Root 直下のテキストを集める(detail の中身は別でフェードするので除外)
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