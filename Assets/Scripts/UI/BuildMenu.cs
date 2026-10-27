using System;
using System.Collections.Generic;
using Evergreen.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>
    /// Folder-tab build menu. Each BuildingDefinition in the catalog becomes a card.
    /// Clicking a card picks that building and starts placing it right away (click again to put it down).
    /// The chevron on the card opens the details (one at a time); the Place button in there works too.
    /// Picking raises StructureSelected, which the placement system listens to.
    /// </summary>
    public class BuildMenu : MonoBehaviour
    {
        /// <summary>One tab along the top of the menu. Filters cards to one category (or shows all).</summary>
        [Serializable]
        public class CategoryTab
        {
            public Button button;

            [Tooltip("If ticked, this tab shows every building and ignores Category.")]
            public bool showsAll;
            public BuildingCategory category;

            [Tooltip("Optional. Its height grows when the tab is active (folder-tab look).")]
            public LayoutElement layout;

            [Tooltip("Optional. The old lime stripe along the top of the tab. Always hidden now.")]
            public GameObject activeIndicator;
        }

        /// <summary>How often (seconds) card affordability is re-checked.</summary>
        const float RefreshInterval = 0.25f;

        [SerializeField] ResourceManager resources;
        [SerializeField] ResourceIcons icons;

        [Tooltip("Every building the player can build, in display order.")]
        [SerializeField] List<BuildingDefinition> catalog = new List<BuildingDefinition>();

        [Header("Layout")]
        [Tooltip("Cards are spawned as children of this.")]
        [SerializeField] Transform cardParent;
        [Tooltip("Hidden card that gets cloned once per building.")]
        [SerializeField] BuildCard cardTemplate;
        [SerializeField] Text moneyText;
        [SerializeField] List<CategoryTab> tabs = new List<CategoryTab>();

        [Header("Tab look")]
        [FormerlySerializedAs("tabActive")]
        [SerializeField] Color activeTabColor = new Color(0.082f, 0.102f, 0.098f);
        [FormerlySerializedAs("tabActiveLabel")]
        [SerializeField] Color activeTabLabelColor = new Color(0.608f, 0.882f, 0.365f);
        [FormerlySerializedAs("tabInactive")]
        [SerializeField] Color inactiveTabColor = new Color(0.169f, 0.2f, 0.188f);
        [FormerlySerializedAs("tabInactiveLabel")]
        [SerializeField] Color inactiveTabLabelColor = new Color(0.788f, 0.824f, 0.808f);
        [SerializeField] float activeTabHeight = 46f;
        [SerializeField] float inactiveTabHeight = 40f;

        /// <summary>Raised with the definition when the player presses Place, or null when cleared.</summary>
        public event Action<BuildingDefinition> StructureSelected;

        /// <summary>The building the player chose to place, or null.</summary>
        public BuildingDefinition Selected { get; private set; }
        public IReadOnlyList<BuildingDefinition> Catalog => catalog;

        readonly List<BuildCard> cards = new List<BuildCard>();
        CategoryTab activeTab;
        float secondsUntilRefresh;

        // ---------- Unity lifecycle ----------

        void Awake()
        {
            if (resources == null) resources = FindFirstObjectByType<ResourceManager>();
        }

        void Start()
        {
            SpawnCards();

            foreach (CategoryTab tab in tabs)
            {
                CategoryTab clickedTab = tab; // Local copy so each lambda captures its own tab.
                if (clickedTab.button != null) clickedTab.button.onClick.AddListener(() => SelectTab(clickedTab));
            }

            SelectTab(tabs.Count > 0 ? tabs[0] : null);
            RefreshAffordability();
        }

        void Update()
        {
            // Unscaled so it still updates while the game is paused.
            secondsUntilRefresh -= Time.unscaledDeltaTime;
            if (secondsUntilRefresh <= 0f)
            {
                secondsUntilRefresh = RefreshInterval;
                RefreshAffordability();
            }
        }

        void SpawnCards()
        {
            if (cardTemplate == null) return;
            cardTemplate.gameObject.SetActive(false);

            foreach (BuildingDefinition building in catalog)
            {
                if (building == null) continue;

                BuildCard card = Instantiate(cardTemplate, cardParent);
                card.name = "Card - " + building.displayName;
                card.gameObject.SetActive(true);
                card.Setup(building, icons);

                if (card.HeaderButton != null) card.HeaderButton.onClick.AddListener(() => PickCard(card));
                if (card.DetailsButton != null) card.DetailsButton.onClick.AddListener(() => ToggleCard(card));
                if (card.PlaceButton != null) card.PlaceButton.onClick.AddListener(() => PlaceCard(card));
                cards.Add(card);
            }
        }

        /// <summary>Updates the money readout and greys out cards the player can't afford.</summary>
        void RefreshAffordability()
        {
            float money = resources != null ? resources.Money : float.PositiveInfinity;
            if (moneyText != null && resources != null) moneyText.text = $"${Mathf.FloorToInt(money):N0}";

            foreach (BuildCard card in cards)
            {
                float cost = card.Definition.cost;
                bool affordable = resources == null || resources.CanAfford(cost);

                // CanAfford requires at least $1 left over, so the shortfall is cost - money + 1.
                float moneyShort = Mathf.Max(0f, cost - money + 1f);
                card.SetAffordable(affordable, moneyShort);
            }
        }

        // ---------- Card actions ----------

        /// <summary>Opens the clicked card and closes every other one. Clicking an open card closes it.</summary>
        void ToggleCard(BuildCard clickedCard)
        {
            ClearUiFocus();
            bool shouldOpen = !clickedCard.IsOpen;
            foreach (BuildCard card in cards)
                card.SetOpen(card == clickedCard && shouldOpen);
            RebuildLayout();
        }

        /// <summary>Clicking the card: pick it for placement, or drop it if it's already picked.
        /// If it can't be afforded, open the details instead so the player sees how much is missing.</summary>
        void PickCard(BuildCard card)
        {
            ClearUiFocus();
            if (Selected == card.Definition) { ClearSelection(); return; }

            if (resources != null && !resources.CanAfford(card.Definition.cost))
            {
                if (!card.IsOpen) ToggleCard(card);
                return;
            }
            PlaceCard(card);
        }

        void PlaceCard(BuildCard card)
        {
            ClearUiFocus();
            if (resources != null && !resources.CanAfford(card.Definition.cost)) return;

            Selected = card.Definition;
            RefreshPicked();
            StructureSelected?.Invoke(Selected);
        }

        public void ClearSelection()
        {
            if (Selected == null) return;
            Selected = null;
            RefreshPicked();
            StructureSelected?.Invoke(null);
        }

        void RefreshPicked()
        {
            foreach (BuildCard card in cards) card.SetPicked(card.Definition == Selected);
        }

        public void CollapseAll()
        {
            foreach (BuildCard card in cards) card.SetOpen(false);
            RebuildLayout();
        }

        // ---------- Tabs ----------

        /// <summary>Shows only the cards in this tab's category and restyles the tab row.</summary>
        void SelectTab(CategoryTab tab)
        {
            ClearUiFocus();
            activeTab = tab;

            foreach (BuildCard card in cards)
            {
                bool belongsToTab = tab == null || tab.showsAll || card.Definition.category == tab.category;
                card.gameObject.SetActive(belongsToTab);
                if (!belongsToTab) card.SetOpen(false);
            }

            foreach (CategoryTab otherTab in tabs)
                StyleTab(otherTab, otherTab == activeTab);

            RebuildLayout();
        }

        void StyleTab(CategoryTab tab, bool isActive)
        {
            if (tab.button == null) return;

            if (tab.button.targetGraphic != null)
                tab.button.targetGraphic.color = isActive ? activeTabColor : inactiveTabColor;

            Text label = tab.button.GetComponentInChildren<Text>();
            if (label != null) label.color = isActive ? activeTabLabelColor : inactiveTabLabelColor;

            if (tab.layout != null)
            {
                float height = isActive ? activeTabHeight : inactiveTabHeight;
                tab.layout.preferredHeight = height;
                tab.layout.minHeight = height;
            }

            // The lime highlight stripe was removed from the design, so keep it hidden on every tab.
            if (tab.activeIndicator != null) tab.activeIndicator.SetActive(false);
        }

        // ---------- Helpers ----------

        void RebuildLayout()
        {
            if (cardParent is RectTransform cardParentRect) LayoutRebuilder.MarkLayoutForRebuild(cardParentRect);
        }

        /// <summary>Keeps Space (the pause hotkey) from re-clicking the last pressed button.</summary>
        static void ClearUiFocus()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
