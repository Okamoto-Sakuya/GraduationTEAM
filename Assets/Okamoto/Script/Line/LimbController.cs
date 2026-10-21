using UnityEngine;
using UnityEngine.Events;

public class LimbController : MonoBehaviour
{
    public enum LimbType
    {
        RightArm,
        LeftArm,
        RightLeg,
        LeftLeg
    }

    [Header("四肢の種類")]
    [SerializeField] private LimbType limbType;

    [Header("HP設定")]
    [SerializeField] private int maxHP = 25;
    [SerializeField] private int currentHP = 25;

    [Header("破壊時の設定")]
    [SerializeField] private GameObject destroyEffectPrefab;
    [SerializeField] private UnityEvent onLimbDestroyed;

    public int CurrentHP => currentHP;
    public int MaxHP => maxHP;
    public LimbType Type => limbType;

    public bool IsDestroyed { get; private set; }

    public bool IsAlive => !IsDestroyed;

    public System.Action<LimbController> OnDestroyed;

    private void Awake()
    {
        maxHP = Mathf.Max(1, maxHP);
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        IsDestroyed = currentHP <= 0;
    }

    // ダメージを受ける
    public void TakeDamage(int damage)
    {
        if (IsDestroyed || damage <= 0)
        {
            return;
        }

        currentHP = Mathf.Max(0, currentHP - damage);

        Debug.Log(
            $"{gameObject.name} がダメージを受けました。" +
            $" HP：{currentHP}/{maxHP}"
        );

        if (currentHP <= 0)
        {
            DestroyLimb();
        }
    }

    // HPを回復する
    public void Heal(int amount)
    {
        if (IsDestroyed || amount <= 0)
        {
            return;
        }

        currentHP = Mathf.Min(maxHP, currentHP + amount);
    }

    // 四肢が破壊されたとき
    private void DestroyLimb()
    {
        if (IsDestroyed)
        {
            return;
        }

        IsDestroyed = true;

        Debug.Log($"{gameObject.name} が破壊されました。");
        OnDestroyed?.Invoke(this);

        if (destroyEffectPrefab != null)
        {
            Instantiate(
                destroyEffectPrefab,
                transform.position,
                Quaternion.identity
            );
        }

        onLimbDestroyed?.Invoke();
    }
}