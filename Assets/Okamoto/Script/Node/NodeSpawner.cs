using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NodeSpawner : MonoBehaviour
{
    [System.Serializable]
    public class SpawnPoint
    {
        [Header("出現する基本線")]
        public BasicLine basicLine;

        [Header("出現確率の重み")]
        [Min(0.01f)]
        public float weight = 1f;

        [HideInInspector]
        public float currentWeight;

        [HideInInspector]
        public int spawnCount;
    }

    [Header("ノードのPrefab")]
    [SerializeField] private GameObject nodePrefab;

    [Header("出現候補")]
    [SerializeField]
    private List<SpawnPoint> spawnPoints =
        new List<SpawnPoint>();

    [Header("出現設定")]
    [SerializeField] private float spawnInterval = 3f;
    [SerializeField] private int maxNodeCount = 10;
    [SerializeField] private bool spawnOnStart = true;

    [Header("出現確率の調整")]
    [Tooltip("出現後、その場所の重みを何倍にするか")]
    [Range(0.01f, 1f)]
    [SerializeField] private float weightReductionRate = 0.5f;

    [Tooltip("出現していない場所の重みを増やす量")]
    [Min(0f)]
    [SerializeField] private float unusedWeightIncrease = 0.2f;

    private readonly List<GameObject> activeNodes =
        new List<GameObject>();

    private Coroutine spawnCoroutine;

    private void Start()
    {
        // 初期重みを設定
        foreach (SpawnPoint point in spawnPoints)
        {
            if (point != null)
            {
                point.currentWeight = point.weight;
                point.spawnCount = 0;
            }
        }

        if (spawnOnStart)
        {
            spawnCoroutine = StartCoroutine(SpawnLoop());
        }
    }

    private IEnumerator SpawnLoop()
    {
        // 最初のノードを生成
        TrySpawnNode();

        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);

            // 最大数を超えないようにする
            RemoveNullNodes();

            if (activeNodes.Count < maxNodeCount)
            {
                TrySpawnNode();
            }
        }
    }

    // ノードを1体生成
    public void TrySpawnNode()
    {
        if (nodePrefab == null)
        {
            Debug.LogWarning("NodeSpawner：Node Prefabが設定されていません。");
            return;
        }

        RemoveNullNodes();

        if (activeNodes.Count >= maxNodeCount)
        {
            return;
        }

        SpawnPoint selectedPoint = SelectSpawnPoint();

        if (selectedPoint == null || selectedPoint.basicLine == null)
        {
            Debug.LogWarning("NodeSpawner：有効な出現場所がありません。");
            return;
        }

        BasicLine basicLine = selectedPoint.basicLine;

        if (!basicLine.isActive || basicLine.startPoint == null)
        {
            return;
        }

        // 指定したBasicLineのStart Pointから生成
        GameObject node = Instantiate(
            nodePrefab,
            basicLine.startPoint.position,
            Quaternion.identity
        );

        NodeController controller = node.GetComponent<NodeController>();

        if (controller != null)
        {
            controller.SetSpawnBasicLine(basicLine);
        }

        activeNodes.Add(node);

        // 選ばれた場所の重みを下げる
        selectedPoint.currentWeight = Mathf.Max(
            0.01f,
            selectedPoint.currentWeight * weightReductionRate
        );

        selectedPoint.spawnCount++;

        // 選ばれなかった場所の重みを上げる
        foreach (SpawnPoint point in spawnPoints)
        {
            if (point == null || point == selectedPoint)
            {
                continue;
            }

            if (point.basicLine == null || !point.basicLine.isActive)
            {
                continue;
            }

            point.currentWeight += unusedWeightIncrease;
        }

        Debug.Log(
            $"ノード出現：{basicLine.name} / " +
            $"出現回数：{selectedPoint.spawnCount}"
        );
    }

    // 重み付きランダムで出現位置を選択
    private SpawnPoint SelectSpawnPoint()
    {
        List<SpawnPoint> candidates = new List<SpawnPoint>();

        float totalWeight = 0f;

        foreach (SpawnPoint point in spawnPoints)
        {
            if (point == null || point.basicLine == null)
            {
                continue;
            }

            if (!point.basicLine.isActive ||
                point.basicLine.startPoint == null)
            {
                continue;
            }

            float weight = Mathf.Max(0.01f, point.currentWeight);

            candidates.Add(point);
            totalWeight += weight;
        }

        if (candidates.Count == 0 || totalWeight <= 0f)
        {
            return null;
        }

        float randomValue = Random.Range(0f, totalWeight);

        foreach (SpawnPoint point in candidates)
        {
            randomValue -= Mathf.Max(0.01f, point.currentWeight);

            if (randomValue <= 0f)
            {
                return point;
            }
        }

        return candidates[candidates.Count - 1];
    }

    // 削除済みノードをリストから除去
    private void RemoveNullNodes()
    {
        activeNodes.RemoveAll(node => node == null);
    }

    // 外部からノードを1体生成したい場合に使用
    public void SpawnOneNode()
    {
        TrySpawnNode();
    }

    // 自動生成を停止
    public void StopSpawning()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }

    // 自動生成を再開
    public void ResumeSpawning()
    {
        if (spawnCoroutine == null)
        {
            spawnCoroutine = StartCoroutine(SpawnLoop());
        }
    }
}
