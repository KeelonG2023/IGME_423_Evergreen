using UnityEngine;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>The four meters in the top bar. Used to pick the tooltip's accent color.</summary>
    public enum HudResource { Money, Eco, Population, Happiness }

    /// <summary>
    /// Small hover label for the top-bar resource meters ("Money", "Eco", ...).
    /// Builds its own UI in code, so it needs no scene setup: SimulationHUD creates one
    /// and puts a ResourceHoverTarget on each meter.
    /// </summary>
    public class ResourceTooltip : MonoBehaviour
    {
        // Sizes are in canvas pixels (the HUD canvas scales from 1920x1080).
        const float PanelWidth = 180f;
        const float AccentStripHeight = 5f;
        const int TitleFontSize = 24;

        /// <summary>Space between the bottom of the meter and the top of the tooltip.</summary>
        const float GapBelowMeter = 10f;

        /// <summary>Closest the tooltip may get to the screen edge.</summary>
        const float ScreenMargin = 16f;

        Canvas canvas;
        RectTransform canvasRect;
        RectTransform panel;
        Image accentStrip;
        Text titleText;

        /// <summary>The meter currently being hovered, or null when hidden.</summary>
        RectTransform hoveredMeter;

        // Reused every frame so positioning doesn't allocate.
        readonly Vector3[] meterCorners = new Vector3[4];

        // ---------- Creation ----------

        /// <summary>Builds a hidden tooltip on the given canvas. Falls back to Unity's built-in font if none is given.</summary>
        public static ResourceTooltip Create(Canvas canvas, Font font)
        {
            Canvas rootCanvas = canvas.rootCanvas;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var tooltipObject = new GameObject("ResourceTooltip", typeof(RectTransform));
            tooltipObject.transform.SetParent(rootCanvas.transform, false);

            var tooltip = tooltipObject.AddComponent<ResourceTooltip>();
            tooltip.BuildUi(rootCanvas, font);
            return tooltip;
        }

        void BuildUi(Canvas rootCanvas, Font font)
        {
            canvas = rootCanvas;
            canvasRect = (RectTransform)rootCanvas.transform;

            panel = (RectTransform)transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 1f); // Top-center, so it hangs below the meter.
            panel.sizeDelta = new Vector2(PanelWidth, 0f);

            BuildBackground();
            BuildLayout();
            BuildAccentStrip();
            titleText = BuildTitle(font);

            gameObject.SetActive(false);
        }

        void BuildBackground()
        {
            var background = gameObject.AddComponent<Image>();
            background.color = ArcadePalette.Panel;
            background.raycastTarget = false;

            var dropShadow = gameObject.AddComponent<Shadow>();
            dropShadow.effectColor = ArcadePalette.ShadowCol;
            dropShadow.effectDistance = new Vector2(0f, -5f);

            // Never block the mouse, or hovering the tooltip itself would make it flicker closed.
            var canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        /// <summary>Padding around the title, and auto-height so the panel fits its text.</summary>
        void BuildLayout()
        {
            var layout = gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(left: 14, right: 14, top: 15, bottom: 10);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var sizeFitter = gameObject.AddComponent<ContentSizeFitter>();
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>Colored strip across the full top edge, outside the layout padding.</summary>
        void BuildAccentStrip()
        {
            var stripObject = new GameObject("Accent", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            stripObject.transform.SetParent(panel, false);
            stripObject.GetComponent<LayoutElement>().ignoreLayout = true;

            var stripRect = (RectTransform)stripObject.transform;
            stripRect.anchorMin = new Vector2(0f, 1f);
            stripRect.anchorMax = new Vector2(1f, 1f);
            stripRect.pivot = new Vector2(0.5f, 1f);
            stripRect.sizeDelta = new Vector2(0f, AccentStripHeight);
            stripRect.anchoredPosition = Vector2.zero;

            accentStrip = stripObject.GetComponent<Image>();
            accentStrip.raycastTarget = false;
        }

        Text BuildTitle(Font font)
        {
            var titleObject = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleObject.transform.SetParent(panel, false);

            var title = titleObject.GetComponent<Text>();
            title.font = font;
            title.fontSize = TitleFontSize;
            title.fontStyle = FontStyle.Bold;
            title.color = Color.white;
            title.alignment = TextAnchor.MiddleCenter;
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Overflow;
            title.raycastTarget = false;
            return title;
        }

        // ---------- Public API ----------

        public void Show(RectTransform meter, HudResource resource, string title)
        {
            if (meter == null) return;
            hoveredMeter = meter;

            titleText.text = title;
            accentStrip.color = AccentColorFor(resource);

            gameObject.SetActive(true);
            panel.SetAsLastSibling(); // Draw on top of the rest of the HUD.
            PositionBelowMeter();
        }

        /// <summary>
        /// Hides the tooltip, but only if it's currently showing this meter. That way a late
        /// "exit" from the old meter can't hide the tooltip that just opened for a new one.
        /// </summary>
        public void Hide(RectTransform meter)
        {
            if (meter != hoveredMeter) return;
            hoveredMeter = null;
            gameObject.SetActive(false);
        }

        // ---------- Every frame ----------

        void LateUpdate()
        {
            // The meter was destroyed or hidden while hovered.
            if (hoveredMeter == null || !hoveredMeter.gameObject.activeInHierarchy)
            {
                hoveredMeter = null;
                gameObject.SetActive(false);
                return;
            }

            // Re-position each frame in case the HUD layout or screen size changes.
            PositionBelowMeter();
        }

        static Color AccentColorFor(HudResource resource) => resource switch
        {
            HudResource.Money => ArcadePalette.Amber,
            HudResource.Eco => ArcadePalette.Lime,
            HudResource.Population => ArcadePalette.Sky,
            _ => ArcadePalette.Pink
        };

        /// <summary>Centers the tooltip under the hovered meter, kept inside the screen.</summary>
        void PositionBelowMeter()
        {
            // Make sure the panel's height reflects the current text before clamping.
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

            // Corners go bottom-left, top-left, top-right, bottom-right.
            hoveredMeter.GetWorldCorners(meterCorners);
            Vector3 meterBottomCenter = (meterCorners[0] + meterCorners[3]) * 0.5f;

            // Overlay canvases don't use a camera for UI conversion.
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 meterBottomScreen = RectTransformUtility.WorldToScreenPoint(uiCamera, meterBottomCenter);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, meterBottomScreen, uiCamera, out Vector2 meterBottomOnCanvas))
                return;

            Vector2 tooltipPosition = meterBottomOnCanvas - new Vector2(0f, GapBelowMeter);

            // Keep it on screen (the pivot is top-center).
            Vector2 halfCanvasSize = canvasRect.rect.size * 0.5f;
            Vector2 panelSize = panel.rect.size;
            tooltipPosition.x = Mathf.Clamp(tooltipPosition.x,
                -halfCanvasSize.x + panelSize.x * 0.5f + ScreenMargin,
                halfCanvasSize.x - panelSize.x * 0.5f - ScreenMargin);
            tooltipPosition.y = Mathf.Max(tooltipPosition.y, -halfCanvasSize.y + panelSize.y + ScreenMargin);

            panel.anchoredPosition = tooltipPosition;
        }
    }
}
