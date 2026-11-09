using System.Collections.Generic;
using Evergreen.Systems;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>
    /// One structure in the build menu.
    /// Collapsed: icon, name, lifespan, cost and up to three yield chips.
    /// Expanded: description, labeled yields, lifetime totals, payback time and a Place button.
    /// BuildMenu clones one of these from a template for every building in its catalog.
    /// UpgradeMenu clones the same template for upgrade cards (SetupUpgrade) and for the
    /// selected-building summary at the top of the upgrade list (SetupSummary).
    /// </summary>
    public class BuildCard : MonoBehaviour
    {
        /// <summary>Most yield chips shown on a collapsed card, so the row doesn't overflow.</summary>
        const int MaxChips = 3;

        /// <summary>For buildings that never expire, lifetime totals are shown over this many years.</summary>
        const int YearsShownForPermanentBuildings = 10;

        // Colors that only the build card uses.
        static readonly Color LockedNameColor = ArcadePalette.Hex(0x8E9893);
        static readonly Color PlaceLabelColor = ArcadePalette.Hex(0x1C3A08);
        static readonly Color CostTextColor = ArcadePalette.Hex(0x3A2600);
        static readonly Color UnaffordableCostPill = ArcadePalette.Hex(0x4A2A28);
        static readonly Color UnaffordableText = ArcadePalette.Hex(0xFFB8AE);
        static readonly Color FadedIcon = new Color(1f, 1f, 1f, 0.45f);

        [Header("Header (always visible)")]
        [Tooltip("Clicking this expands / collapses the card.")]
        [FormerlySerializedAs("button")]
        [SerializeField] Button headerButton;
        [SerializeField] Image icon;
        [SerializeField] Text nameText;
        [SerializeField] Text costText;
        [FormerlySerializedAs("costPill")]
        [SerializeField] Image costPillBackground;
        [SerializeField] Text lifespanText;
        [Tooltip("Shown when the player can't afford this building.")]
        [SerializeField] GameObject lockedOverlay;
        [Tooltip("Arrow that flips when the card opens. Clicking it opens / closes the details.")]
        [SerializeField] RectTransform chevron;
        [Tooltip("Optional. Opens / closes the details. If empty, the chevron is made clickable automatically.")]
        [SerializeField] Button detailsButton;
        [SerializeField] Outline selectedOutline;

        [Header("Collapsed")]
        [SerializeField] Transform chipRow;
        [SerializeField] YieldChip chipTemplate;

        [Header("Expanded")]
        [FormerlySerializedAs("details")]
        [SerializeField] GameObject detailsPanel;
        [SerializeField] Text descriptionText;
        [SerializeField] YieldTile moneyTile;
        [SerializeField] YieldTile ecoTile;
        [SerializeField] YieldTile populationTile;
        [SerializeField] YieldTile happinessTile;
        [FormerlySerializedAs("lifetimeHeader")]
        [SerializeField] Text lifetimeHeaderText;
        [FormerlySerializedAs("lifetimeTotals")]
        [SerializeField] Text lifetimeTotalsText;
        [SerializeField] Text paybackText;
        [SerializeField] Button placeButton;
        [FormerlySerializedAs("placeBackground")]
        [SerializeField] Image placeButtonBackground;
        [FormerlySerializedAs("placeLabel")]
        [SerializeField] Text placeButtonLabel;

        public BuildingDefinition Definition { get; private set; }

        /// <summary>Set when this card shows an upgrade instead of a building.</summary>
        public BuildingUpgrade Upgrade { get; private set; }
        public Button HeaderButton => headerButton;
        public Button PlaceButton => placeButton;
        public Button DetailsButton => detailsButton;
        public bool IsOpen { get; private set; }

        // ---------- Setup ----------

        /// <summary>Fills every text, icon and number on the card from a building definition.</summary>
        public void Setup(BuildingDefinition building, ResourceIcons icons)
        {
            EnsureDetailsButton();
            Definition = building;
            ResourceYield yearly = building.yearlyYield;

            if (icon != null) icon.sprite = building.icon;
            if (nameText != null) nameText.text = building.displayName;
            if (costText != null) costText.text = $"${building.cost:N0}";
            if (lifespanText != null)
                lifespanText.text = building.lifespanYears > 0 ? $"{building.lifespanYears} yr lifespan" : "Lasts forever";

            // Collapsed view: one chip per non-zero yield, up to MaxChips.
            if (chipTemplate != null && icons != null)
            {
                chipTemplate.gameObject.SetActive(false);
                int chipsShown = 0;
                TryAddRatingChip(icons, building.happinessRating, ref chipsShown);
                TryAddChip(icons.money, yearly.money, "", ref chipsShown);
                TryAddChip(icons.eco, yearly.eco, "", ref chipsShown);
                TryAddChip(icons.population, yearly.population, "", ref chipsShown);
            }

            // Expanded view: one labeled tile per resource.
            if (icons != null)
            {
                if (moneyTile != null) moneyTile.Set(icons.money, yearly.money, "$", "", "money");
                if (ecoTile != null) ecoTile.Set(icons.eco, yearly.eco, "", "", "eco");
                if (populationTile != null) populationTile.Set(icons.population, yearly.population, "", "", "people");
                if (happinessTile != null) SetRatingTile(icons, building.happinessRating);
            }

            if (descriptionText != null)
            {
                descriptionText.text = building.description;
                descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(building.description));
            }

            bool livesForever = building.lifespanYears <= 0;
            FillLifetimeSection(
                yearly,
                building.cost,
                livesForever ? YearsShownForPermanentBuildings : building.lifespanYears,
                livesForever ? $"PER {YearsShownForPermanentBuildings} YEARS" : $"OVER ITS {building.lifespanYears}-YEAR LIFESPAN",
                "Costs money every year — built for eco & happiness, not profit",
                livesForever);

            if (placeButtonLabel != null) placeButtonLabel.text = "Place " + building.displayName;
            SetOpen(false);
            SetPicked(false);
        }

        /// <summary>Emoji-only chip for the building's happiness rating.</summary>
        void TryAddRatingChip(ResourceIcons icons, HappinessRating rating, ref int chipsShown, int maxChips = MaxChips)
        {
            if (chipsShown >= maxChips) return;
            Sprite emoji = icons.happinessRatings != null ? icons.happinessRatings.EmojiFor(rating) : null;
            if (emoji == null) emoji = icons.happiness;

            YieldChip chip = Instantiate(chipTemplate, chipRow);
            chip.gameObject.SetActive(true);
            chip.SetIconOnly(emoji);
            chipsShown++;
        }

        /// <summary>Expanded tile: rating emoji + its name ("Love") instead of a number.</summary>
        void SetRatingTile(ResourceIcons icons, HappinessRating rating)
        {
            HappinessRatings table = icons.happinessRatings;
            Sprite emoji = table != null ? table.EmojiFor(rating) : null;
            string label = table != null ? table.LabelFor(rating) : rating.ToString();
            happinessTile.SetText(emoji != null ? emoji : icons.happiness, label, ArcadePalette.Pink, "happiness");
        }

        /// <summary>Adds a chip for a non-zero yield, unless the row is already full.</summary>
        void TryAddChip(Sprite resourceIcon, float perYear, string suffix, ref int chipsShown, int maxChips = MaxChips)
        {
            if (ArcadePalette.IsNearlyZero(perYear) || chipsShown >= maxChips) return;

            YieldChip chip = Instantiate(chipTemplate, chipRow);
            chip.gameObject.SetActive(true);
            chip.Set(resourceIcon, perYear, "", suffix);
            chipsShown++;
        }

        /// <summary>Lifetime totals ("over its 30-year lifespan: +$2,400 ...") and the payback line.</summary>
        /// <param name="yearly">Yield per year to total up.</param>
        /// <param name="cost">One-time price, used for the payback line.</param>
        /// <param name="yearsToTotal">How many years the totals cover; payback must fit inside this.</param>
        /// <param name="header">Small caps line above the totals.</param>
        /// <param name="noMoneyBackText">Payback line used when it never earns money.</param>
        /// <param name="neverExpires">If true, it always pays for itself eventually, however long it takes.</param>
        void FillLifetimeSection(ResourceYield yearly, float cost, int yearsToTotal, string header, string noMoneyBackText, bool neverExpires)
        {
            if (lifetimeHeaderText != null) lifetimeHeaderText.text = header;

            if (lifetimeTotalsText != null)
            {
                var totalParts = new List<string>();
                AddColoredTotal(totalParts, yearly.money * yearsToTotal, "$", "");
                AddColoredTotal(totalParts, yearly.eco * yearsToTotal, "", " eco");
                AddColoredTotal(totalParts, yearly.population * yearsToTotal, "", " people");
                lifetimeTotalsText.text = string.Join("    ", totalParts);
            }

            if (paybackText != null)
            {
                bool earnsMoney = yearly.money > 0f;
                if (earnsMoney)
                {
                    int yearsToPayBack = Mathf.CeilToInt(cost / yearly.money);
                    bool paysOffInTime = neverExpires || yearsToPayBack <= yearsToTotal;

                    paybackText.text = paysOffInTime
                        ? $"Pays for itself in {yearsToPayBack} year{(yearsToPayBack == 1 ? "" : "s")}"
                        : $"Won't earn back its cost ({yearsToPayBack} yrs needed)";
                    paybackText.color = paysOffInTime ? ArcadePalette.Lime : ArcadePalette.Coral;
                }
                else
                {
                    paybackText.text = noMoneyBackText;
                    paybackText.color = ArcadePalette.Amber;
                }
            }
        }

        /// <summary>Adds e.g. "+$2,400" in green (or red if negative). Skips zero totals.</summary>
        static void AddColoredTotal(List<string> totalParts, float total, string prefix, string suffix)
        {
            if (ArcadePalette.IsNearlyZero(total)) return;
            string colorHex = ColorUtility.ToHtmlStringRGB(ArcadePalette.DeltaColor(total));
            totalParts.Add($"<color=#{colorHex}>{ArcadePalette.Signed(total, prefix, suffix)}</color>");
        }

        // ---------- Upgrade cards ----------

        /// <summary>
        /// Fills the card from an upgrade: cost, the yearly change it adds (chips + tiles),
        /// description, and totals over the building's remaining lifespan.
        /// </summary>
        /// <param name="yearsRemaining">Years the building has left (Infinity if it never expires).</param>
        public void SetupUpgrade(BuildingUpgrade upgrade, ResourceIcons icons, float yearsRemaining)
        {
            EnsureDetailsButton();
            Definition = null;
            Upgrade = upgrade;
            ResourceYield change = upgrade.yearlyChange;

            if (icon != null) icon.sprite = upgrade.icon != null ? upgrade.icon : MainResourceIcon(change, icons);
            if (nameText != null) nameText.text = upgrade.displayName;
            if (costText != null) costText.text = $"${upgrade.cost:N0}";
            if (costPillBackground != null) costPillBackground.gameObject.SetActive(true);
            if (lifespanText != null) lifespanText.text = "Upgrade";

            ClearChips();
            if (chipTemplate != null && icons != null)
            {
                chipTemplate.gameObject.SetActive(false);
                int chipsShown = 0;
                TryAddChip(icons.money, change.money, "", ref chipsShown);
                TryAddChip(icons.eco, change.eco, "", ref chipsShown);
                TryAddChip(icons.population, change.population, "", ref chipsShown);
                TryAddChip(icons.happiness, upgrade.happinessRatingChange, "", ref chipsShown);
            }

            if (icons != null)
            {
                if (moneyTile != null) moneyTile.Set(icons.money, change.money, "$", "", "money");
                if (ecoTile != null) ecoTile.Set(icons.eco, change.eco, "", "", "eco");
                if (populationTile != null) populationTile.Set(icons.population, change.population, "", "", "people");
                if (happinessTile != null) happinessTile.Set(icons.happiness, upgrade.happinessRatingChange, "", "", "happiness rating");
            }

            if (descriptionText != null)
            {
                descriptionText.text = upgrade.description;
                descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(upgrade.description));
            }

            bool livesForever = float.IsInfinity(yearsRemaining);
            int yearsLeft = livesForever ? YearsShownForPermanentBuildings : Mathf.Max(0, Mathf.CeilToInt(yearsRemaining));
            FillLifetimeSection(
                change,
                upgrade.cost,
                yearsLeft,
                livesForever ? $"PER {YearsShownForPermanentBuildings} YEARS" : $"OVER THE BUILDING'S LAST {yearsLeft} YEAR{(yearsLeft == 1 ? "" : "S")}",
                change.money < 0f ? "Costs money every year — bought for eco & happiness" : "Doesn't earn money back — bought for eco & happiness",
                livesForever);

            SetOpen(false);
            SetPicked(false);
        }

        /// <summary>Installed / expired / affordable look for an upgrade card.</summary>
        /// <param name="moneyShort">How much more money is needed. Shown on the Buy button.</param>
        public void SetUpgradeState(bool installed, bool buildingExpired, bool affordable, float moneyShort)
        {
            bool canBuy = !installed && !buildingExpired && affordable;
            bool dimmed = !installed && !canBuy;

            if (lockedOverlay != null) lockedOverlay.SetActive(dimmed);
            if (selectedOutline != null) selectedOutline.enabled = installed;
            if (icon != null) icon.color = dimmed ? FadedIcon : Color.white;
            if (nameText != null) nameText.color = dimmed ? LockedNameColor : Color.white;

            if (costPillBackground != null)
                costPillBackground.color = installed ? ArcadePalette.Lime : (canBuy ? ArcadePalette.Amber : UnaffordableCostPill);
            if (costText != null)
            {
                costText.text = installed ? "Owned" : $"${Upgrade.cost:N0}";
                costText.color = installed ? PlaceLabelColor : (canBuy ? CostTextColor : UnaffordableText);
            }
            if (lifespanText != null) lifespanText.text = installed ? "Installed" : "Upgrade";

            if (placeButton != null) placeButton.interactable = canBuy;
            if (placeButtonBackground != null) placeButtonBackground.color = canBuy ? ArcadePalette.Lime : ArcadePalette.Track;
            if (placeButtonLabel != null)
            {
                if (installed) placeButtonLabel.text = "Installed";
                else if (buildingExpired) placeButtonLabel.text = "Building has expired";
                else if (!affordable) placeButtonLabel.text = $"Need ${Mathf.CeilToInt(moneyShort):N0} more";
                else placeButtonLabel.text = $"Buy for ${Upgrade.cost:N0}";
                placeButtonLabel.color = canBuy ? PlaceLabelColor : (installed ? ArcadePalette.TextMuted : UnaffordableText);
            }
        }

        // ---------- Skill tree nodes ----------

        /// <summary>
        /// Skill tree look: same card as the build menu, but it never expands and the cost pill
        /// shows the research cost. Clicking the card selects the node (wired by NodeBehavior).
        /// </summary>
        public void SetupTreeNode(BuildingDefinition building, ResourceIcons icons)
        {
            Setup(building, icons);
            if (chevron != null) chevron.gameObject.SetActive(false);
            if (detailsButton != null) detailsButton.gameObject.SetActive(false);
        }

        /// <summary>Skill tree look for an upgrade node: the upgrade card, never expanding.</summary>
        public void SetupTreeNode(BuildingUpgrade upgrade, ResourceIcons icons)
        {
            SetupUpgrade(upgrade, icons, float.PositiveInfinity);
            if (chevron != null) chevron.gameObject.SetActive(false);
            if (detailsButton != null) detailsButton.gameObject.SetActive(false);
        }

        /// <summary>Relabels the Place button (the skill tree uses it as its Research button).</summary>
        public void SetActionButton(string label, bool interactable)
        {
            if (placeButton != null) placeButton.interactable = interactable;
            if (placeButtonBackground != null) placeButtonBackground.color = interactable ? ArcadePalette.Lime : ArcadePalette.Track;
            if (placeButtonLabel != null)
            {
                placeButtonLabel.text = label;
                placeButtonLabel.color = interactable ? PlaceLabelColor : ArcadePalette.TextMuted;
            }
        }

        /// <summary>Locked / unlocked / purchased / selected look for a skill tree node.</summary>
        /// <param name="canAfford">False turns the cost pill red while the node is still unlocked.</param>
        public void SetTreeNodeState(bool unlocked, bool purchased, bool selected, float researchCost, bool canAfford = true)
        {
            bool dimmed = !unlocked && !purchased;

            if (lockedOverlay != null) lockedOverlay.SetActive(dimmed);
            if (selectedOutline != null) selectedOutline.enabled = selected;
            if (icon != null) icon.color = dimmed ? FadedIcon : Color.white;
            if (nameText != null) nameText.color = dimmed ? LockedNameColor : Color.white;

            if (costPillBackground != null)
                costPillBackground.color = purchased ? ArcadePalette.Lime : (dimmed || !canAfford ? UnaffordableCostPill : ArcadePalette.Amber);
            if (costText != null)
            {
                costText.text = purchased ? "Unlocked" : (dimmed ? "Locked" : $"{researchCost:0} pts");
                costText.color = purchased ? PlaceLabelColor : (dimmed || !canAfford ? UnaffordableText : CostTextColor);
            }
            SetOpen(false);
        }

        // ---------- Selected-building summary ----------

        /// <summary>
        /// Turns the card into a read-only summary of a placed building: icon, name, years left,
        /// upgrades owned and its current yearly yield (with upgrades). No cost, no details, not clickable.
        /// Safe to call repeatedly to refresh.
        /// </summary>
        public void SetupSummary(BuildingYield building, ResourceIcons icons)
        {
            Definition = building.Definition;
            Upgrade = null;

            if (icon != null) { icon.sprite = building.Icon; icon.color = Color.white; }
            if (nameText != null) { nameText.text = building.DisplayName; nameText.color = Color.white; }
            if (costPillBackground != null) costPillBackground.gameObject.SetActive(false);
            if (lockedOverlay != null) lockedOverlay.SetActive(building.IsExpired);
            if (chevron != null) chevron.gameObject.SetActive(false);
            if (headerButton != null) headerButton.enabled = false;
            if (selectedOutline != null) selectedOutline.enabled = false;

            if (lifespanText != null)
            {
                string age;
                if (building.IsExpired) age = "Expired";
                else if (float.IsInfinity(building.YearsRemaining)) age = "Lasts forever";
                else
                {
                    int yearsLeft = Mathf.CeilToInt(building.YearsRemaining);
                    age = $"{yearsLeft} yr{(yearsLeft == 1 ? "" : "s")} left";
                }

                int offered = building.AvailableUpgrades.Count;
                string upgrades = offered == 0
                    ? "No upgrades"
                    : $"{building.InstalledUpgrades.Count}/{offered} upgrades";
                lifespanText.text = $"{age}  ·  {upgrades}";
            }

            ClearChips();
            if (chipTemplate != null && icons != null)
            {
                chipTemplate.gameObject.SetActive(false);
                ResourceYield yearly = building.YearlyYield;
                int chipsShown = 0;
                const int summaryChips = 4; // the summary has room for all four
                TryAddRatingChip(icons, building.HappinessRating, ref chipsShown, summaryChips);
                TryAddChip(icons.money, yearly.money, "", ref chipsShown, summaryChips);
                TryAddChip(icons.eco, yearly.eco, "", ref chipsShown, summaryChips);
                TryAddChip(icons.population, yearly.population, "", ref chipsShown, summaryChips);
            }

            SetOpen(false);
        }

        /// <summary>Removes chips added by a previous Setup call (keeps the hidden template).</summary>
        void ClearChips()
        {
            if (chipRow == null) return;
            for (int i = chipRow.childCount - 1; i >= 0; i--)
            {
                Transform child = chipRow.GetChild(i);
                if (chipTemplate != null && child == chipTemplate.transform) continue;
                Destroy(child.gameObject);
            }
        }

        /// <summary>Icon of whichever resource the change moves the most (for upgrades without their own icon).</summary>
        static Sprite MainResourceIcon(ResourceYield change, ResourceIcons icons)
        {
            if (icons == null) return null;
            Sprite best = icons.money;
            float bestSize = Mathf.Abs(change.money) / 10f; // money numbers run ~10x bigger than the rest
            if (Mathf.Abs(change.eco) > bestSize) { best = icons.eco; bestSize = Mathf.Abs(change.eco); }
            if (Mathf.Abs(change.population) / 5f > bestSize) { best = icons.population; bestSize = Mathf.Abs(change.population) / 5f; }
            if (Mathf.Abs(change.happiness) > bestSize) best = icons.happiness;
            return best;
        }

        /// <summary>
        /// Clicking the card itself now picks the building for placement, so the details need their own
        /// button. Turns the chevron into one, with a comfortable invisible hit area around it.
        /// </summary>
        void EnsureDetailsButton()
        {
            if (detailsButton != null || chevron == null) return;

            var hitArea = new GameObject("Details Hit Area", typeof(RectTransform), typeof(Image));
            var hitRect = (RectTransform)hitArea.transform;
            hitRect.SetParent(chevron, false);
            hitRect.anchorMin = Vector2.zero;
            hitRect.anchorMax = Vector2.one;
            hitRect.sizeDelta = new Vector2(24f, 24f); // a little bigger than the arrow itself
            Image hitImage = hitArea.GetComponent<Image>();
            hitImage.color = new Color(0f, 0f, 0f, 0f); // invisible, but still catches clicks
            hitImage.raycastTarget = true;

            detailsButton = chevron.gameObject.AddComponent<Button>();
            detailsButton.targetGraphic = chevron.GetComponent<Graphic>() != null ? chevron.GetComponent<Graphic>() : hitImage;
            detailsButton.transition = Selectable.Transition.ColorTint;
        }

        /// <summary>Outline on the card the player is currently placing.</summary>
        public void SetPicked(bool picked)
        {
            if (selectedOutline != null) selectedOutline.enabled = picked;
        }

        // ---------- State ----------

        /// <summary>Greys the card out and disables Place when the player can't afford it.</summary>
        /// <param name="moneyShort">How much more money is needed. Shown on the Place button.</param>
        public void SetAffordable(bool affordable, float moneyShort)
        {
            if (lockedOverlay != null) lockedOverlay.SetActive(!affordable);
            if (costPillBackground != null) costPillBackground.color = affordable ? ArcadePalette.Amber : UnaffordableCostPill;
            if (costText != null) costText.color = affordable ? CostTextColor : UnaffordableText;
            if (icon != null) icon.color = affordable ? Color.white : FadedIcon;
            if (nameText != null) nameText.color = affordable ? Color.white : LockedNameColor;

            if (placeButton != null) placeButton.interactable = affordable;
            if (placeButtonBackground != null) placeButtonBackground.color = affordable ? ArcadePalette.Lime : ArcadePalette.Track;
            if (placeButtonLabel != null && Definition != null)
            {
                placeButtonLabel.text = affordable
                    ? "Place " + Definition.displayName
                    : $"Need ${Mathf.CeilToInt(moneyShort):N0} more";
                placeButtonLabel.color = affordable ? PlaceLabelColor : UnaffordableText;
            }
        }

        // Kept for older callers: "selected" now just means expanded.
        public void SetAffordable(bool affordable) => SetAffordable(affordable, 0f);
        public void SetSelected(bool selected) => SetOpen(selected);

        /// <summary>Expands (true) or collapses (false) the card.</summary>
        public void SetOpen(bool open)
        {
            IsOpen = open;
            if (detailsPanel != null) detailsPanel.SetActive(open);
            if (chipRow != null) chipRow.gameObject.SetActive(!open);
            if (chevron != null) chevron.localEulerAngles = new Vector3(0f, 0f, open ? 180f : 0f);
        }
    }
}
