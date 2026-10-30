using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// タイトル演出
///   1. 中央の文字(紅)がふわっと出る
///   2. 左(東方)と右(魔郷)が中央から左右に広がって出る
///   3. サブタイトルが出る
///   4. キャラクターが下から出る
/// 各パーツに CanvasGroup を付けて、下のフィールドに入れるだけ。
public class TitleLogoAnimator : MonoBehaviour
{
    [Serializable]
    public class Part
    {
        public CanvasGroup target;
        public float delay;              // 開始までの秒数
        public float duration = 0.8f;    // かける秒数
        public Vector2 slideFrom;        // 開始時のずれ(px)
        public float scaleFrom = 1f;     // 開始時の拡大率

        [NonSerialized] public Vector2 restPos;
        [NonSerialized] public Vector3 restScale;
    }

    [Header("パーツ(delay / duration は Inspector で調整)")]
    [SerializeField] Part center = new Part { delay = 0.0f, duration = 0.8f, scaleFrom = 0.95f };                     // 真ん中
    [SerializeField] Part left = new Part { delay = 0.6f, duration = 1.0f, slideFrom = new Vector2(60f, 0f) };        // 左
    [SerializeField] Part right = new Part { delay = 0.6f, duration = 1.0f, slideFrom = new Vector2(-60f, 0f) };    　// 右
    [SerializeField] Part subtitle = new Part { delay = 1.6f, duration = 0.8f };                                      // サブタイトル
    [SerializeField] Part character = new Part { delay = 2.0f, duration = 1.2f, slideFrom = new Vector2(0f, -40f) };  // キャラクター

    [SerializeField] bool playOnStart = true;
    public UnityEvent onFinished;

    Part[] parts;

    void Awake()
    {
        parts = new[] { center, left, right, subtitle, character };
        foreach (var p in parts)
        {
            if (p.target == null) continue;
            var rt = (RectTransform)p.target.transform;
            p.restPos = rt.anchoredPosition;
            p.restScale = rt.localScale;
            Apply(p, 0f);
        }
    }

    void Start()
    {
        if (playOnStart) Play();
    }

    public void Play()
    {
        StopAllCoroutines();
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        float total = 0f;
        foreach (var p in parts) total = Mathf.Max(total, p.delay + p.duration);

        for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
        {
            foreach (var p in parts)
                Apply(p, Mathf.Clamp01((t - p.delay) / Mathf.Max(0.0001f, p.duration)));
            yield return null;
        }
        foreach (var p in parts) Apply(p, 1f);
        onFinished.Invoke();
    }

    static void Apply(Part p, float progress)
    {
        if (p.target == null) return;
        var rt = (RectTransform)p.target.transform;
        float e = 1f - (1f - progress) * (1f - progress) * (1f - progress);   // ease-out cubic
        p.target.alpha = e;
        rt.anchoredPosition = p.restPos + p.slideFrom * (1f - e);
        rt.localScale = Vector3.Lerp(p.restScale * p.scaleFrom, p.restScale, e);
    }
}