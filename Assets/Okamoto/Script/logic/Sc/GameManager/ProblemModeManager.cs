
using System.Collections;
using TMPro;
using UnityEngine;

public class ProblemModeManager : MonoBehaviour
{
    public enum ProblemState
    {
        Waiting,
        Drawing,
        Moving,
        Clear,
        GameOver
    }

    [Header("Nodeの出現設定")]
    public BasicLine[] startingLines;
    public NodeController nodePrefab;
    public NodeController.NodeType startingNodeType =
        NodeController.NodeType.Good;

    [Header("ゴール設定")]
    [Tooltip("ゴールにしたいRecoveryPointをアタッチ")]
    public RecoveryPoint goalRecoveryPoint;

    [Tooltip("ゴールの上に表示するワールド空間のTextMeshPro")]
    public TextMeshPro goalLabel;

    public Vector3 goalLabelOffset = new Vector3(0f, 0.5f, 0f);

    [Header("時間設定")]
    [Min(0.1f)]
    public float drawingTimeLimit = 10f;

    [Min(0.1f)]
    public float movementTimeLimit = 20f;

    [Header("線を引くスクリプト")]
    public Behaviour wireDrawingController;

    [Header("UI")]
    public TMP_Text timerText;
    public TMP_Text resultText;

    [Header("開始設定")]
    public bool startAutomatically = true;

    public ProblemState CurrentState { get; private set; }
        = ProblemState.Waiting;

    public NodeController CurrentNode { get; private set; }

    public RecoveryPoint GoalRecoveryPoint => goalRecoveryPoint;

    public float RemainingTime { get; private set; }

    private Coroutine gameRoutine;

    private void Start()
    {
        if (resultText != null)
            resultText.gameObject.SetActive(false);

        if (timerText != null)
            timerText.gameObject.SetActive(false);

        UpdateGoalLabel();

        // 線引きは問題開始時にのみ許可する
        SetDrawingEnabled(false);

        if (startAutomatically)
            StartProblem();
    }

    private void Update()
    {
        // ゴールの位置に「ここ」を追従させる
        UpdateGoalLabel();
    }

    // =====================================================
    // ゴール表示
    // =====================================================

    private void UpdateGoalLabel()
    {
        if (goalLabel == null)
            return;

        if (goalRecoveryPoint == null ||
            goalRecoveryPoint.IsDestroyed)
        {
            goalLabel.gameObject.SetActive(false);
            return;
        }

        goalLabel.gameObject.SetActive(
            CurrentState != ProblemState.Clear &&
            CurrentState != ProblemState.GameOver);

        goalLabel.text = "ここ";
        goalLabel.transform.position =
            goalRecoveryPoint.transform.position + goalLabelOffset;
    }

    // =====================================================
    // 問題開始
    // =====================================================

    public void StartProblem()
    {
        if (CurrentState != ProblemState.Waiting)
            return;

        gameRoutine = StartCoroutine(ProblemRoutine());
    }

