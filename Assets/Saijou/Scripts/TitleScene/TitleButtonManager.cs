using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// タイトル画面のボタン管理。
/// タイトル演出(TitleLogoAnimator)が終わったら「Click on the screen」を点滅表示し、
/// クリックされたらテキストを消してメニューボタンを表示する。
/// 演出を追加したい箇所には「【演出追加】」コメントを入れてある。
/// （Ctrl+F で「演出追加」と検索すると全箇所が見つかる）
/// </summary>
public class TitleButtonManager : MonoBehaviour
{
    // ========================================
    // Inspector 設定項目
    // ========================================

    [Header("タイトル画面のボタン")]
    [SerializeField] private Button startButton;     // 開始
    [SerializeField] private Button zukanButton;     // 図鑑
    [SerializeField] private Button settingButton;   // 設定

    [Header("図鑑画面のボタン")]
    [SerializeField] private Button achievementButton;   // 実績（アチーブメント）
    [SerializeField] private Button helpButton;          // ヘルプ
    [SerializeField] private Button zukanBackButton;     // 戻る（図鑑→タイトル）

    [Header("設定画面のボタン")]
    [SerializeField] private Button settingBackButton;   // 戻る（設定→タイトル）

    [Header("ウィンドウ内の戻るボタン")]
    [SerializeField] private Button achievementCloseButton; // 実績ウィンドウの戻る
    [SerializeField] private Button helpCloseButton;        // ヘルプウィンドウの戻る

    [Header("クリック待ち表示")]
    [SerializeField] private CanvasGroup clickPromptGroup;   // 「Click on the screen」のCanvasGroup
    [SerializeField] private GameObject menuButtonsRoot;     // メニューボタンの親オブジェクト
    [SerializeField] private float promptDelay = 0.5f;       // タイトル演出終了後、プロンプトが出るまでの待ち
    [SerializeField] private float fadeDuration = 1.0f;      // フェード片道の秒数
    [SerializeField] private float minAlpha = 0.2f;          // 一番薄い時のアルファ

    [Header("画面・ウィンドウ")]
    [SerializeField] private GameObject titleScreen;        // タイトル画面
    [SerializeField] private GameObject zukanPanel;         // 図鑑Panel
    [SerializeField] private GameObject settingScreen;      // 設定画面
    [SerializeField] private GameObject achievementWindow;  // アチーブメントウィンドウ
    [SerializeField] private GameObject helpWindow;         // ヘルプウィンドウ

    [Header("開始ボタンで移動するシーン名")]
    [SerializeField] private string nextSceneName = "CharacterSelect";

    // ========================================
    // 内部状態
    // ========================================

    private bool waitingForClick = false;
    private float fadeTimer = 0f;

    // ========================================
    // Unity イベント
    // ========================================

    private void Awake()
    {
        // 開始時はタイトル以外を閉じておく
        // 【演出追加】ここは初期化なので基本的に演出は不要（即座に非表示でOK）
        HideObject(zukanPanel);
        HideObject(settingScreen);
        HideObject(achievementWindow);
        HideObject(helpWindow);

        // メニューボタンとクリック待ちテキストは、タイトル演出が終わるまで隠しておく
        HideObject(menuButtonsRoot);
        if (clickPromptGroup != null)
        {
            clickPromptGroup.alpha = 0f;
            clickPromptGroup.gameObject.SetActive(false);
        }

        waitingForClick = false;   // 演出が終わるまではクリックを受け付けない
    }

    private void Update()
    {
        if (!waitingForClick) return;

        // フェードの往復(PingPongで 0→1→0 を繰り返す)
        if (clickPromptGroup != null)
        {
            fadeTimer += Time.unscaledDeltaTime;
            float t = Mathf.PingPong(fadeTimer / fadeDuration, 1f);
            clickPromptGroup.alpha = Mathf.Lerp(minAlpha, 1f, t);
        }

        // 画面のどこかをクリック/タッチしたら
        if (Input.GetMouseButtonDown(0))
        {
            OnClickAnywhere();
        }
    }

    private void OnEnable()
    {
        AddListener(startButton, OnClickStart);
        AddListener(zukanButton, OnClickZukan);
        AddListener(settingButton, OnClickSetting);
        AddListener(achievementButton, OnClickAchievement);
        AddListener(helpButton, OnClickHelp);
        AddListener(zukanBackButton, OnClickZukanBack);
        AddListener(settingBackButton, OnClickSettingBack);
        AddListener(achievementCloseButton, OnClickAchievementClose);
        AddListener(helpCloseButton, OnClickHelpClose);
    }

    private void OnDisable()
    {
        RemoveListener(startButton, OnClickStart);
        RemoveListener(zukanButton, OnClickZukan);
        RemoveListener(settingButton, OnClickSetting);
        RemoveListener(achievementButton, OnClickAchievement);
        RemoveListener(helpButton, OnClickHelp);
        RemoveListener(zukanBackButton, OnClickZukanBack);
        RemoveListener(settingBackButton, OnClickSettingBack);
        RemoveListener(achievementCloseButton, OnClickAchievementClose);
        RemoveListener(helpCloseButton, OnClickHelpClose);
    }

    // ========================================
    // タイトル演出 → Click on the screen → メニュー
    // ========================================

