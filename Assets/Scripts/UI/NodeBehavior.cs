using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Evergreen.Systems;
using UnityEngine.UI;

/// <summary>
/// Represents a node within the skill tree
/// Stores the node's build card and happiness cost
/// Updates the visual style of itself and any dependant nodes/branches when purchased
/// </summary>
public class NodeBehavior : MonoBehaviour
{
    [SerializeField] public BuildingDefinition building;
    [SerializeField] public float researchCost;
    [SerializeField] public bool unlocked;
    [SerializeField] public bool purchased;
    [SerializeField] private List<NodeBehavior> unlocks;
    [SerializeField] private List<Image> treeLines;
    [SerializeField] private NodeInfoDisplay infoDisplay;
    [SerializeField] private Button button;
    [SerializeField] private bool isActive;
    [SerializeField] private bool startActive;
    [SerializeField] private Button selectedState;
    [SerializeField] private Button unlockedState;
    [SerializeField] private Button lockedState;
    [SerializeField] private Button purchasedState;

    void Start()
    {
        if (startActive) SetActive();
        else SetInactive();
    }

    void Update()
    {
        
    }

    public void SetActive()
    {
        if (isActive) return;
        isActive = true;
        SetButtonState(selectedState);
        infoDisplay.DisplayNodeInfo(this);
    }

    public void SetInactive()
    {
        isActive = false;
        if (purchased) SetButtonState(purchasedState);
        else if (unlocked) SetButtonState(unlockedState);
        else SetButtonState(lockedState);
    }

    /// <summary>Changes the viusal style of the node to reflect its state </summary>
    private void SetButtonState(Button template)
    {
        button.transition = template.transition;
        button.colors = template.colors;
        button.spriteState = template.spriteState;
        button.animationTriggers = template.animationTriggers;
    }

    /// <summary>Updates the visual look of the dependent branches and nodes </summary>
    public void PurchaseNode()
    {
        purchased = true;
        for (int i = 0; i < unlocks.Count; i++)
        {
            unlocks[i].UnlockNode();
            treeLines[i].color = Color.white;
        }

    }

    public void UnlockNode()
    {
        unlocked = true;
        SetButtonState(unlockedState);
    }
}
