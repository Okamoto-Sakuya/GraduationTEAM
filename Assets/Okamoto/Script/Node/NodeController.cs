using System;
using TMPro;
using UnityEngine;

// ノード本体: 経路に沿った移動・良性/悪性とレベルの変化・見た目・回収時の効果
public class NodeController : MonoBehaviour
{
    [Serializable]
    public class MalignantStat
    {
        public int damage;   // 四肢へのダメージ
        public int sanity;   // SAN値の減少量
        public int score;    // スコア

        public MalignantStat() { }

        public MalignantStat(int damage, int sanity, int score)
        {
            this.damage = damage;
            this.sanity = sanity;
            this.score = score;
        }
    }

    [Header("移動")]
    [SerializeField] private float moveSpeed = 1f;
    [SerializeField] private BasicLine spawnBasicLine;

    [Header("行き止まり")]
    [Tooltip("進める線がなくなったとき、固まらずにノードを消す")]
    [SerializeField] private bool destroyOnDeadEnd = true;

    [Header("ゲージ(良性/悪性の変化)")]
    [SerializeField] private float spawnGauge = 100f;
    [Tooltip("1秒あたりのゲージ増減量")]
    [SerializeField] private float gaugeSpeed = 10f;
    [Tooltip("出現時に良性になる確率(0?1)")]
    [Range(0f, 1f)]
    [SerializeField] private float benignSpawnChance = 0.5f;

    [Header("レベルごとの数値(レベル1?4の順)")]
    [SerializeField]
    private MalignantStat[] malignantStats =
    {
        new MalignantStat(5, 3, 500),
        new MalignantStat(7, 5, 350),
        new MalignantStat(10, 8, 200),
        new MalignantStat(15, 10, 50)
    };

    [SerializeField]
    private int[] benignScores = { 1000, 1500, 2000, 3000 };

    [Header("見た目(レベル1?4の順に画像を設定)")]
    [SerializeField] private Sprite[] benignSprites = new Sprite[4];
    [SerializeField] private Sprite[] malignantSprites = new Sprite[4];

    [Tooltip("画像が未設定のときの色と大きさ")]
    [SerializeField] private Color benignColor = new Color(0.3f, 0.6f, 1f, 1f);
    [SerializeField] private Color malignantColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private float fallbackSize = 0.5f;
    [SerializeField] private int spriteSortingOrder = 12;

    [Header("ゲージ表示(ノードの周りのリング)")]
    [SerializeField] private float ringRadius = 0.4f;
    [SerializeField] private float ringWidth = 0.06f;

    [Header("レベル変化ゲージ(次に見た目が変わるまでの残り)")]
    [SerializeField] private bool showLevelBar = true;
    [SerializeField] private float levelBarWidth = 0.6f;
    [SerializeField] private float levelBarHeight = 0.07f;
    [Tooltip("ノードの中心から、リングの外側へ離す距離")]
    [SerializeField] private float levelBarGap = 0.12f;
    [SerializeField] private Color levelBarColor = new Color(1f, 0.95f, 0.55f, 1f);
    [Tooltip("変化まで残りこの秒数を切ると点滅して知らせる")]
    [SerializeField] private float levelBarBlinkTime = 0.8f;

    [Header("スコア表示(ノードの上に、今回収したときのスコアを表示)")]
    [SerializeField] private bool showScoreText = true;
    [Tooltip("ONにすると悪性ノードにもスコアを表示する")]
    [SerializeField] private bool showScoreOnMalignant = false;
    [Tooltip("文字の大きさ(ワールド空間のTextMeshPro)")]
    [SerializeField] private float scoreTextSize = 3f;
    [Tooltip("リングの上端から、文字までの距離")]
    [SerializeField] private float scoreTextGap = 0.2f;
    [SerializeField] private string scoreTextFormat = "+{0:N0}";
    [SerializeField] private Color scoreTextColor = Color.white;

    // 四肢(回収点)に到達したとき(スコア・ダメージ適用後に呼ばれる)
    public event Action<NodeController, LimbController> OnCollected;

    public bool IsMoving { get; private set; }
    public BasicLine SpawnBasicLine => spawnBasicLine;

    // 状態の参照用
    public bool IsMalignant => isMalignant;
    public float Gauge => gauge;
    public int Level => Mathf.Clamp(Mathf.FloorToInt(gauge / 25f) + 1, 1, 4);

    // 経路
    private PathLine line;
    private float s;
    private int dir = 1;
    private bool placed;

    // 良性/悪性とゲージ
    private float gauge;
    private bool isMalignant;
    private bool rising;   // trueなら増加中、falseなら減少中

