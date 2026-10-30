#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// メニュー Tools > Result > Create Result Prefab で、リザルト表示のPrefabのひな形を作る。
// Editorフォルダに置くこと(例: Assets/Okamoto/Script/Editor/)
public static class ResultPanelCreator
{
    private const string PrefabPath = "Assets/ResultPanel.prefab";

    [MenuItem("Tools/Result/Create Result Prefab")]
    public static void Create()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "ResultPanelを作成",
                PrefabPath + " はすでにあります。上書きすると、手を加えた内容は消えます。",
                "上書きする",
                "キャンセル");

            if (!overwrite)
            {
                return;
            }
        }

        ResultPanel panel = ResultPanelBuilder.Build();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(panel.gameObject, PrefabPath);

        Object.DestroyImmediate(panel.gameObject);

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);

        Debug.Log(PrefabPath + " を作成しました。GameManagerの Result Panel Prefab に設定してください。");
    }
}
#endif