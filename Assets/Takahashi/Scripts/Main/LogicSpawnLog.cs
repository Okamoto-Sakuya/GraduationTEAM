using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// ロジックモードの出現ログ。出たノードの種類・順番・位置と、その結果（どこに回収されたか等）を記録する。
///   Unity 側 : Console に 1 体ずつ出す（[出現ログ] …）
///   Excel 側 : CSV（UTF-8 BOM 付き。Excel でそのまま開ける）に書き出す
///       エディタ … プロジェクトフォルダ（Assets と同じ階層）の SpawnLogs/
///       ビルド   … Application.persistentDataPath/SpawnLogs/
///       ファイル名 … logic_spawnlog_stage{ステージ}_{日時}.csv（1 プレイ 1 ファイル）
///   CSV は問題が終わるたび・クリア / ゲームオーバー時・再生停止時に上書き保存する
/// </summary>
public class LogicSpawnLog
{
    public class Record
    {
        public int serial, stage, problem, order;
        public float time;
        public string mode;
        public int type;
        public bool bad;
        public string line = "", limb = "";
        public string result = "未回収";
        public string collectLimb = "", collectKind = "";
        public int collectLevel, score;
        public float damage, san;
    }

    public static readonly string[] TypeNames =
        { "", "良性", "悪性", "良性(2つ同時)", "悪性(2つ同時)", "四肢対応", "足早", "重い", "途中停止" };

    public static string TypeName(int type) => type >= 1 && type < TypeNames.Length ? TypeNames[type] : "type" + type;

    public string FilePath { get; }
    public IReadOnlyList<Record> Records => records;

    readonly List<Record> records = new List<Record>();
    readonly int stage;
    readonly string mode;
    readonly bool toConsole;
    string summary;

    public LogicSpawnLog(int stage, string mode, string folder, bool toConsole)
    {
        this.stage = stage;
        this.mode = mode;
        this.toConsole = toConsole;

        string baseDir = Application.isEditor
            ? Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
            : Application.persistentDataPath;
        string dir = Path.Combine(baseDir, string.IsNullOrEmpty(folder) ? "SpawnLogs" : folder);
        FilePath = Path.Combine(dir, $"logic_spawnlog_stage{stage}_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
    }

    /// <summary>出現 1 体分を記録</summary>
    public Record Add(int problem, int order, float time, int type, bool bad, string line, string limb)
    {
        var r = new Record
        {
            serial = records.Count + 1, stage = stage, problem = problem, order = order, time = time,
            mode = mode, type = type, bad = bad, line = line ?? "", limb = limb ?? ""
        };
        records.Add(r);
        if (toConsole)
            Debug.Log($"[出現ログ] #{r.serial} 問題{problem} 順番{order} {TypeName(type)}" +
                      $"{(type >= 5 ? (bad ? "(悪性)" : "(良性)") : "")}{(r.limb != "" ? " 部位:" + r.limb : "")} @{r.line} {time:0.0}秒");
        return r;
    }

    /// <summary>回収・消滅などの結果を記録</summary>
    public void SetResult(Record r, string result)
    {
        if (r == null) return;
        r.result = result;
        if (!toConsole) return;
        string detail = r.collectLimb != ""
            ? $" {r.collectLimb} ← {r.collectKind}Lv{r.collectLevel} スコア+{r.score}" +
              (r.damage > 0 ? $" ダメージ{r.damage:0}" : "") + (r.san > 0 ? $" SAN-{r.san:0}" : "")
            : "";
        Debug.Log($"[出現ログ] #{r.serial} {TypeName(r.type)} → {result}{detail}");
    }

    /// <summary>ステージ終了時（クリア / ゲームオーバー）の一行</summary>
    public void Finish(string result, int score)
    {
        int collected = 0;
        foreach (var r in records) if (r.collectLimb != "") collected++;
        summary = $"# 結果,{result},スコア,{score},出現,{records.Count},回収,{collected}";
        if (toConsole) Debug.Log($"[出現ログ] ステージ{stage} {result}　スコア {score}　出現 {records.Count} 体 / 回収 {collected} 体");
    }

    /// <summary>CSV に上書き保存</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            var sb = new StringBuilder();
            sb.AppendLine("通し番号,ステージ,問題,出現順番,出現時刻(秒),出現モード,nodetype,種類,出現時の性質,出現位置,対応部位," +
                          "結果,回収した部位,回収時の性質,回収時Lv,スコア,ダメージ,SAN減少");
            foreach (var r in records)
            {
                sb.Append(r.serial).Append(',').Append(r.stage).Append(',').Append(r.problem).Append(',')
                  .Append(r.order).Append(',').Append(r.time.ToString("0.00")).Append(',').Append(Esc(r.mode)).Append(',')
                  .Append(r.type).Append(',').Append(Esc(TypeName(r.type))).Append(',').Append(r.bad ? "悪性" : "良性").Append(',')
                  .Append(Esc(r.line)).Append(',').Append(Esc(r.limb)).Append(',')
                  .Append(Esc(r.result)).Append(',').Append(Esc(r.collectLimb)).Append(',').Append(Esc(r.collectKind)).Append(',')
                  .Append(r.collectLimb != "" ? r.collectLevel.ToString() : "").Append(',')
                  .Append(r.collectLimb != "" ? r.score.ToString() : "").Append(',')
                  .Append(r.damage > 0 ? r.damage.ToString("0.##") : "").Append(',')
                  .Append(r.san > 0 ? r.san.ToString("0.##") : "")
                  .AppendLine();
            }
            if (summary != null) sb.AppendLine(summary);
            File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(true));   // BOM 付き → Excel で文字化けしない
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[出現ログ] CSV を保存できませんでした：{FilePath}\n{e.Message}");
        }
    }

    static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}
