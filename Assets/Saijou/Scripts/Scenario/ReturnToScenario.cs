using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// LogicScene1?5 / RushScene1?4 に置く共通スクリプト。
/// リザルト画面のボタンなどから ReturnToScenario() を呼ぶと、
/// ScenarioScene に戻って、さっきのCSVの続きから再生される。
/// </summary>
public class ReturnToScenario : MonoBehaviour
{
    [Header("戻り先")]
    [SerializeField] private string scenarioSceneName = "ScenarioScene";

    [Header("再開位置が無いときの移動先")]
    [SerializeField] private string fallbackSceneName = "CharacterSelectScene";

    private bool isLoading = false;

    // リザルトのボタン（OnClick）などから呼ぶ
    public void ReturnToScenarioScene()
    {
        // 二重呼び出し防止
        if (isLoading) return;
        isLoading = true;

        if (ScenarioResume.HasData)
        {
            SceneManager.LoadScene(scenarioSceneName);
        }
        else
        {
            Debug.LogWarning("再開位置が保存されていません。代わりのシーンへ移動します。");
            SceneManager.LoadScene(fallbackSceneName);
        }
    }
}