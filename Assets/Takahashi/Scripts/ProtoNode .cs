using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ノード（青＝良性 / 赤＝悪性）。
/// ・ゲージ値 0〜100 を持ち、時間で減少 → 0で性質が反転し、今度は上昇
/// ・レベルはゲージ値から 1(0-24) 2(25-49) 3(50-74) 4(75-100)
/// ・周りのリングが残りゲージ（良性出現は反時計回り、悪性出現は時計回り）
/// </summary>
public class ProtoNode : MonoBehaviour
{
    public enum Kind { Good, Bad }

    public static readonly Color GoodColor = new Color(0.0f, 0.69f, 0.94f);
    public static readonly Color BadColor  = new Color(0.78f, 0.0f, 0.0f);

    public Kind CurrentKind { get; private set; }
    public Kind SpawnKind   { get; private set; }
    public float Value      { get; private set; }   // 0〜100
    public RectTransform Rect => (RectTransform)transform;

    public int Level => Value < 25f ? 1 : Value < 50f ? 2 : Value < 75f ? 3 : 4;

    bool rising;          // 反転後は上昇
    Image body, ring;
    Text orderLabel;
    CanvasGroup group;

    /// <summary>消滅準備の点滅・消滅判定中の半透明に使用（0〜1）</summary>
    public void SetAlpha(float a) => group.alpha = a;

    public static ProtoNode Create(Transform parent, Kind kind, float startValue, Font font, float size = 60f)
    {
        var rt = UIShapes.CreateRect(parent, "Node_" + kind, Vector2.zero, new Vector2(size, size));
        var n = rt.gameObject.AddComponent<ProtoNode>();
        n.group = rt.gameObject.AddComponent<CanvasGroup>();
        n.group.blocksRaycasts = false;

        UIShapes.CreateImage(rt, "RingBg", Vector2.zero, new Vector2(size, size), UIShapes.Ring, new Color(1, 1, 1, 0.15f));

        n.ring = UIShapes.CreateImage(rt, "Ring", Vector2.zero, new Vector2(size, size), UIShapes.Ring, Color.white);
        n.ring.type = Image.Type.Filled;
        n.ring.fillMethod = Image.FillMethod.Radial360;
        n.ring.fillOrigin = (int)Image.Origin360.Top;
        // 向きは見た目確認しながら要調整
        n.ring.fillClockwise = (kind == Kind.Bad);

        n.body = UIShapes.CreateImage(rt, "Body", Vector2.zero, new Vector2(size * 0.72f, size * 0.72f), UIShapes.Circle, Color.white);

        n.orderLabel = UIShapes.CreateText(rt, "Order", "", font, 26, Color.white, TextAnchor.MiddleCenter,
                                           Vector2.zero, new Vector2(size, size), new Vector2(0.5f, 0.5f));
        n.orderLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);

        n.SpawnKind = kind;
        n.CurrentKind = kind;
        n.Value = startValue;
        n.rising = false;
        n.Refresh();
        return n;
    }

    /// <summary>問題準備フェーズで「何番目に来るか」を表示。0で非表示</summary>
    public void SetOrder(int order) => orderLabel.text = order > 0 ? order.ToString() : "";

    public void TickGauge(float dt, float perSecond)
    {
        if (rising)
        {
            Value = Mathf.Min(100f, Value + perSecond * dt);
        }
        else
        {
            Value -= perSecond * dt;
            if (Value <= 0f)
            {
                Value = -Value;   // はみ出た分を上昇側へ
                CurrentKind = CurrentKind == Kind.Good ? Kind.Bad : Kind.Good;
                rising = true;
            }
        }
        Refresh();
    }

    void Refresh()
    {
        Color c = CurrentKind == Kind.Good ? GoodColor : BadColor;
        body.color = c;
        ring.color = Color.Lerp(c, Color.white, 0.35f);
        ring.fillAmount = Value / 100f;
    }
}
