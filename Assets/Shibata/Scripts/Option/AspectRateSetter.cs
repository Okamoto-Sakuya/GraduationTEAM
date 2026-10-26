
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AspectRateSetter : MonoBehaviour
{
    public enum Type
    {
        A,  // 2560 * 1440
        B,  // 1920 * 1080
        C,  // 1280 * 720
        D   // FullScreen
    }

    [Serializable]
    public class Node
    {
        public Image image;
    }

    [Header("解像度設定")]
    [SerializeField] Node[] nodes = new Node[4];

    [Header("解像度表示テキスト")]
    [SerializeField] TextMeshProUGUI resolutionText;

    [Header("初期設定")]
    [SerializeField] Type type = Type.B;

    void Start()
    {
        Changer();
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
            nodes[i].image.color = (i == (int)type)
                ? Color.red
                : Color.white;
        }

        // Textは1つだけ使い、文字列を更新する
        if (resolutionText != null)
        {
            resolutionText.text = GetDisplayName(type);
        }
    }

    /// <summary>
    /// 左右のボタンから呼び出す
    /// true  : 右ボタン（次へ）
    /// false : 左ボタン（前へ）
    /// </summary>
    public void ToggleAspRate(bool b)
    {
        int count = Enum.GetValues(typeof(Type)).Length;
        int current = (int)type;

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

        type = (Type)current;

        Changer();
        ApplyAspectRate();
    }

    /// <summary>
    /// 選択中の設定名を取得
    /// </summary>
    string GetDisplayName(Type setting)
    {
        switch (setting)
        {
            case Type.A:
                return "2560 × 1440";

            case Type.B:
                return "1920 × 1080";

            case Type.C:
                return "1280 × 720";

            case Type.D:
                return "Full Screen";

            default:
                return "";
        }
    }

    /// <summary>
    /// 選択中の解像度を適用
    /// </summary>
    void ApplyAspectRate()
    {
        switch (type)
        {
            case Type.A:
                AspectRateCoontroller.instance.SetAspectRate(1280, 720);
                //UpdateColor(0);
                break;

            case Type.B:
                AspectRateCoontroller.instance.SetAspectRate(1920, 1080);
                //UpdateColor(1);
                break;

            case Type.C:
                AspectRateCoontroller.instance.SetAspectRate(2560, 1440);
                //UpdateColor(2);
                break;

            case Type.D:
                AspectRateCoontroller.instance.SetFullScreenMode(true);
                //UpdateColor(3);
                break;
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
