using System;
using UnityEngine;

public class NodeController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 1f;
    [SerializeField] private BasicLine spawnBasicLine;

    [Header("行き止まり")]
    [Tooltip("進める線がなくなったとき、固まらずにノードを消す")]
    [SerializeField] private bool destroyOnDeadEnd = true;

    // 四肢(回収点)に到達したとき。スコア・ダメージ処理はここに登録する
    public event Action<NodeController, LimbController> OnCollected;

    public bool IsMoving { get; private set; }
    public BasicLine SpawnBasicLine => spawnBasicLine;

    private PathLine line;
    private float s;
    private int dir = 1;
    private bool placed;

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

    // 分岐点に着いたときの処理。続行できなければfalse
    private bool Arrive(PathGraph g, PathVertex v)
    {
        if (v.limb != null && v.limb.IsAlive)
        {
            transform.position = v.pos;
            OnCollected?.Invoke(this, v.limb);
            Destroy(gameObject);
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
}