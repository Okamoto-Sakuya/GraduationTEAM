using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("スコア・体力")]
    public TMP_Text scoreText;
    public TMP_Text healthText;
    public TMP_Text gameOverText;

    [Header("造線ゲージ")]
    public Slider wireGauge;

    private void Awake()
    {
        Instance = this;

        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            UpdateUI(
                GameManager.Instance.Score,
                GameManager.Instance.Health);
        }
    }

    private void Update()
    {
        if (GameManager.Instance != null &&
            GameManager.Instance.IsGameOver &&
            gameOverText != null &&
            !gameOverText.gameObject.activeSelf)
        {
            gameOverText.gameObject.SetActive(true);
        }
    }

    public void UpdateUI(int score, int health)
    {
        if (scoreText != null)
        {
            scoreText.text = $"SCORE: {score:D6}";
        }

        if (healthText != null)
        {
            healthText.text = $"HP: {health}";
        }
    }

    public void UpdateWireGauge(float normalizedValue)
    {
        if (wireGauge != null)
        {
            wireGauge.value = Mathf.Clamp01(normalizedValue);
        }
    }
}