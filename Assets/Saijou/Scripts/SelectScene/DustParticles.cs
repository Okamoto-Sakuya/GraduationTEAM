using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DustParticles : MonoBehaviour
{
    [SerializeField] int count = 70;
    [SerializeField, Range(0f, 1f)] float petalRatio = 0.28f;
    [SerializeField] Color petalColor = new Color(0.69f, 0f, 0.11f);
    [SerializeField] Color dustColor = new Color(0.91f, 0.88f, 0.88f);

    class P
    {
        public RectTransform rt;
        public Image img;
        public bool petal;
        public float vx, vy, ph, rotSpeed;
    }

    readonly List<P> list = new List<P>();
    RectTransform area;
    Sprite circle;

    void Start()
    {
        area = (RectTransform)transform;
        circle = MakeCircle();
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("p", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var p = new P { rt = (RectTransform)go.transform, img = go.GetComponent<Image>() };
            p.img.sprite = circle;
            p.img.raycastTarget = false;
            p.rt.anchorMin = p.rt.anchorMax = new Vector2(0.5f, 0.5f);
            Respawn(p, true);
            list.Add(p);
        }
    }

    void Respawn(P p, bool anywhere)
    {
        float w = area.rect.width, h = area.rect.height;
        p.petal = Random.value < petalRatio;
        float r = p.petal ? Random.Range(5f, 11f) : Random.Range(1f, 3f);
        p.rt.sizeDelta = p.petal ? new Vector2(r * 3.2f, r * 1.2f) : new Vector2(r * 2f, r * 2f);
        Color c = p.petal ? petalColor : dustColor;
        c.a = Random.Range(0.15f, 0.65f);
        p.img.color = c;
        p.vx = Random.Range(-10f, 20f);
        p.vy = -Random.Range(12f, 55f);   // ‰º‚Ö—Ž‚¿‚é
        p.ph = Random.Range(0f, 6.28f);
        p.rotSpeed = Random.Range(-40f, 40f);
        float x = Random.Range(-w / 2f, w / 2f);
        float y = anywhere ? Random.Range(-h / 2f, h / 2f) : h / 2f + 10f;
        p.rt.anchoredPosition = new Vector2(x, y);
        p.rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
    }

    void Update()
    {
        float w = area.rect.width, h = area.rect.height;
        float dt = Time.unscaledDeltaTime;
        foreach (var p in list)
        {
            Vector2 pos = p.rt.anchoredPosition;
            pos.x += (p.vx + Mathf.Sin(Time.time * 0.55f + p.ph) * 15f) * dt;
            pos.y += p.vy * dt;
            p.rt.anchoredPosition = pos;
            if (p.petal) p.rt.Rotate(0, 0, p.rotSpeed * dt);

            if (pos.y < -h / 2f - 20f || pos.x > w / 2f + 20f || pos.x < -w / 2f - 20f)
                Respawn(p, false);
        }
    }

    Sprite MakeCircle()
    {
        const int s = 32;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(s / 2f, s / 2f)) / (s / 2f);
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1f - d)));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }
}