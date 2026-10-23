using System;
using UnityEngine;

namespace Evergreen.Systems
{
    public enum GameSpeed
    {
        Paused = 0,
        Normal = 1,
        Fast = 2,
        Fastest = 3
    }

    /// <summary>
    /// The single source of simulation time. Everything that changes over in-game time
    /// (resource yields, quota checks, the 20-year win) listens to this clock's events
    /// instead of running its own timer.
    ///
    /// Pause/speed only affect the simulation. Time.timeScale is never touched, so UI
    /// animation and camera movement keep working while the city is paused.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SimulationClock : MonoBehaviour
    {
        public static SimulationClock Instance { get; private set; }

        [Header("Timing")]
        [Tooltip("Real-time seconds per in-game year at 1x speed. 300 = 5 minutes.")]
        [SerializeField, Min(1f)] float secondsPerYear = 300f;

        [Tooltip("Simulation ticks per in-game year. 12 = one tick per month (25 s at 1x).")]
        [SerializeField, Min(1)] int ticksPerYear = 12;

        [Tooltip("The year number shown when the game starts.")]
        [SerializeField] int startYear = 1;

        [SerializeField] GameSpeed startingSpeed = GameSpeed.Normal;

        /// <summary>Fires once per tick. Resource yields are applied here.</summary>
        public event Action<SimulationClock> Ticked;

        /// <summary>
        /// Fires after the last tick of each in-game year has been processed.
        /// Argument = number of full years elapsed (1, 2, 3...). Hook the 20-year win
        /// and the quota checks here. Doesn't fire if that tick caused a game over.
        /// </summary>
        public event Action<int> YearCompleted;

        public event Action<GameSpeed> SpeedChanged;

        /// <summary>Fires once when the simulation is permanently stopped (game over).</summary>
        public event Action<string> Halted;

        public GameSpeed Speed { get; private set; }
        public bool IsPaused => Speed == GameSpeed.Paused;
        public bool IsHalted { get; private set; }
        public string HaltReason { get; private set; }

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
        public float YearProgress =>
            Mathf.Clamp01((TickOfYear + tickTimer / SecondsPerTick) / ticksPerYear);

        float tickTimer;
        GameSpeed speedBeforePause = GameSpeed.Normal;

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

            tickTimer += Time.deltaTime * (int)Speed;

            float secondsPerTick = SecondsPerTick;
            int safety = 8; // don't spiral after a long frame hitch
            while (tickTimer >= secondsPerTick && !IsHalted && safety-- > 0)
            {
                tickTimer -= secondsPerTick;
                AdvanceTick();
            }
        }

        void AdvanceTick()
        {
            TotalTicks++;
            Ticked?.Invoke(this);

            if (!IsHalted && TotalTicks % ticksPerYear == 0)
                YearCompleted?.Invoke(YearsElapsed);
        }

        // ---------- Controls (call these from UI buttons / hotkeys) ----------

        public void SetSpeed(GameSpeed speed)
        {
            if (IsHalted || speed == Speed)
                return;

            if (speed != GameSpeed.Paused)
                speedBeforePause = speed;

            Speed = speed;
            SpeedChanged?.Invoke(Speed);
        }

        /// <summary>Int overload so it can be wired from a Button's OnClick in the inspector (0-3).</summary>
        public void SetSpeed(int speed) => SetSpeed((GameSpeed)Mathf.Clamp(speed, 0, 3));

        public void Pause() => SetSpeed(GameSpeed.Paused);
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

        [ContextMenu("Debug: Advance One Tick")]
        public void DebugAdvanceTick()
        {
            if (!IsHalted) AdvanceTick();
        }

        [ContextMenu("Debug: Advance One Year")]
        public void DebugAdvanceYear()
        {
            for (int i = 0; i < ticksPerYear && !IsHalted; i++)
                AdvanceTick();
        }
    }
}
