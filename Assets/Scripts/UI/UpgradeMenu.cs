using System.Collections.Generic;
using Evergreen.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>
    /// Turns the build panel into an upgrade panel while a placed building is selected.
    /// Selecting a building (BuildingClickSelector) hides the build tabs and cards and shows:
    ///   - a compact two-line summary of the building (name, years left, upgrades owned, inline yields),
    ///   - a "&lt; Back to Build" button pinned to the bottom of the panel (the tab row is hidden),
    ///   - one card per upgrade its BuildingDefinition offers, with a Buy button.
    /// Deselecting (Back, Esc, clicking empty ground) puts the build menu back.
    /// Upgrade cards are clones of the build menu's card template, so they share its look.
    /// Lives on the Build Panel next to BuildMenu; every reference is found automatically if left empty.
    /// </summary>
    public class UpgradeMenu : MonoBehaviour
    {
        /// <summary>How often (seconds) affordability, years left and yields are re-checked.</summary>
        const float RefreshInterval = 0.25f;

        [SerializeField] BuildMenu buildMenu;
        [SerializeField] BuildingClickSelector selector;
        [SerializeField] ResourceManager resources;
        [Tooltip("Optional. If the panel is slid away, it slides back out when a building is selected.")]
        [SerializeField] BuildPanelToggle panelToggle;

        [Header("Panel parts (found by name under this object if empty)")]
        [Tooltip("Header/Title. Shows the building's name while upgrading.")]
        [SerializeField] Text titleText;
        [Tooltip("Tabs. Its tab buttons are hidden while upgrading.")]
        [SerializeField] Transform tabsRow;
        [Tooltip("Tabs/Tab All. Cloned to make the Back button so it matches the tabs.")]
        [SerializeField] Button backButtonTemplate;
        [Tooltip("List. Scrolled back to the top when a building is opened.")]
        [SerializeField] ScrollRect scrollRect;

        [Header("Text")]
        [SerializeField] string backLabel = "< Back to Build";
        [SerializeField] string titleWhileUpgrading = "Upgrades";

        /// <summary>The building whose upgrades are showing, or null when the build menu is showing.</summary>
        public BuildingYield Target { get; private set; }
        public bool IsShowingUpgrades { get; private set; }

        readonly List<BuildCard> upgradeCards = new List<BuildCard>();
        readonly List<GameObject> hiddenTabs = new List<GameObject>();
        BuildingSummaryHeader summaryHeader;
        Button backButton;
        GameObject backRow;
        string buildTitle;
        float secondsUntilRefresh;

        // ---------- Unity lifecycle ----------

        void Awake()
        {
            if (buildMenu == null) buildMenu = GetComponent<BuildMenu>();
            if (selector == null) selector = FindFirstObjectByType<BuildingClickSelector>(FindObjectsInactive.Include);
            if (resources == null) resources = FindFirstObjectByType<ResourceManager>();
            if (panelToggle == null) panelToggle = GetComponent<BuildPanelToggle>();

            if (titleText == null) titleText = FindChild<Text>("Header/Title");
            if (tabsRow == null) tabsRow = transform.Find("Tabs");
            if (backButtonTemplate == null) backButtonTemplate = FindChild<Button>("Tabs/Tab All");
            if (scrollRect == null) scrollRect = FindChild<ScrollRect>("List");

            if (titleText != null) buildTitle = titleText.text;
        }

        void OnEnable()
        {
            if (selector != null) selector.SelectionChanged += OnSelectionChanged;
        }

        void OnDisable()
        {
            if (selector != null) selector.SelectionChanged -= OnSelectionChanged;
            CloseUpgrades();
        }

        void Update()
        {
            if (!IsShowingUpgrades) return;

            // The building was destroyed while its upgrades were open.
            if (Target == null)
            {
                CloseUpgrades();
                return;
            }

            // Unscaled so it still updates while the game is paused.
            secondsUntilRefresh -= Time.unscaledDeltaTime;
            if (secondsUntilRefresh <= 0f)
            {
                secondsUntilRefresh = RefreshInterval;
                RefreshCards();
            }
        }

        // ---------- Opening / closing ----------

        void OnSelectionChanged(BuildingYield building)
        {
            if (building != null) OpenUpgrades(building);
            else CloseUpgrades();
        }

        /// <summary>Shows the upgrades for a building in place of the build menu.</summary>
        public void OpenUpgrades(BuildingYield building)
        {
            if (building == null) { CloseUpgrades(); return; }

            Target = building;
            if (!IsShowingUpgrades) EnterUpgradeView();

            SpawnCards();
            RefreshCards();
            if (panelToggle != null && panelToggle.IsCollapsed) panelToggle.SetCollapsed(false);
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
        }

        /// <summary>Removes the upgrade cards and puts the build menu back.</summary>
        public void CloseUpgrades()
        {
            Target = null;
            ClearCards();
            if (IsShowingUpgrades) ExitUpgradeView();
        }

        void EnterUpgradeView()
        {
            IsShowingUpgrades = true;
            if (buildMenu != null)
            {
                buildMenu.ClearSelection();
                buildMenu.SetBuildCardsVisible(false);
            }

            // Hide the category tabs and show Back in their place.
            hiddenTabs.Clear();
            if (tabsRow != null)
            {
                foreach (Transform tab in tabsRow)
                {
                    if (backButton != null && tab == backButton.transform) continue;
                    if (!tab.gameObject.activeSelf) continue;
                    hiddenTabs.Add(tab.gameObject);
                    tab.gameObject.SetActive(false);
                }
            }

            if (tabsRow != null) tabsRow.gameObject.SetActive(false);

            EnsureBackButton();
            if (backRow != null) backRow.SetActive(true);
            if (titleText != null) titleText.text = titleWhileUpgrading;
        }

        void ExitUpgradeView()
        {
            IsShowingUpgrades = false;

            if (backRow != null) backRow.SetActive(false);
            if (tabsRow != null) tabsRow.gameObject.SetActive(true);
            foreach (GameObject tab in hiddenTabs)
                if (tab != null) tab.SetActive(true);
            hiddenTabs.Clear();

            if (titleText != null && buildTitle != null) titleText.text = buildTitle;
            if (buildMenu != null) buildMenu.SetBuildCardsVisible(true);
        }

        /// <summary>
        /// Clones the "All" tab into a full-width Back button the first time it's needed,
        /// and puts it in its own row at the bottom of the panel, under the list.
        /// </summary>
        void EnsureBackButton()
        {
            if (backButton != null || backButtonTemplate == null) return;

            // Row with a little breathing room above it (the panel's layout has no spacing).
            backRow = new GameObject("Back Row", typeof(RectTransform), typeof(VerticalLayoutGroup));
            backRow.transform.SetParent(transform, false);
            var rowLayout = backRow.GetComponent<VerticalLayoutGroup>();
            rowLayout.padding = new RectOffset(0, 0, 20, 0);
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;

            // Directly under the list so it sits at the bottom of the panel.
            Transform list = scrollRect != null ? scrollRect.transform : null;
            if (list != null && list.parent == transform) backRow.transform.SetSiblingIndex(list.GetSiblingIndex() + 1);

            backButton = Instantiate(backButtonTemplate, backRow.transform);
            backButton.name = "Back To Build";
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(OnBackClicked);

            Text label = backButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = backLabel;
                label.color = ArcadePalette.Lime;
            }

            // The tab's "selected" stripe would look odd on a standalone button.
            Transform stripe = backButton.transform.Find("Active Stripe");
            if (stripe != null) stripe.gameObject.SetActive(false);

            LayoutElement layout = backButton.GetComponent<LayoutElement>();
            if (layout == null) layout = backButton.gameObject.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
            layout.minHeight = layout.preferredHeight = 84f;

            backRow.SetActive(false);
        }

        void OnBackClicked()
        {
            ClearUiFocus();
            if (selector != null) selector.Deselect(); // fires SelectionChanged(null) -> CloseUpgrades
            else CloseUpgrades();
        }

        // ---------- Cards ----------

        void SpawnCards()
        {
            ClearCards();
            if (buildMenu == null || buildMenu.CardTemplate == null) return;

            summaryHeader = CreateSummaryHeader();
            if (summaryHeader != null) summaryHeader.Refresh(Target, buildMenu.Icons);

            foreach (BuildingUpgrade upgrade in Target.AvailableUpgrades)
            {
                if (upgrade == null) continue;
                if (!ResearchUnlocks.IsAvailable(upgrade)) continue; // not researched in the skill tree yet

                BuildCard card = CreateCard("Upgrade - " + upgrade.displayName);
                card.SetupUpgrade(upgrade, buildMenu.Icons, Target.YearsRemaining);

                if (card.HeaderButton != null) card.HeaderButton.onClick.AddListener(() => ToggleCard(card));
                if (card.DetailsButton != null) card.DetailsButton.onClick.AddListener(() => ToggleCard(card));
                if (card.PlaceButton != null) card.PlaceButton.onClick.AddListener(() => Buy(card.Upgrade));
                upgradeCards.Add(card);
            }

            RebuildLayout();
        }

        /// <summary>Builds the two-line header, borrowing fonts and the rounded background from the card template.</summary>
        BuildingSummaryHeader CreateSummaryHeader()
        {
            BuildCard template = buildMenu.CardTemplate;
            Font bold = FontOf(template.transform, "Header/Names/Name");
            Font regular = FontOf(template.transform, "Header/Names/Lifespan");
            if (bold == null && titleText != null) bold = titleText.font;
            if (regular == null) regular = bold;

            Image templateBg = template.GetComponent<Image>();
            BuildingSummaryHeader header = BuildingSummaryHeader.Create(
                buildMenu.CardParent, bold, regular, templateBg != null ? templateBg.sprite : null);
            header.name = "Summary - " + Target.DisplayName;
            header.transform.SetAsLastSibling();
            return header;
        }

        static Font FontOf(Transform root, string path)
        {
            Transform child = root.Find(path);
            Text text = child != null ? child.GetComponent<Text>() : null;
            return text != null ? text.font : null;
        }

        BuildCard CreateCard(string cardName)
        {
            BuildCard card = Instantiate(buildMenu.CardTemplate, buildMenu.CardParent);
            card.name = cardName;
            card.transform.SetAsLastSibling();
            card.gameObject.SetActive(true);
            return card;
        }

        void ClearCards()
        {
            if (summaryHeader != null) Destroy(summaryHeader.gameObject);
            summaryHeader = null;
            foreach (BuildCard card in upgradeCards)
                if (card != null) Destroy(card.gameObject);
            upgradeCards.Clear();
        }

        /// <summary>Updates the summary (yields, years left) and each upgrade's owned / affordable state.</summary>
        void RefreshCards()
        {
            if (Target == null) return;
            if (summaryHeader != null) summaryHeader.Refresh(Target, buildMenu.Icons);

            float money = resources != null ? resources.Money : float.PositiveInfinity;
            foreach (BuildCard card in upgradeCards)
            {
                BuildingUpgrade upgrade = card.Upgrade;
                bool installed = Target.HasUpgrade(upgrade);
                bool affordable = resources == null || resources.CanAfford(upgrade.cost);

                // CanAfford requires at least $1 left over, so the shortfall is cost - money + 1.
                float moneyShort = Mathf.Max(0f, upgrade.cost - money + 1f);
                card.SetUpgradeState(installed, Target.IsExpired, affordable, moneyShort);
            }
        }

        /// <summary>Opens the clicked card and closes the other upgrade cards. Clicking an open card closes it.</summary>
        void ToggleCard(BuildCard clickedCard)
        {
            ClearUiFocus();
            bool shouldOpen = !clickedCard.IsOpen;
            foreach (BuildCard card in upgradeCards)
                card.SetOpen(card == clickedCard && shouldOpen);
            RebuildLayout();
        }

        void Buy(BuildingUpgrade upgrade)
        {
            ClearUiFocus();
            if (Target == null || upgrade == null) return;
            if (Target.TryInstall(upgrade, resources)) RefreshCards();
        }

        // ---------- Helpers ----------

        T FindChild<T>(string path) where T : Component
        {
            Transform child = transform.Find(path);
            return child != null ? child.GetComponent<T>() : null;
        }

        void RebuildLayout()
        {
            if (buildMenu != null && buildMenu.CardParent is RectTransform cardParentRect)
                LayoutRebuilder.MarkLayoutForRebuild(cardParentRect);
        }

        /// <summary>Keeps Space (the pause hotkey) from re-clicking the last pressed button.</summary>
        static void ClearUiFocus()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
