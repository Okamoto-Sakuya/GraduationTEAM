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

    [Header("スコア")]
    public int score = 0;

    [Tooltip("画面に全体のスコアを表示するテキスト")]
    public TMP_Text scoreText;

    [Tooltip("{0}にスコアが入る。D8は8桁のゼロ埋め(例: 00011200)")]
    public string scoreTextFormat = "{0:D8}";

    [Tooltip("Score Textが未設定のとき、画面右上にスコア表示を自動で作る")]
    public bool autoCreateScoreHud = true;

    [Header("ゲームオーバー表示")]
    [Tooltip("ゲームオーバー時に表示するUI。未設定なら簡易的なUIを自動で作る")]
    public GameObject gameOverPanel;

    [Tooltip("スコアを表示するテキスト(任意)")]
    public TMP_Text gameOverScoreText;

    [Tooltip("ゲームオーバーの理由を表示するテキスト(任意)")]
    public TMP_Text gameOverReasonText;

    [Tooltip("{0}にスコアが入る")]
    public string scoreFormat = "SCORE {0:N0}";

    [Tooltip("ゲームオーバー中はゲームを止める")]
    public bool pauseOnGameOver = true;

    private bool isGameOver;

    public bool IsGameOver => isGameOver;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // リトライ後に止まったままにならないようにする
        Time.timeScale = 1f;

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    private void Start()
    {
        if (scoreText == null && autoCreateScoreHud)
        {
            CreateDefaultScoreHud();
        }

        UpdateScoreUI();
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

        if (currentSanity <= 0f)
        {
            GameOver("SAN値が0になった");
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
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = string.Format(scoreTextFormat, score);
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

        GameOver("すべての四肢が欠損した");
    }

    private void GameOver(string reason)
    {
        if (isGameOver)
        {
            return;
        }

        isGameOver = true;

        Debug.Log($"Game Over: {reason}");

        ShowGameOverUI(reason);

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

    // ===== スコア表示(プレイ中) =====

    // スコア表示を用意していない場合の簡易表示(画面右上)
    private void CreateDefaultScoreHud()
    {
        GameObject canvasObject = new GameObject("HudCanvas");

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject textObject = new GameObject("ScoreText");
        textObject.transform.SetParent(canvasObject.transform, false);

        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = 64f;
        label.alignment = TextAlignmentOptions.TopRight;
        label.color = Color.white;
        label.raycastTarget = false;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(800f, 100f);
        rect.anchoredPosition = new Vector2(-40f, -30f);

        scoreText = label;
    }

    // ===== ゲームオーバーUI =====

    private void ShowGameOverUI(string reason)
    {
        if (gameOverPanel == null)
        {
            CreateDefaultGameOverUI();
        }

        if (gameOverScoreText != null)
        {
            gameOverScoreText.text = string.Format(scoreFormat, score);
        }

        if (gameOverReasonText != null)
        {
            gameOverReasonText.text = reason;
        }

        gameOverPanel.SetActive(true);
    }

    // UIを用意していない場合の簡易表示(GAME OVER / スコア / RETRYボタン)
    private void CreateDefaultGameOverUI()
    {
        GameObject canvasObject = new GameObject("GameOverCanvas");

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>();

        // ボタンを押せるようにEventSystemを用意
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        // 画面全体を覆う暗い背景
        GameObject background = new GameObject("Background");
        background.transform.SetParent(canvasObject.transform, false);

        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = new Color(0f, 0f, 0f, 0.75f);

        RectTransform backgroundRect = backgroundImage.rectTransform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        CreateText(background.transform, "GAME OVER", 140, new Vector2(0f, 160f));

        gameOverScoreText = CreateText(
            background.transform, "", 64, new Vector2(0f, 20f));

        // リトライボタン
        GameObject buttonObject = new GameObject("RetryButton");
        buttonObject.transform.SetParent(background.transform, false);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.25f, 0.25f, 0.3f, 1f);

        RectTransform buttonRect = buttonImage.rectTransform;
        buttonRect.sizeDelta = new Vector2(360f, 100f);
        buttonRect.anchoredPosition = new Vector2(0f, -140f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(Retry);

        CreateText(buttonObject.transform, "RETRY", 52, Vector2.zero);

        gameOverPanel = background;
    }

    private TMP_Text CreateText(
        Transform parent,
        string text,
        float fontSize,
        Vector2 position)
    {
        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;

        RectTransform rect = label.rectTransform;
        rect.sizeDelta = new Vector2(1400f, 200f);
        rect.anchoredPosition = position;

        return label;
    }
}