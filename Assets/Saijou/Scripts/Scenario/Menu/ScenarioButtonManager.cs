using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// シナリオ画面のボタン管理。
/// Logボタンでログウィンドウ、設定ボタンで設定ウィンドウを表示し、
/// それぞれのBackボタンで非表示にする。
/// UIのOn/Offボタン（1つ）でScenarioUI全体の表示/非表示を切り替える。
/// 演出を追加したい箇所には「【演出追加】」コメントを入れてある。
/// （Ctrl+F で「演出追加」と検索すると全箇所が見つかる）
/// </summary>
public class ScenarioButtonManager : MonoBehaviour
{
    // ========================================
    // Inspector 設定項目
    // ========================================

    [Header("ボタン")]
    [SerializeField] private Button logButton;       // Logボタン
    [SerializeField] private Button settingButton;   // 設定ボタン
    [SerializeField] private Button uiToggleButton;  // UI On/Offボタン（1つ）

    [Header("ウィンドウ内のBackボタン")]
    [SerializeField] private Button logBackButton;      // ログウィンドウのBackボタン
    [SerializeField] private Button settingBackButton;  // 設定ウィンドウのBackボタン

    [Header("ウィンドウ")]
    [SerializeField] private GameObject logWindow;      // ログウィンドウ
    [SerializeField] private GameObject settingWindow;  // 設定ウィンドウ

    [Header("UI全体")]
    [SerializeField] private GameObject scenarioUI;     // On/Offで切り替えるScenarioUI

    // ScenarioUIが表示中かどうか（ScenarioPlayerなど外部から参照する用）
    public bool IsUIVisible => scenarioUI == null || scenarioUI.activeSelf;

    // ログ・設定ウィンドウが開いているかどうか（ScenarioPlayerから参照する用）
    public bool IsWindowOpen =>
        (logWindow != null && logWindow.activeSelf) ||
        (settingWindow != null && settingWindow.activeSelf);

    // 同一フレームでの「消した直後に出る／出した直後に消える」誤動作防止用
    private int hiddenFrame = -1;
    private int shownFrame = -1;

    // ========================================
    // Unity イベント
    // ========================================

    private void Awake()
    {
        // 開始時はウィンドウを閉じておく
        // 【演出追加】ここは初期化なので基本的に演出は不要（即座に非表示でOK）
        HideObject(logWindow);
        HideObject(settingWindow);

        // ScenarioUIは開始時は表示状態にしておく
        ShowObject(scenarioUI);
    }

    private void OnEnable()
    {
        if (logButton != null) logButton.onClick.AddListener(OnClickLog);
        if (settingButton != null) settingButton.onClick.AddListener(OnClickSetting);
        if (uiToggleButton != null) uiToggleButton.onClick.AddListener(OnClickUIToggle);
        if (logBackButton != null) logBackButton.onClick.AddListener(OnClickLogBack);
        if (settingBackButton != null) settingBackButton.onClick.AddListener(OnClickSettingBack);
    }

    private void OnDisable()
    {
        if (logButton != null) logButton.onClick.RemoveListener(OnClickLog);
        if (settingButton != null) settingButton.onClick.RemoveListener(OnClickSetting);
        if (uiToggleButton != null) uiToggleButton.onClick.RemoveListener(OnClickUIToggle);
        if (logBackButton != null) logBackButton.onClick.RemoveListener(OnClickLogBack);
        if (settingBackButton != null) settingBackButton.onClick.RemoveListener(OnClickSettingBack);
    }

    // ========================================
    // ボタンの入口
    // ========================================

    // Logボタン：ログウィンドウを開く
    private void OnClickLog()
    {
        // 【演出追加】ボタンクリック時の演出（決定SEなど）

        // 【演出追加】ログウィンドウ 開演出
        //   （例：フェードイン、スライドイン）

        ShowObject(logWindow);
    }

    // 設定ボタン：設定ウィンドウを開く
    private void OnClickSetting()
    {
        // 【演出追加】ボタンクリック時の演出（決定SEなど）

        // 【演出追加】設定ウィンドウ 開演出
        //   （例：フェードイン、スライドイン）

        ShowObject(settingWindow);
    }

    // UI On/Offボタン：ScenarioUIを非表示にする
    // （再表示は、非表示中に画面のどこかを左クリックしたとき。Updateで処理）
    private void OnClickUIToggle()
    {
        if (scenarioUI == null) return;

        // 同じクリックで再表示された直後に、すぐ消えてしまうのを防ぐ
        if (shownFrame == Time.frameCount) return;

        if (scenarioUI.activeSelf)
        {
            // 【演出追加】ボタンクリック時の演出（決定SEなど）

            // 【演出追加】ScenarioUI 閉演出
            //   （例：フェードアウト）
            HideObject(scenarioUI);
            hiddenFrame = Time.frameCount;
        }
    }

    // ScenarioUIが非表示のとき、左クリックで再表示する
    private void Update()
    {
        if (scenarioUI == null || scenarioUI.activeSelf) return;
        if (!Input.GetMouseButtonUp(0)) return;

        // 非表示にしたクリックそのもので再表示されるのを防ぐ
        if (hiddenFrame == Time.frameCount) return;

        // 【演出追加】ScenarioUI 開演出
        //   （例：フェードイン）
        ShowObject(scenarioUI);
        shownFrame = Time.frameCount;
    }

    // ログウィンドウのBackボタン：ログウィンドウを閉じる
    private void OnClickLogBack()
    {
        // 【演出追加】ボタンクリック時の演出（キャンセルSEなど）

        // 【演出追加】ログウィンドウ 閉演出
        //   （例：フェードアウト、スライドアウト）

        HideObject(logWindow);
    }

    // 設定ウィンドウのBackボタン：設定ウィンドウを閉じる
    private void OnClickSettingBack()
    {
        // 【演出追加】ボタンクリック時の演出（キャンセルSEなど）

        // 【演出追加】設定ウィンドウ 閉演出
        //   （例：フェードアウト、スライドアウト）

        HideObject(settingWindow);
    }

    // ========================================
    // 表示/非表示（全ウィンドウ共通）
    // ========================================

    private void ShowObject(GameObject obj)
    {
        // 【演出追加】共通の「表示」演出はここ
        //   （例：CanvasGroup のアルファを 0→1 にする）
        //   ウィンドウごとに演出を変えたい場合は、各ボタンの入口側に書く。

        if (obj != null) obj.SetActive(true);
    }

    private void HideObject(GameObject obj)
    {
        // 【演出追加】共通の「非表示」演出はここ
        //   （例：CanvasGroup のアルファを 1→0 にしてから SetActive(false)）
        //   ウィンドウごとに演出を変えたい場合は、各ボタンの入口側に書く。

        if (obj != null) obj.SetActive(false);
    }
}