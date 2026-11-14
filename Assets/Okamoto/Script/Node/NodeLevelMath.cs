// ラッシュ(NodeController)とロジック(ProtoNode)で共通の、ノードのレベル計算。
//  ・ゲージの大きさは 0?100。減少して0になると良性⇔悪性が入れ替わり、増加に転じる。
//    100に届くと折り返して減少する。
//  ・レベルの区切りは mins(Lv1?4の下限)。nodestatus_master の lv〇min を絶対値で渡す。
//  ・「レベル変化ゲージ」は、次にレベル(または良性/悪性)が変わるまでの残り割合(1→0)を返す。
public static class NodeLevelMath
{
    public static readonly float[] DefaultGoodMin = { 0f, 25f, 50f, 75f };
    public static readonly float[] DefaultBadMin = { 1f, 25f, 50f, 75f };

    // 大きさ value のレベル(1?4)
    public static int GetLevel(float value, float[] mins)
    {
        if (mins == null || mins.Length < 4)
        {
            mins = DefaultGoodMin;
        }

        int level = 1;

        for (int i = 1; i < 4; i++)
        {
            if (value >= mins[i])
            {
                level = i + 1;
            }
        }

        return level;
    }

    // 次に見た目(レベル、または良性/悪性)が変わるまでの残り割合(1→0)と、残りのゲージ量。
    //  ・減少中: 現在のレベルの下限(Lv1なら0 = 良性/悪性の入れ替わり)まで
    //  ・増加中: 現在のレベルの上限まで。
    //           Lv4で増加中のときは、100で折り返してLv3に下がるまで
    public static float GetChangeFraction(float value, bool rising, float[] mins, out float remain)
    {
        if (mins == null || mins.Length < 4)
        {
            mins = DefaultGoodMin;
        }

        int band = GetLevel(value, mins) - 1;

        float lower = band == 0 ? 0f : mins[band];
        float upper = band < 3 ? mins[band + 1] : 100f;
        float span;

        if (!rising)
        {
            remain = value - lower;
            span = upper - lower;
        }
        else if (band == 3)
        {
            remain = (100f - value) + (100f - lower);
            span = 2f * (100f - lower);
        }
        else
        {
            remain = upper - value;
            span = upper - lower;
        }

        if (span <= 0f)
        {
            return 0f;
        }

        float fraction = remain / span;

        return fraction < 0f ? 0f : (fraction > 1f ? 1f : fraction);
    }
}