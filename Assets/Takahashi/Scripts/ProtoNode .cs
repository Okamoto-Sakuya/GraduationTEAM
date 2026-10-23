using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ノード（青＝良性 / 赤＝悪性）。
/// ・ゲージ値 0〜100 を往復。減少 → 0で性質が反転して上昇 → 100で折り返して減少 → …（ずっと変化し続ける）
///   例（良性出現）: 良性100→0 → 悪性0→100 → 悪性100→0 → 良性0→100 → 良性100→0 …
/// ・レベルはゲージ値から 1(0-24) 2(25-49) 3(50-74) 4(75-100)
/// ・周りのリングが残りゲージ（良性出現は反時計回り、悪性出現は時計回り。反転しても向きは出現時のまま）
/// </summary>
public class ProtoNode : MonoBehaviour
{
    public enum Kind { Good, Bad }

    public static readonly Color GoodColor = new Color(0.0f, 0.69f, 0.94f);
    public static readonly Color BadColor = new Color(0.78f, 0.0f, 0.0f);

    // 画像差し替え（LogicModeScreen の Inspector から設定。null なら仮の丸・輪）
    public static Sprite GoodSprite;
    public static Sprite BadSprite;
    public static Sprite RingSprite;
    public static float DefaultSize = 60f;

    public Kind CurrentKind { get; private set; }
    public Kind SpawnKind { get; private set; }
    public float Value { get; private set; }   // 0〜100
    public RectTransform Rect => (RectTransform)transform;

    public int Level => Value < 25f ? 1 : Value < 50f ? 2 : Value < 75f ? 3 : 4;

    bool rising;          // true = 上昇中 / false = 減少中
    Image body, ring;
    Text orderLabel;
    CanvasGroup group;

    /// <summary>消滅準備の点滅・消滅判定中の半透明に使用（0〜1）</summary>
    public void SetAlpha(float a) => group.alpha = a;

    public static ProtoNode Create(Transform parent, Kind kind, float startValue, Font font, float size = -1f)
    {
        if (size <= 0f) size = DefaultSize;
        Sprite ringSprite = RingSprite != null ? RingSprite : UIShapes.Ring;
        var rt = UIShapes.CreateRect(parent, "Node_" + kind, Vector2.zero, new Vector2(size, size));
        var n = rt.gameObject.AddComponent<ProtoNode>();
        n.group = rt.gameObject.AddComponent<CanvasGroup>();
        n.group.blocksRaycasts = false;

        UIShapes.CreateImage(rt, "RingBg", Vector2.zero, new Vector2(size, size), ringSprite, new Color(1, 1, 1, 0.15f));

        n.ring = UIShapes.CreateImage(rt, "Ring", Vector2.zero, new Vector2(size, size), ringSprite, Color.white);
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

    /// <summary>
    /// ゲージ更新。0 と 100 の間を往復し、0 に届くたびに性質が反転する。
    ///   減少 → 0 到達：性質反転、上昇に転じる
    ///   上昇 → 100 到達：折り返して減少
    /// </summary>
    public void TickGauge(float dt, float perSecond)
    {
        float step = perSecond * dt;

        if (rising)
        {
            Value += step;
            if (Value >= 100f)
            {
                Value = 200f - Value;   // はみ出た分を減少側へ
                rising = false;
            }
        }
        else
        {
            Value -= step;
            if (Value <= 0f)
            {
                Value = -Value;         // はみ出た分を上昇側へ
                CurrentKind = CurrentKind == Kind.Good ? Kind.Bad : Kind.Good;
                rising = true;
            }
        }

        Value = Mathf.Clamp(Value, 0f, 100f);   // 1フレームで大きく進んだ場合の保険
        Refresh();
    }

    void Refresh()
    {
        Color c = CurrentKind == Kind.Good ? GoodColor : BadColor;

        // 本体：画像があればその画像（色はそのまま）、無ければ白い丸に色を乗せる
        Sprite s = CurrentKind == Kind.Good ? GoodSprite : BadSprite;
        if (s != null)
        {
            body.sprite = s;
            body.preserveAspect = true;
            body.color = Color.white;
        }
        else
        {
            body.sprite = UIShapes.Circle;
            body.color = c;
        }
        ring.color = Color.Lerp(c, Color.white, 0.35f);
        ring.fillAmount = Value / 100f;
    }
}