    /// <summary>
    /// TitleLogoAnimator の onFinished から呼ぶ。
    /// </summary>
    public void OnTitleIntroFinished()
    {
        StartCoroutine(ShowPromptAfterDelay());
    }

    private IEnumerator ShowPromptAfterDelay()
    {
        yield return new WaitForSecondsRealtime(promptDelay);

        if (clickPromptGroup != null)
        {
            clickPromptGroup.gameObject.SetActive(true);
            clickPromptGroup.alpha = 0f;
        }
        fadeTimer = 0f;
        waitingForClick = true;   // ここからクリック受付 & 点滅開始
    }

    private void OnClickAnywhere()
    {
        waitingForClick = false;

        // クリック待ちテキストを消す
        if (clickPromptGroup != null) clickPromptGroup.gameObject.SetActive(false);

        // メニューボタンを表示
        ShowObject(menuButtonsRoot);

        // 【演出追加】決定SEやメニューボタンのフェードインなど
    }

    // ========================================
    // ボタンの入口
    // ========================================

    // 開始ボタン：シーン移動
    private void OnClickStart()
    {
        // 【演出追加】ボタンクリック時の演出
        //   （例：決定SE、ボタンの押下アニメーション）

        // 【演出追加】シーン移動前の演出（タイトル→キャラセレ 遷移演出）
        //   （例：フェードアウト、画面ワイプ）
        //   演出が終わってからシーン移動したい場合は、
        //   コルーチンやDOTween等で待ってから LoadScene を呼ぶ。

        SceneManager.LoadScene(nextSceneName);
    }

    // 図鑑ボタン：タイトル→図鑑Panel
    private void OnClickZukan()
    {
        // 【演出追加】ボタンクリック時の演出（決定SEなど）

        // 【演出追加】タイトル→図鑑 遷移演出
        //   （例：フェードアウト→フェードイン、スライド）

        HideObject(titleScreen);
        ShowObject(zukanPanel);
    }

    // 設定ボタン：タイトル→設定画面
    private void OnClickSetting()
    {
        // 【演出追加】ボタンクリック時の演出（決定SEなど）

        // 【演出追加】タイトル→設定 遷移演出
        //   （例：フェードアウト→フェードイン、スライド）

        HideObject(titleScreen);
        ShowObject(settingScreen);
    }

    // 実績ボタン：実績ウィンドウを開く
    private void OnClickAchievement()
    {
        // 【演出追加】ボタンクリック時の演出（決定SEなど）

        // 【演出追加】実績ウィンドウ 開演出
        //   （例：拡大しながらフェードイン、ポップアップ）

        ShowObject(achievementWindow);
    }

    // ヘルプボタン：ヘルプウィンドウを開く
    private void OnClickHelp()
    {
        // 【演出追加】ボタンクリック時の演出（決定SEなど）

        // 【演出追加】ヘルプウィンドウ 開演出
        //   （例：拡大しながらフェードイン、ポップアップ）

        ShowObject(helpWindow);
    }

    // 実績ウィンドウの戻るボタン
    private void OnClickAchievementClose()
    {
        // 【演出追加】ボタンクリック時の演出（キャンセルSEなど）

        // 【演出追加】実績ウィンドウ 閉演出
        //   （例：縮小しながらフェードアウト）

        HideObject(achievementWindow);
    }

    // ヘルプウィンドウの戻るボタン
    private void OnClickHelpClose()
    {
        // 【演出追加】ボタンクリック時の演出（キャンセルSEなど）

        // 【演出追加】ヘルプウィンドウ 閉演出
        //   （例：縮小しながらフェードアウト）

        HideObject(helpWindow);
    }

    // 図鑑画面の戻るボタン：図鑑→タイトル
    private void OnClickZukanBack()
    {
        // 【演出追加】ボタンクリック時の演出（キャンセルSEなど）

        // 【演出追加】図鑑→タイトル 遷移演出
        //   （例：フェードアウト→フェードイン、スライド）

        HideObject(zukanPanel);
        ShowObject(titleScreen);
    }

    // 設定画面の戻るボタン：設定→タイトル
    private void OnClickSettingBack()
    {
        // 【演出追加】ボタンクリック時の演出（キャンセルSEなど）

        // 【演出追加】設定→タイトル 遷移演出
        //   （例：フェードアウト→フェードイン、スライド）

        HideObject(settingScreen);
        ShowObject(titleScreen);
    }

    // ========================================
    // 表示/非表示（全画面・ウィンドウ共通）
    // ========================================

    private void ShowObject(GameObject obj)
    {
        // 【演出追加】全画面・ウィンドウ共通の「表示」演出はここ
        //   （例：CanvasGroup のアルファを 0→1 にする）
        //   画面ごとに演出を変えたい場合は、各ボタンの入口側に書く。

        if (obj != null) obj.SetActive(true);
    }

    private void HideObject(GameObject obj)
    {
        // 【演出追加】全画面・ウィンドウ共通の「非表示」演出はここ
        //   （例：CanvasGroup のアルファを 1→0 にしてから SetActive(false)）
        //   画面ごとに演出を変えたい場合は、各ボタンの入口側に書く。

        if (obj != null) obj.SetActive(false);
    }

    // ========================================
    // ボタン登録の補助
    // ========================================

    private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) button.onClick.AddListener(action);
    }

    private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) button.onClick.RemoveListener(action);
    }
}