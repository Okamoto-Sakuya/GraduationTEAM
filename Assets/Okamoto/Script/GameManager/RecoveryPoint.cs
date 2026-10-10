using UnityEngine;

public class RecoveryPoint : MonoBehaviour
{
    [Header("‰ñŽû“_‚Ì–¼‘O")]
    public string recoveryPointName;

    public void Recover(NodeController node)
    {
        if (node == null) return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RecoverNode(node.nodeType);
        }

        node.MarkRecovered();
    }
}