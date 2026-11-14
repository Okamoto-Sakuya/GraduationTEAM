using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class LanguageController : MonoBehaviour
{
    public static LanguageController instance;

    [Serializable]
    public class Node
    {
        public Image image;
    }

    [Header("言語設定")]
    [SerializeField] Node[] nodes = new Node[4];

    [Header("現在の言語表示テキスト")]
    [SerializeField] TextMeshProUGUI resolutionText;

    enum Lang
    {
        JP,// 日本語
        EN,// 英語
        HN,// 中国語(簡体字)
        TW,// 中国語(簡体字)
    }
    Lang lang;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 選択中の解像度を表示する
    /// 選択中のImageは赤色、それ以外は白色
    /// </summary>
    void Changer()
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] == null || nodes[i].image == null)
                continue;

            // 選択中のImageだけ赤色にする
            nodes[i].image.color = (i == (int)lang)
                ? Color.red
                : Color.white;
        }

        // Textは1つだけ使い、文字列を更新する
        if (resolutionText != null)
        {
            resolutionText.text = GetDisplayName(lang);
        }
    }

    /// <summary>
    /// 左右のボタンから呼び出す
    /// true  : 右ボタン（次へ）
    /// false : 左ボタン（前へ）
    /// </summary>
    public void ToggleLanguage(bool b)
    {
        int count = Enum.GetValues(typeof(Type)).Length;
        int current = (int)lang;

        if (b)
        {
            // 次の設定へ
            current = (current + 1) % count;
        }
        else
        {
            // 前の設定へ
            current = (current - 1 + count) % count;
        }

        lang = (Lang)current;

        Changer();
        ApplyLanguage();
    }

    public void Enter()
    {

    }

    /// <summary>
    /// 選択中の言語を適用
    /// </summary>
    void ApplyLanguage()
    {
        switch (lang)
        {
            case Lang.JP:
                LocalizationSettings.SelectedLocale = Locale.CreateLocale("ja");
                break;

            case Lang.EN:
                LocalizationSettings.SelectedLocale = Locale.CreateLocale("en");
                break;

            case Lang.HN:
                LocalizationSettings.SelectedLocale = Locale.CreateLocale("zh-Hans");
                break;

            case Lang.TW:
                LocalizationSettings.SelectedLocale = Locale.CreateLocale("zh-TW");
                break;
        }
    }

    /// <summary>
    /// 選択中の設定名を取得
    /// </summary>
    string GetDisplayName(Lang setting)
    {
        switch (setting)
        {
            case Lang.JP:
                return "JP";

            case Lang.EN:
                return "EN";

            case Lang.HN:
                return "HN";

            case Lang.TW:
                return "TW";

            default:
                return "";
        }
    }

    void UpdateColor(int num)
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] == null || nodes[i].image == null)
                continue;

            if (i == num)
                nodes[i].image.color = Color.red;
            else
                nodes[i].image.color = Color.white;
        }
    }
}
