using Evergreen.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>
    /// Top row of the Arcade HUD: the date badge plus the four resource meters.
    /// Also sets up the hover tooltips that name each meter.
    /// </summary>
    public class SimulationHUD : MonoBehaviour
    {
        static readonly string[] MonthNames =
            { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };

        /// <summary>How often (seconds) the numbers are redrawn. The year bar still moves every frame.</summary>
        const float RefreshInterval = 0.25f;

        [SerializeField] SimulationClock clock;
        [SerializeField] ResourceManager resources;

        [Header("Date")]
        [SerializeField] Text yearText;
        [SerializeField] Text goalText;
        [SerializeField] Text monthText;
        [SerializeField] RectTransform yearProgressFill;
        [Tooltip("Shown as 'YEAR 7 / 20'. Win logic lives elsewhere.")]
        [SerializeField] int goalYears = 20;

        [Header("Meters")]
        [SerializeField] MeterWidget money;
        [SerializeField] MeterWidget eco;
        [SerializeField] MeterWidget population;
        [SerializeField] MeterWidget happiness;

        [Header("Hover tooltips")]
        [Tooltip("Show the resource name when the mouse is over a meter.")]
        [SerializeField] bool showTooltips = true;

        float secondsUntilRefresh;

        // ---------- Unity lifecycle ----------

        void Awake()
        {
            if (clock == null) clock = FindFirstObjectByType<SimulationClock>();
            if (resources == null) resources = FindFirstObjectByType<ResourceManager>();
            if (showTooltips) SetupTooltips();
        }

        void Start() => Refresh();

        void Update()
        {
            // The year bar moves smoothly every frame.
            if (clock != null && yearProgressFill != null)
                yearProgressFill.anchorMax = new Vector2(clock.YearProgress, 1f);

            // Numbers only need redrawing a few times a second. Unscaled so it works while paused.
            secondsUntilRefresh -= Time.unscaledDeltaTime;
            if (secondsUntilRefresh <= 0f)
            {
                secondsUntilRefresh = RefreshInterval;
                Refresh();
            }
        }

        // ---------- Drawing ----------

        public void Refresh()
        {
            RefreshDate();
            RefreshMeters();
        }

        void RefreshDate()
        {
            if (clock == null) return;

            if (yearText != null) yearText.text = $"YEAR {clock.CurrentYear}";
            if (goalText != null) goalText.text = $"/ {goalYears}";
            if (monthText != null)
            {
                // With 12 ticks a year each tick is a month; otherwise show "3/8"-style progress.
                bool ticksAreMonths = clock.TicksPerYear == 12;
                monthText.text = ticksAreMonths
                    ? MonthNames[clock.TickOfYear]
                    : $"{clock.TickOfYear + 1}/{clock.TicksPerYear}";
            }
        }

        void RefreshMeters()
        {
            if (resources == null) return;
            ResourceYield yearlyNet = resources.GetYearlyNet();

            if (money != null)
            {
                money.SetValue($"${Mathf.FloorToInt(resources.Money):N0}");
                money.SetDelta(yearlyNet.money);
            }
            if (eco != null)
            {
                eco.SetValue($"{Mathf.FloorToInt(resources.Eco):N0}");
                eco.SetDelta(yearlyNet.eco);
            }
            if (population != null)
            {
                population.SetValue($"{Mathf.FloorToInt(resources.Population):N0}");
                population.SetDelta(yearlyNet.population);
            }
            if (happiness != null)
            {
                happiness.SetValue($"{Mathf.RoundToInt(resources.Happiness)}%");
                happiness.SetDelta(yearlyNet.happiness, "", "%");
                happiness.SetFill(resources.Happiness / 100f);
            }
        }

        // ---------- Tooltips ----------

        /// <summary>Creates one shared tooltip and adds a hover trigger to each meter.</summary>
        void SetupTooltips()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            // Reuse the HUD's own font so the tooltip matches the meters.
            Text anyHudText = GetComponentInChildren<Text>(true);
            Font hudFont = anyHudText != null ? anyHudText.font : null;
            ResourceTooltip tooltip = ResourceTooltip.Create(canvas, hudFont);

            AddHoverTooltip(money, HudResource.Money, "Money", tooltip);
            AddHoverTooltip(eco, HudResource.Eco, "Eco", tooltip);
            AddHoverTooltip(population, HudResource.Population, "Population", tooltip);
            AddHoverTooltip(happiness, HudResource.Happiness, "Happiness", tooltip);
        }

        static void AddHoverTooltip(MeterWidget meter, HudResource resource, string title, ResourceTooltip tooltip)
        {
            if (meter == null) return;

            var hoverTarget = meter.GetComponent<ResourceHoverTarget>();
            if (hoverTarget == null) hoverTarget = meter.gameObject.AddComponent<ResourceHoverTarget>();

            hoverTarget.tooltip = tooltip;
            hoverTarget.resource = resource;
            hoverTarget.title = title;
        }
    }
}
