using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>One resource pill in the top bar: big value, per-year change, optional fill bar.</summary>
    public class MeterWidget : MonoBehaviour
    {
        [SerializeField] Text valueText;

        [Tooltip("Shows the per-year change, e.g. \"+80 / yr\".")]
        [FormerlySerializedAs("deltaText")]
        [SerializeField] Text perYearChangeText;

        [Tooltip("Optional. Stretched horizontally from 0 to 1.")]
        [FormerlySerializedAs("fill")]
        [SerializeField] RectTransform fillBar;

        public void SetValue(string formattedValue)
        {
            if (valueText != null) valueText.text = formattedValue;
        }

        /// <summary>Shows a signed per-year change, green if positive and coral if negative.</summary>
        public void SetDelta(float perYear, string prefix = "", string suffix = "")
        {
            if (perYearChangeText == null) return;
            perYearChangeText.text = ArcadePalette.Signed(perYear, prefix, suffix) + " / yr";
            perYearChangeText.color = ArcadePalette.DeltaColor(perYear);
        }

        /// <summary>Fills the bar from 0 (empty) to 1 (full).</summary>
        public void SetFill(float fillAmount)
        {
            if (fillBar != null) fillBar.anchorMax = new Vector2(Mathf.Clamp01(fillAmount), 1f);
        }
    }
}
