using UnityEngine;

// マウスカーソルの画像を、コードで作る(画像ファイルの用意・インポート設定は不要)。
// LineManagerの「カーソル画像」に画像を割り当てると、そちらが優先される
public static class CursorImages
{
    public const int Size = 32;

    public static readonly Vector2 ArrowHotspot = new Vector2(1f, 1f);
    public static readonly Vector2 PenHotspot = new Vector2(2f, 3f);   // ペン先(左上基準)
    public static readonly Vector2 CrossHotspot = new Vector2(16f, 16f);

    private static readonly Color Black = new Color(0.05f, 0.05f, 0.05f, 1f);
    private static readonly Color White = Color.white;
    private static readonly Color Green = new Color(0.1f, 0.8f, 0.35f, 1f);
    private static readonly Color Red = new Color(0.9f, 0.1f, 0.1f, 1f);

    private static readonly Vector2[] ArrowShape =
    {
        new Vector2(1f, 1f),
        new Vector2(1f, 24f),
        new Vector2(7f, 18.5f),
        new Vector2(12f, 30f),
        new Vector2(16f, 28f),
        new Vector2(11f, 17f),
        new Vector2(19f, 17f),
    };

    // 通常: 白い矢印(黒の縁)
    public static Texture2D Arrow() { return DrawArrow(Black); }

    // 始点設置可能: 緑の縁取り
    public static Texture2D ArrowStart() { return DrawArrow(Green); }

    // 造線設置不能: ペン(黒の縁)
    public static Texture2D Pen() { return DrawPen(Black); }

    // 造線設置可能: ペンの緑の縁取り
    public static Texture2D PenReady() { return DrawPen(Green); }

    // 造線設置禁止: 赤い×
    public static Texture2D Cross()
    {
        Texture2D tex = NewTexture();
        Vector2 a1 = new Vector2(6f, 6f), a2 = new Vector2(26f, 26f);
        Vector2 b1 = new Vector2(26f, 6f), b2 = new Vector2(6f, 26f);

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);

                float d = Mathf.Min(DistSeg(p, a1, a2), DistSeg(p, b1, b2));

                Color c = Clear();

                if (d <= 2.6f) c = Red;
                else if (d <= 4.2f) c = White;

                Set(tex, x, y, c);
            }
        }

        tex.Apply();
        return tex;
    }

    private static Texture2D DrawArrow(Color outline)
    {
        Texture2D tex = NewTexture();

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);

                Color c = Clear();

                if (InsidePolygon(p, ArrowShape))
                {
                    c = DistToPolygon(p, ArrowShape) < 1.6f ? outline : White;
                }
                else if (DistToPolygon(p, ArrowShape) < 1.6f)
                {
                    c = outline;
                }

                Set(tex, x, y, c);
            }
        }

        tex.Apply();
        return tex;
    }

    private static Texture2D DrawPen(Color outline)
    {
        Texture2D tex = NewTexture();

        Vector2 tip = new Vector2(2f, 29f);
        Vector2 top = new Vector2(27f, 4f);
        Vector2 axis = top - tip;
        float len = axis.magnitude;

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);

                float t = Mathf.Clamp01(Vector2.Dot(p - tip, axis) / (len * len));
                Vector2 onAxis = tip + axis * t;
                float d = Vector2.Distance(p, onAxis);

                float radius = Mathf.Lerp(0.6f, 3.6f, Mathf.Min(1f, t * 2.5f));

                Color c = Clear();

                if (d <= radius)
                {
                    // ペン先の近くはインク色、軸は白
                    c = t < 0.18f ? Black : White;
                }
                else if (d <= radius + 1.6f)
                {
                    c = outline;
                }

                Set(tex, x, y, c);
            }
        }

        tex.Apply();
        return tex;
    }

    private static Texture2D NewTexture()
    {
        Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        return tex;
    }

    private static Color Clear() { return new Color(0f, 0f, 0f, 0f); }

    // 左上を原点としたy(下向き)で書き込む
    private static void Set(Texture2D tex, int x, int y, Color c)
    {
        tex.SetPixel(x, Size - 1 - y, c);
    }

    private static float DistSeg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    private static float DistToPolygon(Vector2 p, Vector2[] poly)
    {
        float best = float.MaxValue;

        for (int i = 0; i < poly.Length; i++)
        {
            best = Mathf.Min(best, DistSeg(p, poly[i], poly[(i + 1) % poly.Length]));
        }

        return best;
    }

    private static bool InsidePolygon(Vector2 p, Vector2[] poly)
    {
        bool inside = false;

        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) /
                      (poly[j].y - poly[i].y) + poly[i].x)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}