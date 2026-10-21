using UnityEngine;
using UnityEngine.SceneManagement;

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

    [Header("ゲームオーバー")]
    public string gameOverSceneName = "GameOver";

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
            GameOver();
        }
    }

    public void AddScore(int amount)
    {
        score += amount;
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

        GameOver();
    }

    private void GameOver()
    {
        if (isGameOver)
        {
            return;
        }

        isGameOver = true;

        Debug.Log("Game Over");

        SceneManager.LoadScene(gameOverSceneName);
    }
}