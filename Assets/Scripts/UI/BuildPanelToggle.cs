using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>
    /// Slides the build panel off the right side of the screen (the tab stays visible)
    /// when the collapse button is clicked, and back again on the next click.
    /// The button and its arrow live in the scene (child "Collapse Button" of the panel).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BuildPanelToggle : MonoBehaviour
    {
        const float SlideDurationSeconds = 0.2f;

        // Arrows point the way the panel will move when clicked.
        const string CollapseArrow = ">";
        const string ExpandArrow = "<";

        [Tooltip("The tab on the panel's left edge that toggles the panel.")]
        [SerializeField] Button collapseButton;
        [Tooltip("Arrow label on the button; flips direction with the panel state.")]
        [SerializeField] Text arrowText;

        public bool IsCollapsed { get; private set; }

        RectTransform panel;

        /// <summary>The panel's x position when open (as placed in the scene).</summary>
        float openX;

        /// <summary>
        /// The panel's x position when collapsed: shifted right by its on-screen width, so the panel
        /// sits just off the right edge and only the button stays visible.
        /// </summary>
        float collapsedX;

        /// <summary>0 = fully open, 1 = fully collapsed. Animates toward the target.</summary>
        float slideAmount;

        // ---------- Unity lifecycle ----------

        void Awake()
        {
            panel = (RectTransform)transform;
            openX = panel.anchoredPosition.x;

            // The panel is scaled down in the scene (about 0.76), so its real width is rect width * scale.
            // Using rect.width alone slid it too far and pushed the button off screen too.
            float onScreenWidth = panel.rect.width * Mathf.Abs(panel.localScale.x);
            collapsedX = openX + onScreenWidth;

            if (collapseButton != null) collapseButton.onClick.AddListener(OnButtonClicked);
            UpdateArrow();
        }

        void Update()
        {
            float targetSlide = IsCollapsed ? 1f : 0f;
            if (Mathf.Approximately(slideAmount, targetSlide)) return;

            // Unscaled so the panel still slides while the game is paused.
            slideAmount = Mathf.MoveTowards(slideAmount, targetSlide, Time.unscaledDeltaTime / SlideDurationSeconds);

            float easedSlide = Mathf.SmoothStep(0f, 1f, slideAmount);
            Vector2 position = panel.anchoredPosition;
            position.x = Mathf.Lerp(openX, collapsedX, easedSlide);
            panel.anchoredPosition = position;
        }

        // ---------- Public API ----------

        public void Toggle() => SetCollapsed(!IsCollapsed);

        public void SetCollapsed(bool collapsed)
        {
            IsCollapsed = collapsed;
            UpdateArrow();
        }

        // ---------- Button ----------

        void OnButtonClicked()
        {
            Toggle();

            // Deselect so Space (pause hotkey) doesn't also "click" this button.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        void UpdateArrow()
        {
            if (arrowText != null) arrowText.text = IsCollapsed ? ExpandArrow : CollapseArrow;
        }
    }
}
