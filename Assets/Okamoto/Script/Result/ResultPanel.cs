using TMPro;
using UnityEngine;
using UnityEngine.UI;

// リザルトに表示する内容
public class ResultData
{
    public bool cleared;           // true=クリア、false=ゲームオーバー
    public int score;              // 取得スコア
    public string rank;            // スコアランク(S, A, B, C, D)
    public int[] benignCounts;     // 取得した良性ノードの数(レベル1?4の順)
    public int[] malignantCounts;  // 取得した悪性ノードの数(レベル1?4の順)
}

// リザルト表示のPrefabに付けるスクリプト。
// 見た目はPrefabの中で自由に作り替えてよい。ここに各テキストなどを割り当てるだけで動く
public class ResultPanel : MonoBehaviour
{
    [Header("テキスト")]
    public TMP_Text titleText;
    public TMP_Text scoreText;
    public TMP_Text rankText;

    [Header("取得ノード(レベル1?4の順に割り当てる)")]
    public TMP_Text[] benignCountTexts = new TMP_Text[4];
    public TMP_Text[] malignantCountTexts = new TMP_Text[4];

    [Header("ボタン")]
    public Button retryButton;

    [Header("表示の形式")]
    public string gameOverTitle = "GAME OVER";
    public string clearTitle = "CLEAR!";

    [Tooltip("{0}にスコアが入る")]
    public string scoreFormat = "SCORE {0:N0}";

    [Tooltip("{0}にランクが入る")]
    public string rankFormat = "RANK {0}";

    [Tooltip("{0}=レベル、{1}=取得した数")]
    public string countFormat = "Lv{0}  x{1}";

    [Header("ランクの色")]
    public Color rankColorS = new Color(1f, 0.85f, 0.2f, 1f);
    public Color rankColorA = new Color(1f, 0.55f, 0.2f, 1f);
    public Color rankColorB = new Color(0.4f, 0.9f, 0.4f, 1f);
    public Color rankColorC = new Color(0.4f, 0.8f, 1f, 1f);
    public Color rankColorD = new Color(0.7f, 0.7f, 0.7f, 1f);

    private void Awake()
    {
        if (retryButton != null)
        {
            retryButton.onClick.AddListener(OnRetry);
        }
    }

    private void OnRetry()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.Retry();
        }
    }

    public void Show(ResultData data)
    {
        if (titleText != null)
        {
            titleText.text = data.cleared ? clearTitle : gameOverTitle;
        }

        if (scoreText != null)
        {
            scoreText.text = string.Format(scoreFormat, data.score);
        }

        if (rankText != null)
        {
            rankText.text = string.Format(rankFormat, data.rank);
            rankText.color = GetRankColor(data.rank);
        }

        ShowCounts(benignCountTexts, data.benignCounts);
        ShowCounts(malignantCountTexts, data.malignantCounts);

        gameObject.SetActive(true);
    }

    private void ShowCounts(TMP_Text[] texts, int[] counts)
    {
        if (texts == null)
        {
            return;
        }

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null)
            {
                continue;
            }

            int count = (counts != null && i < counts.Length) ? counts[i] : 0;

            texts[i].text = string.Format(countFormat, i + 1, count);
        }
    }

    private Color GetRankColor(string rank)
    {
        switch (rank)
        {
            case "S": return rankColorS;
            case "A": return rankColorA;
            case "B": return rankColorB;
            case "C": return rankColorC;
            default: return rankColorD;
        }
    }
}