    // 見た目
    private SpriteRenderer spriteRenderer;
    private bool ownsVisual;
    private LineRenderer ringTrack;
    private LineRenderer ringArc;
    private LineRenderer levelBarBack;
    private LineRenderer levelBarFill;
    private TextMeshPro scoreText;
    private int shownLevel = -1;
    private bool shownMalignant;

    private static Sprite fallbackSprite;
    private static Material ringMaterial;

    private void Awake()
    {
        gauge = Mathf.Clamp(spawnGauge, 0f, 100f);
        rising = false;
        isMalignant = UnityEngine.Random.value >= benignSpawnChance;

        SetupVisual();
        SetupRing();
        SetupLevelBar();
        SetupScoreText();
        RefreshVisual();
        UpdateRing();
        UpdateLevelBar();
    }

    private void Start()
    {
        if (!placed) Place();
    }

    public void SetSpawnBasicLine(BasicLine basicLine)
    {
        spawnBasicLine = basicLine;
        Place();
    }

    private void Place()
    {
        var g = PathGraph.Instance;
        if (g == null)
        {
            Debug.LogError($"{name}: シーンに PathGraph がありません。", this);
            return;
        }

        if (spawnBasicLine == null)
        {
            Debug.LogError($"{name}: spawnBasicLine が未設定です。", this);
            return;
        }

        line = g.GetLine(spawnBasicLine);
        if (line == null)
        {
            Debug.LogError(
                $"{name}: {spawnBasicLine.name} が PathGraph に登録されていません" +
                "(LineManager の Basic Lines 未登録、isActive=false、Start・End未設定)。", this);
            return;
        }

        s = 0f;
        dir = 1;
        placed = true;
        IsMoving = true;
        transform.position = line.PointAt(0f);
    }

    private void Update()
    {
        TickGauge(Time.deltaTime);
        RefreshVisual();
        UpdateRing();
        UpdateLevelBar();

        if (!IsMoving) return;

        var g = PathGraph.Instance;
        if (g == null) return;

        g.RebuildIfDirty();

        // 乗っていた線が消えた場合
        if (line == null || line.removed)
        {
            DeadEnd("乗っていた線が消えました");
            return;
        }

        float remain = moveSpeed * Time.deltaTime;

        for (int guard = 0; remain > 0f && guard < 8; guard++)
        {
            Junction next = g.NextJunction(line, s, dir);
            if (next == null)
            {
                DeadEnd("この線の先に分岐点がありません");
                return;
            }

            float dist = Mathf.Abs(next.s - s);
            if (remain < dist)
            {
                s += dir * remain;
                break;
            }

            remain -= dist;
            s = next.s;

            if (!Arrive(g, next.v)) return;
        }

        transform.position = line.PointAt(s);
    }

    // ===== ゲージ =====

    // ゲージは 0?100 を往復する。
    // 減少中に0になると 良性⇔悪性 が入れ替わって増加に転じ、
    // 増加中に100になると同じ性質のまま減少に転じる。
    // 例: 悪性2→3→4→3→2→1→良性1→2…
    private void TickGauge(float dt)
    {
        gauge += (rising ? 1f : -1f) * gaugeSpeed * dt;

        if (rising && gauge >= 100f)
        {
            gauge = 100f - (gauge - 100f);
            rising = false;
        }
        else if (!rising && gauge <= 0f)
        {
            gauge = -gauge;
            rising = true;
            isMalignant = !isMalignant;
        }

        gauge = Mathf.Clamp(gauge, 0f, 100f);
    }

    // ===== 回収 =====

    // 分岐点に着いたときの処理。続行できなければfalse
    private bool Arrive(PathGraph g, PathVertex v)
    {
        if (v.limb != null && v.limb.IsAlive)
        {
            transform.position = v.pos;
            Collect(v.limb);
            return false;
        }

        if (!g.TryChoose(v, line, dir, out var nLine, out var nDir, out var nS))
        {
            transform.position = line.PointAt(s);
            DeadEnd("進める線がありません");
            return false;
        }

        line = nLine;
        dir = nDir;
        s = nS;
        return true;
    }

    // 四肢に到達: スコア加算、悪性ならダメージとSAN値の減少
    private void Collect(LimbController limb)
    {
        int index = Level - 1;
        GameManager gm = GameManager.Instance;

        if (isMalignant)
        {
            MalignantStat stat = malignantStats[Mathf.Clamp(index, 0, malignantStats.Length - 1)];

            // 四肢の残り体力を超えた分のダメージは、SAN値にも入る
            int overflow = limb.TakeDamage(stat.damage);

            if (gm != null)
            {
                gm.AddScore(stat.score);
                gm.ReduceSanity(stat.sanity + overflow);
                gm.CheckGameOver();
            }
        }
        else
        {
            int score = benignScores[Mathf.Clamp(index, 0, benignScores.Length - 1)];

            if (gm != null)
            {
                gm.AddScore(score);
            }
        }

        OnCollected?.Invoke(this, limb);
        Destroy(gameObject);
    }

