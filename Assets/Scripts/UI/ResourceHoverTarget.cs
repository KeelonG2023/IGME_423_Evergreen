using UnityEngine;
using UnityEngine.EventSystems;

namespace Evergreen.UI
{
    /// <summary>
    /// Shows the ResourceTooltip while the mouse is over this meter.
    /// SimulationHUD adds this automatically, so it doesn't need to be placed by hand.
    /// The meter needs a raycast-target Image (its background) for hover to register.
    /// </summary>
    public class ResourceHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public ResourceTooltip tooltip;
        public HudResource resource;

        [Tooltip("Text shown in the tooltip, e.g. \"Money\".")]
        public string title;

        RectTransform MeterRect => (RectTransform)transform;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tooltip != null) tooltip.Show(MeterRect, resource, title);
        }

        public void OnPointerExit(PointerEventData eventData) => HideTooltip();

        // If the meter gets hidden mid-hover, don't leave the tooltip stranded on screen.
        void OnDisable() => HideTooltip();

        void HideTooltip()
        {
            if (tooltip != null) tooltip.Hide(MeterRect);
        }
    }
}
