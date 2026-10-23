using System;
using UnityEngine;

namespace Evergreen.Systems
{
    /// <summary>
    /// Put this on any building. While active (and not expired) it counts toward the
    /// city's totals every tick. Values are per in-game year (negative = cost/upkeep).
    /// If a BuildingDefinition is assigned, its name, icon, yields and lifespan are used.
    /// Needs a Collider2D for the click tooltip to find it.
    /// </summary>
    public class BuildingYield : MonoBehaviour, IYieldSource
    {
        /// <summary>Fires when any building reaches the end of its lifespan.</summary>
        public static event Action<BuildingYield> Expired;

        [SerializeField] BuildingDefinition definition;

        [Header("Used when no definition is assigned")]
        [SerializeField] string displayName = "Building";
        [Tooltip("Per in-game year. Split evenly across the year's ticks. Negative = upkeep / damage.")]
        [SerializeField] ResourceYield yearlyYield;
        [Tooltip("Years before the building expires. 0 = never.")]
        [SerializeField, Min(0)] int lifespanYears;

        [Tooltip("Tinted grey when the building expires. Optional.")]
        [SerializeField] SpriteRenderer tintOnExpire;

        SimulationClock clock;

        public BuildingDefinition Definition => definition;
        public string DisplayName => definition != null ? definition.displayName : displayName;
        public Sprite Icon => definition != null ? definition.icon : null;
        public int LifespanYears => definition != null ? definition.lifespanYears : lifespanYears;
        public int AgeTicks { get; private set; }
        public bool IsExpired { get; private set; }

        public ResourceYield YearlyYield
        {
            get => IsExpired ? ResourceYield.Zero : (definition != null ? definition.yearlyYield : yearlyYield);
            set { definition = null; yearlyYield = value; }
        }

        /// <summary>Remaining lifespan in years (fractional). Infinity if it never expires.</summary>
        public float YearsRemaining
        {
            get
            {
                if (LifespanYears <= 0) return float.PositiveInfinity;
                int perYear = clock != null ? clock.TicksPerYear : 12;
                return Mathf.Max(0f, LifespanYears - AgeTicks / (float)perYear);
            }
        }

        /// <summary>Call from the placement system right after spawning.</summary>
        public void ApplyDefinition(BuildingDefinition def)
        {
            definition = def;
            AgeTicks = 0;
            IsExpired = false;
        }

        void OnEnable()
        {
            if (!IsExpired) ResourceManager.Register(this);
        }

        void OnDisable() => ResourceManager.Unregister(this);

        void Start()
        {
            clock = SimulationClock.Instance != null ? SimulationClock.Instance : FindFirstObjectByType<SimulationClock>();
            if (clock != null) clock.Ticked += OnTick;
        }

        void OnDestroy()
        {
            if (clock != null) clock.Ticked -= OnTick;
        }

        void OnTick(SimulationClock c)
        {
            if (IsExpired || !isActiveAndEnabled || LifespanYears <= 0)
                return;

            AgeTicks++;
            if (AgeTicks >= LifespanYears * c.TicksPerYear)
                Expire();
        }

        void Expire()
        {
            IsExpired = true;
            ResourceManager.Unregister(this);
            if (tintOnExpire != null) tintOnExpire.color = new Color(0.45f, 0.45f, 0.45f);
            Expired?.Invoke(this);
        }
    }
}
