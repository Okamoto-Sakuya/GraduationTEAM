using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("ゲーム設定")]
    public int initialHealth = 8;
    public int scorePerGoodNode = 100;
    public int scorePerBadNode = -100;
    public int damagePerBadNode = 1;

    public int Score { get; private set; }
    public int Health { get; private set; }
    public bool IsGameOver { get; private set; }

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
        IsGameOver = false;
    }

    public void RecoverNode(NodeController.NodeType type)
    {
        if (IsGameOver) return;

        if (type == NodeController.NodeType.Good)
        {
            Score += scorePerGoodNode;
        }
        else
        {
            Score += scorePerBadNode;
            Health -= damagePerBadNode;
            Health = Mathf.Max(0, Health);
        }

        RefreshUI();

        if (Health <= 0)
        {
            IsGameOver = true;
            Debug.Log("ゲームオーバー");
        }
    }

    private void RefreshUI()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateUI(Score, Health);
        }
    }
}