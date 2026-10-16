using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// タイトルシーンマネージャー
/// </summary>
public class TitleManager : MonoBehaviour
{
    [Header("シーン設定")]
    [SerializeField] private string selectSceneName = "SelectScene";

    /// <summary>
    /// ボタンを押したらゲームシーンへ移動
    /// </summary>
    public void StartGame()
    {
        SceneManager.LoadScene(selectSceneName);
    }

    /// <summary>
    /// ゲームを終了
    /// </summary>
    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("ゲームを終了します");
    }
}