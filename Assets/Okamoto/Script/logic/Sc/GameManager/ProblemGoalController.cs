
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ProblemGoalController : MonoBehaviour
{
    [Header("問題モード管理")]
    public ProblemModeManager problemModeManager;

    private void Awake()
    {
        Collider2D goalCollider = GetComponent<Collider2D>();
        goalCollider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (problemModeManager == null)
            return;

        NodeController node = other.GetComponent<NodeController>();

        // 子オブジェクトにColliderがある場合にも対応
        if (node == null)
            node = other.GetComponentInParent<NodeController>();

        if (node == null)
            return;

        if (node != problemModeManager.CurrentNode)
            return;

        problemModeManager.NotifyGoalReached();
    }
}
