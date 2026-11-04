using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Evergreen.Systems;
using UnityEngine.UI;
using Evergreen.UI;

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

    [Header("Card look (uses the build menu's card)")]
    [Tooltip("Shows the build menu's building card instead of the plain node button.")]
    [SerializeField] private bool useBuildCard = true;
    [SerializeField] private float cardWidth = 420f;
    [Tooltip("Thickness of the branch lines when they're stretched between cards.")]
    [SerializeField] private float lineThickness = 8f;

    private BuildCard card;

    void Start()
    {
        if (useBuildCard) SpawnCard();
        if (startActive) SetActive();
        else SetInactive();
    }

    /// <summary>Puts a copy of the build menu's card on this node and hides the node's own button art.</summary>
    private void SpawnCard()
    {
        BuildMenu menu = FindFirstObjectByType<BuildMenu>(FindObjectsInactive.Include);
        if (menu == null || menu.CardTemplate == null || building == null) return;

        card = Instantiate(menu.CardTemplate, transform);
        card.name = "Card - " + building.displayName;
        card.gameObject.SetActive(true);
        card.SetupTreeNode(building, menu.Icons);
        if (card.HeaderButton != null) card.HeaderButton.onClick.AddListener(SetActive);

        // Size the node to the card: fixed width, height from the card's own layout.
        var cardRect = (RectTransform)card.transform;
        cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(cardWidth, cardRect.sizeDelta.y);
        var fitter = card.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        LayoutRebuilder.ForceRebuildLayoutImmediate(cardRect);
        ((RectTransform)transform).sizeDelta = cardRect.sizeDelta;

        // Hide the old node visuals.
        if (button != null && button.targetGraphic != null) button.targetGraphic.enabled = false;
        Transform label = transform.Find("Label");
        if (label != null) label.gameObject.SetActive(false);

        RefreshCard();
        ConnectLines();
    }

    /// <summary>Stretches each branch line from this card to the card it unlocks.</summary>
    private void ConnectLines()
    {
        for (int i = 0; i < unlocks.Count && i < treeLines.Count; i++)
        {
            if (unlocks[i] == null || treeLines[i] == null) continue;
            RectTransform line = treeLines[i].rectTransform;
            Vector3 from = transform.position;
            Vector3 to = unlocks[i].transform.position;
            Vector3 delta = to - from;
            float scale = line.lossyScale.x != 0f ? line.lossyScale.x : 1f;

            line.position = (from + to) / 2f;
            line.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            line.sizeDelta = new Vector2(delta.magnitude / scale, lineThickness);
            // Draw the line just behind whichever of the two cards comes first.
            if (line.parent == transform.parent && unlocks[i].transform.parent == transform.parent)
                line.SetSiblingIndex(Mathf.Min(transform.GetSiblingIndex(), unlocks[i].transform.GetSiblingIndex()));
        }
    }

    private void RefreshCard()
    {
        if (card != null) card.SetTreeNodeState(unlocked, purchased, isActive, researchCost);
    }

    void Update()
    {
        
    }

    public void SetActive()
    {
        if (isActive) return;
        isActive = true;
        SetButtonState(selectedState);
        RefreshCard();
        infoDisplay.DisplayNodeInfo(this);
    }

    public void SetInactive()
    {
        isActive = false;
        if (purchased) SetButtonState(purchasedState);
        else if (unlocked) SetButtonState(unlockedState);
        else SetButtonState(lockedState);
        RefreshCard();
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
        RefreshCard();

    }

    public void UnlockNode()
    {
        unlocked = true;
        SetButtonState(unlockedState);
        RefreshCard();
    }
}
