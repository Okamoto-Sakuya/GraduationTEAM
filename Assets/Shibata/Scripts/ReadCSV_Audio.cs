using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ReadCSV_Audio : MonoBehaviour
{
    string fileName = "volume";

    //============================================================
    // 音量管理クラス
    //============================================================
    [System.Serializable]
    public class ClipVolumes
    {
        public string name;
        [Range(0f, 1f)]
        public float volume = 1f;
    }

    ClipVolumes clipVolumes;

    [SerializeField]
    List<ClipVolumes> clipVolumesList = new();

    //============================================================
    // Start
    //============================================================

    void Start()
    {
        TextAsset csvFile = Resources.Load<TextAsset>(fileName);

        if (csvFile == null)
        {
            Debug.LogError("CSVファイルが見つかりませんでした");
            return;
        }

        StringReader reader = new StringReader(csvFile.text);

        string line;
        bool isFirstLine = true;

        while ((line = reader.ReadLine()) != null)
        {
            // 1行目をスキップ
            if (isFirstLine)
            {
                isFirstLine = false;
                continue;
            }

            string[] values = line.Split(',');

            // データ不足をチェック
            if (values.Length < 2)
                continue;

            if (!float.TryParse(
                values[1],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float volume))
            {
                Debug.LogWarning("音量の読み込みに失敗: " + line);
                continue;
            }

            clipVolumes = new ClipVolumes();
            clipVolumes.name = values[0].Trim();
            clipVolumes.volume = Mathf.Clamp01(volume);

            clipVolumesList.Add(clipVolumes);

            Debug.Log("name: " + clipVolumes.name);
            Debug.Log("volume: " + clipVolumes.volume);
        }
    }

    //============================================================
    // SE名から音量を取得
    //============================================================
    public float GetVolume(string seName)
    {
        foreach (ClipVolumes data in clipVolumesList)
        {
            if (data.name == seName)
            {
                return data.volume;
            }
        }

        // CSVに登録されていないSEは最大音量
        Debug.LogWarning("音量設定が見つかりません: " + seName);
        return 1f;
    }
}