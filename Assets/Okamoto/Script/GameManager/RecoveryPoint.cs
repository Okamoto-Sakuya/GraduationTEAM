using System;
using UnityEngine;

public class RecoveryPoint : MonoBehaviour
{
    [Header("回収点の名前")]
    public string recoveryPointName;

    [Header("体力管理コンポーネント")]
    [SerializeField]
    private RecoveryPointHealth health;

    public int CurrentHealth => health != null ? health.CurrentHealth : 0;
    public bool IsDestroyed => isDestroyed;

    // =====================================================
    // 破壊時に削除する基本線
    // =====================================================

    [Header("破壊時に削除する基本線")]
    [Tooltip("この回収地点のHPが0になったとき削除するBasicLine")]
    [SerializeField]
    private BasicLine[] basicLinesToRemove;

    // =====================================================
    // 破壊対象のLinePoint
    // =====================================================

    [Header("破壊する対象のLinePoint")]
    [Tooltip("指定すると、そのLinePointのGameObjectを削除する。空欄なら自分自身のLinePoint")]
    [SerializeField]
    private LinePoint targetLinePoint;

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

        [Tooltip("基本線の名前")]
        public string lineName;
    }

    [Header("破壊時に生成する基本線リスト")]
    public BasicLineSpawnSettings[] basicLinesToSpawn =
        new BasicLineSpawnSettings[1];

    private bool isDestroyed;

    // =====================================================
    // 初期化
    // =====================================================

    private void Awake()
    {
        if (health == null)
        {
            health = GetComponent<RecoveryPointHealth>();
        }

        if (health == null)
        {
            Debug.LogError(
                $"{gameObject.name}: RecoveryPointHealthがありません。",
                this
            );
        }

        LinePoint point = GetComponent<LinePoint>();

        if (point != null)
        {
            point.recoveryPoint = this;
            point.isRecoveryPoint = true;
        }
    }

    // =====================================================
    // ノードが回収点に到着したときの処理
    // =====================================================

    public void Recover(NodeController node)
    {
        if (node == null || isDestroyed)
            return;

        if (node.nodeType == NodeController.NodeType.Bad)
        {
            int damage = GetDamageByLevel(node.level);
            int sanityDamage = GetSanityDamageByLevel(node.level);

            TakeDamage(damage);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.DamageSanity(sanityDamage);
            }

            Debug.Log(
                $"{recoveryPointName}：Bad Lv.{node.level} " +
                $"ダメージ {damage}、SAN減少 {sanityDamage}"
            );

            node.MarkRecovered();
            return;
        }

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

            default: return 3;
        }
    }
// =====================================================
// 回収地点のHPを減らす
// =====================================================

public void TakeDamage(int damage)
    {
        if (isDestroyed)
            return;

        if (health == null)
        {
            Debug.LogError(
                $"{recoveryPointName}: RecoveryPointHealthが設定されていません。",
                this
            );
            return;
        }

        if (damage <= 0)
            return;

        // すでにHPが0なら、破壊処理だけ実行
        if (health.IsDead)
        {
            DestroyRecoveryPoint();
            return;
        }

        // HPを減らす
        health.TakeDamage(damage);

        Debug.Log(
            $"[回収点ダメージ] {recoveryPointName} " +
            $"HP={health.CurrentHealth}/{health.maxHealth}",
            this
        );

        // HPが0になったら破壊処理
        if (health.IsDead)
        {
            Debug.Log(
                $"[回収点破壊開始] {recoveryPointName}",
                this
            );

            DestroyRecoveryPoint();
        }
    }


    // =====================================================
    // HPが0になったときの破壊処理
    // =====================================================

    private void DestroyRecoveryPoint()
    {
        if (isDestroyed)
            return;

        // 破壊処理の重複実行を防止
        isDestroyed = true;

        // この部位自身のLinePointだけを取得
        LinePoint ownPoint = GetComponent<LinePoint>();

        // Inspectorで指定された削除対象だけを削除
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

        // 自分のLinePointだけを無効化
        if (ownPoint != null)
        {
            ownPoint.isRecoveryPoint = false;
            ownPoint.recoveryPoint = null;
        }

        // 破壊時に生成する線をすべて生成
        if (spawnBasicLineWhenDestroyed)
        {
            SpawnBasicLines();
        }

        Debug.Log($"{recoveryPointName} が破壊されました。");

        // 自分の部位だけを削除
        Destroy(gameObject);
    }


    // =====================================================
    // 設定された基本線を生成
    // =====================================================

    private void SpawnBasicLines()
    {
        if (basicLinesToSpawn == null ||
            basicLinesToSpawn.Length == 0)
        {
            Debug.LogWarning(
                $"{recoveryPointName}: 生成する基本線が設定されていません。",
                this
            );
            return;
        }

        int spawnedCount = 0;

        for (int i = 0; i < basicLinesToSpawn.Length; i++)
        {
            BasicLineSpawnSettings settings = basicLinesToSpawn[i];

            if (settings == null ||
                settings.basicLinePrefab == null ||
                settings.newLinePointA == null ||
                settings.newLinePointB == null ||
                settings.newLinePointA == settings.newLinePointB)
            {
                Debug.LogWarning(
                    $"{recoveryPointName}: 基本線リストのElement {i}の設定を確認してください。",
                    this
                );
                continue;
            }

            BasicLine newLine = Instantiate(
                settings.basicLinePrefab,
                Vector3.zero,
                Quaternion.identity
            );

            newLine.pointA = settings.newLinePointA;
            newLine.pointB = settings.newLinePointB;

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

            newLine.spawnWeight = 0f;
            newLine.isSpawnSource = false;

            // 新しい線を両端のLinePointに登録
            // TrackEdgeのRegisterPoints()はprotectedなので、
            // BasicLine側に公開用メソッドが必要
            newLine.RegisterPointsForRuntime();

            spawnedCount++;

            Debug.Log(
                $"{recoveryPointName}：基本線「{newLine.lineName}」を生成しました。"
            );
        }

        Debug.Log(
            $"{recoveryPointName}：基本線を {spawnedCount} 本生成しました。"
        );
    }
}