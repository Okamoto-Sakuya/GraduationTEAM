
using UnityEngine;

public class NodeController : MonoBehaviour
{
    public enum NodeType
    {
        Good,
        Bad
    }

    [Header("ノード設定")]
    public NodeType nodeType = NodeType.Good;

    [Header("ノードレベル")]
    [Range(1, 4)]
    public int level = 1;

    public float moveSpeed = 2f;

    // =====================================================
    // ゲージ設定
    // =====================================================

    [Header("ノードゲージ")]
    public float maxGauge = 100f;
    public float gaugeChangePerSecond = 10f;

    [SerializeField]
    private float currentGauge = 100f;

    public float CurrentGauge => currentGauge;

    // 悪性レベルが上昇中かどうか
    private bool increasingBadLevel = true;

    // =====================================================
    // 良性レベル別画像
    // =====================================================

    [Header("良性レベル別画像")]
    public Sprite goodLevel1Sprite;
    public Sprite goodLevel2Sprite;
    public Sprite goodLevel3Sprite;
    public Sprite goodLevel4Sprite;

    // =====================================================
    // 悪性レベル別画像
    // =====================================================

    [Header("悪性レベル別画像")]
    public Sprite badLevel1Sprite;
    public Sprite badLevel2Sprite;
    public Sprite badLevel3Sprite;
    public Sprite badLevel4Sprite;

    // =====================================================
    // 種類変更時間（旧設定との互換用）
    // =====================================================

    [Header("種類変更時間（ゲージ方式では未使用）")]
    public float goodToBadTime = 10f;
    public float badToGoodTime = 15f;

    // =====================================================
    // 悪性レベル別ダメージ
    // =====================================================

    [Header("悪性レベル別ダメージ")]
    public int level1Damage = 5;
    public int level2Damage = 7;
    public int level3Damage = 10;
    public int level4Damage = 15;

    // =====================================================
    // 悪性レベル別SAN値
    // =====================================================

    [Header("悪性レベル別SAN値")]
    public int level1SAN = 3;
    public int level2SAN = 5;
    public int level3SAN = 8;
    public int level4SAN = 10;

    public int CurrentDamage => GetBadDamage();
    public int CurrentSAN => GetBadSAN();

    // =====================================================
    // 見た目
    // =====================================================

    [Header("見た目")]
    public SpriteRenderer spriteRenderer;
    public Color goodColor = Color.blue;
    public Color badColor = Color.red;
    public Color greenGoodColor = Color.green;
    public bool useGreenForGood;

    [Header("ルート選択")]
    [Tooltip("接続されている造線を優先して進む")]
    public bool prioritizeDrawnWire = true;

    private TrackEdge currentEdge;
    private LinePoint fromPoint;
    private LinePoint targetPoint;
    private TrackEdge previousEdge;

    private float progress;
    private float stateTimer;

    private bool initialized;
    private bool recovered;

    private float wireWarningAlpha = 1f;

    // =====================================================
    // 初期化
    // =====================================================

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        maxGauge = Mathf.Max(0f, maxGauge);
        gaugeChangePerSecond = Mathf.Max(0f, gaugeChangePerSecond);
        currentGauge = Mathf.Clamp(currentGauge, 0f, maxGauge);

        level = Mathf.Clamp(level, 1, 4);

