using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class FilmGrain : MonoBehaviour
{
    [SerializeField, Range(0f, 0.5f)] float opacity = 0.09f;
    [SerializeField] float interval = 0.09f; // ‚´‚ç‚Â‚«‚ÌXVŠÔŠui•bj

    const int Size = 160;
    Texture2D tex;
    Color32[] px;
    float timer;

    void Awake()
    {
        tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point; // ‚´‚ç‚´‚ç‚ğ‘e‚¢‚Ü‚ÜŒ©‚¹‚é
        px = new Color32[Size * Size];

        var raw = GetComponent<RawImage>();
        raw.texture = tex;
        raw.color = Color.white;
        raw.raycastTarget = false;
        Refresh();
    }

    void Update()
    {
        timer += Time.unscaledDeltaTime;
        if (timer < interval) return;
        timer = 0f;
        Refresh();
    }

    void Refresh()
    {
        byte a = (byte)(opacity * 255f);
        for (int i = 0; i < px.Length; i++)
        {
            byte v = (byte)Random.Range(0, 256);
            px[i] = new Color32(v, v, v, a);
        }
        tex.SetPixels32(px);
        tex.Apply();
    }
}