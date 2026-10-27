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
                TryAddChip(icons.money, yearly.money, "", ref chipsShown);
                TryAddChip(icons.eco, yearly.eco, "", ref chipsShown);
                TryAddChip(icons.population, yearly.population, "", ref chipsShown);
                TryAddChip(icons.happiness, yearly.happiness, "%", ref chipsShown);
            }

            // Expanded view: one labeled tile per resource.
            if (icons != null)
            {
                if (moneyTile != null) moneyTile.Set(icons.money, yearly.money, "$", "", "money");
                if (ecoTile != null) ecoTile.Set(icons.eco, yearly.eco, "", "", "eco");
                if (populationTile != null) populationTile.Set(icons.population, yearly.population, "", "", "people");
                if (happinessTile != null) happinessTile.Set(icons.happiness, yearly.happiness, "", "%", "happiness");
            }

            if (descriptionText != null)
            {
                descriptionText.text = building.description;
                descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(building.description));
            }

            FillLifetimeSection(building);

            if (placeButtonLabel != null) placeButtonLabel.text = "Place " + building.displayName;
            SetOpen(false);
            SetPicked(false);
        }

        /// <summary>Adds a chip for a non-zero yield, unless the row is already full.</summary>
        void TryAddChip(Sprite resourceIcon, float perYear, string suffix, ref int chipsShown)
        {
            if (ArcadePalette.IsNearlyZero(perYear) || chipsShown >= MaxChips) return;

            YieldChip chip = Instantiate(chipTemplate, chipRow);
            chip.gameObject.SetActive(true);
            chip.Set(resourceIcon, perYear, "", suffix);
            chipsShown++;
        }

        /// <summary>Lifetime totals ("over its 30-year lifespan: +$2,400 ...") and the payback line.</summary>
        void FillLifetimeSection(BuildingDefinition building)
        {
            ResourceYield yearly = building.yearlyYield;
            int lifespan = building.lifespanYears;
            bool livesForever = lifespan <= 0;

            if (lifetimeHeaderText != null)
                lifetimeHeaderText.text = livesForever
                    ? $"PER {YearsShownForPermanentBuildings} YEARS"
                    : $"OVER ITS {lifespan}-YEAR LIFESPAN";

            int yearsToTotal = livesForever ? YearsShownForPermanentBuildings : lifespan;

            if (lifetimeTotalsText != null)
            {
                var totalParts = new List<string>();
                AddColoredTotal(totalParts, yearly.money * yearsToTotal, "$", "");
                AddColoredTotal(totalParts, yearly.eco * yearsToTotal, "", " eco");
                AddColoredTotal(totalParts, yearly.population * yearsToTotal, "", " people");
                AddColoredTotal(totalParts, yearly.happiness * yearsToTotal, "", "% happy");
                lifetimeTotalsText.text = string.Join("    ", totalParts);
            }

            if (paybackText != null)
            {
                bool earnsMoney = yearly.money > 0f;
                if (earnsMoney)
                {
                    int yearsToPayBack = Mathf.CeilToInt(building.cost / yearly.money);
                    bool paysOffBeforeExpiring = livesForever || yearsToPayBack <= lifespan;

                    paybackText.text = paysOffBeforeExpiring
                        ? $"Pays for itself in {yearsToPayBack} year{(yearsToPayBack == 1 ? "" : "s")}"
                        : $"Won't earn back its cost ({yearsToPayBack} yrs needed)";
                    paybackText.color = paysOffBeforeExpiring ? ArcadePalette.Lime : ArcadePalette.Coral;
                }
                else
                {
                    paybackText.text = "Costs money every year — built for eco & happiness, not profit";
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
