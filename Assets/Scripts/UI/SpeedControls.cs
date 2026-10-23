using Evergreen.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>Pause / 1x / 2x / 3x buttons. Highlights whichever speed is active.</summary>
    public class SpeedControls : MonoBehaviour
    {
        [SerializeField] SimulationClock clock;
        [SerializeField] Button pauseButton;
        [SerializeField] Button speed1Button;
        [SerializeField] Button speed2Button;
        [SerializeField] Button speed3Button;
        [SerializeField] Color activeColor = new Color(0.608f, 0.882f, 0.365f);
        [SerializeField] Color activeLabelColor = new Color(0.11f, 0.227f, 0.031f);
        [SerializeField] Color inactiveColor = new Color(0.22f, 0.25f, 0.24f);
        [SerializeField] Color inactiveLabelColor = Color.white;

        void Awake()
        {
            if (clock == null) clock = FindFirstObjectByType<SimulationClock>();
        }

        void OnEnable()
        {
            if (clock != null) clock.SpeedChanged += Highlight;
        }

        void OnDisable()
        {
            if (clock != null) clock.SpeedChanged -= Highlight;
        }

        void Start()
        {
            Wire(pauseButton, GameSpeed.Paused);
            Wire(speed1Button, GameSpeed.Normal);
            Wire(speed2Button, GameSpeed.Fast);
            Wire(speed3Button, GameSpeed.Fastest);
            if (clock != null) Highlight(clock.Speed);
        }

        void Wire(Button b, GameSpeed speed)
        {
            if (b == null) return;
            b.onClick.AddListener(() =>
            {
                if (clock == null) return;
                if (speed == GameSpeed.Paused) clock.TogglePause();
                else clock.SetSpeed(speed);
                // Deselect so Space (pause hotkey) doesn't also "click" this button.
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            });
        }

        void Highlight(GameSpeed s)
        {
            Tint(pauseButton, s == GameSpeed.Paused);
            Tint(speed1Button, s == GameSpeed.Normal);
            Tint(speed2Button, s == GameSpeed.Fast);
            Tint(speed3Button, s == GameSpeed.Fastest);
        }

        void Tint(Button b, bool active)
        {
            if (b == null) return;
            if (b.targetGraphic != null) b.targetGraphic.color = active ? activeColor : inactiveColor;
            Text label = b.GetComponentInChildren<Text>();
            if (label != null) label.color = active ? activeLabelColor : inactiveLabelColor;
        }
    }
}
