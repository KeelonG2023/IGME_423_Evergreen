using System.Resources;
using UnityEngine;
using Evergreen.Systems;
using UnityEngine.UI;
using Evergreen.UI;

/// <summary>
/// Displays information about the selected node
/// Handles the purchasing of a node, connecting to other game systems
/// Updates the visual style of the purchase button
/// </summary>
public class NodeInfoDisplay : MonoBehaviour
{
    private Evergreen.Systems.ResourceManager resourceManager;
    private NodeBehavior currentNode = null;
    [SerializeField] private BuildMenu buildMenu;
    [SerializeField] private Button purchaseButton;
    [SerializeField] private Text buildingTitle;
    [SerializeField] private Text researchCost;
    [SerializeField] private Text buildingDescription;
    [SerializeField] private Text purchaseLabel;
    [SerializeField] private Button purchaseableState;
    [SerializeField] private Button purchasedState;
    [SerializeField] private Button unpurchaseableState;
    private bool purchaseable = false;

    void Start()
    {
        resourceManager = Evergreen.Systems.ResourceManager.Instance;
    }

    // Update is called once per frame
    void Update()
    {
        if (currentNode.purchased || !currentNode.unlocked || purchaseable) return;
        if (resourceManager.Happiness >= currentNode.researchCost)
        {
            purchaseable = true;
            SetButtonState(purchaseableState);
        }
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
            purchaseable = true;
        }
    }

    /// <summary>Calls all functions to update game when a node is purchased </summary>
    public void Purchase()
    {
        if (!currentNode.unlocked || resourceManager.Happiness < currentNode.researchCost) return;
        resourceManager.SpendHappiness(currentNode.researchCost);
        buildMenu.AddCard(currentNode.building);
        currentNode.PurchaseNode();
        SetButtonState(purchasedState);
        purchaseLabel.text = "Purchased";
        purchaseable = false;
    }

    /// <summary>Changes the visual state of the purchase button </summary>
    private void SetButtonState(Button template)
    {
        purchaseButton.transition = template.transition;
        purchaseButton.colors = template.colors;
        purchaseButton.spriteState = template.spriteState;
        purchaseButton.animationTriggers = template.animationTriggers;
    }
}
