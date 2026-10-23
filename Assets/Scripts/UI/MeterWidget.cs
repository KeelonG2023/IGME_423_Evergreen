using UnityEngine;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>One resource pill in the top bar: big value, per-year delta, optional fill bar.</summary>
    public class MeterWidget : MonoBehaviour
    {
        [SerializeField] Text valueText;
        [SerializeField] Text deltaText;
        [Tooltip("Optional. Stretched horizontally from 0 to 1.")]
        [SerializeField] RectTransform fill;

        public void SetValue(string value)
        {
            if (valueText != null) valueText.text = value;
        }

        public void SetDelta(float perYear, string prefix = "", string suffix = "")
        {
            if (deltaText == null) return;
            deltaText.text = ArcadePalette.Signed(perYear, prefix, suffix) + " / yr";
            deltaText.color = ArcadePalette.DeltaColor(perYear);
        }

        public void SetFill(float t)
        {
            if (fill != null) fill.anchorMax = new Vector2(Mathf.Clamp01(t), 1f);
        }
    }
}
