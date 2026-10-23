using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 1つのパネル(Mother/Daughter)ぶんの「4ステージ + もや」を管理する。
/// パネルごとに1つ付け、saveKey を別々にすると進行が独立する。
/// パネルが表示されるたびにセーブ済みの進行を読み込み、もやをじわっと晴らす。
/// </summary>
public class FogStageController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] RawImage fogImage;                      // FogOverlay マテリアルを付けた RawImage
    [SerializeField] Canvas canvas;                          // 空なら親から自動取得
    [SerializeField] Button[] stageButtons = new Button[4];  // Stage1(左上) Stage2(右上) Stage3(左下) Stage4(右下)

    [Header("進行の保存")]
    [SerializeField] string saveKey = "Mother";              // パネルごとに別の名前にする（例: Mother / Daughter）

    [Header("もやの設定")]
    [SerializeField, Range(0.1f, 0.8f)] float holeRadius = 0.32f;
    [SerializeField] float openSpeed = 0.8f;
    [Range(0f, 1f)] public float centerFog = 1f;             // 中央のもや（ヒガンバナ演出で下げる用）

    [Header("イベント")]
    public UnityEvent onAllStagesCleared;                    // Stage4クリア済みでパネルを開いた時 / 今クリアした時

    const string KeyPrefix = "FogStage.Unlocked.";
    static readonly int HolesId = Shader.PropertyToID("_Holes");
    static readonly int AspectId = Shader.PropertyToID("_Aspect");
    static readonly int CenterFogId = Shader.PropertyToID("_CenterFog");

    Material mat;
    readonly Vector4[] holes = new Vector4[4];
    readonly float[] open = new float[4];
    int unlockedCount = 1; // 1?4 = 解放済みステージ数 / 5 = 全クリア

    // ---------- ゲームシーン側から呼べる（このコンポーネントが無いシーンでもOK） ----------
    /// <summary>ステージをクリアしたと保存する。例: FogStageController.MarkCleared("Mother", 2);</summary>
    public static void MarkCleared(string key, int stage)
    {
        int next = Mathf.Clamp(stage, 1, 4) + 1;
        if (next > PlayerPrefs.GetInt(KeyPrefix + key, 1))
        {
            PlayerPrefs.SetInt(KeyPrefix + key, next);
            PlayerPrefs.Save();
        }
    }
    // -----------------------------------------------------------------------------------

    void Awake()
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        mat = new Material(fogImage.material);   // アセットを書き換えないようコピー
        fogImage.material = mat;
        fogImage.raycastTarget = false;
    }

    void OnEnable()
    {
        // パネルが開くたびに、セーブを読み直して、もやを閉じた状態からじわっと晴らす
        unlockedCount = Mathf.Clamp(PlayerPrefs.GetInt(KeyPrefix + saveKey, 1), 1, 5);
        for (int i = 0; i < 4; i++) open[i] = 0f;
        centerFog = 1f;
        RefreshButtons();
        if (unlockedCount >= 5) onAllStagesCleared?.Invoke();
    }

    void Update()
    {
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        for (int i = 0; i < 4; i++)
        {
            float target = i < unlockedCount ? 1f : 0f;
            open[i] = Mathf.MoveTowards(open[i], target, openSpeed * Time.unscaledDeltaTime);

            Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, stageButtons[i].transform.position);
            holes[i] = new Vector4(sp.x / Screen.width, sp.y / Screen.height, holeRadius, Mathf.SmoothStep(0f, 1f, open[i]));
        }

        mat.SetVectorArray(HolesId, holes);
        mat.SetFloat(AspectId, (float)Screen.width / Screen.height);
        mat.SetFloat(CenterFogId, centerFog);

        // 1キーでStage1クリア → Stage2周りのもやが晴れる
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            OnStageCleared(1);
        }
        // 2キーでStage1クリア → Stage3周りのもやが晴れる
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            OnStageCleared(2);
        }
        // 3キーでStage1クリア → Stage4周りのもやが晴れる
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            OnStageCleared(3);
        }
    }

    /// <summary>このパネルでステージnをクリアしたときに呼ぶ（1?4）。</summary>
    public void OnStageCleared(int stage)
    {
        MarkCleared(saveKey, stage);
        unlockedCount = Mathf.Clamp(PlayerPrefs.GetInt(KeyPrefix + saveKey, 1), 1, 5);
        RefreshButtons();
        if (unlockedCount >= 5) onAllStagesCleared?.Invoke();
    }

    void RefreshButtons()
    {
        for (int i = 0; i < stageButtons.Length; i++)
            stageButtons[i].interactable = i < unlockedCount;
    }

    [ContextMenu("Reset Progress")]
    public void ResetProgress()
    {
        PlayerPrefs.DeleteKey(KeyPrefix + saveKey);
        PlayerPrefs.Save();
        if (!isActiveAndEnabled) return;
        unlockedCount = 1;          // 表示中なら、その場で反映する
        centerFog = 1f;
        RefreshButtons();
    }
}