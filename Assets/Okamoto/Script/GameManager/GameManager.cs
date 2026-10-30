using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("四肢")]
    public LimbController[] limbs;

    [Header("造線管理")]
    public LineManager lineManager;

    [Header("SAN値")]
    public float maxSanity = 100f;
    public float currentSanity = 100f;

    [Tooltip("SAN値が減るほど画面を赤くする演出を使う(SanityScreenEffectを自動で追加)")]
    public bool useSanityScreenEffect = true;

    [Tooltip("画面にSAN値を表示するテキスト(未設定なら自動で作る)")]
    public TMP_Text sanityText;

    [Tooltip("{0}=現在のSAN値、{1}=最大値")]
    public string sanityTextFormat = "SAN {0:F0}/{1:F0}";

    [Header("スコア")]
    public int score = 0;

    [Tooltip("画面に全体のスコアを表示するテキスト")]
    public TMP_Text scoreText;

    [Tooltip("{0}にスコアが入る。D8は8桁のゼロ埋め(例: 00011200)")]
    public string scoreTextFormat = "{0:D8}";

    [Tooltip("Score Textが未設定のとき、画面右上にスコア表示を自動で作る")]
    public bool autoCreateScoreHud = true;

    [Header("クリア")]
    [Tooltip("このスコアに達したらクリア。0以下ならクリアなし")]
    public int clearScore = 50000;

    [Header("リザルト")]
    [Tooltip("リザルト表示のPrefab。Canvasごとでよい。ResultPanelは、ルートでも子でもよい。未設定なら簡易表示を自動で作る")]
    public GameObject resultPanelPrefab;

    [Tooltip("任意: シーン上のCanvas。設定すると、Prefabをこのcanvasの下に出す(Prefab側にCanvasがなくてもよい)。未設定ならPrefabのCanvasで出す")]
    public Canvas resultParentCanvas;

    [Tooltip("ゲームオーバー・クリア中はゲームを止める")]
    public bool pauseOnGameOver = true;

    [Header("スコアランク(スコアがこの値以上でそのランク。どれにも届かなければD)")]
    public int rankS = 50000;
    public int rankA = 35000;
    public int rankB = 20000;
    public int rankC = 10000;

    // ゲームオーバーまたはクリアで終了したか(スポナーなどはこれを見て止まる)
    private bool isGameOver;
    private bool isCleared;

    // 回収したノードの数(レベル1?4の順)
    private readonly int[] benignCollected = new int[4];
    private readonly int[] malignantCollected = new int[4];

    public bool IsGameOver => isGameOver;
    public bool IsCleared => isCleared;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // SAN値が減るほど画面が赤くなる演出(付けていなければ自動で追加)
        if (useSanityScreenEffect &&
            GetComponent<SanityScreenEffect>() == null)
        {
            gameObject.AddComponent<SanityScreenEffect>();
        }

        // リトライ後に止まったままにならないようにする
        Time.timeScale = 1f;
    }

    private void Start()
    {
        if (autoCreateScoreHud)
        {
            if (scoreText == null)
            {
                scoreText = CreateHudText(
                    "ScoreText",
                    new Vector2(1f, 1f),
                    new Vector2(-40f, -30f),
                    TextAlignmentOptions.TopRight,
                    64f
                );
            }

            if (sanityText == null)
            {
                sanityText = CreateHudText(
                    "SanityText",
                    new Vector2(0f, 1f),
                    new Vector2(40f, -30f),
                    TextAlignmentOptions.TopLeft,
                    48f
                );
            }
        }

        UpdateScoreUI();
        UpdateSanityUI();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            Time.timeScale = 1f;
        }
    }

    public void ReduceSanity(float amount)
    {
        if (isGameOver)
        {
            return;
        }

        currentSanity = Mathf.Clamp(
            currentSanity - amount,
            0f,
            maxSanity
        );

        // SAN値が0になってもゲームオーバーにはならない
        // (ゲームオーバーは、すべての四肢の体力が0になったときだけ)
        UpdateSanityUI();
    }

    private void UpdateSanityUI()
    {
        if (sanityText != null)
        {
            sanityText.text = string.Format(
                sanityTextFormat,
                currentSanity,
                maxSanity
            );
        }
    }

    public void AddScore(int amount)
    {
        if (isGameOver)
        {
            return;
        }

        score += amount;

        UpdateScoreUI();

        // スコアがクリア条件に達したらクリア
        if (clearScore > 0 && score >= clearScore)
        {
            Finish(true, "SCORE REACHED " + clearScore);
        }
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = string.Format(scoreTextFormat, score);
        }
    }

    // ノードを回収したことを記録する(リザルトの「取得ノード」用)。
    // スコア加算でクリアになる場合に数え漏れないよう、AddScoreより先に呼ぶ
    public void RecordNodeCollected(bool malignant, int level)
    {
        if (isGameOver)
        {
            return;
        }

        int index = Mathf.Clamp(level - 1, 0, 3);

        if (malignant)
        {
            malignantCollected[index]++;
        }
        else
        {
            benignCollected[index]++;
        }
    }

    public void ReduceLineGauge(float amount)
    {
        if (lineManager == null)
        {
            return;
        }

        lineManager.SetMaxLineGauge(
            Mathf.Max(
                0f,
                lineManager.GetCurrentLineGauge() - amount
            )
        );
    }

    // すべての四肢が欠損したらゲームオーバー
    public void CheckGameOver()
    {
        if (isGameOver)
        {
            return;
        }

        foreach (LimbController limb in limbs)
        {
            if (limb != null && limb.IsAlive)
            {
                return;
            }
        }

        Finish(false, "ALL LIMBS LOST");
    }

    // スコアに応じたランク(S, A, B, C, D)
    public string GetRank(int value)
    {
        if (value >= rankS) return "S";
        if (value >= rankA) return "A";
        if (value >= rankB) return "B";
        if (value >= rankC) return "C";
        return "D";
    }

    // ゲームオーバー・クリアの共通処理
    private void Finish(bool cleared, string reason)
    {
        if (isGameOver)
        {
            return;
        }

        isGameOver = true;
        isCleared = cleared;

        // 原因の確認用に、SAN値と各四肢の体力もログに出す
        string limbInfo = "";

        foreach (LimbController limb in limbs)
        {
            if (limb != null)
            {
                limbInfo += $" [{limb.name} HP {limb.CurrentHP}/{limb.MaxHP}]";
            }
        }

        Debug.Log(
            $"{(cleared ? "Clear" : "Game Over")}: {reason} / SCORE {score} / " +
            $"SAN {currentSanity:F0}/{maxSanity:F0} /{limbInfo}"
        );

        ShowResult(cleared);

        if (pauseOnGameOver)
        {
            Time.timeScale = 0f;
        }
    }

    // リトライボタンから呼ぶ(現在のシーンを読み込み直す)
    public void Retry()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ===== リザルト =====

    private void ShowResult(bool cleared)
    {
        EnsureEventSystem();

        ResultData data = new ResultData
        {
            cleared = cleared,
            score = score,
            rank = GetRank(score),
            benignCounts = (int[])benignCollected.Clone(),
            malignantCounts = (int[])malignantCollected.Clone()
        };

        ResultPanel panel = null;

        if (resultPanelPrefab != null)
        {
            Transform parent = resultParentCanvas != null
                ? resultParentCanvas.transform
                : null;

            GameObject obj = Instantiate(resultPanelPrefab, parent, false);

            // Prefabのルートが非表示でも出せるようにする
            obj.SetActive(true);

            // ResultPanelは、ルートでも子でもよい
            panel = obj.GetComponentInChildren<ResultPanel>(true);

            if (panel == null)
            {
                Debug.LogError(
                    $"GameManager: {resultPanelPrefab.name} に ResultPanel が付いていません。" +
                    "Prefabのルートか子に ResultPanel を付けて、テキストなどを割り当ててください。" +
                    "今回は簡易表示で代わりに出します。", this);

                Destroy(obj);
            }
        }
        else
        {
            Debug.LogWarning(
                "GameManager: Result Panel Prefab が未設定なので、簡易表示を作ります。", this);
        }

        if (panel == null)
        {
            panel = ResultPanelBuilder.Build();
        }

        panel.Show(data);
    }

    // ボタンを押せるようにEventSystemを用意
    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }

    // ===== スコア表示(プレイ中) =====

    private Canvas hudCanvas;

    // 表示を用意していない場合の簡易表示(スコア・SAN値)を作る
    private TMP_Text CreateHudText(
        string objectName,
        Vector2 anchor,
        Vector2 position,
        TextAlignmentOptions alignment,
        float fontSize)
    {
        if (hudCanvas == null)
        {
            GameObject canvasObject = new GameObject("HudCanvas");

            hudCanvas = canvasObject.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(hudCanvas.transform, false);

        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = Color.white;
        label.raycastTarget = false;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = new Vector2(800f, 100f);
        rect.anchoredPosition = position;

        return label;
    }
}