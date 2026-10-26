using System;
using UnityEngine;

namespace Evergreen.Systems
{
    /// <summary>How fast simulation time runs. The number is the time multiplier.</summary>
    public enum GameSpeed
    {
        Paused = 0,
        Normal = 1,
        Fast = 2,
        Fastest = 3
    }

    /// <summary>
    /// The single source of simulation time. Everything that changes over in-game time
    /// (resource yields, building aging, the 20-year win) listens to this clock's events
    /// instead of running its own timer.
    ///
    /// Time moves in "ticks". By default there are 12 ticks per year (one per month)
    /// and a year lasts 5 real minutes at 1x speed.
    ///
    /// Pause/speed only affect the simulation. Time.timeScale is never touched, so UI
    /// animation and camera movement keep working while the city is paused.
    /// </summary>
    [DefaultExecutionOrder(-100)] // Runs before everything else so listeners see a fresh tick.
    public class SimulationClock : MonoBehaviour
    {
        public static SimulationClock Instance { get; private set; }

        /// <summary>
        /// Most ticks processed in a single frame. After a long hitch (e.g. a breakpoint)
        /// this stops the clock from firing hundreds of ticks at once to "catch up".
        /// </summary>
        const int MaxTicksPerFrame = 8;

        [Header("Timing")]
        [Tooltip("Real-time seconds per in-game year at 1x speed. 300 = 5 minutes.")]
        [SerializeField, Min(1f)] float secondsPerYear = 300f;

        [Tooltip("Simulation ticks per in-game year. 12 = one tick per month (25 s at 1x).")]
        [SerializeField, Min(1)] int ticksPerYear = 12;

        [Tooltip("The year number shown when the game starts.")]
        [SerializeField] int startYear = 1;

        [SerializeField] GameSpeed startingSpeed = GameSpeed.Normal;

        // ---------- Events ----------

        /// <summary>Fires once per tick. Resource yields are applied here.</summary>
        public event Action<SimulationClock> Ticked;

        /// <summary>
        /// Fires after the last tick of each in-game year has been processed.
        /// Argument = number of full years elapsed (1, 2, 3...). Hook the 20-year win
        /// and yearly checks here. Doesn't fire if that tick caused a game over.
        /// </summary>
        public event Action<int> YearCompleted;

        /// <summary>Fires whenever the speed changes, including pause and resume.</summary>
        public event Action<GameSpeed> SpeedChanged;

        /// <summary>Fires once when the simulation is permanently stopped (game over).</summary>
        public event Action<string> Halted;

        // ---------- State ----------

        public GameSpeed Speed { get; private set; }
        public bool IsPaused => Speed == GameSpeed.Paused;

        /// <summary>True after game over. A halted clock can't be resumed.</summary>
        public bool IsHalted { get; private set; }
        public string HaltReason { get; private set; }

        /// <summary>Ticks since the game started.</summary>
        public int TotalTicks { get; private set; }
        public int TicksPerYear => ticksPerYear;
        public float SecondsPerYear => secondsPerYear;
        public float SecondsPerTick => secondsPerYear / ticksPerYear;

        /// <summary>Full in-game years completed so far (0 during the first year).</summary>
        public int YearsElapsed => TotalTicks / ticksPerYear;

        /// <summary>The year currently being played (startYear during the first year).</summary>
        public int CurrentYear => startYear + YearsElapsed;

        /// <summary>0-based tick within the current year. With 12 ticks/year this is the month index.</summary>
        public int TickOfYear => TotalTicks % ticksPerYear;

        /// <summary>0..1 progress through the current year, smooth between ticks.</summary>
        public float YearProgress
        {
            get
            {
                float fractionOfCurrentTick = secondsSinceLastTick / SecondsPerTick;
                return Mathf.Clamp01((TickOfYear + fractionOfCurrentTick) / ticksPerYear);
            }
        }

        /// <summary>Real seconds (already multiplied by speed) accumulated toward the next tick.</summary>
        float secondsSinceLastTick;

        /// <summary>What Resume() goes back to.</summary>
        GameSpeed speedBeforePause = GameSpeed.Normal;

        // ---------- Unity lifecycle ----------

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"Second SimulationClock on '{name}' ignored. Only one is allowed.", this);
                Destroy(this);
                return;
            }

            Instance = this;
            Speed = startingSpeed;
            if (startingSpeed != GameSpeed.Paused)
                speedBeforePause = startingSpeed;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (IsHalted || IsPaused)
                return;

            float speedMultiplier = (int)Speed;
            secondsSinceLastTick += Time.deltaTime * speedMultiplier;

            float secondsPerTick = SecondsPerTick;
            int ticksThisFrame = 0;
            while (secondsSinceLastTick >= secondsPerTick && !IsHalted && ticksThisFrame < MaxTicksPerFrame)
            {
                secondsSinceLastTick -= secondsPerTick;
                AdvanceTick();
                ticksThisFrame++;
            }
        }

        void AdvanceTick()
        {
            TotalTicks++;
            Ticked?.Invoke(this);

            bool yearJustEnded = TotalTicks % ticksPerYear == 0;
            if (!IsHalted && yearJustEnded)
                YearCompleted?.Invoke(YearsElapsed);
        }

        // ---------- Controls (call these from UI buttons / hotkeys) ----------

        public void SetSpeed(GameSpeed newSpeed)
        {
            if (IsHalted || newSpeed == Speed)
                return;

            if (newSpeed != GameSpeed.Paused)
                speedBeforePause = newSpeed;

            Speed = newSpeed;
            SpeedChanged?.Invoke(Speed);
        }

        /// <summary>Int overload so it can be wired from a Button's OnClick in the inspector (0-3).</summary>
        public void SetSpeed(int speedIndex) => SetSpeed((GameSpeed)Mathf.Clamp(speedIndex, 0, 3));

        public void Pause() => SetSpeed(GameSpeed.Paused);

        /// <summary>Unpauses at whatever speed was running before the pause.</summary>
        public void Resume() => SetSpeed(speedBeforePause);

        public void TogglePause()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        /// <summary>Stops the simulation for good (game over). Speed controls stop working.</summary>
        public void Halt(string reason)
        {
            if (IsHalted)
                return;

            Speed = GameSpeed.Paused;
            IsHalted = true;
            HaltReason = reason;
            SpeedChanged?.Invoke(Speed);
            Halted?.Invoke(reason);
        }

        // ---------- Debug (right-click the component in the Inspector) ----------

        [ContextMenu("Debug: Advance One Tick")]
        public void DebugAdvanceTick()
        {
            if (!IsHalted) AdvanceTick();
        }

        [ContextMenu("Debug: Advance One Year")]
        public void DebugAdvanceYear()
        {
            for (int tick = 0; tick < ticksPerYear && !IsHalted; tick++)
                AdvanceTick();
        }
    }
}
