using System.Collections.Generic;
using UnityEngine;

// 4つのシートに従ってノードを出現させる
//  ステージの表   : 経過時間ごとの 出現グループ・出現頻度・スピード
//  ノードグループ : グループの中から抽選して、ノードの種類(良性/悪性/2つ同時/四肢対応/足早)を決める
//  左右の表       : 出現するサイドを抽選(同じサイドに偏らないようにする)
//  基本線の表     : サイドの中で、出現する基本線を抽選(同じ基本線に偏らないようにする)
public class StageNodeSpawner : MonoBehaviour
{
    private enum Side
    {
        Left,
        Right
    }

    // ノードの種類(ノードグループの表のnodetype)
    //  1=良性 2=悪性 3=良性2つ 4=悪性2つ
    private const int LeftArmType = 5;    // 四肢対応: 左腕
    private const int LeftLegType = 6;    // 四肢対応: 左足
    private const int RightArmType = 7;   // 四肢対応: 右腕
    private const int RightLegType = 8;   // 四肢対応: 右足
    private const int FastType = 9;       // 足早ノード(番号が違う場合はここを変える)

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

    [Header("特殊ノード")]
    [Tooltip("足早ノードの速度倍率。現在のステージのノードスピードに掛ける")]
    [SerializeField] private float fastSpeedMultiplier = 2f;

    [Tooltip("動作確認用: 1?9にすると、表に関係なく、すべてそのtypeのノードを出す。0なら表のとおり")]
    [Range(0, 9)]
    [SerializeField] private int debugForceNodeType = 0;

    [Tooltip("四肢対応ノード・足早ノードが、悪性で出現する確率(0?1)")]
    [Range(0f, 1f)]
    [SerializeField] private float specialMalignantChance = 0.5f;

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

        // 動作確認用: typeを固定する
        if (debugForceNodeType > 0)
        {
            picked = new NodeRow { id = 0, group = row.group, type = debugForceNodeType };
        }

        if (picked == null)
        {
            Debug.LogWarning(
                $"ノードグループ {row.group} のデータがありません。", this);
            return;
        }

        bool limbNode = picked.type >= LeftArmType && picked.type <= RightLegType;
        bool fastNode = picked.type == FastType;

        // 四肢対応ノード・足早ノードは、確率で悪性になる
        bool malignant =
            picked.type == 2 ||
            picked.type == 4 ||
            ((limbNode || fastNode) && Random.value < specialMalignantChance);

        int count = (picked.type == 3 || picked.type == 4) ? 2 : 1;

        // 足早ノード: ステージのノードスピードに倍率を掛ける
        float speedMultiplier = fastNode ? fastSpeedMultiplier : 1f;

        // 四肢対応ノード: typeで運ぶ先の四肢が決まる
        bool hasTarget = false;
        LimbController.LimbType targetType = LimbController.LimbType.RightArm;

        if (limbNode)
        {
            hasTarget = TryGetTargetLimb(picked.type, out targetType);

            if (!hasTarget)
            {
                return;
            }
        }

        if (CountAliveNodes() + count > maxNodeCount)
        {
            return;
        }

        Debug.Log(
            $"出現: type={picked.type} 悪性={malignant} " +
            $"speed={row.speed * speedMultiplier}", this);

        BasicLine firstLine = null;

        for (int i = 0; i < count; i++)
        {
            // 2つ目は、1つ目と別の基本線に出す
            BasicLine line = PickLine(firstLine);

            if (line == null)
            {
                break;
            }

            SpawnNode(
                line,
                malignant,
                row.speed * speedMultiplier,
                hasTarget,
                targetType,
                fastNode
            );

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

    // typeから、運ぶ先の四肢を決める。その四肢が壊れていたら出さない
    private bool TryGetTargetLimb(int nodeType, out LimbController.LimbType type)
    {
        switch (nodeType)
        {
            case LeftArmType: type = LimbController.LimbType.LeftArm; break;
            case LeftLegType: type = LimbController.LimbType.LeftLeg; break;
            case RightArmType: type = LimbController.LimbType.RightArm; break;
            case RightLegType: type = LimbController.LimbType.RightLeg; break;
            default:
                type = LimbController.LimbType.RightArm;
                return false;
        }

        if (GameManager.Instance == null || GameManager.Instance.limbs == null)
        {
            return false;
        }

        foreach (LimbController limb in GameManager.Instance.limbs)
        {
            if (limb != null && limb.IsAlive && limb.Type == type)
            {
                return true;
            }
        }

        return false;
    }

    private void SpawnNode(
        BasicLine line,
        bool malignant,
        float speed,
        bool hasTarget,
        LimbController.LimbType targetType,
        bool fast)
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

        if (hasTarget)
        {
            node.SetTargetLimb(targetType);
        }

        if (fast)
        {
            node.SetFast();
        }

        node.SetSpawnBasicLine(line);
    }

    private int CountAliveNodes()
    {
        return FindObjectsByType<NodeController>(FindObjectsSortMode.None).Length;
    }
}