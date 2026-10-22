using UnityEngine;
using UnityEngine.Events;

public class LimbController : MonoBehaviour
{
    public enum LimbType
    {
        RightArm,
        LeftArm,
        RightLeg,
        LeftLeg
    }

    [Header("四肢の種類")]
    [SerializeField] private LimbType limbType;

    [Header("HP設定")]
    [SerializeField] private int maxHP = 25;
    [SerializeField] private int currentHP = 25;

    [Header("体力バー")]
    [SerializeField] private bool showHpBar = true;
    [Tooltip("四肢の位置からバーまでのずれ。上の四肢は+Y、下の四肢は-Yにすると見やすい")]
    [SerializeField] private Vector2 barOffset = new Vector2(0f, 0.5f);
    [Tooltip("バーの幅と高さ")]
    [SerializeField] private Vector2 barSize = new Vector2(0.6f, 0.08f);
    [SerializeField] private Color barColor = new Color(0.25f, 0.85f, 0.35f, 1f);
    [Tooltip("体力が少なくなったときのバーの色")]
    [SerializeField] private Color barLowColor = new Color(0.95f, 0.3f, 0.25f, 1f);
    [Range(0f, 1f)]
    [SerializeField] private float lowHpRatio = 0.3f;
    [SerializeField] private Color barBackColor = new Color(0.05f, 0.05f, 0.05f, 0.85f);
    [SerializeField] private int barSortingOrder = 20;

    [Header("破壊時の設定")]
    [SerializeField] private GameObject destroyEffectPrefab;
    [SerializeField] private UnityEvent onLimbDestroyed;

    public int CurrentHP => currentHP;
    public int MaxHP => maxHP;
    public LimbType Type => limbType;

    public bool IsDestroyed { get; private set; }

    public bool IsAlive => !IsDestroyed;

    public System.Action<LimbController> OnDestroyed;

    // 体力バーの表示用
    private Transform barRoot;
    private Transform barFill;
    private SpriteRenderer barFillRenderer;
    private static Sprite barSprite;

    private void Awake()
    {
        maxHP = Mathf.Max(1, maxHP);
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        IsDestroyed = currentHP <= 0;

        CreateHpBar();
        UpdateHpBar();
    }

    private void LateUpdate()
    {
        if (barRoot != null)
        {
            barRoot.position = transform.position + (Vector3)barOffset;
        }
    }

    private void OnDestroy()
    {
        if (barRoot != null)
        {
            Destroy(barRoot.gameObject);
        }
    }

    // ダメージを受ける
    // 戻り値: 残り体力を超えた分のダメージ(SAN値に回す分)
    public int TakeDamage(int damage)
    {
        if (IsDestroyed || damage <= 0)
        {
            return 0;
        }

        int before = currentHP;

        currentHP = Mathf.Max(0, before - damage);

        int overflow = Mathf.Max(0, damage - before);

        Debug.Log(
            $"{gameObject.name} がダメージを受けました。" +
            $" HP：{currentHP}/{maxHP}"
        );

        UpdateHpBar();

        if (currentHP <= 0)
        {
            DestroyLimb();
        }

        return overflow;
    }

    // HPを回復する
    public void Heal(int amount)
    {
        if (IsDestroyed || amount <= 0)
        {
            return;
        }

        currentHP = Mathf.Min(maxHP, currentHP + amount);

        UpdateHpBar();
    }

    // 四肢が破壊されたとき
    private void DestroyLimb()
    {
        if (IsDestroyed)
        {
            return;
        }

        IsDestroyed = true;

        UpdateHpBar();

        Debug.Log($"{gameObject.name} が破壊されました。");
        OnDestroyed?.Invoke(this);

        if (destroyEffectPrefab != null)
        {
            Instantiate(
                destroyEffectPrefab,
                transform.position,
                Quaternion.identity
            );
        }

        onLimbDestroyed?.Invoke();
    }

    // ===== 体力バー =====

    private void CreateHpBar()
    {
        if (!showHpBar)
        {
            return;
        }

        barRoot = new GameObject(name + "_HpBar").transform;
        barRoot.position = transform.position + (Vector3)barOffset;

        const float pad = 0.02f;

        // 背景(暗い枠)
        GameObject back = new GameObject("Back");
        back.transform.SetParent(barRoot, false);
        back.transform.localPosition = new Vector3(-barSize.x * 0.5f - pad, 0f, 0f);
        back.transform.localScale = new Vector3(barSize.x + pad * 2f, barSize.y + pad * 2f, 1f);

        SpriteRenderer backRenderer = back.AddComponent<SpriteRenderer>();
        backRenderer.sprite = GetBarSprite();
        backRenderer.color = barBackColor;
        backRenderer.sortingOrder = barSortingOrder;

        // 体力(左端を基準に右へ伸びる)
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(barRoot, false);
        fill.transform.localPosition = new Vector3(-barSize.x * 0.5f, 0f, 0f);

        barFill = fill.transform;

        barFillRenderer = fill.AddComponent<SpriteRenderer>();
        barFillRenderer.sprite = GetBarSprite();
        barFillRenderer.color = barColor;
        barFillRenderer.sortingOrder = barSortingOrder + 1;
    }

    private void UpdateHpBar()
    {
        if (barRoot == null)
        {
            return;
        }

        // 欠損したらバーを消す
        if (IsDestroyed)
        {
            barRoot.gameObject.SetActive(false);
            return;
        }

        float ratio = Mathf.Clamp01((float)currentHP / maxHP);

        barFill.localScale = new Vector3(barSize.x * ratio, barSize.y, 1f);
        barFillRenderer.color = ratio <= lowHpRatio ? barLowColor : barColor;
    }

    // 1x1の白い画像(左端が基準)。大きさはscaleで決める
    private static Sprite GetBarSprite()
    {
        if (barSprite != null)
        {
            return barSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        barSprite = Sprite.Create(
            texture,
            new Rect(0, 0, 1, 1),
            new Vector2(0f, 0.5f),
            1f
        );

        return barSprite;
    }
}