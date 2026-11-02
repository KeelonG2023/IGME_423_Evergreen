using UnityEngine;

public class PurchaseNode : MonoBehaviour
{
    [SerializeField] private NodeInfoDisplay display;

    public void OnClick()
    {
        display.Purchase();
    }
}
