/// <summary>
/// シーンをまたいで「どのCSVの、どのidから再生するか」を持ち回る入れ物。
/// MonoBehaviourではないので、シーンに置く必要はない。
/// </summary>
public static class ScenarioResume
{
    public static bool HasData { get; private set; }
    public static string StoryName { get; private set; }
    public static int StartId { get; private set; }

    // 再開位置を保存する（ストーリー名はCSVのファイル名・拡張子なし）
    public static void Set(string storyName, int startId)
    {
        StoryName = storyName;
        StartId = startId;
        HasData = true;
    }

    // 保存してある再開位置を取り出す。取り出したら消える（1回限り）
    public static bool TryConsume(out string storyName, out int startId)
    {
        storyName = StoryName;
        startId = StartId;

        bool had = HasData;
        Clear();
        return had;
    }

    public static void Clear()
    {
        HasData = false;
        StoryName = null;
        StartId = 0;
    }
}