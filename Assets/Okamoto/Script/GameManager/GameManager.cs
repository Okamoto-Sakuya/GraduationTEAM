
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("ゲーム設定")]
    public int initialHealth = 8;
    public int scorePerGoodNode = 100;
    public int scorePerBadNode = -100;
    public int damagePerBadNode = 1;

    [Header("SAN値設定")]
    public int initialSanity = 100;

    [Header("体力Image UI")]
    public HealthImageUI healthImageUI;

    [Header("全部位破壊時のテキスト")]
    public TMP_Text allPartsDestroyedText;

    private RecoveryPointHealth[] allRecoveryPoints;
    private bool allPartsDestroyedShown;

    public int Score { get; private set; }
    public int Health { get; private set; }
    public int Sanity { get; private set; }
    public bool IsGameOver { get; private set; }

    public bool IsSanityDepleted => Sanity <= 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        Score = 0;
        Health = initialHealth;
        Sanity = Mathf.Max(0, initialSanity);
        IsGameOver = false;
    }

    private void Start()
    {
        if (healthImageUI != null)
        {
            healthImageUI.ResetHealth(Health);
        }

        RefreshUI();

        // 全部位の体力を確認する対象を取得
        allRecoveryPoints =
            FindObjectsByType<RecoveryPointHealth>(
                FindObjectsSortMode.None);

        // 最初はテキストを非表示
        if (allPartsDestroyedText != null)
        {
            allPartsDestroyedText.gameObject.SetActive(false);
        }

        allPartsDestroyedShown = false;
        CheckAllPartsDestroyed();
    }

    public void RecoverNode(NodeController.NodeType type)
    {
        if (IsGameOver)
            return;

        if (type == NodeController.NodeType.Good)
        {
            Score += scorePerGoodNode;
        }
        else
        {
            Score += scorePerBadNode;

            Health -= damagePerBadNode;
            Health = Mathf.Max(0, Health);

            if (healthImageUI != null)
            {
                healthImageUI.UpdateHealth(Health);
            }
        }

        RefreshUI();

        if (Health <= 0)
        {
            IsGameOver = true;
            Debug.Log("ゲームオーバー");
        }
    }

    /// <summary>
    /// Badノードが回収地点に到達したときにSAN値を減らす。
    /// 回収地点HPへのダメージはRecoveryPoint側で処理する。
    /// </summary>
    public void DamageSanity(int amount)
    {
        if (IsGameOver || amount <= 0)
            return;

        Sanity = Mathf.Max(0, Sanity - amount);

        Debug.Log($"SAN値: {Sanity}/{initialSanity}");

        if (Sanity <= 0)
        {
            Debug.Log("SAN値が0になりました。");
        }
    }

    public void ResetSanity()
    {
        Sanity = Mathf.Max(0, initialSanity);
        Debug.Log($"SAN値をリセットしました: {Sanity}");
    }

    private void RefreshUI()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateUI(Score, Health);
        }
    }
    // =====================================================
    // 全部位の体力を確認
    // =====================================================

    public void CheckAllPartsDestroyed()
    {
        if (allPartsDestroyedShown)
            return;

        if (allRecoveryPoints == null ||
            allRecoveryPoints.Length == 0)
        {
            return;
        }

        foreach (RecoveryPointHealth part in allRecoveryPoints)
        {
            // まだ存在していて、HPが残っている部位がある
            if (part != null && !part.IsDead)
            {
                return;
            }
        }

        // すべての部位が破壊された
        allPartsDestroyedShown = true;

        if (allPartsDestroyedText != null)
        {
            allPartsDestroyedText.gameObject.SetActive(true);
            allPartsDestroyedText.text = "すべての部位が破壊されました";
        }

        Debug.Log("全部位が破壊されました！");
    }
}
