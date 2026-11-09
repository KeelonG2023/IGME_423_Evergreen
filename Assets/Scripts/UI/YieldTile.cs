using UnityEngine;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>Labeled yield box on an expanded build card: icon, big signed value, small label.</summary>
    public class YieldTile : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] Text valueText;
        [SerializeField] Text labelText;

        /// <summary>Icon with a word instead of a number (e.g. heart emoji + "Love").</summary>
        public void SetText(Sprite sprite, string value, Color valueColor, string label)
        {
            if (icon != null) icon.sprite = sprite;
            if (valueText != null)
            {
                valueText.text = value;
                valueText.color = valueColor;
            }
            if (labelText != null) labelText.text = label;
        }

        public void Set(Sprite resourceIcon, float perYear, string prefix, string suffix, string label)
        {
            if (icon != null) icon.sprite = resourceIcon;
            if (valueText != null)
            {
                valueText.text = ArcadePalette.Signed(perYear, prefix, suffix);
                valueText.color = ArcadePalette.DeltaColor(perYear);
            }
            if (labelText != null) labelText.text = label;
        }
    }
}
