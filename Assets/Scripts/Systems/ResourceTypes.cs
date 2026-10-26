using System;

namespace Evergreen.Systems
{
    /// <summary>
    /// A bundle of the four simulated resources. The same struct is used for three jobs:
    /// a building's yearly yield, a one-off change (like a disaster), and the city's totals.
    /// </summary>
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

        /// <summary>All four resources set to zero.</summary>
        public static ResourceYield Zero => default;

        // Math operators so yields can be added up and scaled like plain numbers,
        // e.g. "yearlyNet * (1f / ticksPerYear)".

        public static ResourceYield operator +(ResourceYield left, ResourceYield right) =>
            new ResourceYield(
                left.money + right.money,
                left.eco + right.eco,
                left.population + right.population,
                left.happiness + right.happiness);

        public static ResourceYield operator -(ResourceYield left, ResourceYield right) =>
            new ResourceYield(
                left.money - right.money,
                left.eco - right.eco,
                left.population - right.population,
                left.happiness - right.happiness);

        public static ResourceYield operator *(ResourceYield yield, float scale) =>
            new ResourceYield(
                yield.money * scale,
                yield.eco * scale,
                yield.population * scale,
                yield.happiness * scale);

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

    /// <summary>Build menu tabs. Every BuildingDefinition belongs to exactly one.</summary>
    public enum BuildingCategory
    {
        Energy,
        Housing,
        Nature,
        Services
    }

    /// <summary>
    /// Why the game ended. It's a set of flags because more than one resource
    /// can hit zero on the same tick (e.g. Bankrupt | EcoCollapse).
    /// </summary>
    [Flags]
    public enum GameOverReason
    {
        None = 0,
        Bankrupt = 1 << 0,
        EcoCollapse = 1 << 1,
        Depopulated = 1 << 2
    }
}
