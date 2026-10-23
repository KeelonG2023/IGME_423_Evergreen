using System.Collections.Generic;
using Evergreen.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>
    /// One structure in the build menu. Collapsed: icon, name, lifespan, cost and yield chips.
    /// Expanded: description, labeled yields, lifetime totals, payback and a Place button.
    /// Cloned from a template by BuildMenu.
    /// </summary>
    public class BuildCard : MonoBehaviour
    {
        [Header("Header (always visible)")]
        [Tooltip("Clicking this expands / collapses the card.")]
        [SerializeField] Button button;
        [SerializeField] Image icon;
        [SerializeField] Text nameText;
        [SerializeField] Text costText;
        [SerializeField] Image costPill;
        [SerializeField] Text lifespanText;
        [SerializeField] GameObject lockedOverlay;
        [SerializeField] RectTransform chevron;
        [SerializeField] Outline selectedOutline;

        [Header("Collapsed")]
        [SerializeField] Transform chipRow;
        [SerializeField] YieldChip chipTemplate;

        [Header("Expanded")]
        [SerializeField] GameObject details;
        [SerializeField] Text descriptionText;
        [SerializeField] YieldTile moneyTile;
        [SerializeField] YieldTile ecoTile;
        [SerializeField] YieldTile populationTile;
        [SerializeField] YieldTile happinessTile;
        [SerializeField] Text lifetimeHeader;
        [SerializeField] Text lifetimeTotals;
        [SerializeField] Text paybackText;
        [SerializeField] Button placeButton;
        [SerializeField] Image placeBackground;
        [SerializeField] Text placeLabel;

        static readonly Color LockedName = ArcadePalette.Hex(0x8E9893);
        static readonly Color PlaceText = ArcadePalette.Hex(0x1C3A08);

        public BuildingDefinition Definition { get; private set; }
        public Button Button => button;
        public Button PlaceButton => placeButton;
        public bool IsOpen { get; private set; }

        public void Setup(BuildingDefinition def, ResourceIcons icons)
        {
            Definition = def;
            ResourceYield y = def.yearlyYield;

            if (icon != null) icon.sprite = def.icon;
            if (nameText != null) nameText.text = def.displayName;
            if (costText != null) costText.text = $"${def.cost:N0}";
            if (lifespanText != null)
                lifespanText.text = def.lifespanYears > 0 ? $"{def.lifespanYears} yr lifespan" : "Lasts forever";

            // Collapsed chips: up to three non-zero yields.
            if (chipTemplate != null && icons != null)
            {
                chipTemplate.gameObject.SetActive(false);
                int added = 0;
                added += AddChip(icons.money, y.money, "", added);
                added += AddChip(icons.eco, y.eco, "", added);
                added += AddChip(icons.population, y.population, "", added);
                AddChip(icons.happiness, y.happiness, "%", added);
            }

            // Expanded details
            if (icons != null)
            {
                if (moneyTile != null) moneyTile.Set(icons.money, y.money, "$", "", "money");
                if (ecoTile != null) ecoTile.Set(icons.eco, y.eco, "", "", "eco");
                if (populationTile != null) populationTile.Set(icons.population, y.population, "", "", "people");
                if (happinessTile != null) happinessTile.Set(icons.happiness, y.happiness, "", "%", "happiness");
            }
            if (descriptionText != null)
            {
                descriptionText.text = def.description;
                descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(def.description));
            }
            FillLifetime(def);

            if (placeLabel != null) placeLabel.text = "Place " + def.displayName;
            SetOpen(false);
        }

        int AddChip(Sprite sprite, float value, string suffix, int alreadyAdded)
        {
            if (Mathf.Abs(value) < 0.0001f || alreadyAdded >= 3) return 0;
            YieldChip chip = Instantiate(chipTemplate, chipRow);
            chip.gameObject.SetActive(true);
            chip.Set(sprite, value, "", suffix);
            return 1;
        }

        void FillLifetime(BuildingDefinition def)
        {
            ResourceYield y = def.yearlyYield;
            int life = def.lifespanYears;

            if (lifetimeHeader != null)
                lifetimeHeader.text = life > 0 ? $"OVER ITS {life}-YEAR LIFESPAN" : "PER 10 YEARS";
            int span = life > 0 ? life : 10;

            if (lifetimeTotals != null)
            {
                var parts = new List<string>();
                AddTotal(parts, y.money * span, "$", "");
                AddTotal(parts, y.eco * span, "", " eco");
                AddTotal(parts, y.population * span, "", " people");
                AddTotal(parts, y.happiness * span, "", "% happy");
                lifetimeTotals.text = string.Join("    ", parts);
            }

            if (paybackText != null)
            {
                if (y.money > 0f)
                {
                    int years = Mathf.CeilToInt(def.cost / y.money);
                    bool paysOff = life <= 0 || years <= life;
                    paybackText.text = paysOff
                        ? $"Pays for itself in {years} year{(years == 1 ? "" : "s")}"
                        : $"Won't earn back its cost ({years} yrs needed)";
                    paybackText.color = paysOff ? ArcadePalette.Lime : ArcadePalette.Coral;
                }
                else
                {
                    paybackText.text = "Costs money every year — built for eco & happiness, not profit";
                    paybackText.color = ArcadePalette.Amber;
                }
            }
        }

        static void AddTotal(List<string> parts, float v, string prefix, string suffix)
        {
            if (Mathf.Abs(v) < 0.0001f) return;
            string hex = ColorUtility.ToHtmlStringRGB(ArcadePalette.DeltaColor(v));
            parts.Add($"<color=#{hex}>{ArcadePalette.Signed(v, prefix, suffix)}</color>");
        }

        public void SetAffordable(bool affordable, float shortfall)
        {
            if (lockedOverlay != null) lockedOverlay.SetActive(!affordable);
            if (costPill != null) costPill.color = affordable ? ArcadePalette.Amber : ArcadePalette.Hex(0x4A2A28);
            if (costText != null) costText.color = affordable ? ArcadePalette.Hex(0x3A2600) : ArcadePalette.Hex(0xFFB8AE);
            if (icon != null) icon.color = affordable ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            if (nameText != null) nameText.color = affordable ? Color.white : LockedName;

            if (placeButton != null) placeButton.interactable = affordable;
            if (placeBackground != null) placeBackground.color = affordable ? ArcadePalette.Lime : ArcadePalette.Track;
            if (placeLabel != null && Definition != null)
            {
                placeLabel.text = affordable ? "Place " + Definition.displayName : $"Need ${Mathf.CeilToInt(shortfall):N0} more";
                placeLabel.color = affordable ? PlaceText : ArcadePalette.Hex(0xFFB8AE);
            }
        }

        // Kept for older callers: "selected" now just means expanded.
        public void SetAffordable(bool affordable) => SetAffordable(affordable, 0f);
        public void SetSelected(bool selected) => SetOpen(selected);

        public void SetOpen(bool open)
        {
            IsOpen = open;
            if (details != null) details.SetActive(open);
            if (chipRow != null) chipRow.gameObject.SetActive(!open);
            if (selectedOutline != null) selectedOutline.enabled = open;
            if (chevron != null) chevron.localEulerAngles = new Vector3(0f, 0f, open ? 180f : 0f);
        }
    }
}
