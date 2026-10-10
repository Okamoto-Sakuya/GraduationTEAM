using System.Collections;
using UnityEngine;

public class NodeSpawner : MonoBehaviour
{
    [Header("出現する基本線（6本）")]
    public BasicLine[] basicLines = new BasicLine[6];

    [Header("ノードPrefab")]
    public NodeController nodePrefab;

    [Header("出現設定")]
    [Min(0.1f)]
    public float spawnInterval = 2f;

    [Range(0f, 1f)]
    public float badNodeChance = 0.35f;

    [Header("出現確率の調整")]
    [Min(0f)]
    public float weightIncrease = 0.5f;

    [Range(0.01f, 1f)]
    public float selectedWeightMultiplier = 0.5f;

    [Min(0.01f)]
    public float minimumWeight = 0.1f;

    private float[] weights;
    private Coroutine spawnCoroutine;

    private void Start()
    {
        if (basicLines == null || basicLines.Length == 0 ||
            nodePrefab == null)
        {
            Debug.LogError(
                "NodeSpawner：基本線またはノードPrefabが未設定です。",
                this);

            enabled = false;
            return;
        }

        weights = new float[basicLines.Length];

        for (int i = 0; i < basicLines.Length; i++)
        {
            weights[i] = basicLines[i] != null
                ? Mathf.Max(minimumWeight, basicLines[i].spawnWeight)
                : 0f;
        }

        spawnCoroutine = StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            if (GameManager.Instance == null ||
                !GameManager.Instance.IsGameOver)
            {
                SpawnNode();
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    // =====================================================
    // Nodeの出現
    // Is Recovery PointにチェックがないPoint Aだけを候補にする
    // =====================================================
    private void SpawnNode()
    {
        float totalWeight = 0f;

        // 出現可能な基本線の重みを合計
        for (int i = 0; i < basicLines.Length; i++)
        {
            if (!CanSpawnAt(i))
                continue;

            totalWeight += weights[i];
        }

        // 出現できる場所がない
        if (totalWeight <= 0f)
            return;

        // 重み付きランダムで基本線を選択
        float randomValue = Random.value * totalWeight;
        float cumulativeWeight = 0f;
        int selectedIndex = -1;

        for (int i = 0; i < basicLines.Length; i++)
        {
            if (!CanSpawnAt(i))
                continue;

            cumulativeWeight += weights[i];

            if (randomValue <= cumulativeWeight)
            {
                selectedIndex = i;
                break;
            }
        }

        if (selectedIndex < 0)
            return;

        BasicLine selectedLine = basicLines[selectedIndex];

        // Point Aの位置からNodeを出現させる
        NodeController node = Instantiate(
            nodePrefab,
            selectedLine.pointA.transform.position,
            Quaternion.identity);

        // 良性・悪性をランダムで決定
        NodeController.NodeType type =
            Random.value < badNodeChance
                ? NodeController.NodeType.Bad
                : NodeController.NodeType.Good;

        node.Initialize(selectedLine, type);

        // 選ばれた基本線の出現確率を下げる
        weights[selectedIndex] = Mathf.Max(
            minimumWeight,
            weights[selectedIndex] * selectedWeightMultiplier);

        // 選ばれなかった基本線の出現確率を上げる
        for (int i = 0; i < weights.Length; i++)
        {
            if (i == selectedIndex || basicLines[i] == null)
                continue;

            weights[i] += weightIncrease;
        }
    }

    // =====================================================
    // 出現可能な場所か確認
    // =====================================================
    private bool CanSpawnAt(int index)
    {
        if (index < 0 || index >= basicLines.Length)
            return false;

        BasicLine line = basicLines[index];

        if (line == null ||
            line.pointA == null ||
            line.pointB == null)
        {
            return false;
        }

        // Point Aにチェックが入っている場合は出現禁止
        if (line.pointA.isRecoveryPoint)
            return false;

        return true;
    }

    public float GetWeight(int index)
    {
        if (weights == null ||
            index < 0 ||
            index >= weights.Length)
        {
            return 0f;
        }

        return weights[index];
    }
}