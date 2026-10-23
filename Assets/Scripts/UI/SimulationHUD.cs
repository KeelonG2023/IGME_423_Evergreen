using Evergreen.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>Top row of the Arcade HUD: date badge plus the four resource meters.</summary>
    public class SimulationHUD : MonoBehaviour
    {
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

        static readonly string[] Months =
            { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };

        const float RefreshInterval = 0.25f;
        float refreshTimer;

        void Awake()
        {
            if (clock == null) clock = FindFirstObjectByType<SimulationClock>();
            if (resources == null) resources = FindFirstObjectByType<ResourceManager>();
        }

        void Start() => Refresh();

        void Update()
        {
            if (clock != null && yearProgressFill != null)
                yearProgressFill.anchorMax = new Vector2(clock.YearProgress, 1f);

            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = RefreshInterval;
                Refresh();
            }
        }

        public void Refresh()
        {
            if (clock != null)
            {
                if (yearText != null) yearText.text = $"YEAR {clock.CurrentYear}";
                if (goalText != null) goalText.text = $"/ {goalYears}";
                if (monthText != null)
                    monthText.text = clock.TicksPerYear == 12
                        ? Months[clock.TickOfYear]
                        : $"{clock.TickOfYear + 1}/{clock.TicksPerYear}";
            }

            if (resources == null) return;
            ResourceYield net = resources.GetYearlyNet();

            if (money != null)
            {
                money.SetValue($"${Mathf.FloorToInt(resources.Money):N0}");
                money.SetDelta(net.money);
            }
            if (eco != null)
            {
                eco.SetValue($"{Mathf.FloorToInt(resources.Eco):N0}");
                eco.SetDelta(net.eco);
            }
            if (population != null)
            {
                population.SetValue($"{Mathf.FloorToInt(resources.Population):N0}");
                population.SetDelta(net.population);
            }
            if (happiness != null)
            {
                happiness.SetValue($"{Mathf.RoundToInt(resources.Happiness)}%");
                happiness.SetDelta(net.happiness, "", "%");
                happiness.SetFill(resources.Happiness / 100f);
            }
        }
    }
}
