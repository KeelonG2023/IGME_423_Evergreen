using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>
    /// Adds a small tab on the build panel's left edge. Clicking it slides the panel
    /// off the right side of the screen (the tab stays visible) and clicking again
    /// slides it back. Builds its own button in code; BuildMenu adds this at startup.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BuildPanelToggle : MonoBehaviour
    {
        // Sizes are in canvas pixels (the HUD canvas scales from 1920x1080).
        const float ButtonWidth = 40f;
        const float ButtonHeight = 88f;

        /// <summary>How far down from the panel's top edge the button sits.</summary>
        const float ButtonTopOffset = 16f;

        const int ArrowFontSize = 30;
        const float SlideDurationSeconds = 0.2f;

        // Arrows point the way the panel will move when clicked.
        const string CollapseArrow = ">";
        const string ExpandArrow = "<";

        public bool IsCollapsed { get; private set; }

        RectTransform panel;
        Text arrowText;

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

            BuildButton();
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

        // ---------- Building the button ----------

        void BuildButton()
        {
            var buttonObject = new GameObject("Collapse Button",
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(panel, false);

            // The panel has a layout group; this keeps the button out of it.
            buttonObject.GetComponent<LayoutElement>().ignoreLayout = true;

            // Anchored to the panel's top-left corner with a right-side pivot,
            // so the button hangs just outside the panel's left edge.
            var buttonRect = (RectTransform)buttonObject.transform;
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(1f, 1f);
            buttonRect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            buttonRect.anchoredPosition = new Vector2(0f, -ButtonTopOffset);

            // Match the panel's background so the tab looks attached to it.
            Image panelBackground = GetComponent<Image>();
            var buttonBackground = buttonObject.GetComponent<Image>();
            buttonBackground.color = panelBackground != null ? panelBackground.color : ArcadePalette.Panel;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = buttonBackground;
            button.onClick.AddListener(OnButtonClicked);

            arrowText = BuildArrowText(buttonRect);
        }

        Text BuildArrowText(RectTransform buttonRect)
        {
            var arrowObject = new GameObject("Arrow", typeof(RectTransform), typeof(Text));
            arrowObject.transform.SetParent(buttonRect, false);

            // Stretch to fill the button.
            var arrowRect = (RectTransform)arrowObject.transform;
            arrowRect.anchorMin = Vector2.zero;
            arrowRect.anchorMax = Vector2.one;
            arrowRect.offsetMin = Vector2.zero;
            arrowRect.offsetMax = Vector2.zero;

            // Reuse the panel's own font so the arrow matches the rest of the menu.
            Text anyPanelText = GetComponentInChildren<Text>(true);
            Font font = anyPanelText != null ? anyPanelText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var arrow = arrowObject.GetComponent<Text>();
            arrow.font = font;
            arrow.fontSize = ArrowFontSize;
            arrow.fontStyle = FontStyle.Bold;
            arrow.alignment = TextAnchor.MiddleCenter;
            arrow.color = ArcadePalette.Lime;
            arrow.raycastTarget = false;
            return arrow;
        }

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
