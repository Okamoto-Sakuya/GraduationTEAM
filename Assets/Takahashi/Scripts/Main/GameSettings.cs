using UnityEngine;

/// <summary>
/// ゲーム全体の設定（どのスクリプトからでも使える共通の入れ物）。
///   読む : GameSettings.Operation
///   書く : GameSettings.Operation = GameSettings.OperationType.Click;（PlayerPrefs に保存され、次回起動時も残る）
///   変更を受け取る : GameSettings.Changed += () => { ... };
///
/// 今は Inspector（GameSettingsInspector）から変更する仮の形。
/// 設定画面を作るときは、そのボタンから GameSettings.Operation を書き換えるだけでよい。
/// </summary>
public static class GameSettings
{
    /// <summary>造線の操作タイプ（メイン共通仕様「操作方法」）</summary>
    public enum OperationType
    {
        DragAndDrop,   // 基本線上でクリック → ドラッグ → 別の基本線上でドロップ
        Click          // 基本線上でクリック → カーソルを動かす → 別の基本線上でクリック
    }

    const string KeyOperation = "Settings.Operation";

    static bool loaded;
    static OperationType operation = OperationType.DragAndDrop;

    /// <summary>設定が変わったときに呼ばれる</summary>
    public static event System.Action Changed;

    public static OperationType Operation
    {
        get { Load(); return operation; }
        set
        {
            Load();
            if (operation == value) return;
            operation = value;
            PlayerPrefs.SetInt(KeyOperation, (int)value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        int v = PlayerPrefs.GetInt(KeyOperation, (int)OperationType.DragAndDrop);
        operation = System.Enum.IsDefined(typeof(OperationType), v) ? (OperationType)v : OperationType.DragAndDrop;
    }

    /// <summary>表示名（設定画面用）</summary>
    public static string LabelOf(OperationType t) => t == OperationType.Click ? "クリック" : "ドラッグ&ドロップ";
}
