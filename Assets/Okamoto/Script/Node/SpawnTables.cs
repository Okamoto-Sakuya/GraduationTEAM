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
    public int type;         // 1=良性 2=悪性 3=良性(2つ同時) 4=悪性(2つ同時)
}

// ステージの表の1行
public class StageRow
{
    public int stage;
    public int time;         // 経過時間(100 = 10.0秒)
    public int level;
    public int group;        // 出現するノードグループ
    public int rate;         // 出現するノードの頻度
    public int speed;        // 出現したノードのスピード
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
            // id, group, type
            if (cells.Length < 3) continue;

            if (TryInt(cells[0], out int id) &&
                TryInt(cells[1], out int group) &&
                TryInt(cells[2], out int type))
            {
                rows.Add(new NodeRow { id = id, group = group, type = type });
            }
        }

        return rows;
    }

    public static List<StageRow> ParseStageRows(string text)
    {
        var rows = new List<StageRow>();

        foreach (string[] cells in ReadRows(text))
        {
            // stage, time, level, group, rate, speed
            if (cells.Length < 6) continue;

            if (TryInt(cells[0], out int stage) &&
                TryInt(cells[1], out int time) &&
                TryInt(cells[2], out int level) &&
                TryInt(cells[3], out int group) &&
                TryInt(cells[4], out int rate) &&
                TryInt(cells[5], out int speed))
            {
                rows.Add(new StageRow
                {
                    stage = stage,
                    time = time,
                    level = level,
                    group = group,
                    rate = rate,
                    speed = speed
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

    // id, nodegroup, nodetype (1=良性 2=悪性 3=良性2つ 4=悪性2つ)
    public const string NodeGroup =
@"1,1,1
2,1,1
3,1,1
4,1,1
5,1,1
6,1,2
7,1,2
8,1,2
9,1,2
10,1,2
11,2,1
12,2,1
13,2,1
14,2,1
15,2,1
16,2,2
17,2,2
18,2,2
19,2,2
20,2,2
21,2,3
22,2,3
23,2,4
24,2,4
25,3,1
26,3,1
27,3,1
28,3,1
29,3,2
30,3,2
31,3,2
32,3,2
33,3,3
34,3,3
35,3,3
36,3,3
37,3,4
38,3,4
39,3,4
40,3,4";

    // stage, time(100=10.0秒), level, nodegroup, rate, speed
    public const string Stage =
@"1,0,1,1,10,10
1,100,2,1,11,10
1,200,2,2,12,10
1,300,3,2,13,12
1,400,5,3,14,12
1,500,6,3,15,14";
}