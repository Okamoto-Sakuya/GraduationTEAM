using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// テキストログの管理と表示。
/// ScenarioPlayerが1行表示するたびに AddLog() を呼ぶ。
/// 同じidは1回しか追加しない。ウィンドウが開いたとき最新までスクロールする。
/// </summary>
public class ScenarioLogView : MonoBehaviour
{
    [Header("表示")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;
    [SerializeField] private TMP_Text logEntryPrefab;

    [Header("設定")]
    [SerializeField] private int maxEntries = 200;
    // {0}=話者 {1}=本文。話者の色や改行はここで調整できる
    [SerializeField] private string speakerFormat = "<b>{0}</b>\n{1}";
    [SerializeField] private string noSpeakerFormat = "{0}";

    private readonly List<GameObject> entries = new();
    private readonly HashSet<int> loggedIds = new();

    // 1行追加（同じidなら無視。speakerが空なら地の文）
    // colorはCSVのcolor列（例: #FF0000）。空なら Prefab の色のまま
    public void AddLog(int id, string speaker, string text, string color = "")
    {
        if (string.IsNullOrEmpty(text)) return;
        if (!loggedIds.Add(id)) return;

        string line = string.IsNullOrEmpty(speaker)
            ? string.Format(noSpeakerFormat, text)
            : string.Format(speakerFormat, speaker, text);

        // false: 親に合わせてスケールや位置が崩れないようにする
        TMP_Text entry = Instantiate(logEntryPrefab, content, false);
        entry.richText = true;
        entry.text = line;

        // 色の指定があればログにも反映する
        if (!string.IsNullOrEmpty(color) &&
            ColorUtility.TryParseHtmlString(color, out Color parsedColor))
        {
            entry.color = parsedColor;
        }

        entries.Add(entry.gameObject);

        // 古いログを削除
        while (entries.Count > maxEntries)
        {
            Destroy(entries[0]);
            entries.RemoveAt(0);
        }

        // 表示中なら最新までスクロール
        if (gameObject.activeInHierarchy)
            StartCoroutine(ScrollToBottomNextFrame());
    }

    // 全消去（ストーリー切り替え時など）
    public void Clear()
    {
        foreach (GameObject e in entries)
            if (e != null) Destroy(e);
        entries.Clear();
        loggedIds.Clear();
    }

    // ウィンドウが開いたら最新ログを表示
    private void OnEnable()
    {
        StartCoroutine(ScrollToBottomNextFrame());
    }

    // レイアウト計算が終わるのを1フレーム待ってからスクロールする
    private IEnumerator ScrollToBottomNextFrame()
    {
        yield return null;
        ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        if (scrollRect == null || content == null) return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        // Contentが見える範囲より短いときは、上に揃えたまま動かさない
        float viewportHeight = scrollRect.viewport != null
            ? scrollRect.viewport.rect.height
            : ((RectTransform)scrollRect.transform).rect.height;

        if (content.rect.height <= viewportHeight)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            return;
        }

        scrollRect.verticalNormalizedPosition = 0f;
    }
}