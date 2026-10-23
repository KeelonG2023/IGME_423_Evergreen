using System;
using System.Collections.Generic;
using UnityEngine;

namespace Evergreen.Systems
{
    /// <summary>
    /// Owns money, eco score, population and happiness. Every clock tick it adds up the yields of
    /// every registered IYieldSource and applies them. If money, eco or population
    /// reaches zero, it halts the clock and raises GameOver.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        // Static so buildings can register even if they enable before the manager exists.
        static readonly List<IYieldSource> sources = new List<IYieldSource>();
        public static IReadOnlyList<IYieldSource> Sources => sources;

        public static void Register(IYieldSource source)
        {
            if (source != null && !sources.Contains(source))
                sources.Add(source);
        }

        public static void Unregister(IYieldSource source) => sources.Remove(source);

        // Clear between play sessions when domain reload is disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => sources.Clear();

        [SerializeField] SimulationClock clock;

        [Header("Starting values")]
        [SerializeField] float startingMoney = 1000f;
        [SerializeField] float startingEco = 50f;
        [SerializeField] float startingPopulation = 100f;
        [SerializeField, Range(0f, 100f)] float startingHappiness = 60f;

        public float Money { get; private set; }
        public float Eco { get; private set; }
        public float Population { get; private set; }

        /// <summary>0-100. Not a fail state.</summary>
        public float Happiness { get; private set; }

        public ResourceYield Current => new ResourceYield(Money, Eco, Population, Happiness);

        /// <summary>What the most recent tick added (yearly net / ticks per year).</summary>
        public ResourceYield LastTickDelta { get; private set; }

        public bool IsGameOver { get; private set; }
        public GameOverReason GameOverReason { get; private set; }

        /// <summary>Fires whenever any resource value changes.</summary>
        public event Action<ResourceManager> ResourcesChanged;

        /// <summary>Fires once, when money, eco or population hits zero.</summary>
        public event Action<GameOverReason> GameOver;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"Second ResourceManager on '{name}' ignored. Only one is allowed.", this);
                Destroy(this);
                return;
            }

            Instance = this;
            if (clock == null)
                clock = FindFirstObjectByType<SimulationClock>();
            if (clock == null)
                Debug.LogError("ResourceManager needs a SimulationClock in the scene.", this);

            Money = startingMoney;
            Eco = startingEco;
            Population = startingPopulation;
            Happiness = startingHappiness;
        }

        void OnEnable()
        {
            if (clock != null) clock.Ticked += OnTick;
        }

        void OnDisable()
        {
            if (clock != null) clock.Ticked -= OnTick;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start() => ResourcesChanged?.Invoke(this);

        /// <summary>Sum of every building's yearly yield, i.e. the city's projected net per year.</summary>
        public ResourceYield GetYearlyNet()
        {
            ResourceYield total = ResourceYield.Zero;
            for (int i = sources.Count - 1; i >= 0; i--)
            {
                IYieldSource source = sources[i];
                if (source == null || (source is UnityEngine.Object obj && obj == null))
                {
                    sources.RemoveAt(i); // destroyed without unregistering
                    continue;
                }
                total += source.YearlyYield;
            }
            return total;
        }

        void OnTick(SimulationClock c)
        {
            if (IsGameOver)
                return;

            ResourceYield delta = GetYearlyNet() * (1f / c.TicksPerYear);
            LastTickDelta = delta;
            Apply(delta);
        }

        void Apply(ResourceYield delta)
        {
            Money += delta.money;
            Eco += delta.eco;
            Population += delta.population;
            Happiness = Mathf.Clamp(Happiness + delta.happiness, 0f, 100f);
            ResourcesChanged?.Invoke(this);
            CheckForGameOver();
        }

        // ---------- API for other systems ----------

        /// <summary>One-off gain or loss (events, disasters, demolition refunds). Can trigger game over.</summary>
        public void AddResources(ResourceYield delta)
        {
            if (!IsGameOver)
                Apply(delta);
        }

        /// <summary>
        /// True if the player can pay this and still have money left. Spending all
        /// your money is blocked so that building something can't instantly end the game.
        /// </summary>
        public bool CanAfford(float cost) => !IsGameOver && cost >= 0f && Money - cost > 0f;

        /// <summary>Use for building placement costs. Returns false (and changes nothing) if unaffordable.</summary>
        public bool TrySpend(float cost)
        {
            if (!CanAfford(cost))
                return false;

            Money -= cost;
            ResourcesChanged?.Invoke(this);
            return true;
        }

        void CheckForGameOver()
        {
            GameOverReason reason = GameOverReason.None;
            if (Money <= 0f) reason |= GameOverReason.Bankrupt;
            if (Eco <= 0f) reason |= GameOverReason.EcoCollapse;
            if (Population <= 0f) reason |= GameOverReason.Depopulated;

            if (reason == GameOverReason.None)
                return;

            Money = Mathf.Max(0f, Money);
            Eco = Mathf.Max(0f, Eco);
            Population = Mathf.Max(0f, Population);

            IsGameOver = true;
            GameOverReason = reason;

            if (clock != null)
                clock.Halt(reason.ToString());

            ResourcesChanged?.Invoke(this);
            GameOver?.Invoke(reason);
        }
    }
}
