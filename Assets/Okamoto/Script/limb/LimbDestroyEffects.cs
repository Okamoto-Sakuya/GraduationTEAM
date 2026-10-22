using System.Collections.Generic;
using UnityEngine;

// 四肢が欠損したときの効果をまとめて管理する
// ・基本線の追加(あらかじめシーンに置いた無効な基本線を有効にする)
// ・基本線の消滅
// ・最大造線ゲージの減少
public class LimbDestroyEffects : MonoBehaviour
{
    [System.Serializable]
    public class Rule
    {
        [Tooltip("管理用のメモ(例: 左腕欠損)")]
        public string label;

        [Tooltip("この四肢が欠損したときに効果を発動する")]
        public LimbController limb;

        [Tooltip("欠損時に追加(有効化)する基本線。ゲーム開始時は自動で無効になる")]
        public BasicLine[] linesToAdd;

        [Tooltip("欠損時に消滅(無効化)させる基本線")]
        public BasicLine[] linesToRemove;

        [Tooltip("欠損時に減らす最大造線ゲージ(0なら変化なし)")]
        public float maxLineGaugeReduction;
    }

    [SerializeField] private List<Rule> rules = new List<Rule>();

    private void Awake()
    {
        // 追加用の基本線は、欠損するまで無効にしておく
        // (NodeSpawnerのStartより先に実行されるよう、Awakeで処理する)
        foreach (Rule rule in rules)
        {
            if (rule == null || rule.linesToAdd == null)
            {
                continue;
            }

            foreach (BasicLine line in rule.linesToAdd)
            {
                if (line != null)
                {
                    line.isActive = false;
                }
            }
        }
    }

    private void Start()
    {
        foreach (Rule rule in rules)
        {
            if (rule == null || rule.limb == null)
            {
                continue;
            }

            Rule captured = rule;

            rule.limb.OnDestroyed += _ => Apply(captured);
        }
    }

    private void Apply(Rule rule)
    {
        Debug.Log($"{rule.label}: 欠損の効果を適用します。");

        // 基本線の追加
        if (rule.linesToAdd != null)
        {
            foreach (BasicLine line in rule.linesToAdd)
            {
                if (line != null)
                {
                    line.isActive = true;
                }
            }
        }

        // 基本線の消滅
        if (rule.linesToRemove != null)
        {
            foreach (BasicLine line in rule.linesToRemove)
            {
                if (line != null)
                {
                    line.isActive = false;
                }
            }
        }

        // 最大造線ゲージの減少
        if (rule.maxLineGaugeReduction > 0f &&
            GameManager.Instance != null &&
            GameManager.Instance.lineManager != null)
        {
            LineManager lineManager = GameManager.Instance.lineManager;

            lineManager.SetMaxLineGauge(
                lineManager.GetMaxLineGauge() - rule.maxLineGaugeReduction
            );
        }

        // 経路を作り直す(ノードの移動に反映)
        if (PathGraph.Instance != null)
        {
            PathGraph.Instance.MarkDirty();
        }
    }
}