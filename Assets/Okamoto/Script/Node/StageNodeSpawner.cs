using System.Collections.Generic;
using UnityEngine;

// 4つのシートに従ってノードを出現させる
//  ステージの表   : 経過時間ごとの 出現グループ・出現頻度・スピード
//  ノードグループ : グループの中から抽選して、ノードの種類(良性/悪性/2つ同時)を決める
//  左右の表       : 出現するサイドを抽選(同じサイドに偏らないようにする)
//  基本線の表     : サイドの中で、出現する基本線を抽選(同じ基本線に偏らないようにする)
public class StageNodeSpawner : MonoBehaviour
{
    private enum Side
    {
        Left,
        Right
    }

    [Header("ノード")]
    [SerializeField] private GameObject nodePrefab;

    [Header("出現する基本線(画面外から出現する線)")]
    [Tooltip("左側の基本線(例: 左上・左中間・左下)")]
    [SerializeField] private BasicLine[] leftLines;

    [Tooltip("右側の基本線(例: 右上・右中間・右下)")]
    [SerializeField] private BasicLine[] rightLines;

    [Header("ステージ")]
    [SerializeField] private int stage = 1;
    [SerializeField] private bool startOnStart = true;

    [Header("データ(未設定なら内蔵の初期データを使う)")]
    [Tooltip("基本線の表: id, count, sameline, differentline")]
    [SerializeField] private TextAsset baselineTable;

    [Tooltip("左右の表: id, count, same, opposite")]
    [SerializeField] private TextAsset sideTable;

    [Tooltip("ノードグループの表: id, nodegroup, nodetype")]
    [SerializeField] private TextAsset nodeGroupTable;

    [Tooltip("ステージの表: stage, time, level, nodegroup, rate, speed")]
    [SerializeField] private TextAsset stageTable;

    [Header("調整")]
    [Tooltip("rate(出現頻度)は、この秒数あたりの出現数。10なら60秒に10個")]
    [SerializeField] private float rateUnitSeconds = 60f;

    [Tooltip("speed × この値 = ノードの移動速度。10なら1.0")]
    [SerializeField] private float speedScale = 0.1f;

    [Tooltip("画面上のノードがこの数以上なら、新しく出さない")]
    [SerializeField] private int maxNodeCount = 30;

    // 読み込んだデータ
    private List<CountRow> baselineRows;
    private List<CountRow> sideRows;
    private List<NodeRow> nodeRows;
    private List<StageRow> stageRows;

    // 進行状況
    private bool running;
    private float elapsed;
    private float timer;

    // 左右・基本線の連続回数
    private bool hasLastSide;
    private Side lastSide;
    private int sideStreak;

    private BasicLine lastLine;
    private int lineStreak;

    // 外部から参照する用
    public int CurrentLevel { get; private set; }
    public float Elapsed => elapsed;

    private void Awake()
    {
        baselineRows = SpawnTables.ParseCountRows(
            baselineTable != null ? baselineTable.text : SpawnTableDefaults.Baseline);

        sideRows = SpawnTables.ParseCountRows(
            sideTable != null ? sideTable.text : SpawnTableDefaults.Side);

        nodeRows = SpawnTables.ParseNodeRows(
            nodeGroupTable != null ? nodeGroupTable.text : SpawnTableDefaults.NodeGroup);

        stageRows = SpawnTables.ParseStageRows(
            stageTable != null ? stageTable.text : SpawnTableDefaults.Stage);
    }

    private void Start()
    {
        if (startOnStart)
        {
            StartStage(stage);
        }
    }

    // ステージを最初から始める
    public void StartStage(int stageNumber)
    {
        stage = stageNumber;
        elapsed = 0f;
        timer = 0f;

        hasLastSide = false;
        sideStreak = 0;
        lastLine = null;
        lineStreak = 0;

        running = true;
    }

    public void StopSpawning()
    {
        running = false;
    }

