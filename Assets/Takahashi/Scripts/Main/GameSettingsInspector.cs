using UnityEngine;

/// <summary>
/// GameSettings を Inspector から変更するための仮の窓口（設定画面ができるまでの代わり）。
/// 空の GameObject にアタッチして使う。LogicModeScreen はシーンに無ければ自動で追加する。
/// 再生中に Inspector の値を変えると、すぐゲームに反映される。
/// </summary>
public class GameSettingsInspector : MonoBehaviour
{
    [Header("操作タイプ（設定画面の代わり。再生中に変えてもすぐ反映）")]
    public GameSettings.OperationType operation = GameSettings.OperationType.DragAndDrop;

    [Tooltip("true = 再生開始時に、保存されている設定ではなく上の値を使う")]
    public bool applyOnStart = true;

    /// <summary>コードから AddComponent するとき true にしておくと、初期値ではなく保存済みの設定を使う</summary>
    public static bool adoptSavedOnNextAwake;

    void Awake()
    {
        bool adopt = adoptSavedOnNextAwake;
        adoptSavedOnNextAwake = false;
        if (adopt) { applyOnStart = false; operation = GameSettings.Operation; return; }
        if (applyOnStart) GameSettings.Operation = operation;
        else operation = GameSettings.Operation;
    }

    void OnValidate()
    {
        if (Application.isPlaying) GameSettings.Operation = operation;
    }

    void Update()
    {
        // ほかのスクリプト（将来の設定画面など）が変えた値を Inspector にも映す
        if (operation != GameSettings.Operation) operation = GameSettings.Operation;
    }
}
