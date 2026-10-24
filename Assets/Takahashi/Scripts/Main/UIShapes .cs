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

    public static Sprite White    => _white    ??= MakeSprite(4, (x, y, s) => 1f);
    public static Sprite Circle   => _circle   ??= MakeSprite(128, CircleAlpha);
    public static Sprite Ring     => _ring     ??= MakeSprite(128, RingAlpha);
    public static Sprite TriDown  => _triDown  ??= Polygon(128, new[] { new Vector2(0.5f, 0.06f), new Vector2(0.95f, 0.9f), new Vector2(0.05f, 0.9f) });
    public static Sprite TriUp    => _triUp    ??= Polygon(128, new[] { new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.1f), new Vector2(0.5f, 0.94f) });
    public static Sprite TriRight => _triRight ??= Polygon(128, new[] { new Vector2(0.1f, 0.05f), new Vector2(0.95f, 0.5f), new Vector2(0.1f, 0.95f) });

    // ---------------- スプライト生成 ----------------

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
        return MakeSprite(size, (x, y, s) =>
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
            return Mathf.Clamp01(minD + 0.5f);
        });
    }

    static Sprite MakeSprite(int size, Func<int, int, int, float> alpha)
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
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
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
