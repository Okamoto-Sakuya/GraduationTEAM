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
        public float volume;
    }
    ClipVolumes clipVolumes;

    [SerializeField]
    List<ClipVolumes> clipVolumesList = new();

    void Start()
    {
        TextAsset csvFile = Resources.Load<TextAsset>(fileName);
        
        if(csvFile == null)
        {
            Debug.LogError("CSVファイルが見つかりませんでした");
            return;
        }

        StringReader reader = new StringReader(csvFile.text);

        string line;
        bool isFirstLine = true;

        while ((line = reader.ReadLine()) != null)
        {
            //1行目をスキップ
            if (isFirstLine)
            {
                isFirstLine = false;
                continue;
            }

            string[] values = line.Split(',');

            //読み取り部分
            clipVolumes = new ClipVolumes();
            clipVolumes.name = values[0];
            clipVolumes.volume = float.Parse(values[1]);
            clipVolumesList.Add(clipVolumes);

            Debug.Log("name" +  clipVolumes.name);
            Debug.Log("volume" +  clipVolumes.volume);
        }
    }
}
