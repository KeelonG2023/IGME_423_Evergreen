using System;
using System.Collections.Generic;
using Evergreen.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>
    /// Folder-tab build menu. Each BuildingDefinition in the catalog becomes a card.
    /// Clicking a card expands it (one at a time); its Place button raises
    /// StructureSelected, which the placement system listens to.
    /// </summary>
    public class BuildMenu : MonoBehaviour
    {
        [Serializable]
        public class CategoryTab
        {
            public Button button;
            public bool showsAll;
            public BuildingCategory category;
            [Tooltip("Optional. Its height grows when the tab is active (folder-tab look).")]
            public LayoutElement layout;
            [Tooltip("Optional. Shown only on the active tab (the lime stripe).")]
            public GameObject activeIndicator;
        }

        [SerializeField] ResourceManager resources;
        [SerializeField] ResourceIcons icons;
        [SerializeField] List<BuildingDefinition> catalog = new List<BuildingDefinition>();

        [Header("Layout")]
        [SerializeField] Transform cardParent;
        [SerializeField] BuildCard cardTemplate;
        [SerializeField] Text moneyText;
        [SerializeField] List<CategoryTab> tabs = new List<CategoryTab>();

        [Header("Tab look")]
        [SerializeField] Color tabActive = new Color(0.082f, 0.102f, 0.098f);
        [SerializeField] Color tabActiveLabel = new Color(0.608f, 0.882f, 0.365f);
        [SerializeField] Color tabInactive = new Color(0.169f, 0.2f, 0.188f);
        [SerializeField] Color tabInactiveLabel = new Color(0.788f, 0.824f, 0.808f);
        [SerializeField] float activeTabHeight = 46f;
        [SerializeField] float inactiveTabHeight = 40f;

        /// <summary>Raised with the definition when the player presses Place, or null when cleared.</summary>
        public event Action<BuildingDefinition> StructureSelected;

        public BuildingDefinition Selected { get; private set; }
        public IReadOnlyList<BuildingDefinition> Catalog => catalog;

        readonly List<BuildCard> cards = new List<BuildCard>();
        CategoryTab activeTab;
        float refreshTimer;

        void Awake()
        {
            if (resources == null) resources = FindFirstObjectByType<ResourceManager>();
        }

        void Start()
        {
            if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);

            foreach (BuildingDefinition def in catalog)
            {
                if (def == null || cardTemplate == null) continue;
                BuildCard card = Instantiate(cardTemplate, cardParent);
                card.name = "Card - " + def.displayName;
                card.gameObject.SetActive(true);
                card.Setup(def, icons);
                if (card.Button != null) card.Button.onClick.AddListener(() => ToggleCard(card));
                if (card.PlaceButton != null) card.PlaceButton.onClick.AddListener(() => PlaceCard(card));
                cards.Add(card);
            }

            foreach (CategoryTab tab in tabs)
            {
                CategoryTab t = tab;
                if (t.button != null) t.button.onClick.AddListener(() => SelectTab(t));
            }
            SelectTab(tabs.Count > 0 ? tabs[0] : null);
            Refresh();
        }

        void Update()
        {
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = 0.25f;
                Refresh();
            }
        }

        void Refresh()
        {
            float money = resources != null ? resources.Money : float.PositiveInfinity;
            if (moneyText != null && resources != null) moneyText.text = $"${Mathf.FloorToInt(money):N0}";

            foreach (BuildCard card in cards)
            {
                float cost = card.Definition.cost;
                bool affordable = resources == null || resources.CanAfford(cost);
                // CanAfford keeps at least $1, so the shortfall is cost - money + 1.
                card.SetAffordable(affordable, Mathf.Max(0f, cost - money + 1f));
            }
        }

        void ToggleCard(BuildCard card)
        {
            Deselect();
            bool open = !card.IsOpen;
            foreach (BuildCard c in cards) c.SetOpen(c == card && open);
            RebuildLayout();
        }

        void PlaceCard(BuildCard card)
        {
            Deselect();
            if (resources != null && !resources.CanAfford(card.Definition.cost)) return;
            Selected = card.Definition;
            StructureSelected?.Invoke(Selected);
        }

        public void ClearSelection()
        {
            if (Selected == null) return;
            Selected = null;
            StructureSelected?.Invoke(null);
        }

        public void CollapseAll()
        {
            foreach (BuildCard c in cards) c.SetOpen(false);
            RebuildLayout();
        }

        void SelectTab(CategoryTab tab)
        {
            Deselect();
            activeTab = tab;
            foreach (BuildCard card in cards)
            {
                bool show = tab == null || tab.showsAll || card.Definition.category == tab.category;
                card.gameObject.SetActive(show);
                if (!show) card.SetOpen(false);
            }

            foreach (CategoryTab t in tabs)
            {
                if (t.button == null) continue;
                bool active = t == activeTab;
                if (t.button.targetGraphic != null) t.button.targetGraphic.color = active ? tabActive : tabInactive;
                Text label = t.button.GetComponentInChildren<Text>();
                if (label != null) label.color = active ? tabActiveLabel : tabInactiveLabel;
                if (t.layout != null) t.layout.preferredHeight = t.layout.minHeight = active ? activeTabHeight : inactiveTabHeight;
                if (t.activeIndicator != null) t.activeIndicator.SetActive(active);
            }
            RebuildLayout();
        }

        void RebuildLayout()
        {
            if (cardParent is RectTransform rt) LayoutRebuilder.MarkLayoutForRebuild(rt);
        }

        static void Deselect()
        {
            // Keeps Space (pause hotkey) from re-clicking the last pressed button.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
