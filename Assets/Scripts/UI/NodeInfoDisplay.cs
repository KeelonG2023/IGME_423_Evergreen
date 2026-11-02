using System.Resources;
using UnityEngine;
using Evergreen.Systems;
using UnityEngine.UI;

public class NodeInfoDisplay : MonoBehaviour
{
    private Evergreen.Systems.ResourceManager resourceManager;
    private NodeBehavior currentNode = null;
    [SerializeField] private Button purchaseButton;
    [SerializeField] private Text buildingTitle;
    [SerializeField] private Text researchCost;
    [SerializeField] private Text buildingDescription;
    [SerializeField] private Text purchaseLabel;
    [SerializeField] private Button purchaseableState;
    [SerializeField] private Button purchasedState;
    [SerializeField] private Button unpurchaseableState;


    void Start()
    {
        resourceManager = Evergreen.Systems.ResourceManager.Instance;
    }

    // Update is called once per frame
    void Update()
    {
        if (currentNode.purchased || currentNode.unlocked) return;
    }

    public void DisplayNodeInfo(NodeBehavior newNode)
    {
        if (currentNode != null) currentNode.SetInactive();
        currentNode = newNode;
        buildingTitle.text = currentNode.building.displayName;
        researchCost.text = "Cost: " + currentNode.researchCost;
        buildingDescription.text = currentNode.building.description;

        if (currentNode.purchased)
        {
            SetButtonState(purchasedState);
            purchaseLabel.text = "Purchased";
        }
        else if (!currentNode.unlocked)
        {
            SetButtonState(unpurchaseableState);
            purchaseLabel.text = "Locked";
        }
        else if (resourceManager.Happiness < currentNode.researchCost)
        {
            SetButtonState(unpurchaseableState);
            purchaseLabel.text = "Purchase";
        }
        else
        {
            SetButtonState(purchaseableState);
            purchaseLabel.text = "Purchase";
        }
    }

    public void Purchase()
    {
        if (!currentNode.unlocked || resourceManager.Happiness < currentNode.researchCost) return;
        resourceManager.SpendHappiness(currentNode.researchCost);
        currentNode.PurchaseNode();
        SetButtonState(purchasedState);
        purchaseLabel.text = "Purchased";
    }

    private void SetButtonState(Button template)
    {
        purchaseButton.transition = template.transition;
        purchaseButton.colors = template.colors;
        purchaseButton.spriteState = template.spriteState;
        purchaseButton.animationTriggers = template.animationTriggers;
    }
}
