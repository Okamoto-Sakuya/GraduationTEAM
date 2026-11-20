using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ノード（青＝良性 / 赤＝悪性）。
/// ・属性値（メイン共通仕様 11/12）は −100〜+100。0 以上が良性、マイナスが悪性。
///   内部では「大きさ Value（0〜100）＋ 性質 CurrentKind」で持つ（SignedValue = 良性なら +Value、悪性なら −Value）。
///   +100 → 0 → −100 → 0 → +100 … と往復するのは、大きさで見ると 100→0（反転）→100→0（反転）… と同じ
///   例（良性出現）: 良性100→0 → 悪性0→100 → 悪性100→0 → 良性0→100 → 良性100→0 …
/// ・レベルは大きさから。区切りは nodestatus_master の lv〇min（SetLevelThresholds）。初期値 1(0-24) 2(25-49) 3(50-74) 4(75-100)
/// ・周りのリングが残りゲージ（良性出現は反時計回り、悪性出現は時計回り。反転しても向きは出現時のまま）
/// ・特殊ノードは下に種類の札を出す（SetTag。「右腕」「迅速」「重圧」「停滞」「交錯」「造線」など）
/// ・見た目（仮）：良性 = 青い玉 / 悪性 = 赤いトゲトゲ（目なし）＋ゲージのリング。種類ごとのイラストは GoodSprite / BadSprite で差し替え。出現順は右上の白いバッジ
/// ・LockGauge でゲージを固定できる（交錯ノード：時間で変化せず、FlipKind でのみ良性⇔悪性が入れ替わる）
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

    // 仮の見た目の色（図鑑の絵に寄せる：良性 = 青い玉 / 悪性 = 赤いトゲトゲ）
    static readonly Color GoodBody = new Color(0.28f, 0.62f, 0.95f);
    static readonly Color GoodRing = new Color(0.70f, 0.90f, 1f);
    static readonly Color BadBody = new Color(0.82f, 0.06f, 0.10f);
    static readonly Color BadCore = new Color(0.40f, 0.02f, 0.05f);
    static readonly Color BadRing = new Color(1f, 0.38f, 0.36f);
    static readonly Color BadgeColor = new Color(0.95f, 0.94f, 0.92f);   // 番号バッジの地
    static readonly Color BadgeText = new Color(0.08f, 0.08f, 0.12f);

    public Kind CurrentKind { get; private set; }
    public Kind SpawnKind { get; private set; }
    public float Value { get; private set; }   // 大きさ 0〜100
    public RectTransform Rect => (RectTransform)transform;

    /// <summary>属性値 −100〜+100（良性 = プラス、悪性 = マイナス）</summary>
    public float SignedValue => CurrentKind == Kind.Good ? Value : -Value;

    // レベルの下限（大きさ）。Lv1〜4。悪性は lv-〇min の絶対値
    float[] goodMin = { 0f, 25f, 50f, 75f };
    float[] badMin = { 1f, 25f, 50f, 75f };

    /// <summary>今のレベル 1〜4（良性・悪性それぞれの区切りで判定）</summary>
    public int Level
    {
        get
        {
            var mins = CurrentKind == Kind.Good ? goodMin : badMin;
            int lv = 1;
            for (int i = 1; i < 4 && i < mins.Length; i++) if (Value >= mins[i]) lv = i + 1;
            return lv;
        }
    }

    /// <summary>レベルの区切りを設定（nodestatus_master の lv〇min。悪性は絶対値で渡す）</summary>
    public void SetLevelThresholds(float[] good, float[] bad)
    {
        if (good != null && good.Length >= 4) goodMin = (float[])good.Clone();
        if (bad != null && bad.Length >= 4) badMin = (float[])bad.Clone();
    }

    /// <summary>true = ゲージが時間で変化しない（交差ノード）</summary>
    public bool GaugeLocked { get; private set; }

    bool rising;          // true = 上昇中 / false = 減少中
    Image body, ring, glow, orderBg, core, shine;
    Text orderLabel, tagLabel;
    Image tagBg;
    float size;
    CanvasGroup group;

    Color tint = Color.white;

    /// <summary>本体とリングに掛ける色（停滞ノードが止まっている間の灰色など。白で元に戻る）</summary>
    public void SetTint(Color c) { tint = c; Refresh(); }

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
        n.size = size;

        // 外側の光（仮の見た目のときだけ色が乗る）
        n.glow = UIShapes.CreateImage(rt, "Glow", Vector2.zero, Vector2.one * size * 1.5f, UIShapes.SoftCircle, Color.clear);

        UIShapes.CreateImage(rt, "RingBg", Vector2.zero, new Vector2(size, size), ringSprite, new Color(1, 1, 1, 0.15f));

        n.ring = UIShapes.CreateImage(rt, "Ring", Vector2.zero, new Vector2(size, size), ringSprite, Color.white);
        n.ring.type = Image.Type.Filled;
        n.ring.fillMethod = Image.FillMethod.Radial360;
        n.ring.fillOrigin = (int)Image.Origin360.Top;
        // 向きは見た目確認しながら要調整
        n.ring.fillClockwise = (kind == Kind.Bad);

        n.body = UIShapes.CreateImage(rt, "Body", Vector2.zero, new Vector2(size * 0.76f, size * 0.76f), UIShapes.Circle, Color.white);
        n.core = UIShapes.CreateImage(rt, "Core", Vector2.zero, Vector2.one * size * 0.46f, UIShapes.Circle, Color.white);    // 悪性の中の暗い丸
        n.shine = UIShapes.CreateImage(rt, "Shine", Vector2.zero, Vector2.one * size * 0.62f, UIShapes.SoftCircle, Color.white); // 良性の中の白い光

        // 出現順の番号（右上の白いバッジ）
        Vector2 badgePos = BadgeOffset(new Vector2(1, 1), size);
        float badge = Mathf.Max(20f, size * 0.32f);
        n.orderBg = UIShapes.CreateImage(rt, "OrderBg", badgePos, Vector2.one * badge, UIShapes.Circle, BadgeColor);
        n.orderLabel = UIShapes.CreateText(rt, "Order", "", font, Mathf.RoundToInt(badge * 0.78f), BadgeText, TextAnchor.MiddleCenter,
                                           badgePos, Vector2.one * badge, new Vector2(0.5f, 0.5f));
        n.orderBg.gameObject.SetActive(false);

        // 種類の札（ノードの下）
        Vector2 tagPos = new Vector2(0, -(size * 0.5f + 14f));
        n.tagBg = UIShapes.CreateImage(rt, "TagBg", tagPos, new Vector2(52, 24), UIShapes.White, new Color(0f, 0f, 0f, 0.7f));
        n.tagLabel = UIShapes.CreateText(rt, "Tag", "", font, 18, Color.white, TextAnchor.MiddleCenter,
                                         tagPos, new Vector2(80, 24), new Vector2(0.5f, 0.5f));
        n.tagBg.gameObject.SetActive(false);

        n.SpawnKind = kind;
        n.CurrentKind = kind;
        n.Value = startValue;
        n.rising = false;
        n.Refresh();
        return n;
    }

    /// <summary>問題準備フェーズで「何番目に来るか」を表示。0で非表示</summary>
    public void SetOrder(int order)
    {
        orderLabel.text = order > 0 ? order.ToString() : "";
        orderBg.gameObject.SetActive(order > 0);
    }

    /// <summary>番号バッジの位置（corner = (±1, ±1)。右上が (1, 1)）</summary>
    public static Vector2 BadgeOffset(Vector2 corner, float size) =>
        new Vector2(Mathf.Sign(corner.x) * size * 0.34f, Mathf.Sign(corner.y) * size * 0.38f);

    /// <summary>番号バッジの位置を変える（ノードの中心から。画面外などで見えないとき）</summary>
    public void SetBadgeOffset(Vector2 offset)
    {
        orderBg.rectTransform.anchoredPosition = offset;
        orderLabel.rectTransform.anchoredPosition = offset;
    }

    /// <summary>種類の札が出ているか</summary>
    public bool HasTag => tagBg != null && tagBg.gameObject.activeSelf;

    /// <summary>ノードの下の札（空文字で非表示）</summary>
    public void SetTag(string text)
    {
        bool on = !string.IsNullOrEmpty(text);
        tagLabel.text = on ? text : "";
        tagBg.gameObject.SetActive(on);
        if (on) tagBg.rectTransform.sizeDelta = new Vector2(Mathf.Max(40f, text.Length * 19f + 12f), 24f);
    }

    /// <summary>
    /// ゲージ更新。0 と 100 の間を往復し、0 に届くたびに性質が反転する。
    ///   減少 → 0 到達：性質反転、上昇に転じる
    ///   上昇 → 100 到達：折り返して減少
    /// </summary>
    public void TickGauge(float dt, float perSecond)
    {
        if (GaugeLocked) return;   // 固定中（交差ノード）は変化しない
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

    /// <summary>ゲージを value で固定する（以後 TickGauge で変化しない）</summary>
    public void LockGauge(float value)
    {
        GaugeLocked = true;
        Value = Mathf.Clamp(value, 0f, 100f);
        Refresh();
    }

    /// <summary>良性⇔悪性を入れ替える（大きさはそのまま。交錯ノードなら +100 ⇔ −100）</summary>
    public void FlipKind()
    {
        CurrentKind = CurrentKind == Kind.Good ? Kind.Bad : Kind.Good;
        Refresh();
    }

    void Refresh()
    {
        Color c = CurrentKind == Kind.Good ? GoodColor : BadColor;

        // 本体：画像があればその画像（色はそのまま）、無ければ 良性 = 青い玉 / 悪性 = 赤いトゲトゲ（目なし）
        bool good = CurrentKind == Kind.Good;
        Sprite s = good ? GoodSprite : BadSprite;
        if (s != null)
        {
            body.sprite = s;
            body.preserveAspect = true;
            body.color = tint;
            glow.color = Color.clear;
            core.enabled = shine.enabled = false;
        }
        else
        {
            body.preserveAspect = false;
            body.sprite = good ? UIShapes.Circle : UIShapes.Starburst;
            body.color = (good ? GoodBody : BadBody) * tint;
            core.enabled = !good;
            core.color = BadCore * tint;
            shine.enabled = good;
            shine.color = new Color(1f, 1f, 1f, 0.9f) * tint;
            glow.color = new Color(c.r, c.g, c.b, good ? 0.5f : 0.45f) * tint;
        }
        ring.color = (good ? GoodRing : BadRing) * tint;
        ring.fillAmount = Value / 100f;
    }
}