using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class StageSelectCamera : MonoBehaviour
{
    [System.Serializable]
    public class BgFocus
    {
        public Vector2 position;
        public float scale = 2.5f;
    }

    [Header("参照")]
    [SerializeField] RectTransform world;
    [SerializeField] RectTransform background;
    [SerializeField] Button leftArrow;
    [SerializeField] Button rightArrow;
    [SerializeField] Image fadeOverlay;

    [Header("移動")]
    [SerializeField] float pageWidth = 1920f;
    [SerializeField] float duration = 1.6f;
    [SerializeField] BgFocus[] bgFocus;

    [Header("暗転")]
    [SerializeField] Color fadeColor = new Color(0.1f, 0f, 0.016f);   // 暗い赤
    [SerializeField, Range(0f, 1f)] float maxDarkness = 0.92f;        // 一番暗いときの濃さ
    [SerializeField, Range(0.05f, 0.5f)] float fadeInRatio = 0.25f;   // 暗くなっていく時間の割合
    [SerializeField, Range(0.5f, 0.95f)] float fadeOutStart = 0.65f;  // ここから明るくなり始める

    [Header("震え")]
    [SerializeField] float shakeAmount = 8f;

    int index = 0;
    bool isMoving = false;

    void Start()
    {
        if (bgFocus == null || bgFocus.Length == 0)
        {
            Debug.LogError("Bg Focus が未設定です。InspectorでSizeを設定してください。", this);
            return;
        }

        leftArrow.onClick.AddListener(() => Move(-1));
        rightArrow.onClick.AddListener(() => Move(+1));
        SetFade(0f);
        ApplyImmediately();
        UpdateArrows();
    }

    void Move(int dir)
    {
        if (isMoving) return; // 移動中の連打を無視

        int next = Mathf.Clamp(index + dir, 0, bgFocus.Length - 1);
        if (next == index) return;
        index = next;

        StartCoroutine(Slide());
    }

    IEnumerator Slide()
    {
        isMoving = true;
        leftArrow.interactable = false;
        rightArrow.interactable = false;

        Vector2 worldStart = world.anchoredPosition;
        Vector2 worldEnd = new Vector2(-index * pageWidth, 0);

        Vector2 bgPosStart = background.anchoredPosition;
        float bgScaleStart = background.localScale.x;
        Vector2 bgPosEnd = bgFocus[index].position;
        float bgScaleEnd = bgFocus[index].scale;

        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float tc = Mathf.Clamp01(t);
            float e = Mathf.SmoothStep(0, 1, tc);

            // 震え：移動の真ん中で一番強く、始めと終わりは0
            float j = Mathf.Sin(tc * Mathf.PI) * shakeAmount;
            Vector2 jitter = new Vector2(Random.Range(-j, j), Random.Range(-j, j));

            world.anchoredPosition = Vector2.Lerp(worldStart, worldEnd, e) + jitter;
            background.anchoredPosition = Vector2.Lerp(bgPosStart, bgPosEnd, e) + jitter;
            background.localScale = Vector3.one * Mathf.Lerp(bgScaleStart, bgScaleEnd, e);

            // 暗さ：暗くなる → 最大のまま維持 → 明るくなる
            SetFade(FadeCurve(tc) * maxDarkness);

            yield return null;
        }

        ApplyImmediately();
        SetFade(0f);

        isMoving = false;
        leftArrow.interactable = true;
        rightArrow.interactable = true;
        UpdateArrows();
    }

    // 0 → 1（すばやく暗く） → 1のまま維持 → 0（ゆっくり明るく）
    float FadeCurve(float t)
    {
        if (t < fadeInRatio)
            return Mathf.SmoothStep(0f, 1f, t / fadeInRatio);

        if (t < fadeOutStart)
            return 1f;

        return Mathf.SmoothStep(0f, 1f, (1f - t) / (1f - fadeOutStart));
    }

    void SetFade(float alpha)
    {
        if (fadeOverlay == null) return;
        fadeOverlay.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
    }

    void ApplyImmediately()
    {
        world.anchoredPosition = new Vector2(-index * pageWidth, 0);
        background.anchoredPosition = bgFocus[index].position;
        background.localScale = Vector3.one * bgFocus[index].scale;
    }

    void UpdateArrows()
    {
        leftArrow.gameObject.SetActive(index > 0);
        rightArrow.gameObject.SetActive(index < bgFocus.Length - 1);
    }
}