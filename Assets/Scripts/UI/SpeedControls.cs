using Evergreen.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>Pause / 1x / 2x / 3x buttons. Highlights whichever speed is active.</summary>
    public class SpeedControls : MonoBehaviour
    {
        [SerializeField] SimulationClock clock;

        [Header("Buttons")]
        [SerializeField] Button pauseButton;
        [FormerlySerializedAs("speed1Button")]
        [SerializeField] Button normalSpeedButton;
        [FormerlySerializedAs("speed2Button")]
        [SerializeField] Button fastSpeedButton;
        [FormerlySerializedAs("speed3Button")]
        [SerializeField] Button fastestSpeedButton;

        [Header("Colors")]
        [SerializeField] Color activeColor = new Color(0.608f, 0.882f, 0.365f);
        [SerializeField] Color activeLabelColor = new Color(0.11f, 0.227f, 0.031f);
        [SerializeField] Color inactiveColor = new Color(0.22f, 0.25f, 0.24f);
        [SerializeField] Color inactiveLabelColor = Color.white;

        // ---------- Unity lifecycle ----------

        void Awake()
        {
            if (clock == null) clock = FindFirstObjectByType<SimulationClock>();
        }

        // Listen for speed changes from anywhere (hotkeys too), not just these buttons.
        void OnEnable()
        {
            if (clock != null) clock.SpeedChanged += HighlightActiveSpeed;
        }

        void OnDisable()
        {
            if (clock != null) clock.SpeedChanged -= HighlightActiveSpeed;
        }

        void Start()
        {
            WireButton(pauseButton, GameSpeed.Paused);
            WireButton(normalSpeedButton, GameSpeed.Normal);
            WireButton(fastSpeedButton, GameSpeed.Fast);
            WireButton(fastestSpeedButton, GameSpeed.Fastest);
            if (clock != null) HighlightActiveSpeed(clock.Speed);
        }

        // ---------- Buttons ----------

        void WireButton(Button button, GameSpeed speed)
        {
            if (button == null) return;

            button.onClick.AddListener(() =>
            {
                if (clock == null) return;

                // The pause button toggles, so pressing it again resumes.
                if (speed == GameSpeed.Paused) clock.TogglePause();
                else clock.SetSpeed(speed);

                // Deselect so Space (pause hotkey) doesn't also "click" this button.
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            });
        }

        void HighlightActiveSpeed(GameSpeed activeSpeed)
        {
            TintButton(pauseButton, activeSpeed == GameSpeed.Paused);
            TintButton(normalSpeedButton, activeSpeed == GameSpeed.Normal);
            TintButton(fastSpeedButton, activeSpeed == GameSpeed.Fast);
            TintButton(fastestSpeedButton, activeSpeed == GameSpeed.Fastest);
        }

        void TintButton(Button button, bool isActive)
        {
            if (button == null) return;
            if (button.targetGraphic != null) button.targetGraphic.color = isActive ? activeColor : inactiveColor;

            Text label = button.GetComponentInChildren<Text>();
            if (label != null) label.color = isActive ? activeLabelColor : inactiveLabelColor;
        }
    }
}
