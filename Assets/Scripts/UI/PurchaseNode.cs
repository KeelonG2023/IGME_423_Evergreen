using UnityEngine;

/// <summary>
/// Helper script that connects the purchase button to the skill tree's brain for purchasing a node
/// </summary>
public class PurchaseNode : MonoBehaviour
{
    [SerializeField] private NodeInfoDisplay display;

    public void OnClick()
    {
        display.Purchase();
    }
}
