using UnityEngine.SceneManagement;

/// <summary>
/// シーン間の受け渡し用。
/// ScenarioScene終了後の行き先は「CSV指定 > 既定の行き先」の優先順位で決まる
/// </summary>
public static class SceneFlow
{
    public const string ScenarioScene = "ScenarioScene";

    // SelectButtonManagerが設定する既定の行き先（RushScene / LogicScene）
    public static string DefaultNextScene { get; private set; }

    // CSVで指定された行き先（指定がなければ null）
    public static string OverrideNextScene { get; private set; }

    public static int SelectedStage { get; private set; }

    // 実際に遷移する先（CSV指定があればそれを優先）
    public static string NextScene =>
        string.IsNullOrEmpty(OverrideNextScene) ? DefaultNextScene : OverrideNextScene;

    // セレクト画面から呼ぶ：ScenarioSceneを挟んで遷移
    public static void LoadViaScenario(string defaultNextScene, int stageIndex)
    {
        DefaultNextScene = defaultNextScene;
        OverrideNextScene = null; // 前回のCSV指定を必ずリセット
        SelectedStage = stageIndex;
        SceneManager.LoadScene(ScenarioScene);
    }

    // CSVの解析時に呼ぶ：行き先を上書きする
    public static void SetOverrideNextScene(string sceneName)
    {
        OverrideNextScene = sceneName;
    }

    // ScenarioSceneの終了時に呼ぶ
    public static void LoadNext()
    {
        if (string.IsNullOrEmpty(NextScene))
        {
            UnityEngine.Debug.LogError("SceneFlow: 行き先が設定されていません。SelectSceneから開始してください。");
            return;
        }
        SceneManager.LoadScene(NextScene);
    }
}