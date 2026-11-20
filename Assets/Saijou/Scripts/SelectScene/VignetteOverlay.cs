using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class VignetteOverlay : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] float strength = 0.95f;
    [SerializeField] float innerRadius = 0.35f; // Ç±Ç±Ç‹Ç≈ÇÕìßñæ
    [SerializeField] float outerRadius = 1.0f;  // élã˜Ç≈ç≈ëÂÇÃîZÇ≥

    void Awake()
    {
        const int w = 256, h = 144;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = (x / (w - 1f) - 0.5f) * 2f;
                float dy = (y / (h - 1f) - 0.5f) * 2f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f; // íÜêS0 Å® élã˜1
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(innerRadius, outerRadius, d)) * strength;
                tex.SetPixel(x, y, new Color(0, 0, 0, a));
            }
        }
        tex.Apply();

        var raw = GetComponent<RawImage>();
        raw.texture = tex;
        raw.color = Color.white;
        raw.raycastTarget = false;
    }
}