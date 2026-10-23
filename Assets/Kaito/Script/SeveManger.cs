using System.IO;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SaveManager : MonoBehaviour
{
    //初めからボタンから呼び出す


    // スタートボタンから呼び出す
    public void StartGame()
    {
        // セーブデータの保存先
        string path = Path.Combine(
            Application.dataPath,
            "Kaito",
            "SeveFaile",
            "SaveFile.json"
        );
        Debug.Log("セーブデータのパス：" + path);
        if (!File.Exists(path))
        {
            SaveData data = new SaveData();

            //data.money = 0;
            //data.level = 1;

            JsonManager.DefaultSave(path, data, true);

            Debug.Log("新しいセーブデータを作成しました");
        }
        // ゲームシーンへ移動
        SceneManager.LoadScene("SeveSample");
    }

    // 続きからボタンから呼び出す
    public void ContinueGame()
    {
        // JSONファイルの保存先
        string path = Path.Combine(
            Application.dataPath,
            "Kaito",
            "SeveFaile",
            "SaveFile.json"
        );

        // JSONファイルが存在するか確認
        if (File.Exists(path))
        {
            // ゲームシーンへ移動
            SceneManager.LoadScene("SeveSample");
            Debug.Log("JSONのセーブデータがあります！");
        }
        else
        {
            Debug.Log("JSONのセーブデータがありません！");
        }
    }
}
