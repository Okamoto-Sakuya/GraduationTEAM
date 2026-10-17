
using UnityEngine;
using TMPro;

public class RecoveryPointHealth : MonoBehaviour
{
    [Header("部位設定")]
    public string partName = "右足";

    [Header("体力設定")]
    [Min(1)]
    public int maxHealth = 25;

    [SerializeField]
    private int currentHealth;

    [Header("体力表示UI")]
    [SerializeField]
    private TMP_Text healthText;

    public int CurrentHealth => currentHealth;
    public bool IsDead => currentHealth <= 0;

    private void Awake()
    {
        currentHealth = Mathf.Max(1, maxHealth);
        RefreshHealthUI();
    }

    public void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0)
            return;

        currentHealth = Mathf.Max(0, currentHealth - damage);

        // HP表示を更新
        RefreshHealthUI();

        Debug.Log(
            $"[部位HP] 部位={partName}, " +
            $"Object={gameObject.name}, " +
            $"HP={currentHealth}/{maxHealth}"
        );

        // 部位の体力が変化したので、全部位の状態を確認
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CheckAllPartsDestroyed();
        }
    }

    public void ResetHealth()
    {
        currentHealth = Mathf.Max(1, maxHealth);
        RefreshHealthUI();
    }

    private void RefreshHealthUI()
    {
        if (healthText != null)
        {
            healthText.text =
                $"{partName} {currentHealth}/{maxHealth}";
        }
    }
}
