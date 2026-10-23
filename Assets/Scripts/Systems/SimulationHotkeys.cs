using UnityEngine;
using UnityEngine.InputSystem;

namespace Evergreen.Systems
{
    /// <summary>Space = pause/resume, 1/2/3 = speed.</summary>
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
            Keyboard kb = Keyboard.current;
            if (kb == null || clock == null)
                return;

            if (kb.spaceKey.wasPressedThisFrame) clock.TogglePause();
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) clock.SetSpeed(GameSpeed.Normal);
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) clock.SetSpeed(GameSpeed.Fast);
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) clock.SetSpeed(GameSpeed.Fastest);
        }
    }
}
