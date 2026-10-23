using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>
/// デバッグ用キー（エディタと Development Build でだけ動く）
///   F1?F4 : いま開いているパネルの Stage1?4 をクリアしたことにする
///   F5     : いま開いているパネルの進行をリセット
///   F6     : Mother と Daughter の保存をまとめて消す
/// どのオブジェクトに付けてもよい（Canvas の外が無難）。
/// </summary>
public class FogDebugKeys : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    void Update()
    {
        // 表示中のパネルのコントローラーだけが対象になる（非表示のパネルは含まれない）
        var controllers = FindObjectsByType<FogStageController>(FindObjectsSortMode.None);

        for (int stage = 1; stage <= 4; stage++)
            if (Pressed(stage)) foreach (var c in controllers) c.OnStageCleared(stage);

        if (Pressed(5)) foreach (var c in controllers) c.ResetProgress();

        if (Pressed(6))
        {
            PlayerPrefs.DeleteKey("FogStage.Unlocked.Mother");
            PlayerPrefs.DeleteKey("FogStage.Unlocked.Daughter");
            PlayerPrefs.Save();
            foreach (var c in controllers) c.ResetProgress();
        }
    }

    // n = 1?6 → F1?F6
    static bool Pressed(int n)
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var kb = Keyboard.current;
        if (kb == null) return false;
        switch (n)
        {
            case 1: return kb.f1Key.wasPressedThisFrame;
            case 2: return kb.f2Key.wasPressedThisFrame;
            case 3: return kb.f3Key.wasPressedThisFrame;
            case 4: return kb.f4Key.wasPressedThisFrame;
            case 5: return kb.f5Key.wasPressedThisFrame;
            default: return kb.f6Key.wasPressedThisFrame;
        }
#else
        return Input.GetKeyDown(KeyCode.F1 + (n - 1));
#endif
    }
#endif
}