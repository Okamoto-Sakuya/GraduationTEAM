using System;
using UnityEngine;

[Serializable]
public class SaveData
{
    //============================================================
    // バージョン
    //============================================================

    public int saveVersion = 1;

    //============================================================
    // 前回プレイ情報
    //============================================================

    public enum LastPlayChar
    {
        None,
        Rush,
        Logic,
    }

    public enum LastPlayEnding
    {
        None,
        Bad,
        Normal,
        Happy,
    }

    [Serializable]
    public class LastStageInfo
    {
        public LastPlayChar lastPlayChar;
        public int lastPlayStage;
    }

    public LastStageInfo lastStageInfo = new LastStageInfo();
    public LastPlayEnding lastPlayEnding;

    //============================================================
    // 進行度
    //============================================================

    public int progressRush;
    public int progressLogic;

    //============================================================
    // ステージ情報
    //============================================================

    [Serializable]
    public class StageInfo
    {
        public bool isClear;
        public float hiScore;
    }

    public StageInfo[] rushStages = new StageInfo[4];
    public StageInfo[] logicStages = new StageInfo[5];

    //============================================================
    // 覚え書きフラグ
    //============================================================

    public bool[] memoFlags = new bool[0];

    //============================================================
    // 実績フラグ
    //============================================================

    public bool[] achievementFlags = new bool[0];

    //============================================================
    // システム設定
    //============================================================

    public enum PlayType
    {
        DragAndDrop,
        Click,
    }

    [Serializable]
    public class WindowSize
    {
        public int width;
        public int height;
    }

    [Serializable]
    public class SystemSettings
    {
        public float masterVolume = 1.0f;
        public float bgmVolume = 1.0f;
        public float seVolume = 1.0f;

        public WindowSize windowSize = new WindowSize();

        public float mouseSens = 5.0f;

        public PlayType playType = PlayType.DragAndDrop;
    }

    public SystemSettings settings = new SystemSettings();

    //============================================================
    // 初期データ生成
    //============================================================

    public static SaveData CreateDefault()
    {
        SaveData data = new SaveData();

        data.lastStageInfo = new LastStageInfo();
        data.lastPlayEnding = LastPlayEnding.None;

        data.progressRush = 0;
        data.progressLogic = 0;

        data.rushStages = CreateStages(4);
        data.logicStages = CreateStages(5);

        data.memoFlags = new bool[0];
        data.achievementFlags = new bool[0];

        data.settings = new SystemSettings
        {
            masterVolume = 1.0f,
            bgmVolume = 1.0f,
            seVolume = 1.0f,
            windowSize = new WindowSize
            {
                width = 1920,
                height = 1080
            },
            mouseSens = 5.0f,
            playType = PlayType.DragAndDrop
        };

        return data;
    }

    private static StageInfo[] CreateStages(int count)
    {
        StageInfo[] stages = new StageInfo[count];

        for (int i = 0; i < count; i++)
        {
            stages[i] = new StageInfo();
        }

        return stages;
    }

    //============================================================
    // データ検証・補正
    //============================================================

    public void Validate()
    {
        // 前回プレイ情報
        if (lastStageInfo == null)
            lastStageInfo = new LastStageInfo();

        lastStageInfo.lastPlayStage =
            Mathf.Clamp(lastStageInfo.lastPlayStage, 0, 4);

        if (!Enum.IsDefined(typeof(LastPlayChar),
            lastStageInfo.lastPlayChar))
        {
            lastStageInfo.lastPlayChar = LastPlayChar.None;
        }

        if (!Enum.IsDefined(typeof(LastPlayEnding),
            lastPlayEnding))
        {
            lastPlayEnding = LastPlayEnding.None;
        }

        // 進行度
        progressRush = Mathf.Clamp(progressRush, 0, 4);
        progressLogic = Mathf.Clamp(progressLogic, 0, 5);

        // ステージ
        ValidateStages(ref rushStages, 4);
        ValidateStages(ref logicStages, 5);

        // フラグ
        if (memoFlags == null)
            memoFlags = new bool[0];

        if (achievementFlags == null)
            achievementFlags = new bool[0];

        // システム設定
        if (settings == null)
            settings = new SystemSettings();

        if (settings.windowSize == null)
            settings.windowSize = new WindowSize();

        settings.masterVolume =
            Clamp01(settings.masterVolume);

        settings.bgmVolume =
            Clamp01(settings.bgmVolume);

        settings.seVolume =
            Clamp01(settings.seVolume);

        settings.mouseSens =
            ClampFinite(settings.mouseSens, 0f, 10f, 5f);

        if (settings.windowSize.width <= 0)
            settings.windowSize.width = 1920;

        if (settings.windowSize.height <= 0)
            settings.windowSize.height = 1080;

        if (!Enum.IsDefined(typeof(PlayType), settings.playType))
            settings.playType = PlayType.DragAndDrop;
    }

    private static void ValidateStages(
        ref StageInfo[] stages, int count)
    {
        if (stages == null)
            stages = new StageInfo[count];

        // 配列の長さが違う場合も既存データを引き継ぐ
        if (stages.Length != count)
            Array.Resize(ref stages, count);

        for (int i = 0; i < stages.Length; i++)
        {
            if (stages[i] == null)
                stages[i] = new StageInfo();

            stages[i].hiScore =
                ClampFinite(stages[i].hiScore, 0f, float.MaxValue, 0f);
        }
    }

    private void InitializeStages(StageInfo[] stages)
    {
        for (int i = 0; i < stages.Length; i++)
        {
            if (stages[i] == null)
                stages[i] = new StageInfo();
        }
    }

    private static float Clamp01(float value)
    {
        return ClampFinite(value, 0f, 1f, 1f);
    }

    private static float ClampFinite(
        float value, float min, float max, float defaultValue)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return defaultValue;

        return Mathf.Clamp(value, min, max);
    }
}
