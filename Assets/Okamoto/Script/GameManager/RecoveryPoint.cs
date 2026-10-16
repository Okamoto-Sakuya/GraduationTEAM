
using System;
using UnityEngine;

public class RecoveryPoint : MonoBehaviour
{
    [Header("回収点の名前")]
    public string recoveryPointName;

    [Header("回収点の体力")]
    public int maxHealth = 25;

    [SerializeField]
    private int currentHealth;

    public int CurrentHealth => currentHealth;
    public bool IsDestroyed => isDestroyed;

    // =====================================================
    // 破壊時に生成する基本線
    // =====================================================

    [Header("破壊時に基本線を生成する設定")]
    public bool spawnBasicLineWhenDestroyed;

    [Serializable]
    public class BasicLineSpawnSettings
    {
        [Tooltip("生成する基本線Prefab")]
        public BasicLine basicLinePrefab;

        [Tooltip("生成する基本線の始点")]
        public LinePoint newLinePointA;

        [Tooltip("生成する基本線の終点")]
        public LinePoint newLinePointB;

        [Tooltip("生成する基本線の名前（空欄なら自動設定）")]
        public string lineName;
    }

    [Header("破壊時に生成する基本線リスト")]
    [Tooltip("Sizeを増やすと、生成する基本線を追加できます")]
    public BasicLineSpawnSettings[] basicLinesToSpawn =
        new BasicLineSpawnSettings[1];


    [Header("体力管理コンポーネント")]
    [SerializeField]
    private RecoveryPointHealth health;

    [Header("破壊時に削除する基本線")]
    [Tooltip("この回収地点が破壊された時に削除するBasicLineを指定")]
    [SerializeField]
    private BasicLine[] basicLinesToRemove;


    private bool isDestroyed;

    // =====================================================
    // 初期化
    // =====================================================

    private void Awake()
    {
        currentHealth = Mathf.Max(0, maxHealth);

        LinePoint point = GetComponent<LinePoint>();

        if (point != null)
        {
            point.recoveryPoint = this;
            point.isRecoveryPoint = true;
        }

        if (health == null)
        {
            health = GetComponent<RecoveryPointHealth>();
        }

        if (health == null)
        {
            Debug.LogError(
                $"{gameObject.name}: RecoveryPointHealthが設定されていません。",
                this
            );
        }

    }

    // =====================================================
    // ノードが回収点に到着したときの処理
    // =====================================================

    public void Recover(NodeController node)
    {
        if (node == null || isDestroyed)
            return;

        // Badノードの場合
        if (node.nodeType == NodeController.NodeType.Bad)
        {
            int damage = GetDamageByLevel(node.level);
            int sanityDamage = GetSanityDamageByLevel(node.level);

            // 回収地点のHPを減らす
            TakeDamage(damage);

            // ゲーム全体のSAN値を減らす
            if (GameManager.Instance != null)
            {
                GameManager.Instance.DamageSanity(sanityDamage);
            }

            Debug.Log(
                $"{recoveryPointName}：Bad Lv.{node.level} " +
                $"ダメージ {damage}、SAN減少 {sanityDamage}");

            node.MarkRecovered();
            return;
        }

        // Goodノードは今までどおり回収
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RecoverNode(node.nodeType);
        }

        node.MarkRecovered();
    }

    // =====================================================
    // ノードレベルに応じた回収地点ダメージ
    // =====================================================

    private int GetDamageByLevel(int level)
    {
        switch (level)
        {
            case 1: return 5;
            case 2: return 7;
            case 3: return 10;
            case 4: return 15;
            default:
                Debug.LogWarning($"未対応のノードレベル: {level}");
                return 5;
        }
    }

    // =====================================================
    // ノードレベルに応じたSAN値ダメージ
    // =====================================================

    private int GetSanityDamageByLevel(int level)
    {
        switch (level)
        {
            case 1: return 3;
            case 2: return 5;
            case 3: return 5;
            case 4: return 10;
            default:
                return 3;
        }
    }

    // =====================================================
    // 回収地点のHPを減らす
    // =====================================================


    public void TakeDamage(int damage)
    {
        if (isDestroyed || health == null || health.IsDead || damage <= 0)
            return;

        health.TakeDamage(damage);

        if (health.IsDead)
        {
            DestroyRecoveryPoint();
        }
    }




    /// <summary>
    /// HPが0になった回収地点を破壊する
    /// </summary>

    private void DestroyRecoveryPoint()
    {
        if (isDestroyed)
            return;

        isDestroyed = true;

        LinePoint point = GetComponent<LinePoint>();

        if (point != null)
        {
            point.isRecoveryPoint = false;
            point.recoveryPoint = null;
        }

        // Inspectorで指定された基本線だけを削除
        if (basicLinesToRemove != null)
        {
            foreach (BasicLine line in basicLinesToRemove)
            {
                if (line == null)
                    continue;

                line.UnregisterFromPoints();
                Destroy(line.gameObject);
            }
        }

        // 破壊時に新しい基本線を生成する既存機能
        if (spawnBasicLineWhenDestroyed)
        {
            SpawnBasicLines();
        }

        Debug.Log($"{recoveryPointName} が破壊されました。");

        // この回収地点だけを削除
        Destroy(gameObject);
    }




    // =====================================================
    // 設定された基本線をすべて生成
    // =====================================================

    private void SpawnBasicLines()
    {
        if (basicLinesToSpawn == null ||
            basicLinesToSpawn.Length == 0)
        {
            Debug.LogWarning(
                $"{recoveryPointName}: 生成する基本線が設定されていません。",
                this);
            return;
        }

        int spawnedCount = 0;

        for (int i = 0; i < basicLinesToSpawn.Length; i++)
        {
            BasicLineSpawnSettings settings = basicLinesToSpawn[i];

            if (settings == null)
                continue;

            if (settings.basicLinePrefab == null)
            {
                Debug.LogWarning(
                    $"{recoveryPointName}: 基本線リストのElement {i}にPrefabが設定されていません。",
                    this);
                continue;
            }

            if (settings.newLinePointA == null ||
                settings.newLinePointB == null)
            {
                Debug.LogWarning(
                    $"{recoveryPointName}: 基本線リストのElement {i}に始点または終点が設定されていません。",
                    this);
                continue;
            }

            if (settings.newLinePointA == settings.newLinePointB)
            {
                Debug.LogWarning(
                    $"{recoveryPointName}: 基本線リストのElement {i}の始点と終点が同じです。",
                    this);
                continue;
            }

            // 基本線Prefabを生成
            BasicLine newLine = Instantiate(
                settings.basicLinePrefab,
                Vector3.zero,
                Quaternion.identity);

            // 接続するLinePointを設定
            newLine.pointA = settings.newLinePointA;
            newLine.pointB = settings.newLinePointB;

            // 基本線の名前
            if (!string.IsNullOrEmpty(settings.lineName))
            {
                newLine.lineName = settings.lineName;
            }
            else
            {
                string baseName = string.IsNullOrEmpty(recoveryPointName)
                    ? gameObject.name
                    : recoveryPointName;

                newLine.lineName =
                    $"{baseName}_GeneratedBasicLine_{i + 1}";
            }

            // 生成した線自体から新しいノードを発生させない
            newLine.spawnWeight = 0f;
            newLine.isSpawnSource = false;

            // 線の表示・位置を更新
            newLine.UpdateLine();

            spawnedCount++;

            Debug.Log(
                $"{recoveryPointName}：基本線「{newLine.lineName}」を生成しました。");
        }

        Debug.Log(
            $"{recoveryPointName}：基本線を {spawnedCount} 本生成しました。");
    }
}
