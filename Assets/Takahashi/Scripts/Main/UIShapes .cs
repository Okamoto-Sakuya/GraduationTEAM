using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// プロト用：外部アセットなしで円・リング・三角形のスプライトと
/// UI部品（Image / Text / 線）をコードで生成するヘルパー。
/// 本番アセットができたら、各 Image の sprite を差し替えればOK。
/// </summary>
public static class UIShapes
{
    static Sprite _white, _circle, _ring, _triDown, _triUp, _triRight;

    public static Sprite White => _white ??= MakeSprite(4, (x, y, s) => 1f);
    public static Sprite Circle => _circle ??= MakeSprite(128, CircleAlpha);
    public static Sprite Ring => _ring ??= MakeSprite(128, RingAlpha);
    public static Sprite TriDown => _triDown ??= Polygon(128, new[] { new Vector2(0.5f, 0.06f), new Vector2(0.95f, 0.9f), new Vector2(0.05f, 0.9f) });
    public static Sprite TriUp => _triUp ??= Polygon(128, new[] { new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.1f), new Vector2(0.5f, 0.94f) });
    public static Sprite TriRight => _triRight ??= Polygon(128, new[] { new Vector2(0.1f, 0.05f), new Vector2(0.95f, 0.5f), new Vector2(0.1f, 0.95f) });

    static Sprite _soft, _octagon, _octRing, _octLine, _star, _swirl, _ribbon, _pill;

    /// <summary>中心ほど濃いぼかし円（ノードの光）</summary>
    public static Sprite SoftCircle => _soft ??= MakeSprite(128, SoftAlpha);
    /// <summary>正八角形（頂点が上。回収点の仮画像）</summary>
    public static Sprite Octagon => _octagon ??= Polygon(128, RegularPolygon(8, 90f, 0.48f));
    /// <summary>八角形の枠（回収点の白い枠＝体力。Filled で減らす）</summary>
    public static Sprite OctagonRing => _octRing ??= PolygonBand(128, RegularPolygon(8, 90f, 0.48f), RegularPolygon(8, 90f, 0.39f));
    /// <summary>八角形の枠の外周と内周の細い線だけ（体力が減って白が消えた所に見える赤い線）</summary>
    public static Sprite OctagonOutline => _octLine ??= MakeSprite(128, (x, y, sz) => Mathf.Max(
        Mathf.Clamp01(PolyDist(x, y, sz, RegularPolygon(8, 90f, 0.48f)) + 0.5f) * Mathf.Clamp01(0.5f - PolyDist(x, y, sz, RegularPolygon(8, 90f, 0.462f))),
        Mathf.Clamp01(PolyDist(x, y, sz, RegularPolygon(8, 90f, 0.408f)) + 0.5f) * Mathf.Clamp01(0.5f - PolyDist(x, y, sz, RegularPolygon(8, 90f, 0.39f)))));
    /// <summary>トゲトゲの円（悪性ノード）</summary>
    public static Sprite Starburst => _star ??= MakeSprite(128, StarAlpha);
    /// <summary>渦（回収点の真ん中。回転させる）</summary>
    public static Sprite Swirl => _swirl ??= MakeSprite(128, SwirlAlpha);
    /// <summary>斜めの帯（「再生」「造線数変更」の下に敷く）</summary>
    public static Sprite Ribbon => _ribbon ??= Polygon(128, new[] { new Vector2(0.01f, 0.15f), new Vector2(0.92f, 0.15f), new Vector2(0.99f, 0.85f), new Vector2(0.08f, 0.85f) });
    /// <summary>角丸（両端が半円）のボタン下地。Image.Type.Sliced で使う</summary>
    public static Sprite Pill => _pill ??= MakeSprite(64, PillAlpha, 31);

    // ---------------- スプライト生成 ----------------

    static float SoftAlpha(int x, int y, int s)
    {
        float r = s * 0.5f;
        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
        float a = Mathf.Clamp01(1f - d);
        return a * a;
    }

    static float PillAlpha(int x, int y, int s)
    {
        float h = s * 0.5f, R = h - 2f;   // 角の丸みが 9スライスの枠（31px）に収まるように
        float px = Mathf.Abs(x + 0.5f - h) - (h - 1f - R);
        float py = Mathf.Abs(y + 0.5f - h) - (h - 1f - R);
        float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude + Mathf.Min(Mathf.Max(px, py), 0f) - R;
        return Mathf.Clamp01(0.5f - outside);
    }

    /// <summary>正多角形の頂点（0〜1座標・反時計回り）</summary>
    static Vector2[] RegularPolygon(int n, float startDeg, float radius)
    {
        var pts = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float a = (startDeg + 360f * i / n) * Mathf.Deg2Rad;
            pts[i] = new Vector2(0.5f + Mathf.Cos(a) * radius, 0.5f + Mathf.Sin(a) * radius);
        }
        return pts;
    }

    static float CircleAlpha(int x, int y, int s)
    {
        float r = s * 0.5f;
        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
        return Mathf.Clamp01(r - 1f - d);
    }

    static float RingAlpha(int x, int y, int s)
    {
        float r = s * 0.5f;
        float thick = s * 0.12f;
        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
        return Mathf.Clamp01(r - 1f - d) * Mathf.Clamp01(d - (r - 1f - thick));
    }

    /// <summary>凸多角形（0〜1座標・反時計回り）をアンチエイリアス付きで描く</summary>
    static Sprite Polygon(int size, Vector2[] poly)
    {
        return MakeSprite(size, (x, y, s) => Mathf.Clamp01(PolyDist(x, y, s, poly) + 0.5f));
    }

    /// <summary>凸多角形の内側までの距離（px。内側が正）</summary>
    static float PolyDist(int x, int y, int s, Vector2[] poly)
    {
        var p = new Vector2((x + 0.5f) / s, (y + 0.5f) / s);
        float minD = float.MaxValue;
        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
            Vector2 e = b - a;
            Vector2 inward = new Vector2(-e.y, e.x).normalized;
            minD = Mathf.Min(minD, Vector2.Dot(p - a, inward) * s);
        }
        return minD;
    }

    /// <summary>外側の多角形から内側の多角形をくり抜いた枠</summary>
    static Sprite PolygonBand(int size, Vector2[] outer, Vector2[] inner)
    {
        return MakeSprite(size, (x, y, s) =>
            Mathf.Clamp01(PolyDist(x, y, s, outer) + 0.5f) * Mathf.Clamp01(0.5f - PolyDist(x, y, s, inner)));
    }

    /// <summary>トゲトゲの円（悪性ノードの仮画像）</summary>
    static float StarAlpha(int x, int y, int s)
    {
        const int spikes = 14;
        const float rOut = 0.5f, rIn = 0.41f;
        Vector2 u = new Vector2((x + 0.5f) / s - 0.5f, (y + 0.5f) / s - 0.5f);
        float r = u.magnitude;
        float t = Mathf.Repeat(Mathf.Atan2(u.y, u.x) / (Mathf.PI * 2f) * spikes, 1f);
        float edge = rIn + (rOut - rIn) * (1f - Mathf.Abs(t * 2f - 1f));
        return Mathf.Clamp01((edge - r) * s + 0.5f);
    }

    /// <summary>渦（回収点の真ん中。白い羽根が中心へ巻き込む形）</summary>
    static float SwirlAlpha(int x, int y, int s)
    {
        const int arms = 8;
        Vector2 u = new Vector2((x + 0.5f) / s - 0.5f, (y + 0.5f) / s - 0.5f) * 2f;
        float r = u.magnitude;
        if (r >= 1f) return 0f;
        float phase = Mathf.Atan2(u.y, u.x) * arms + r * 7f;
        float v = 0.5f + 0.5f * Mathf.Cos(phase);
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.9f, v))
             * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.35f, r))
             * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1f, r)));
    }

    static Sprite MakeSprite(int size, Func<int, int, int, float> alpha, int border = 0)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(x, y, size)) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                             SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }

    /// <summary>細い線で囲んだ枠（中は fill 色）。sprite を渡すとその画像1枚（9スライス）で描く</summary>
    public static RectTransform CreateFrame(Transform parent, string name, Vector2 pos, Vector2 size,
                                            Color line, Color fill, float thickness = 2f, Sprite sprite = null)
    {
        if (sprite != null)
        {
            var img = CreateImage(parent, name, pos, size, sprite, Color.white);
            img.type = Image.Type.Sliced;
            return img.rectTransform;
        }
        var bg = CreateImage(parent, name, pos, size, White, fill);
        var rt = bg.rectTransform;
        float w = size.x, h = size.y;
        CreateImage(rt, "T", new Vector2(0, h * 0.5f), new Vector2(w, thickness), White, line);
        CreateImage(rt, "B", new Vector2(0, -h * 0.5f), new Vector2(w, thickness), White, line);
        CreateImage(rt, "L", new Vector2(-w * 0.5f, 0), new Vector2(thickness, h), White, line);
        CreateImage(rt, "R", new Vector2(w * 0.5f, 0), new Vector2(thickness, h), White, line);
        return rt;
    }

    // ---------------- UI部品生成 ----------------

    public static RectTransform CreateRect(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static Image CreateImage(Transform parent, string name, Vector2 pos, Vector2 size, Sprite sprite, Color color)
    {
        var rt = CreateRect(parent, name, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    public static Text CreateText(Transform parent, string name, string text, Font font, int fontSize,
                                  Color color, TextAnchor anchor, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        var rt = CreateRect(parent, name, pos, size);
        rt.pivot = pivot;
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>a→b の直線を細長い Image で描く（つなぎ目が切れないよう両端を少し延長）</summary>
    public static Image CreateLine(Transform parent, Vector2 a, Vector2 b, float thickness, Color color, string name = "Line")
    {
        var rt = CreateRect(parent, name, a, Vector2.zero);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = White;
        img.color = color;
        img.raycastTarget = false;
        SetLine(img, a, b, thickness);
        return img;
    }

    /// <summary>既存の線 Image を a→b に置き直す（点線の再利用などに使用）</summary>
    public static void SetLine(Image img, Vector2 a, Vector2 b, float thickness, bool extendEnds = true)
    {
        var rt = img.rectTransform;
        Vector2 d = b - a;
        float len = d.magnitude;
        Vector2 dir = len > 0.0001f ? d / len : Vector2.right;
        float ext = extendEnds ? thickness * 0.5f : 0f;

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = a - dir * ext;
        rt.sizeDelta = new Vector2(len + ext * 2f, thickness);
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }
}