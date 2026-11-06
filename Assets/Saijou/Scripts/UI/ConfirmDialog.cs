using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 使い回し型の確認ウィンドウ（ノイズ混じりフェードイン付き）。
/// シーンに1つ置いておき、Show() で文言とコールバックだけ渡して使う。
/// </summary>
public class ConfirmDialog : MonoBehaviour
{
    public static ConfirmDialog Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject root;          // ウィンドウ全体（表示/非表示の対象）
    [SerializeField] private CanvasGroup canvasGroup;  // Root に付けたCanvasGroup
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;
    [SerializeField] private TMP_Text yesButtonText;
    [SerializeField] private TMP_Text noButtonText;

    [Header("Noise Fade-In")]
    [SerializeField] private float fadeDuration = 0.8f;   // 出きるまでの時間（秒）
    [SerializeField, Range(0f, 1f)] private float flickerStrength = 0.5f; // ちらつきの強さ
    [SerializeField, Range(0f, 1f)] private float dropChance = 0.15f;     // 一瞬消える確率（毎フレーム）

    [SerializeField] private RectTransform rootRect;   // Root の RectTransform
    [SerializeField] private float jitterAmount = 8f;  // ブレの大きさ（ピクセル）

    private Action onYes;
    private Action onNo;
    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        yesButton.onClick.AddListener(OnYesClicked);
        noButton.onClick.AddListener(OnNoClicked);

        root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 確認ウィンドウを表示する。
    /// noText を null/空にすると「はい」だけのお知らせ用になる。
    /// </summary>
    public void Show(
        string title,
        string message,
        string yesText = "はい",
        string noText = "いいえ",
        Action onYes = null,
        Action onNo = null)
    {
        titleText.text = title;
        messageText.text = message;
        yesButtonText.text = yesText;

        bool hasNo = !string.IsNullOrEmpty(noText);
        noButton.gameObject.SetActive(hasNo);
        if (hasNo) noButtonText.text = noText;

        this.onYes = onYes;
        this.onNo = onNo;

        root.SetActive(true);
        transform.SetAsLastSibling(); // 他のUIより手前に出す

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(NoiseFadeIn());
    }

    // ノイズ混じりのフェードイン：
    // 基本は徐々に濃くなり、序盤ほどアルファがランダムに揺れ、たまに一瞬消える
    private IEnumerator NoiseFadeIn()
    {
        canvasGroup.interactable = false;
        canvasGroup.alpha = 0f;
        Vector2 basePos = rootRect.anchoredPosition;

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(t / fadeDuration);

            // （アルファの計算は今までと同じ）
            float baseAlpha = progress * progress;
            float noise = (UnityEngine.Random.value - 0.5f) * 2f * flickerStrength * (1f - progress);
            float alpha = Mathf.Clamp01(baseAlpha + noise);
            if (UnityEngine.Random.value < dropChance * (1f - progress)) alpha = 0f;
            canvasGroup.alpha = alpha;

            // 序盤ほど大きくブレる
            rootRect.anchoredPosition = basePos + UnityEngine.Random.insideUnitCircle * jitterAmount * (1f - progress);

            yield return null;
        }

        rootRect.anchoredPosition = basePos; // 必ず元の位置に戻す
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        fadeRoutine = null;
    }

    private void OnYesClicked()
    {
        var callback = onYes;
        Close();
        callback?.Invoke();
    }

    private void OnNoClicked()
    {
        var callback = onNo;
        Close();
        callback?.Invoke();
    }

    private void Close()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
        root.SetActive(false);
        onYes = null;
        onNo = null;
    }
}

// ------------------------------------------------------------
// 呼び出し例
// ------------------------------------------------------------
// ConfirmDialog.Instance.Show(
//     "セーブ",
//     "ここにセーブしますか？",
//     "セーブする", "やめる",
//     onYes: () => SaveGame(),
//     onNo:  () => Debug.Log("キャンセル"));