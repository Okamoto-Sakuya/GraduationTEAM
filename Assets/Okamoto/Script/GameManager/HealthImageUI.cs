
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HealthImageUI : MonoBehaviour
{
    [Header("体力Image（最大8個）")]
    public Image[] healthImages = new Image[8];

    [Header("ダメージ演出")]
    public float blinkDuration = 0.6f;
    public float blinkInterval = 0.08f;

    private int currentHealth;
    private Coroutine damageCoroutine;

    private void Start()
    {
        currentHealth = healthImages.Length;

        // 初期状態では全て表示
        for (int i = 0; i < healthImages.Length; i++)
        {
            if (healthImages[i] != null)
            {
                healthImages[i].gameObject.SetActive(true);
                SetAlpha(healthImages[i], 1f);
            }
        }
    }

    /// <summary>
    /// GameManagerから現在のHPを受け取る
    /// </summary>
    public void UpdateHealth(int newHealth)
    {
        newHealth = Mathf.Clamp(newHealth, 0, healthImages.Length);

        // HPが増えた場合
        if (newHealth > currentHealth)
        {
            for (int i = currentHealth; i < newHealth; i++)
            {
                if (healthImages[i] != null)
                {
                    healthImages[i].gameObject.SetActive(true);
                    SetAlpha(healthImages[i], 1f);
                }
            }

            currentHealth = newHealth;
            return;
        }

        // ダメージがなければ何もしない
        if (newHealth == currentHealth)
            return;

        // ダメージを受けたImageを点滅させる
        int oldHealth = currentHealth;
        currentHealth = newHealth;

        if (damageCoroutine != null)
            StopCoroutine(damageCoroutine);

        damageCoroutine = StartCoroutine(
            DamageBlink(oldHealth, newHealth));
    }

    private IEnumerator DamageBlink(int oldHealth, int newHealth)
    {
        // HPが減った分だけ、最後のImageから消す
        for (int i = oldHealth - 1; i >= newHealth; i--)
        {
            if (i < 0 || i >= healthImages.Length)
                continue;

            Image target = healthImages[i];

            if (target == null)
                continue;

            target.gameObject.SetActive(true);
            SetAlpha(target, 1f);

            float elapsed = 0f;
            bool visible = true;

            // 点滅演出
            while (elapsed < blinkDuration)
            {
                visible = !visible;
                SetAlpha(target, visible ? 1f : 0f);

                yield return new WaitForSecondsRealtime(
                    blinkInterval);

                elapsed += blinkInterval;
            }

            // 最後に完全に消す
            SetAlpha(target, 0f);
            target.gameObject.SetActive(false);
        }

        damageCoroutine = null;
    }

    private void SetAlpha(Image image, float alpha)
    {
        if (image == null)
            return;

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    /// <summary>
    /// リトライなどでHPを初期化したいときに使用
    /// </summary>
    public void ResetHealth(int health)
    {
        if (damageCoroutine != null)
        {
            StopCoroutine(damageCoroutine);
            damageCoroutine = null;
        }

        currentHealth = Mathf.Clamp(
            health, 0, healthImages.Length);

        for (int i = 0; i < healthImages.Length; i++)
        {
            if (healthImages[i] == null)
                continue;

            bool show = i < currentHealth;

            healthImages[i].gameObject.SetActive(show);
            SetAlpha(healthImages[i], 1f);
        }
    }
}