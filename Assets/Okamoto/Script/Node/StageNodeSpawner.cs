using System.Collections.Generic;
using UnityEngine;

// 4つのシートに従ってノードを出現させる
//  ステージの表   : レベルごとの 出現ノード数・出現グループ・出現頻度・スピード・造線ゲージ(出し切ったら次のレベル)
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

    // ノードの種類(ノードグループの表のtype)
    private const int NormalType = 1;         // 通常
    private const int SimultaneousType = 2;   // 同時出現(2か所から2個)
    private const int FastType = 3;           // 迅速(スピードが速い)
    private const int HeavyType = 4;          // 重圧(造線の消費量が増える: 未実装・通常として出す)
    private const int StallType = 5;          // 停滞(途中で止まる: 未実装・通常として出す)
    private const int LeftArmType = 6;        // 帰属: 左腕
    private const int RightArmType = 7;       // 帰属: 右腕
    private const int LeftLegType = 8;         // 帰属: 左足
    private const int RightLegType = 9;        // 帰属: 右足
    private const int CrossType = 10;         // 交錯
    private const int LineAddType = 11;       // 造線

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

    [Tooltip("ノードグループの表: id, group, base, type")]
    [SerializeField] private TextAsset nodeGroupTable;

    [Tooltip("ステージの表: stage, linegauge, level, nodecount, group, rate, speed")]
    [SerializeField] private TextAsset stageTable;

    [Header("調整")]
    [Tooltip("rate(出現頻度)は、この秒数あたりの出現数。10なら60秒に10個")]
    [SerializeField] private float rateUnitSeconds = 60f;

    [Tooltip("speed × この値 = ノードの移動速度。10なら1.0")]
    [SerializeField] private float speedScale = 0.1f;

    [Tooltip("最後のレベルのノードを出し切ったあと、そのレベルを繰り返して出し続ける(OFFなら出現が止まる)")]
    [SerializeField] private bool loopLastLevel = true;

    [Tooltip("画面上のノードがこの数以上なら、新しく出さない")]
    [SerializeField] private int maxNodeCount = 30;

    [Tooltip("レベルが切り替わるとき、スピードと頻度を前のレベルの値から新しい値へ変えていく秒数。ステージの表の8列目(transition)があれば、そちらが優先")]
    [SerializeField] private float defaultTransitionSeconds = 5f;

    [Header("特殊ノード")]
    [Tooltip("迅速ノードの速度倍率。現在のステージのノードスピードに掛ける")]
    [SerializeField] private float fastSpeedMultiplier = 2f;

    [Tooltip("動作確認用: 1?11にすると、表に関係なく、すべてそのtypeのノードを出す(基本属性は表のまま)。0なら表のとおり")]
    [Range(0, 11)]
    [SerializeField] private int debugForceNodeType = 0;

    [Tooltip("動作確認用にtypeを固定したとき、基本属性が表にない場合に悪性になる確率(0?1)")]
    [Range(0f, 1f)]
    [SerializeField] private float specialMalignantChance = 0.5f;

    [Header("線追加ノード")]
    [Tooltip("線を追加する先のLineManager。空なら自動で探す")]
    [SerializeField] private LineManager lineManager;

    [Tooltip("追加する造線の角度の範囲。基本線に垂直な向きから±この角度(度)")]
    [Range(0f, 89f)]
    [SerializeField] private float lineAddMaxAngle = 45f;

    [Tooltip("追加する造線の最大の長さ。0なら制限なし")]
    [SerializeField] private float lineAddMaxDistance = 6f;

    [Tooltip("1体の出現で追加する造線の本数")]
    [SerializeField] private int lineAddCount = 1;

    // 読み込んだデータ
    private List<CountRow> baselineRows;
    private List<CountRow> sideRows;
    private List<NodeRow> nodeRows;
    private List<StageRow> stageRows;

    // 進行状況
    private bool running;
    private float elapsed;
    private float timer;

    // ステージの表のうち、今のステージの行(1行 = 1レベル。表の並び順)
    private readonly List<StageRow> levelRows = new List<StageRow>();
    private int rowIndex;
    private int spawnedInRow;
    private float rowStartElapsed;

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

        levelRows.Clear();

        foreach (StageRow row in stageRows)
        {
            if (row.stage == stage)
            {
                levelRows.Add(row);
            }
        }

        rowIndex = 0;
        spawnedInRow = 0;
        rowStartElapsed = 0f;

        if (levelRows.Count == 0)
        {
            Debug.LogWarning($"ステージ {stage} のデータがステージの表にありません。", this);
        }
        else
        {
            // 造線ゲージの最大量(ステージの最初のレベルの値で、満タンから始める)
            FindLineManager();

            if (lineManager != null)
            {
                lineManager.InitLineGauge(levelRows[0].lineGauge);
            }
        }

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

        if (rowIndex >= levelRows.Count)
        {
            return;
        }

        StageRow row = levelRows[rowIndex];

        CurrentLevel = row.level;

        // レベルが切り替わった直後は、前のレベルの値から徐々に変える
        GetBlendedValues(row, out float rate, out float speed);

        timer -= Time.deltaTime;

        if (timer > 0f)
        {
            return;
        }

        spawnedInRow += SpawnWave(row, speed);

        // このレベルのノードを出し切ったら、次のレベルへ
        if (spawnedInRow >= row.nodeCount)
        {
            AdvanceLevel();
        }

        // rate個 / rateUnitSeconds秒 の間隔で出現させる
        float interval = rate > 0f
            ? rateUnitSeconds / rate
            : 1f;

        timer += interval;

        if (timer < 0f)
        {
            timer = interval;
        }
    }

    private void AdvanceLevel()
    {
        if (rowIndex + 1 < levelRows.Count)
        {
            StageRow previous = levelRows[rowIndex];

            rowIndex++;
            spawnedInRow = 0;
            rowStartElapsed = elapsed;

            StageRow next = levelRows[rowIndex];

            Debug.Log(
                $"レベル {next.level} へ(ノード{next.nodeCount}個 グループ{next.group} " +
                $"頻度{next.rate} スピード{next.speed})", this);

            if (next.lineGauge != previous.lineGauge && lineManager != null)
            {
                lineManager.SetMaxLineGauge(next.lineGauge);
            }
        }
        else if (loopLastLevel)
        {
            spawnedInRow = 0;
        }
        else
        {
            running = false;
        }
    }

    private void FindLineManager()
    {
        if (lineManager == null)
        {
            lineManager = FindFirstObjectByType<LineManager>();
        }
    }

    // 現在のスピードと頻度を返す。
    // 現在のレベルに切り替わった時点から、transition秒かけて、前のレベルの値から現在のレベルの値へ変える。
    // (グループは切り替わった時点ですぐ変わる)
    private void GetBlendedValues(StageRow row, out float rate, out float speed)
    {
        rate = row.rate;
        speed = row.speed;

        if (rowIndex <= 0)
        {
            return;
        }

        StageRow previous = levelRows[rowIndex - 1];

        float seconds = row.transition >= 0f ? row.transition : defaultTransitionSeconds;

        if (seconds <= 0f)
        {
            return;
        }

        float t = (elapsed - rowStartElapsed) / seconds;

        if (t >= 1f)
        {
            return;
        }

        t = Mathf.Clamp01(t);

        rate = Mathf.Lerp(previous.rate, row.rate, t);
        speed = Mathf.Lerp(previous.speed, row.speed, t);
    }

    // ===== 出現 =====

    private int SpawnWave(StageRow row, float stageSpeed)
    {
        NodeRow picked = PickNodeRow(row.group);

        // 動作確認用: typeを固定する
        if (debugForceNodeType > 0)
        {
            picked = new NodeRow { id = 0, group = row.group, baseType = picked != null ? picked.baseType : 0, type = debugForceNodeType };
        }

        if (picked == null)
        {
            Debug.LogWarning(
                $"ノードグループ {row.group} のデータがありません。", this);
            return 0;
        }

        bool limbNode = picked.type >= LeftArmType && picked.type <= RightLegType;
        bool fastNode = picked.type == FastType;
        bool crossNode = picked.type == CrossType;
        bool lineAddNode = picked.type == LineAddType;

        // 基本属性: 表のbase(1=良性 2=悪性)のとおり。
        // 表にbaseがないとき(動作確認用にtypeを固定した場合など)は、確率で決める
        bool malignant = picked.baseType != 0
            ? picked.baseType == 2
            : Random.value < specialMalignantChance;

        int count = picked.type == SimultaneousType ? 2 : 1;

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
                return 0;
            }
        }

        if (CountAliveNodes() + count > maxNodeCount)
        {
            return 0;
        }

        Debug.Log(
            $"出現: type={picked.type} 悪性={malignant} " +
            $"speed={stageSpeed * speedMultiplier}", this);

        BasicLine firstLine = null;
        int spawned = 0;

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
                stageSpeed * speedMultiplier,
                hasTarget,
                targetType,
                fastNode,
                crossNode
            );

            // 線追加ノード: 出現時に、ランダムな場所・角度に造線を追加する
            if (lineAddNode)
            {
                AddRandomLines();
            }

            spawned++;

            if (i == 0)
            {
                firstLine = line;
            }
        }

        return spawned;
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
        bool fast,
        bool cross)
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

        if (cross)
        {
            node.SetCrossing();
        }

        node.SetSpawnBasicLine(line);
    }

    private void AddRandomLines()
    {
        if (lineManager == null)
        {
            lineManager = FindFirstObjectByType<LineManager>();
        }

        if (lineManager == null)
        {
            Debug.LogWarning("StageNodeSpawner: LineManagerが見つかりません。", this);
            return;
        }

        for (int i = 0; i < lineAddCount; i++)
        {
            lineManager.AddRandomLine(lineAddMaxAngle, lineAddMaxDistance);
        }
    }

    private int CountAliveNodes()
    {
        return FindObjectsByType<NodeController>(FindObjectsSortMode.None).Length;
    }
}