using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// 「連続回数」で確率が変わる表の1行(基本線の表・左右の表で共通)
//  基本線の表: same=同じ基本線 / other=別の基本線
//  左右の表  : same=同じサイド   / other=反対サイド
public class CountRow
{
    public int count;        // 連続して出現した回数
    public bool atLeast;     // 「4!」のように、その回数以上のときはtrue
    public float same;
    public float other;
}

// 指定出現用の表(ノードグループ)の1行
public class NodeRow
{
    public int id;
    public int group;        // どのノードグループか
    public int baseType;     // 基本属性: 1=良性 2=悪性(0=列がない古い形式)
    public int type;         // ノードの種類
                             //  1=通常 2=同時出現 3=迅速 4=重圧 5=停滞
                             //  6=左腕 7=右腕 8=左足 9=右足(帰属)
                             //  10=交錯 11=造線
}

// ステージの表の1行(1行 = 1レベル)
public class StageRow
{
    public int stage;
    public int lineGauge;    // 造線ゲージの最大量
    public int level;
    public int nodeCount;    // このレベルで出現するノード数
    public int group;        // 出現するノードグループ
    public int rate;         // 出現するノードの頻度
    public int speed;        // 出現したノードのスピード

    // 前のレベルから、この行のスピード・頻度へ変えていく秒数(8列目。なければ-1)
    // -1のときは、StageNodeSpawnerのDefault Transition Secondsを使う
    public float transition = -1f;
}

public static class SpawnTables
{
    // ===== 読み込み =====
    // タブ区切り・カンマ区切りのどちらでも読める。
    // 先頭が数字でない行(見出し)は読み飛ばす。

    public static List<CountRow> ParseCountRows(string text)
    {
        var rows = new List<CountRow>();

        foreach (string[] cells in ReadRows(text))
        {
            // id, count, same, other
            if (cells.Length < 4) continue;

            string countCell = cells[1];

            bool atLeast = countCell.EndsWith("!") || countCell.EndsWith("！");

            if (atLeast)
            {
                countCell = countCell.Substring(0, countCell.Length - 1);
            }

            if (!TryInt(countCell, out int count) ||
                !TryFloat(cells[2], out float same) ||
                !TryFloat(cells[3], out float other))
            {
                continue;
            }

            rows.Add(new CountRow
            {
                count = count,
                atLeast = atLeast,
                same = same,
                other = other
            });
        }

        return rows;
    }

    public static List<NodeRow> ParseNodeRows(string text)
    {
        var rows = new List<NodeRow>();

        foreach (string[] cells in ReadRows(text))
        {
            // id, group, base, type
            if (cells.Length < 4) continue;

            if (TryInt(cells[0], out int id) &&
                TryInt(cells[1], out int group) &&
                TryInt(cells[2], out int baseType) &&
                TryInt(cells[3], out int type))
            {
                rows.Add(new NodeRow
                {
                    id = id,
                    group = group,
                    baseType = baseType,
                    type = type
                });
            }
        }

        return rows;
    }

    public static List<StageRow> ParseStageRows(string text)
    {
        var rows = new List<StageRow>();

        foreach (string[] cells in ReadRows(text))
        {
            // stage, linegauge, level, nodecount, group, rate, speed, [transition]
            if (cells.Length < 7) continue;

            if (TryInt(cells[0], out int stage) &&
                TryInt(cells[1], out int lineGauge) &&
                TryInt(cells[2], out int level) &&
                TryInt(cells[3], out int nodeCount) &&
                TryInt(cells[4], out int group) &&
                TryInt(cells[5], out int rate) &&
                TryInt(cells[6], out int speed))
            {
                // 8列目(任意): スピード・頻度を変えていく秒数
                float transition = -1f;

                if (cells.Length >= 8 && TryFloat(cells[7], out float parsed))
                {
                    transition = parsed;
                }

                rows.Add(new StageRow
                {
                    stage = stage,
                    lineGauge = lineGauge,
                    level = level,
                    nodeCount = nodeCount,
                    group = group,
                    rate = rate,
                    speed = speed,
                    transition = transition
                });
            }
        }

        return rows;
    }

    // 連続回数に合う行を探す。
    //  1. 回数がちょうど同じ行(「4!」のような以上の行は除く)
    //  2. なければ「〇!」の行のうち、回数以上で一番大きいもの
    //  3. それもなければ最後の行
    public static CountRow FindCountRow(List<CountRow> rows, int streak)
    {
        if (rows == null || rows.Count == 0)
        {
            return null;
        }

        foreach (CountRow row in rows)
        {
            if (!row.atLeast && row.count == streak)
            {
                return row;
            }
        }

        CountRow best = null;

        foreach (CountRow row in rows)
        {
            if (row.atLeast && row.count <= streak &&
                (best == null || row.count > best.count))
            {
                best = row;
            }
        }

        return best != null ? best : rows[rows.Count - 1];
    }

    // ===== 内部処理 =====

    private static IEnumerable<string[]> ReadRows(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            string[] cells = line.Split(new[] { '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = cells[i].Trim();
            }

            // 先頭が数字でない行(見出し)は読み飛ばす
            if (cells.Length == 0 || !TryInt(cells[0], out _))
            {
                continue;
            }

            yield return cells;
        }
    }

    private static bool TryInt(string s, out int value)
    {
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryFloat(string s, out float value)
    {
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}

// ファイルを設定しなかったときに使う、シートの初期データ
public static class SpawnTableDefaults
{
    // id, count, sameline, differentline
    public const string Baseline =
@"1,0,50,50
2,1,40,60
3,2,30,70
4,3,20,80
5,4!,0,100";

    // id, count, same, opposite
    public const string Side =
@"1,0,50,50
2,1,40,60
3,2,30,70
4,3,20,80
5,4!,0,100";

    // id, group, base(1=良性 2=悪性), type
    //  1=通常 2=同時出現 3=迅速 4=重圧 5=停滞 6=左腕 7=右腕 8=左足 9=右足 10=交錯 11=造線
    public const string NodeGroup =
@"1,1,1,1
2,1,1,1
3,1,1,1
4,1,2,1
5,1,2,1
6,1,1,2
7,1,1,2
8,1,2,2
9,1,2,2
10,1,2,2
11,2,1,1
12,2,1,1
13,2,1,1
14,2,2,1
15,2,2,1
16,2,1,2
17,2,1,2
18,2,1,2
19,2,2,2
20,2,2,2
21,2,1,3
22,2,2,3
23,2,1,1
24,2,1,1
25,3,1,1
26,3,2,1
27,3,2,1
28,3,2,1
29,3,1,2
30,3,2,2
31,3,1,2
32,3,2,2
33,3,1,3
34,3,1,3
35,3,2,3
36,3,2,3
37,3,1,1
38,3,2,1
39,3,1,1
40,3,2,1";

    // stage, linegauge, level, nodecount, group, rate, speed, [transition(秒・任意)]
    public const string Stage =
@"1,1000,1,10,1,10,10
1,1000,2,14,2,15,15
1,1000,3,16,3,20,20";
}