    // 進めなくなったとき。固まらないように消す(設定で停止にも変更可)
    private void DeadEnd(string reason)
    {
        Debug.LogWarning(
            $"{name}: 行き止まり({reason}) 位置 {transform.position} " +
            $"line={(line != null ? line.key?.ToString() : "null")} s={s:F2} dir={dir}",
            this);

        if (destroyOnDeadEnd)
        {
            Destroy(gameObject);
        }
        else
        {
            StopMoving();
        }
    }

    public void StopMoving() { IsMoving = false; }
    public void ResumeMoving() { IsMoving = true; }

    // ===== 見た目 =====

    private void SetupVisual()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (spriteRenderer == null)
        {
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(transform, false);
            spriteRenderer = visual.AddComponent<SpriteRenderer>();
            ownsVisual = true;
        }

        spriteRenderer.sortingOrder = spriteSortingOrder;
    }

    // 良性/悪性またはレベルが変わったときだけ画像を切り替える
    private void RefreshVisual()
    {
        int level = Level;

        if (level == shownLevel && isMalignant == shownMalignant)
        {
            return;
        }

        shownLevel = level;
        shownMalignant = isMalignant;

        Sprite[] set = isMalignant ? malignantSprites : benignSprites;
        Sprite sprite = (set != null && set.Length >= level)
            ? set[level - 1]
            : null;

        if (sprite != null)
        {
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = Color.white;

            if (ownsVisual)
            {
                spriteRenderer.transform.localScale = Vector3.one;
            }
        }
        else
        {
            // 画像が未設定なら、色付きの円(レベルが高いほど大きい)
            spriteRenderer.sprite = GetFallbackSprite();
            spriteRenderer.color = isMalignant ? malignantColor : benignColor;

            if (ownsVisual)
            {
                float size = fallbackSize * (0.6f + 0.15f * level);
                spriteRenderer.transform.localScale = new Vector3(size, size, 1f);
            }
        }

        // リングの色
        Color ringColor = isMalignant ? malignantColor : benignColor;

        if (ringArc != null)
        {
            ringArc.startColor = ringColor;
            ringArc.endColor = ringColor;
        }

        if (ringTrack != null)
        {
            Color trackColor = ringColor;
            trackColor.a = 0.25f;
            ringTrack.startColor = trackColor;
            ringTrack.endColor = trackColor;
        }

        UpdateScoreText();
    }

    private void SetupRing()
    {
        ringTrack = CreateRingLine("GaugeTrack", spriteSortingOrder + 1);
        ringArc = CreateRingLine("GaugeArc", spriteSortingOrder + 2);

        // 背景のリングは常に一周
        const int segments = 64;
        ringTrack.positionCount = segments + 1;

        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;

            ringTrack.SetPosition(
                i,
                new Vector3(
                    Mathf.Cos(angle) * ringRadius,
                    Mathf.Sin(angle) * ringRadius,
                    0f
                )
            );
        }
    }

    private LineRenderer CreateRingLine(string objectName, int order)
    {
        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(transform, false);

        LineRenderer lr = obj.AddComponent<LineRenderer>();

        if (ringMaterial == null)
        {
            ringMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        lr.sharedMaterial = ringMaterial;
        lr.useWorldSpace = false;
        lr.startWidth = ringWidth;
        lr.endWidth = ringWidth;
        lr.numCapVertices = 4;
        lr.sortingOrder = order;

        return lr;
    }

    // ゲージの残量を、ノードの真上(12時)から伸びる円弧で表示する。
    // 良性は時計回りに伸びる円弧(減少すると反時計回りに縮む)、
    // 悪性は反時計回りに伸びる円弧(増加すると反時計回りに伸びる)。
    private void UpdateRing()
    {
        if (ringArc == null) return;

        float sweep = gauge / 100f * 360f;

        if (sweep < 1f)
        {
            ringArc.enabled = false;
            return;
        }

        ringArc.enabled = true;

        int count = Mathf.Max(2, Mathf.CeilToInt(sweep / 5f) + 1);
        ringArc.positionCount = count;

        float sign = isMalignant ? 1f : -1f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            float angle = (90f + sign * sweep * t) * Mathf.Deg2Rad;

            ringArc.SetPosition(
                i,
                new Vector3(
                    Mathf.Cos(angle) * ringRadius,
                    Mathf.Sin(angle) * ringRadius,
                    0f
                )
            );
        }
    }

    // ===== スコア表示 =====

    private void SetupScoreText()
    {
        if (!showScoreText) return;

        GameObject obj = new GameObject("ScoreText");
        obj.transform.SetParent(transform, false);
        obj.transform.localPosition = new Vector3(0f, ringRadius + scoreTextGap, 0f);

        scoreText = obj.AddComponent<TextMeshPro>();
        scoreText.alignment = TextAlignmentOptions.Center;
        scoreText.fontSize = scoreTextSize;
        scoreText.color = scoreTextColor;
        scoreText.enableWordWrapping = false;
        scoreText.sortingOrder = spriteSortingOrder + 3;
        scoreText.rectTransform.sizeDelta = new Vector2(3f, 0.6f);
    }

    // 良性(設定により悪性も)のとき、今の状態で回収したときのスコアを表示
    private void UpdateScoreText()
    {
        if (scoreText == null) return;

        if (isMalignant && !showScoreOnMalignant)
        {
            scoreText.gameObject.SetActive(false);
            return;
        }

        int index = Level - 1;

        int score = isMalignant
            ? malignantStats[Mathf.Clamp(index, 0, malignantStats.Length - 1)].score
            : benignScores[Mathf.Clamp(index, 0, benignScores.Length - 1)];

        scoreText.gameObject.SetActive(true);
        scoreText.text = string.Format(scoreTextFormat, score);
    }

    // ===== レベル変化ゲージ =====

    // 次に見た目(レベル、または良性/悪性)が変わるまでの残り割合(1→0)と残りゲージ量
    //  ・減少中: 現在のレベルの下限(0なら良性/悪性の入れ替わり)まで
    //  ・増加中: 現在のレベルの上限まで。
    //           レベル4で増加中のときは、100で折り返してレベル3に下がるまで
    private float GetChangeFraction(out float remainGauge)
    {
        int band = Mathf.Clamp(Mathf.FloorToInt(gauge / 25f), 0, 3);
        float lower = band * 25f;

        float remain;
        float span = 25f;

        if (!rising)
        {
            remain = gauge - lower;
        }
        else if (band == 3)
        {
            remain = (100f - gauge) + 25f;
            span = 50f;
        }
        else
        {
            remain = (lower + 25f) - gauge;
        }

        remainGauge = remain;

        return Mathf.Clamp01(remain / span);
    }

    private void SetupLevelBar()
    {
        if (!showLevelBar) return;

        float y = -(ringRadius + levelBarGap);

        levelBarBack = CreateRingLine("LevelBarBack", spriteSortingOrder + 1);
        levelBarFill = CreateRingLine("LevelBarFill", spriteSortingOrder + 2);

        levelBarBack.numCapVertices = 0;
        levelBarFill.numCapVertices = 0;

        levelBarBack.startWidth = levelBarHeight + 0.03f;
        levelBarBack.endWidth = levelBarHeight + 0.03f;
        levelBarFill.startWidth = levelBarHeight;
        levelBarFill.endWidth = levelBarHeight;

        levelBarBack.positionCount = 2;
        levelBarBack.SetPosition(0, new Vector3(-levelBarWidth * 0.5f, y, 0f));
        levelBarBack.SetPosition(1, new Vector3(levelBarWidth * 0.5f, y, 0f));

        Color back = new Color(0.05f, 0.05f, 0.05f, 0.8f);
        levelBarBack.startColor = back;
        levelBarBack.endColor = back;

        levelBarFill.positionCount = 2;
    }

    private void UpdateLevelBar()
    {
        if (levelBarFill == null) return;

        float fraction = GetChangeFraction(out float remainGauge);

        float y = -(ringRadius + levelBarGap);
        float left = -levelBarWidth * 0.5f;

        levelBarFill.SetPosition(0, new Vector3(left, y, 0f));
        levelBarFill.SetPosition(1, new Vector3(left + levelBarWidth * fraction, y, 0f));

        // 変化が近づいたら点滅
        float remainSeconds = gaugeSpeed > 0f ? remainGauge / gaugeSpeed : float.MaxValue;

        Color color = levelBarColor;

        if (remainSeconds <= levelBarBlinkTime)
        {
            color.a = Mathf.Lerp(0.3f, 1f, (Mathf.Sin(Time.time * 20f) + 1f) * 0.5f);
        }

        levelBarFill.startColor = color;
        levelBarFill.endColor = color;
    }

    // 画像が未設定のときに使う白い円(直径1)
    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
        {
            return fallbackSprite;
        }

        const int size = 64;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;

        float center = (size - 1) * 0.5f;
        float radius = size * 0.5f - 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(radius - d + 0.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();

        fallbackSprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );

        return fallbackSprite;
    }
}