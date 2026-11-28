using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 読み込んだセリフを TextMeshPro に表示するスクリプト
/// </summary>
public class ScenarioPlayer : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private ScenarioCsvLoader loader;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private ScenarioButtonManager buttonManager;
    [SerializeField] private ScenarioLogView logView;
    [SerializeField] private PortraitView portraitView;

    [Header("開始設定")]
    [SerializeField] private int startId = 1;

    [Header("テキストの速さ")]
    // 1文字を表示する間隔（秒）
    // 数字が小さいほど速く、大きいほど遅くなる
    [SerializeField, Min(0.001f)]
    private float characterInterval = 0.05f;

    [Header("スキップ設定")]
    // スキップ中に次のセリフへ進む間隔（秒）
    // 数字が小さいほどスキップが速くなる
    [SerializeField, Min(0.01f)]
    private float skipInterval = 0.1f;

    // 現在、文字送り中かどうか
    private bool isTyping = false;

    // スキップモードが有効かどうか
    private bool isSkipping = false;

    // シナリオが終了したかどうか
    private bool isScenarioEnded = false;

    // 現在表示しているシナリオのID
    private int currentId;

    // 文字送り用コルーチン
    private Coroutine typingCoroutine;

    // スキップ用コルーチン
    private Coroutine skipCoroutine;

    // 現在のセリフ全文
    private string currentMessage = "";

    private void Start()
    {
        // CSVを読み込む
        loader.Load();

        int firstId = startId;

        // 他のシーンから戻ってきた場合は、保存してあるCSVと位置から再開する
        if (ScenarioResume.TryConsume(out string storyName, out int resumeId))
        {
            if (loader.SetStory(storyName))
                firstId = resumeId;
        }

        if (logView != null) logView.Clear();

        // 最初のシナリオを表示する
        currentId = firstId;
        ShowScenario(currentId);
    }
    private void Update()
    {
        // UIが非表示の間は、クリックで文章を進めない
        if (buttonManager != null && !buttonManager.IsUIVisible)
            return;

        // ログ・設定ウィンドウが開いている間は進めない
        if (buttonManager != null && buttonManager.IsWindowOpen)
            return;

        // 左クリックされた瞬間だけ判定
        if (!Input.GetMouseButtonDown(0))
            return;

        // クリック位置にButtonがある場合だけ無視する
        if (IsPointerOverButton())
            return;

        // シナリオ終了後なら次のシーンへ移動
        if (isScenarioEnded)
        {
            LoadNextScene();
        }
        // 文字送り中なら全文を表示
        else if (isTyping)
        {
            CompleteTyping();
        }
        // 全文表示済みなら次のセリフへ進む
        else
        {
            Next();
        }
    }

    // 指定したIDのシナリオを表示する
    public void ShowScenario(int id)
    {
        // 指定したIDのデータを取得する
        if (!loader.TryGetScenario(id, out ScenarioData data))
        {
            Debug.LogWarning($"シナリオID {id} が見つかりません。");
            return;
        }

        // キャラ画像（A・B）を表示する
        if (portraitView != null)
            portraitView.Show(data);

        // 話者名を表示する
        speakerText.text = data.Get("speaker");

        // セリフ本文を取得する
        currentMessage = data.Get("text");

        // ログに追加する（同じidは自動で無視される）
        if (logView != null)
            logView.AddLog(id, data.Get("speaker"), currentMessage);

        // セリフ全体の色を設定する
        string color = data.Get("color");

        if (ColorUtility.TryParseHtmlString(color, out Color parsedColor))
        {
            messageText.color = parsedColor;
        }
        else
        {
            // 色が未指定なら白色にする
            messageText.color = Color.white;
        }

        // 前の文字送り処理があれば停止する
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        // 現在のシナリオIDを更新する
        currentId = id;

        // シナリオ終了状態を解除する
        isScenarioEnded = false;

        // セリフの文字送りを開始する
        typingCoroutine = StartCoroutine(TypeText());
    }

    // セリフを1文字ずつ表示する
    private IEnumerator TypeText()
    {
        isTyping = true;

        // 最初はセリフを非表示にする
        messageText.text = currentMessage;
        messageText.ForceMeshUpdate();

        // タグを除いた表示文字数を取得する
        int totalCharacters = messageText.textInfo.characterCount;

        // 表示文字数を0にする
        messageText.maxVisibleCharacters = 0;

        // スキップ中でなければ1文字ずつ表示する
        for (int i = 1; i <= totalCharacters; i++)
        {
            // スキップ中なら残りの文字を一度に表示する
            if (isSkipping)
            {
                break;
            }

            messageText.maxVisibleCharacters = i;

            // 指定された時間だけ待つ
            yield return new WaitForSeconds(characterInterval);
        }

        // 全文を表示する
        messageText.maxVisibleCharacters = int.MaxValue;

        // 文字送りが終了したことを記録する
        isTyping = false;
        typingCoroutine = null;
    }

    // 文字送りを中断して全文を表示する
    private void CompleteTyping()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        // 全文を表示する
        messageText.maxVisibleCharacters = int.MaxValue;
        isTyping = false;
    }

    // 次のセリフへ進む
    public void Next()
    {
        // 終了後は次のセリフへ進まない
        if (isScenarioEnded)
        {
            return;
        }

        // 現在のシナリオデータを取得する
        if (!loader.TryGetScenario(currentId, out ScenarioData data))
        {
            EndScenario();
            return;
        }

        // CSVから次のシナリオIDを取得する
        string nextIdText = data.Get("nextId");

        // 次のIDが存在する場合は次のセリフへ進む
        if (int.TryParse(nextIdText, out int nextId))
        {
            // 存在しないIDへの移動を防ぐ
            if (loader.TryGetScenario(nextId, out _))
            {
                ShowScenario(nextId);
            }
            else
            {
                Debug.LogWarning($"次のシナリオID {nextId} が見つかりません。");
                EndScenario();
            }
        }
        else
        {
            // nextIdが空欄ならシナリオ終了
            EndScenario();
        }
    }

    // シナリオを終了状態にする
    private void EndScenario()
    {
        isScenarioEnded = true;

        // 終了時はスキップを停止する
        StopSkip();

        Debug.Log("シナリオ終了。左クリックで次のシーンへ移動します。");
    }

    // スキップボタンを押したら、このまとまりの最終行のstoryTypeに応じて遷移
    public void ToggleSkip()
    {
        // シナリオの途中でもスキップできるように終了状態にする
        isScenarioEnded = true;

        // 自動スキップ中ならそれも止める
        StopSkip();

        // 文字送りを停止する
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        isTyping = false;

        // シーン遷移する
        LoadNextScene();
    }

    // スキップを開始する
    public void StartSkip()
    {
        if (isScenarioEnded || isSkipping)
        {
            return;
        }

        isSkipping = true;

        // スキップ処理を開始する
        skipCoroutine = StartCoroutine(SkipRoutine());
    }

    // スキップ中の自動進行処理
    private IEnumerator SkipRoutine()
    {
        while (isSkipping && !isScenarioEnded)
        {
            // 文字送り中なら全文を表示する
            if (isTyping)
            {
                CompleteTyping();
            }
            else
            {
                // 全文表示済みなら次のセリフへ進む
                Next();
            }

            // シナリオが終了したらループを抜ける
            if (isScenarioEnded)
            {
                break;
            }

            // 次のセリフへ進むまで少し待つ
            yield return new WaitForSeconds(skipInterval);
        }

        // スキップ状態を解除する
        isSkipping = false;
        skipCoroutine = null;
    }

    // スキップを停止する
    public void StopSkip()
    {
        isSkipping = false;

        if (skipCoroutine != null)
        {
            StopCoroutine(skipCoroutine);
            skipCoroutine = null;
        }
    }

    // 今のセリフから nextId をたどって、そのまとまりの最終行のstoryTypeに応じてシーン遷移する
    public void LoadNextScene()
    {
        // シナリオ終了後にだけ移動する
        if (!isScenarioEnded)
            return;

        // 今のIDから nextId をたどって最終行を取得する
        ScenarioData endData = FindChainEnd(currentId);

        if (endData == null)
        {
            Debug.LogError($"シナリオID {currentId} が見つかりません。");
            return;
        }

        string storyType = endData.Get("storyType").Trim();

        // 空欄ならシーン移動しない
        if (string.IsNullOrEmpty(storyType))
            return;

        string sceneName = ResolveSceneName(storyType);

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError($"未対応のstoryTypeです: {storyType}");
            return;
        }

        // ロジック／ラッシュへ行くときだけ、戻ってきたときの再開位置を保存する
        string lowerType = storyType.ToLowerInvariant();
        if (lowerType.StartsWith("logicintro") || lowerType.StartsWith("rushintro"))
        {
            if (loader.TryGetNextIdAfter(endData.Id, out int resumeId))
                ScenarioResume.Set(loader.CurrentStoryName, resumeId);
            else
                ScenarioResume.Clear();
        }

        SceneManager.LoadScene(sceneName);

        SceneManager.LoadScene(sceneName);
    }

    // startId から nextId をたどって、そのまとまりの最後の行を返す
    // （nextIdが空、または存在しないIDに当たったところで終了。ループ防止付き）
    private ScenarioData FindChainEnd(int startId)
    {
        ScenarioData last = null;
        int id = startId;
        HashSet<int> visited = new();

        while (visited.Add(id) && loader.TryGetScenario(id, out ScenarioData d))
        {
            last = d;
            if (!d.HasNext) break;
            id = d.NextId;
        }

        return last;
    }

    // storyType から遷移先のシーン名を決める。対応外なら空文字
    private string ResolveSceneName(string storyType)
    {
        string lower = storyType.ToLowerInvariant();

        if (lower == "stageend")
            return "CharacterSelectScene";

        // logicintro1?5 → LogicScene1?5
        if (TryGetNumber(lower, "logicintro", 1, 5, out int logicNo))
            return $"LogicScene{logicNo}";

        // rushintro1?4 → RushScene1?4
        if (TryGetNumber(lower, "rushintro", 1, 4, out int rushNo))
            return $"RushScene{rushNo}";

        if (lower == "ending")
            return "TitleScene";

        return "";
    }

    // "logicintro3" のような文字列から末尾の番号を取り出す（範囲チェック付き）
    private bool TryGetNumber(string value, string prefix, int min, int max, out int number)
    {
        number = 0;
        if (!value.StartsWith(prefix)) return false;

        return int.TryParse(value.Substring(prefix.Length), out number)
            && number >= min && number <= max;
    }

    // 外部から文字送り速度を変更する
    public void SetCharacterInterval(float interval)
    {
        characterInterval = Mathf.Max(0.001f, interval);
    }

    // 現在の文字送り速度を取得する
    public float GetCharacterInterval()
    {
        return characterInterval;
    }

    private bool IsPointerOverButton()
    {
        if (EventSystem.current == null)
            return false;

        // マウス位置にあるUIを取得
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = Input.mousePosition;

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (RaycastResult result in results)
        {
            // Button本体、または親オブジェクトにButtonがあれば無視
            if (result.gameObject.GetComponentInParent<Button>() != null)
            {
                return true;
            }
        }

        return false;
    }
}