    private IEnumerator ProblemRoutine()
    {
        CurrentState = ProblemState.Drawing;

        // Nodeを生成する
        if (!SpawnNode())
        {
            FinishGame(false);
            yield break;
        }

        // 線を引く準備中は移動を停止
        CurrentNode.SetProblemModeManager(this);
        CurrentNode.SetProblemModePaused(true);

        SetDrawingEnabled(true);

        RemainingTime = drawingTimeLimit;

        if (timerText != null)
            timerText.gameObject.SetActive(true);

        // 線を引ける時間
        while (RemainingTime > 0f &&
               CurrentState == ProblemState.Drawing)
        {
            RemainingTime -= Time.deltaTime;
            RemainingTime = Mathf.Max(0f, RemainingTime);

            if (timerText != null)
            {
                timerText.text =
                    "線を引ける時間 : " +
                    RemainingTime.ToString("F1") + "秒";
            }

            yield return null;
        }

        if (CurrentState != ProblemState.Drawing)
            yield break;

        // 時間切れ：線を引けなくする
        SetDrawingEnabled(false);

        if (timerText != null)
            timerText.gameObject.SetActive(false);

        // Nodeの移動を開始
        CurrentState = ProblemState.Moving;

        if (CurrentNode == null)
        {
            FinishGame(false);
            yield break;
        }

        CurrentNode.SetProblemModePaused(false);

        RemainingTime = movementTimeLimit;

        // ゴール到達または移動時間切れを待つ
        while (RemainingTime > 0f &&
               CurrentState == ProblemState.Moving)
        {
            if (CurrentNode == null)
            {
                FinishGame(false);
                yield break;
            }

            RemainingTime -= Time.deltaTime;
            RemainingTime = Mathf.Max(0f, RemainingTime);

            if (timerText != null)
            {
                timerText.text =
                    "ゴールまで : " +
                    RemainingTime.ToString("F1") + "秒";
            }

            yield return null;
        }

        if (CurrentState == ProblemState.Moving)
            FinishGame(false);
    }

    // =====================================================
    // Node生成
    // =====================================================

    private bool SpawnNode()
    {
        if (nodePrefab == null)
        {
            Debug.LogError("Node Prefabが未設定です。", this);
            return false;
        }

        if (startingLines == null || startingLines.Length == 0)
        {
            Debug.LogError("Starting Linesが未設定です。", this);
            return false;
        }

        System.Collections.Generic.List<BasicLine> validLines =
            new System.Collections.Generic.List<BasicLine>();

        foreach (BasicLine line in startingLines)
        {
            if (line == null || !line.gameObject.activeInHierarchy)
                continue;

            if (line.pointA == null || line.pointB == null)
                continue;

            validLines.Add(line);
        }

        if (validLines.Count == 0)
        {
            Debug.LogError("使用できる開始基本線がありません。", this);
            return false;
        }

        BasicLine selectedLine =
            validLines[Random.Range(0, validLines.Count)];

        // 生成直後から停止状態にする
        CurrentNode = Instantiate(
            nodePrefab,
            selectedLine.pointA.transform.position,
            Quaternion.identity);

        CurrentNode.SetProblemModeManager(this);
        CurrentNode.SetProblemModePaused(true);

        CurrentNode.Initialize(selectedLine, startingNodeType);

        // Initialize後にも停止状態を明示する
        CurrentNode.SetProblemModePaused(true);

        return true;
    }

    // =====================================================
    // 線引きの有効・無効
    // =====================================================

    private void SetDrawingEnabled(bool enabled)
    {
        if (wireDrawingController != null)
            wireDrawingController.enabled = enabled;
    }

    // =====================================================
    // ゴール到達
    // =====================================================

    public void NotifyGoalReached()
    {
        if (CurrentState != ProblemState.Moving)
            return;

        FinishGame(true);
    }

    // =====================================================
    // 移動失敗
    // =====================================================

    public void NotifyNodeFailed()
    {
        if (CurrentState != ProblemState.Moving)
            return;

        FinishGame(false);
    }

    // =====================================================
    // 結果表示
    // =====================================================

    private void FinishGame(bool cleared)
    {
        if (CurrentState == ProblemState.Clear ||
            CurrentState == ProblemState.GameOver)
            return;

        CurrentState = cleared
            ? ProblemState.Clear
            : ProblemState.GameOver;

        SetDrawingEnabled(false);

        if (CurrentNode != null)
            CurrentNode.SetProblemModePaused(true);

        if (timerText != null)
            timerText.gameObject.SetActive(false);

        if (resultText != null)
        {
            resultText.gameObject.SetActive(true);
            resultText.text = cleared ? "CLEAR!" : "GAME OVER";
        }

        if (goalLabel != null)
            goalLabel.gameObject.SetActive(false);
    }
}
