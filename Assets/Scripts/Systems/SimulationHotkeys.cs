using UnityEngine;
using UnityEngine.InputSystem;

namespace Evergreen.Systems
{
    /// <summary>Keyboard shortcuts for the clock: Space = pause/resume, 1/2/3 = speed.</summary>
    public class SimulationHotkeys : MonoBehaviour
    {
        [SerializeField] SimulationClock clock;

        void Awake()
        {
            if (clock == null)
                clock = FindFirstObjectByType<SimulationClock>();
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || clock == null)
                return;

            if (keyboard.spaceKey.wasPressedThisFrame)
                clock.TogglePause();

            // Number row and numpad both work.
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                clock.SetSpeed(GameSpeed.Normal);
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                clock.SetSpeed(GameSpeed.Fast);
            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                clock.SetSpeed(GameSpeed.Fastest);
        }
    }
}
