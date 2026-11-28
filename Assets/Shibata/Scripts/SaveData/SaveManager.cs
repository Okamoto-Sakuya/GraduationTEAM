using System;
using System.IO;
using UnityEngine;

//使用方法
//============================================================
//SaveData data = SaveManager.Instance.GetSaveData();

//data.rushStages[0].isClear = true;
//data.rushStages[0].hiScore = 1500f;

//SaveManager.Instance.Save();
//============================================================

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private SaveData saveData;

    //============================================================
    // ファイル
    //============================================================

    private string SavePath =>
        Path.Combine(Application.persistentDataPath, "SaveData.json");

    private string BackupPath =>
        SavePath + ".bak";

    private string TempPath =>
        SavePath + ".tmp";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        Load();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    //============================================================
    // Save
    //============================================================

    public void Save()
    {
        if (saveData == null)
        {
            Debug.LogError("セーブデータが存在しません。");
            return;
        }

        try
        {
            // データの補正
            saveData.Validate();

            // JSON変換
            string json = JsonUtility.ToJson(saveData, true);

            // 保存先ディレクトリの作成
            string directory = Path.GetDirectoryName(SavePath);
            Directory.CreateDirectory(directory);

            // 一時ファイルに書き込む
            File.WriteAllText(TempPath, json);

            if (File.Exists(SavePath))
            {
                // 古いバックアップを削除
                if (File.Exists(BackupPath))
                {
                    File.Delete(BackupPath);
                }

                // 元ファイルをバックアップしながら置き換え
                File.Replace(TempPath, SavePath, BackupPath);
            }
            else
            {
                // 初回保存
                File.Move(TempPath, SavePath);
            }

            Debug.Log("セーブ完了");
        }
        catch (Exception e)
        {
            Debug.LogError($"セーブに失敗しました。\n{e}");
        }
    }

    //============================================================
    // Load
    //============================================================

    public void Load()
    {
        // メインファイルを読み込み
        if (TryLoadFile(SavePath))
        {
            Debug.Log("ロード完了");
            return;
        }

        // メインが失敗した場合、バックアップを読み込み
        if (TryLoadFile(BackupPath))
        {
            Debug.LogWarning("バックアップからセーブデータを復旧しました。");

            // メインファイルを復旧
            Save();
            return;
        }

        // どちらも読み込めなかった場合
        saveData = new SaveData();

        saveData.Validate();

        Save();

        Debug.LogWarning("新規セーブデータを作成しました。");
    }

    // ファイル読み込み処理
    private bool TryLoadFile(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);

            SaveData loadedData =
                JsonUtility.FromJson<SaveData>(json);

            if (loadedData == null)
            {
                return false;
            }

            // バージョン確認・データ補正
            loadedData.Validate();

            saveData = loadedData;

            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                $"セーブデータの読み込みに失敗しました。\n{path}\n{e.Message}"
            );

            return false;
        }
    }


    //============================================================
    // Get
    //============================================================

    public SaveData GetSaveData()
    {
        return saveData;
    }

    //============================================================
    // Delete
    //============================================================

    public void DeleteSaveData()
    {
        try
        {
            if (File.Exists(SavePath))
                File.Delete(SavePath);

            if (File.Exists(BackupPath))
                File.Delete(BackupPath);

            if (File.Exists(TempPath))
                File.Delete(TempPath);

            saveData = new SaveData();
            saveData.Validate();

            Debug.Log("セーブデータを削除しました。");
        }
        catch (Exception e)
        {
            Debug.LogError($"セーブデータの削除に失敗しました。\n{e}");
        }
    }
}