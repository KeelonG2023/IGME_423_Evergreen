using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Evergreen.Systems
{
    /// <summary>
    /// Put this on any building. While active (and not expired) it counts toward the
    /// city's totals every tick. Values are per in-game year (negative = cost/upkeep).
    /// If a BuildingDefinition is assigned, its name, icon, yields and lifespan are used;
    /// otherwise the fallback fields below are used.
    /// Needs a Collider2D so BuildingClickSelector can find it when clicked.
    /// </summary>
    public class BuildingYield : MonoBehaviour, IYieldSource
    {
        /// <summary>Fires when any building reaches the end of its lifespan.</summary>
        public static event Action<BuildingYield> Expired;

        /// <summary>Months per year to assume if no SimulationClock can be found.</summary>
        const int FallbackTicksPerYear = 12;

        static readonly Color ExpiredTint = new Color(0.45f, 0.45f, 0.45f);

        [SerializeField] BuildingDefinition definition;

        [Header("Used when no definition is assigned")]
        [SerializeField] string displayName = "Building";

        [Tooltip("Per in-game year. Split evenly across the year's ticks. Negative = upkeep / damage.")]
        [SerializeField] ResourceYield yearlyYield;

        [Tooltip("Years before the building expires. 0 = never.")]
        [SerializeField, Min(0)] int lifespanYears;

        [Tooltip("Optional. This sprite turns grey when the building expires.")]
        [FormerlySerializedAs("tintOnExpire")]
        [SerializeField] SpriteRenderer spriteToGreyOutOnExpire;

        SimulationClock clock;

        // ---------- Read-only info (definition wins over the fallback fields) ----------

        public BuildingDefinition Definition => definition;
        public string DisplayName => definition != null ? definition.displayName : displayName;
        public Sprite Icon => definition != null ? definition.icon : null;
        public int LifespanYears => definition != null ? definition.lifespanYears : lifespanYears;

        /// <summary>How many ticks this building has existed for.</summary>
        public int AgeTicks { get; private set; }

        /// <summary>True once the lifespan has run out. Expired buildings yield nothing.</summary>
        public bool IsExpired { get; private set; }

        public ResourceYield YearlyYield
        {
            get
            {
                if (IsExpired) return ResourceYield.Zero;
                return definition != null ? definition.yearlyYield : yearlyYield;
            }
            // Setting a custom yield detaches the definition so the custom value is actually used.
            set
            {
                definition = null;
                yearlyYield = value;
            }
        }

        /// <summary>Remaining lifespan in years (fractional). Infinity if it never expires.</summary>
        public float YearsRemaining
        {
            get
            {
                if (LifespanYears <= 0) return float.PositiveInfinity;

                int ticksPerYear = clock != null ? clock.TicksPerYear : FallbackTicksPerYear;
                float yearsLived = AgeTicks / (float)ticksPerYear;
                return Mathf.Max(0f, LifespanYears - yearsLived);
            }
        }

        /// <summary>Call from the placement system right after spawning.</summary>
        public void ApplyDefinition(BuildingDefinition newDefinition)
        {
            definition = newDefinition;
            AgeTicks = 0;
            IsExpired = false;
        }

        // ---------- Unity lifecycle ----------

        void OnEnable()
        {
            if (!IsExpired) ResourceManager.Register(this);
        }

        void OnDisable() => ResourceManager.Unregister(this);

        void Start()
        {
            clock = SimulationClock.Instance != null
                ? SimulationClock.Instance
                : FindFirstObjectByType<SimulationClock>();

            if (clock != null) clock.Ticked += OnTick;
        }

        void OnDestroy()
        {
            if (clock != null) clock.Ticked -= OnTick;
        }

        // ---------- Aging ----------

        void OnTick(SimulationClock tickingClock)
        {
            bool livesForever = LifespanYears <= 0;
            if (IsExpired || !isActiveAndEnabled || livesForever)
                return;

            AgeTicks++;
            int lifespanTicks = LifespanYears * tickingClock.TicksPerYear;
            if (AgeTicks >= lifespanTicks)
                Expire();
        }

        void Expire()
        {
            IsExpired = true;
            ResourceManager.Unregister(this);
            if (spriteToGreyOutOnExpire != null) spriteToGreyOutOnExpire.color = ExpiredTint;
            Expired?.Invoke(this);
        }
    }
}
