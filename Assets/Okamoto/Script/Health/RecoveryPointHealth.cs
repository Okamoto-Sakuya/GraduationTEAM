
using UnityEngine;

public class RecoveryPointHealth : MonoBehaviour
{
    [Header("‘Ì—ÍÝ’è")]
    [Min(1)]
    public int maxHealth = 25;

    [SerializeField]
    private int currentHealth;

    public int CurrentHealth => currentHealth;
    public bool IsDead => currentHealth <= 0;

    private void Awake()
    {
        currentHealth = Mathf.Max(1, maxHealth);
    }

    public void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0)
            return;

        currentHealth = Mathf.Max(0, currentHealth - damage);

        Debug.Log(
            $"{gameObject.name} HP: {currentHealth}/{maxHealth}"
        );
    }

    public void ResetHealth()
    {
        currentHealth = Mathf.Max(1, maxHealth);
    }
}
