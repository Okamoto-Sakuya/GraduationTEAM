using System.IO;
using UnityEngine;

public class SaveManagerS : MonoBehaviour
{
    public static SaveManagerS instance;

    private SaveData saveData;

    // セーブファイルの保存先
    private string SavePath =>
        Path.Combine(Application.persistentDataPath, "SaveData.json");

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            Load();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // セーブデータを保存
    public void Save()
    {
        string json = JsonUtility.ToJson(saveData, true);

        File.WriteAllText(SavePath, json);

        Debug.Log("セーブ完了");
    }

    // セーブデータを読み込み
    public void Load()
    {
        if (File.Exists(SavePath))
        {
            string json = File.ReadAllText(SavePath);

            saveData = JsonUtility.FromJson<SaveData>(json);

            Debug.Log("ロード完了");
        }
        else
        {
            // セーブデータが存在しない場合
            saveData = new SaveData();

            Save();

            Debug.Log("新規セーブデータ作成");
        }
    }

    // セーブデータを取得
    public SaveData GetSaveData()
    {
        return saveData;
    }

    // セーブデータを削除
    public void DeleteSaveData()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
        }

        saveData = new SaveData();

        Debug.Log("セーブデータ削除");
    }
}
