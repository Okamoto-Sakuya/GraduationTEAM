using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// セレクトシーンのボタンマネージャー
/// </summary>
public class SelectButtonManager : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button motherButton;
    [SerializeField] private Button daughterButton;
    [SerializeField] private Button motherBackButton;
    [SerializeField] private Button daughterBackButton;

    [Header("Stage Buttons")]
    [SerializeField] private Button[] motherStageButtons;   // MotherStage01Button ～ 5個
    [SerializeField] private Button[] daughterStageButtons; // DaughterStage01Button ～ 4個

    [Header("Default Next Scene (ScenarioScene後の既定の行き先)")]
    [SerializeField] private string rushSceneName = "RushScene";
    [SerializeField] private string logicSceneName = "LogicScene";

    [Header("Panels")]
    [SerializeField] private GameObject selectPanel;
    [SerializeField] private GameObject motherPanel;
    [SerializeField] private GameObject daughterPanel;

    private bool isTransitioning;

    private void Awake()
    {
        motherButton.onClick.AddListener(() =>
            Confirm("母を選びますか？", motherPanel));
        daughterButton.onClick.AddListener(() =>
            Confirm("娘を選びますか？", daughterPanel));

        motherBackButton.onClick.AddListener(() => ChangePanel(motherPanel, selectPanel));
        daughterBackButton.onClick.AddListener(() => ChangePanel(daughterPanel, selectPanel));

        // ステージボタン：母 → ScenarioScene → RushScene（CSV指定があればそちら優先）
        for (int i = 0; i < motherStageButtons.Length; i++)
        {
            int stageIndex = i; // クロージャ対策
            motherStageButtons[i].onClick.AddListener(() => LoadScenario(rushSceneName, stageIndex));
        }

        // ステージボタン：娘 → ScenarioScene → LogicScene（CSV指定があればそちら優先）
        for (int i = 0; i < daughterStageButtons.Length; i++)
        {
            int stageIndex = i;
            daughterStageButtons[i].onClick.AddListener(() => LoadScenario(logicSceneName, stageIndex));
        }
    }

    private void Start()
    {
        // 最初はSelectPanelだけ表示
        selectPanel.SetActive(true);
        motherPanel.SetActive(false);
        daughterPanel.SetActive(false);
    }

    // 確認ウィンドウを出して、「はい」ならパネルを切り替える
    private void Confirm(string message, GameObject targetPanel)
    {
        if (isTransitioning) return;

        ConfirmDialog.Instance.Show(
            "確認",
            message,
            "はい", "いいえ",
            onYes: () => ChangePanel(selectPanel, targetPanel));
    }

    private void OnDestroy()
    {
        motherButton.onClick.RemoveAllListeners();
        daughterButton.onClick.RemoveAllListeners();
        motherBackButton.onClick.RemoveAllListeners();
        daughterBackButton.onClick.RemoveAllListeners();

        foreach (var button in motherStageButtons)
        {
            if (button != null) button.onClick.RemoveAllListeners();
        }
        foreach (var button in daughterStageButtons)
        {
            if (button != null) button.onClick.RemoveAllListeners();
        }
    }

    // ScenarioSceneを挟んで遷移する。defaultNextSceneはCSVに指定がなかった場合の行き先
    private void LoadScenario(string defaultNextScene, int stageIndex)
    {
        if (isTransitioning) return; // 連打防止
        isTransitioning = true;

        SceneFlow.LoadViaScenario(defaultNextScene, stageIndex);
    }

    private void ChangePanel(GameObject from, GameObject to)
    {
        if (isTransitioning) return; // 演出中の連打防止
        StartCoroutine(TransitionRoutine(from, to));
    }

    private IEnumerator TransitionRoutine(GameObject from, GameObject to)
    {
        isTransitioning = true;

        // 遷移前の演出（フェードアウトなど）
        yield return PlayTransitionOut(from);

        from.SetActive(false);
        to.SetActive(true);

        // 遷移後の演出（フェードインなど）
        yield return PlayTransitionIn(to);

        isTransitioning = false;
    }

    // ===== 演出はここに追加する =====
    protected virtual IEnumerator PlayTransitionOut(GameObject panel)
    {
        yield break;
    }

    protected virtual IEnumerator PlayTransitionIn(GameObject panel)
    {
        yield break;
    }
}