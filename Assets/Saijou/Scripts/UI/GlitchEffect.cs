using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 確認ウィンドウ用のグリッチ演出。
/// ・画像(Graphic)には UI/Glitch シェーダーのマテリアルを当てて、ずれ/色ズレ/ノイズを出す
/// ・文字(TMP)は、グリッチ中だけ位置をランダムに揺らす
/// ・Burst() で表示時の派手なグリッチ、その後は時々ランダムに短いグリッチを出す
/// Root(表示/非表示の対象)に付けて使う。
/// </summary>
public class GlitchEffect : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Material glitchMaterial;      // UI/Glitch シェーダーのマテリアル
    [SerializeField] private Graphic[] imageTargets;       // 背景Imageなど(TMPの文字は入れない)
    [SerializeField] private TMP_Text[] jitterTexts;       // 揺らしたい文字

    [Header("Burst (表示した瞬間)")]
    [SerializeField] private float burstDuration = 0.6f;
    [SerializeField, Range(0f, 1f)] private float burstPeak = 1f;

    [Header("Idle (表示中に時々起こる)")]
    [SerializeField] private bool idleGlitch = false;   // オフ：開いた瞬間だけグリッチ
    [SerializeField, Range(0f, 1f)] private float idleIntensity = 0.5f;
    [SerializeField] private Vector2 idleInterval = new Vector2(1.5f, 4f);   // 次のグリッチまでの間隔(秒)
    [SerializeField] private Vector2 idleLength = new Vector2(0.08f, 0.25f); // 1回の長さ(秒)

    [Header("Text Jitter")]
    [SerializeField] private float textJitterPixels = 6f;

    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int TimeId = Shader.PropertyToID("_GlitchTime");

    private Material runtimeMat;
    private Vector2[] textBasePos;
    private float burstStart = -999f;
    private float nextSpike;
    private float spikeEnd;

    private void Awake()
    {
        // マテリアルは複製して使う(元のアセットを書き換えない)
        if (glitchMaterial != null)
        {
            runtimeMat = new Material(glitchMaterial);
            foreach (var g in imageTargets)
                if (g != null) g.material = runtimeMat;
        }

        textBasePos = new Vector2[jitterTexts.Length];
        for (int i = 0; i < jitterTexts.Length; i++)
            if (jitterTexts[i] != null)
                textBasePos[i] = jitterTexts[i].rectTransform.anchoredPosition;
    }

    private void OnEnable()
    {
        burstStart = -999f;
        spikeEnd = 0f;
        nextSpike = Time.unscaledTime + Random.Range(idleInterval.x, idleInterval.y);
        SetIntensity(0f);
    }

    private void OnDisable()
    {
        ResetTexts();
    }

    private void OnDestroy()
    {
        if (runtimeMat != null) Destroy(runtimeMat);
    }

    /// <summary>表示した瞬間などに呼ぶ。強いグリッチが出て、だんだん収まる。</summary>
    public void Burst()
    {
        burstStart = Time.unscaledTime;
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        float v = 0f;

        // 表示直後のバースト：最初が最大で、時間とともに弱まる(ランダムに強弱がぶれる)
        float elapsed = now - burstStart;
        if (elapsed >= 0f && elapsed < burstDuration)
        {
            float p = 1f - elapsed / burstDuration;
            v = burstPeak * p * Random.Range(0.5f, 1f);
        }

        // 表示中の時々起こるグリッチ
        if (idleGlitch)
        {
            if (now >= nextSpike)
            {
                spikeEnd = now + Random.Range(idleLength.x, idleLength.y);
                nextSpike = spikeEnd + Random.Range(idleInterval.x, idleInterval.y);
            }
            if (now < spikeEnd) v = Mathf.Max(v, idleIntensity);
        }

        SetIntensity(v);
        JitterTexts(v);
    }

    private void SetIntensity(float v)
    {
        if (runtimeMat == null) return;
        runtimeMat.SetFloat(IntensityId, v);
        runtimeMat.SetFloat(TimeId, Time.unscaledTime);
    }

    private void JitterTexts(float v)
    {
        for (int i = 0; i < jitterTexts.Length; i++)
        {
            var t = jitterTexts[i];
            if (t == null) continue;

            // 強さに応じた確率で、そのフレームだけ位置をずらす
            if (v > 0.01f && Random.value < v)
            {
                Vector2 offset = new Vector2(
                    Random.Range(-1f, 1f) * textJitterPixels * v,
                    Random.Range(-0.3f, 0.3f) * textJitterPixels * v);
                t.rectTransform.anchoredPosition = textBasePos[i] + offset;
            }
            else
            {
                t.rectTransform.anchoredPosition = textBasePos[i];
            }
        }
    }

    private void ResetTexts()
    {
        if (jitterTexts == null || textBasePos == null) return;
        for (int i = 0; i < jitterTexts.Length; i++)
            if (jitterTexts[i] != null)
                jitterTexts[i].rectTransform.anchoredPosition = textBasePos[i];
    }
}