using System;

namespace Evergreen.Systems
{
    /// <summary>A bundle of the simulated resources. Used for yields, deltas and totals.</summary>
    [Serializable]
    public struct ResourceYield
    {
        public float money;
        public float eco;
        public float population;
        [UnityEngine.Tooltip("Percentage points. Happiness is clamped to 0-100 and is not a fail state.")]
        public float happiness;

        public ResourceYield(float money, float eco, float population, float happiness = 0f)
        {
            this.money = money;
            this.eco = eco;
            this.population = population;
            this.happiness = happiness;
        }

        public static ResourceYield Zero => default;

        public static ResourceYield operator +(ResourceYield a, ResourceYield b) =>
            new ResourceYield(a.money + b.money, a.eco + b.eco, a.population + b.population, a.happiness + b.happiness);

        public static ResourceYield operator -(ResourceYield a, ResourceYield b) =>
            new ResourceYield(a.money - b.money, a.eco - b.eco, a.population - b.population, a.happiness - b.happiness);

        public static ResourceYield operator *(ResourceYield a, float s) =>
            new ResourceYield(a.money * s, a.eco * s, a.population * s, a.happiness * s);

        public override string ToString() =>
            $"Money {money:0.##}, Eco {eco:0.##}, Pop {population:0.##}, Happy {happiness:0.##}%";
    }

    /// <summary>
    /// Anything that produces or costs resources over time. Buildings implement this
    /// (see BuildingYield). Yields are per in-game YEAR; the ResourceManager splits
    /// them evenly across the year's ticks.
    /// </summary>
    public interface IYieldSource
    {
        ResourceYield YearlyYield { get; }
    }

    public enum BuildingCategory
    {
        Energy,
        Housing,
        Nature,
        Services
    }

    [Flags]
    public enum GameOverReason
    {
        None = 0,
        Bankrupt = 1 << 0,
        EcoCollapse = 1 << 1,
        Depopulated = 1 << 2
    }
}