    private void Update()
    {
        if (!running)
        {
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
        {
            return;
        }

        elapsed += Time.deltaTime;

        StageRow row = GetCurrentRow();

        if (row == null)
        {
            return;
        }

        CurrentLevel = row.level;

        timer -= Time.deltaTime;

        if (timer > 0f)
        {
            return;
        }

        SpawnWave(row);

        // rate個 / rateUnitSeconds秒 の間隔で出現させる
        float interval = row.rate > 0
            ? rateUnitSeconds / row.rate
            : 1f;

        timer += interval;

        if (timer < 0f)
        {
            timer = interval;
        }
    }

    // 経過時間に合う、ステージの表の行(timeは0.1秒単位。100=10.0秒)
    private StageRow GetCurrentRow()
    {
        StageRow current = null;

        foreach (StageRow row in stageRows)
        {
            if (row.stage != stage)
            {
                continue;
            }

            if (row.time * 0.1f <= elapsed &&
                (current == null || row.time >= current.time))
            {
                current = row;
            }
        }

        return current;
    }

    // ===== 出現 =====

    private void SpawnWave(StageRow row)
    {
        NodeRow picked = PickNodeRow(row.group);

        if (picked == null)
        {
            Debug.LogWarning(
                $"ノードグループ {row.group} のデータがありません。", this);
            return;
        }

        // 1,3=良性 / 2,4=悪性 / 3,4=2つ同時
        bool malignant = picked.type == 2 || picked.type == 4;
        int count = (picked.type == 3 || picked.type == 4) ? 2 : 1;

        if (CountAliveNodes() + count > maxNodeCount)
        {
            return;
        }

        BasicLine firstLine = null;

        for (int i = 0; i < count; i++)
        {
            // 2つ目は、1つ目と別の基本線に出す
            BasicLine line = PickLine(firstLine);

            if (line == null)
            {
                break;
            }

            SpawnNode(line, malignant, row.speed);

            if (i == 0)
            {
                firstLine = line;
            }
        }
    }

    // グループの中から、1行を同じ確率で抽選する
    private NodeRow PickNodeRow(int group)
    {
        var candidates = new List<NodeRow>();

        foreach (NodeRow row in nodeRows)
        {
            if (row.group == group)
            {
                candidates.Add(row);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    // サイド → 基本線の順に抽選する
    private BasicLine PickLine(BasicLine exclude)
    {
        List<BasicLine> left = GetActiveLines(leftLines, exclude);
        List<BasicLine> right = GetActiveLines(rightLines, exclude);

        if (left.Count == 0 && right.Count == 0)
        {
            return null;
        }

        // ---- サイド ----
        Side side;

        if (left.Count == 0)
        {
            side = Side.Right;
        }
        else if (right.Count == 0)
        {
            side = Side.Left;
        }
        else if (!hasLastSide)
        {
            side = Random.value < 0.5f ? Side.Left : Side.Right;
        }
        else
        {
            CountRow row = SpawnTables.FindCountRow(sideRows, sideStreak);

            bool sameSide = RollSame(row);

            side = sameSide ? lastSide : Opposite(lastSide);
        }

        sideStreak = (hasLastSide && side == lastSide) ? sideStreak + 1 : 1;
        lastSide = side;
        hasLastSide = true;

        // ---- 基本線 ----
        List<BasicLine> candidates = side == Side.Left ? left : right;

        BasicLine chosen;

        if (lastLine != null && candidates.Contains(lastLine))
        {
            CountRow row = SpawnTables.FindCountRow(baselineRows, lineStreak);

            var others = new List<BasicLine>(candidates);
            others.Remove(lastLine);

            // 別の基本線がなければ、同じ基本線になる。
            // 別の基本線が複数あれば、どれも同じ確率
            if (RollSame(row) || others.Count == 0)
            {
                chosen = lastLine;
            }
            else
            {
                chosen = others[Random.Range(0, others.Count)];
            }
        }
        else
        {
            // 前回と別のサイド(または最初)は、サイド内の基本線から同じ確率で選ぶ
            chosen = candidates[Random.Range(0, candidates.Count)];
        }

        lineStreak = (chosen == lastLine) ? lineStreak + 1 : 1;
        lastLine = chosen;

        return chosen;
    }

    // 「同じ」になるか。same : other の割合で抽選する
    private static bool RollSame(CountRow row)
    {
        if (row == null)
        {
            return Random.value < 0.5f;
        }

        float total = row.same + row.other;

        if (total <= 0f)
        {
            return Random.value < 0.5f;
        }

        return Random.value * total < row.same;
    }

    private static Side Opposite(Side side)
    {
        return side == Side.Left ? Side.Right : Side.Left;
    }

    // 有効な(追加済み・消滅していない)基本線だけを返す
    private static List<BasicLine> GetActiveLines(BasicLine[] lines, BasicLine exclude)
    {
        var result = new List<BasicLine>();

        if (lines == null)
        {
            return result;
        }

        foreach (BasicLine line in lines)
        {
            if (line != null &&
                line != exclude &&
                line.isActive &&
                line.startPoint != null)
            {
                result.Add(line);
            }
        }

        return result;
    }

    private void SpawnNode(BasicLine line, bool malignant, int speed)
    {
        if (nodePrefab == null)
        {
            Debug.LogWarning("StageNodeSpawner: Node Prefabが設定されていません。", this);
            return;
        }

        GameObject obj = Instantiate(
            nodePrefab,
            line.startPoint.position,
            Quaternion.identity
        );

        NodeController node = obj.GetComponent<NodeController>();

        if (node == null)
        {
            return;
        }

        node.SetNodeType(malignant);
        node.SetMoveSpeed(speed * speedScale);
        node.SetSpawnBasicLine(line);
    }

    private int CountAliveNodes()
    {
        return FindObjectsByType<NodeController>(FindObjectsSortMode.None).Length;
    }
}