        UpdateAppearance();
    }

    public void Initialize(TrackEdge startingEdge, NodeType type)
    {
        if (startingEdge == null ||
            startingEdge.pointA == null ||
            startingEdge.pointB == null)
        {
            Debug.LogWarning(
                "NodeController: 開始線または接続点が設定されていません。",
                this);
            return;
        }

        currentEdge = startingEdge;
        previousEdge = null;

        fromPoint = startingEdge.pointA;
        targetPoint = startingEdge.pointB;

        nodeType = type;
        level = 1;

        // 出現時のゲージは100
        currentGauge = maxGauge;

        // 悪性化した後はレベル上昇から開始
        increasingBadLevel = true;

        progress = 0f;
        stateTimer = 0f;
        wireWarningAlpha = 1f;

        initialized = true;
        recovered = false;

        transform.position = fromPoint.transform.position;

        UpdateAppearance();
    }

    // =====================================================
    // 更新
    // =====================================================

    private void Update()
    {
        if (!initialized || recovered)
            return;

        if (GameManager.Instance != null &&
            GameManager.Instance.IsGameOver)
        {
            return;
        }

        UpdateGaugeAndLevel();
        MoveAlongEdge();
    }

    // =====================================================
    // ゲージとレベルの更新
    //
    // 良性1 → 2 → 3 → 4
    // 悪性1 → 2 → 3 → 4
    // 悪性4 → 3 → 2 → 1
    // 良性1に戻る
    // =====================================================

    private void UpdateGaugeAndLevel()
    {
        float change = gaugeChangePerSecond * Time.deltaTime;

        // -------------------------------------------------
        // 良性ノード
        // ゲージが0になるたびにレベルアップ
        // 良性レベル4の次は悪性レベル1
        // -------------------------------------------------

        if (nodeType == NodeType.Good)
        {
            currentGauge -= change;
            currentGauge = Mathf.Max(0f, currentGauge);

            if (currentGauge <= 0f)
            {
                if (level < 4)
                {
                    // 良性レベルアップ
                    level++;
                    currentGauge = maxGauge;
                }
                else
                {
                    // 良性レベル4から悪性レベル1へ
                    nodeType = NodeType.Bad;
                    level = 1;
                    currentGauge = 0f;
                    increasingBadLevel = true;
                }

                stateTimer = 0f;
                UpdateAppearance();
            }

            return;
        }

        // -------------------------------------------------
        // 悪性ノード：レベル上昇
        // ゲージが100になるたびにレベルアップ
        // -------------------------------------------------

        if (increasingBadLevel)
        {
            currentGauge += change;
            currentGauge = Mathf.Min(maxGauge, currentGauge);

            if (currentGauge >= maxGauge)
            {
                if (level < 4)
                {
                    level++;
                    currentGauge = 0f;
                }
                else
                {
                    // 悪性レベル4になったら下降開始
                    increasingBadLevel = false;
                    currentGauge = maxGauge;
                }

                stateTimer = 0f;
                UpdateAppearance();
            }
        }
        else
        {
            // -------------------------------------------------
            // 悪性ノード：レベル下降
            // ゲージが0になるたびにレベルダウン
            // -------------------------------------------------

            currentGauge -= change;
            currentGauge = Mathf.Max(0f, currentGauge);

            if (currentGauge <= 0f)
            {
                if (level > 1)
                {
                    level--;
                    currentGauge = maxGauge;
                }
                else
                {
                    // 悪性レベル1から良性レベル1へ
                    nodeType = NodeType.Good;
                    level = 1;
                    currentGauge = maxGauge;
                    increasingBadLevel = true;
                }

                stateTimer = 0f;
                UpdateAppearance();
            }
        }
    }

    // =====================================================
    // 悪性ノードのダメージ取得
    // =====================================================

    public int GetBadDamage()
    {
        if (nodeType != NodeType.Bad)
            return 0;

        switch (level)
        {
            case 1: return level1Damage;
            case 2: return level2Damage;
            case 3: return level3Damage;
            case 4: return level4Damage;
            default: return level1Damage;
        }
    }

    // =====================================================
    // 悪性ノードのSAN値取得
    // =====================================================

    public int GetBadSAN()
    {
        if (nodeType != NodeType.Bad)
            return 0;

        switch (level)
        {
            case 1: return level1SAN;
            case 2: return level2SAN;
            case 3: return level3SAN;
            case 4: return level4SAN;
            default: return level1SAN;
        }
    }

    // =====================================================
    // 現在の線に沿って移動
    // =====================================================

    private void MoveAlongEdge()
    {
        if (currentEdge == null ||
            fromPoint == null ||
            targetPoint == null)
        {
            return;
        }

        Vector3 start = fromPoint.transform.position;
        Vector3 end = targetPoint.transform.position;

        float distance = Vector3.Distance(start, end);

        if (distance <= 0.001f)
        {
            ArriveAtPoint();
            return;
        }

        progress += moveSpeed * Time.deltaTime / distance;
        progress = Mathf.Clamp01(progress);

        transform.position = Vector3.Lerp(start, end, progress);

        if (progress >= 1f)
            ArriveAtPoint();
    }

    // =====================================================
    // 接続点に到着
    // =====================================================

    private void ArriveAtPoint()
    {
        if (targetPoint == null)
            return;

        transform.position = targetPoint.transform.position;

        // 回収点を最優先
        if (targetPoint.isRecoveryPoint)
        {
            RecoveryPoint recovery = targetPoint.recoveryPoint;

            if (recovery == null)
                recovery = targetPoint.GetComponent<RecoveryPoint>();

            if (recovery != null)
            {
                recovery.Recover(this);
                return;
            }
        }

        ChooseNextEdge(targetPoint);
    }

    // =====================================================
    // 次に進む線を選択
    // 造線 → 基本線の順で優先する
    // =====================================================

    private void ChooseNextEdge(LinePoint arrivedPoint)
    {
        if (arrivedPoint == null)
            return;

        TrackEdge nextEdge = null;
        LinePoint nextPoint = null;

        // 優先1：接続されている造線
        if (prioritizeDrawnWire)
        {
            foreach (TrackEdge edge in arrivedPoint.ConnectedEdges)
            {
                if (!(edge is DrawnWire))
                    continue;

                if (!CanUseEdge(edge, arrivedPoint))
                    continue;

                LinePoint otherPoint = edge.GetOtherPoint(arrivedPoint);

                if (otherPoint == null)
                    continue;

                nextEdge = edge;
                nextPoint = otherPoint;
                break;
            }
        }

        // 優先2：基本線
        if (nextEdge == null)
        {
            foreach (TrackEdge edge in arrivedPoint.ConnectedEdges)
            {
                if (!(edge is BasicLine))
                    continue;

                if (!CanUseEdge(edge, arrivedPoint))
                    continue;

                LinePoint otherPoint = edge.GetOtherPoint(arrivedPoint);

                if (otherPoint == null)
                    continue;

                nextEdge = edge;
                nextPoint = otherPoint;
                break;
            }
        }

        // 移動先がない場合
        if (nextEdge == null || nextPoint == null)
        {
            Debug.LogWarning(
                "ノードの移動先がありません: " + arrivedPoint.name,
                arrivedPoint);
            return;
        }

        // 選択した線へ移動
        previousEdge = currentEdge;
        currentEdge = nextEdge;

        fromPoint = arrivedPoint;
        targetPoint = nextPoint;

        progress = 0f;

        if (currentEdge is DrawnWire drawnWire)
        {
            wireWarningAlpha = 1f;
            drawnWire.RegisterPass();
        }
        else
        {
            wireWarningAlpha = 1f;
        }

        UpdateAppearance();
    }

    // =====================================================
    // 線が利用できるか確認
    // =====================================================

    private bool CanUseEdge(
        TrackEdge edge,
        LinePoint arrivedPoint)
    {
        if (edge == null)
            return false;

        // 今いる線へそのまま戻ることはしない
        if (edge == currentEdge)
            return false;

        // 消滅待ちの造線には入らない
        if (!edge.CanEnter())
            return false;

        // 基本線はA→B方向のみ
        // 造線は両端から進める
        if (!edge.CanDepartFrom(arrivedPoint))
            return false;

        if (edge.GetOtherPoint(arrivedPoint) == null)
            return false;

        return true;
    }

    // =====================================================
    // ノードの種類を外部から変更
    // =====================================================

    public void SetType(NodeType newType)
    {
        nodeType = newType;
        level = 1;
        currentGauge = maxGauge;
        increasingBadLevel = true;
        stateTimer = 0f;

        UpdateAppearance();
    }

    // =====================================================
    // レベルに対応した画像と色を設定
    // =====================================================

    private void UpdateAppearance()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
            return;

        Sprite selectedSprite = null;
        Color color;

        if (nodeType == NodeType.Good)
        {
            color = useGreenForGood
                ? greenGoodColor
                : goodColor;

            switch (level)
            {
                case 1:
                    selectedSprite = goodLevel1Sprite;
                    break;
                case 2:
                    selectedSprite = goodLevel2Sprite;
                    break;
                case 3:
                    selectedSprite = goodLevel3Sprite;
                    break;
                case 4:
                    selectedSprite = goodLevel4Sprite;
                    break;
            }
        }
        else
        {
            color = badColor;

            switch (level)
            {
                case 1:
                    selectedSprite = badLevel1Sprite;
                    break;
                case 2:
                    selectedSprite = badLevel2Sprite;
                    break;
                case 3:
                    selectedSprite = badLevel3Sprite;
                    break;
                case 4:
                    selectedSprite = badLevel4Sprite;
                    break;
            }
        }

        // 画像が設定されている場合だけ変更
        if (selectedSprite != null)
            spriteRenderer.sprite = selectedSprite;

        color.a *= wireWarningAlpha;
        spriteRenderer.color = color;
    }

    // =====================================================
    // 造線の消滅処理から参照
    // =====================================================

    public bool IsOnEdge(TrackEdge edge)
    {
        return initialized &&
               !recovered &&
               currentEdge == edge;
    }

    public void SetWireWarningAlpha(float alpha)
    {
        wireWarningAlpha = Mathf.Clamp01(alpha);
        UpdateAppearance();
    }

    // =====================================================
    // 線の分割時にノードの移動先を更新
    // BasicLine用：引数7個
    // =====================================================

    public void HandleEdgeSplit(
        TrackEdge originalEdge,
        LinePoint branchPoint,
        LinePoint oldPointA,
        LinePoint oldPointB,
        TrackEdge firstEdge,
        TrackEdge secondEdge,
        float splitT)
    {
        UpdateEdgeAfterSplit(
            originalEdge,
            branchPoint,
            oldPointA,
            oldPointB,
            firstEdge,
            secondEdge,
            splitT,
            true);
    }

    // =====================================================
    // 線の分割時にノードの移動先を更新
    // DrawnWire用：引数6個
    // =====================================================

    public void HandleEdgeSplit(
        TrackEdge originalEdge,
        LinePoint branchPoint,
        LinePoint oldPointA,
        LinePoint oldPointB,
        TrackEdge firstEdge,
        TrackEdge secondEdge)
    {
        if (oldPointA == null ||
            oldPointB == null ||
            branchPoint == null)
        {
            return;
        }

        Vector3 a = oldPointA.transform.position;
        Vector3 b = oldPointB.transform.position;
        Vector3 direction = b - a;

        float lengthSquared = direction.sqrMagnitude;

        if (lengthSquared <= 0.0001f)
            return;

        float splitT = Vector3.Dot(
            branchPoint.transform.position - a,
            direction) / lengthSquared;

        splitT = Mathf.Clamp01(splitT);

        UpdateEdgeAfterSplit(
            originalEdge,
            branchPoint,
            oldPointA,
            oldPointB,
            firstEdge,
            secondEdge,
            splitT,
            false);
    }

    // =====================================================
    // 分割後の線と進捗を更新
    // =====================================================

    private void UpdateEdgeAfterSplit(
        TrackEdge originalEdge,
        LinePoint branchPoint,
        LinePoint oldPointA,
        LinePoint oldPointB,
        TrackEdge firstEdge,
        TrackEdge secondEdge,
        float splitT,
        bool oneWay)
    {
        if (originalEdge == null ||
            branchPoint == null ||
            oldPointA == null ||
            oldPointB == null ||
            firstEdge == null ||
            secondEdge == null)
        {
            return;
        }

        if (currentEdge != originalEdge)
            return;

        Vector3 a = oldPointA.transform.position;
        Vector3 b = oldPointB.transform.position;
        Vector3 direction = b - a;

        float lengthSquared = direction.sqrMagnitude;

        if (lengthSquared <= 0.0001f)
            return;

        float nodeT = Vector3.Dot(
            transform.position - a,
            direction) / lengthSquared;

        nodeT = Mathf.Clamp01(nodeT);
        splitT = Mathf.Clamp01(splitT);

        bool movingForward = fromPoint == oldPointA;

        if (oneWay)
            movingForward = true;

        bool useFirstEdge = nodeT < splitT;

        if (Mathf.Abs(nodeT - splitT) <= 0.0001f)
            useFirstEdge = !movingForward;

        TrackEdge selectedEdge = useFirstEdge
            ? firstEdge
            : secondEdge;

        LinePoint segmentStart;
        LinePoint segmentEnd;

        if (useFirstEdge)
        {
            segmentStart = movingForward ? oldPointA : branchPoint;
            segmentEnd = movingForward ? branchPoint : oldPointA;
        }
        else
        {
            segmentStart = movingForward ? branchPoint : oldPointB;
            segmentEnd = movingForward ? oldPointB : branchPoint;
        }

        // 基本線はA→B方向のみ
        if (oneWay)
        {
            if (useFirstEdge)
            {
                segmentStart = oldPointA;
                segmentEnd = branchPoint;
            }
            else
            {
                segmentStart = branchPoint;
                segmentEnd = oldPointB;
            }
        }

        float segmentLength = Vector3.Distance(
            segmentStart.transform.position,
            segmentEnd.transform.position);

        float distanceFromStart = Vector3.Distance(
            segmentStart.transform.position,
            transform.position);

        currentEdge = selectedEdge;
        fromPoint = segmentStart;
        targetPoint = segmentEnd;

        progress = segmentLength > 0.0001f
            ? Mathf.Clamp01(distanceFromStart / segmentLength)
            : 0f;

        previousEdge = null;
    }

    // =====================================================
    // 回収処理
    // =====================================================

    public void MarkRecovered()
    {
        if (recovered)
            return;

        recovered = true;
        initialized = false;

        Destroy(gameObject);
    }
}
