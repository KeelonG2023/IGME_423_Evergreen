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

        public void Set(Sprite sprite, float perYear, string prefix, string suffix, string label)
        {
            if (icon != null) icon.sprite = sprite;
            if (valueText != null)
            {
                valueText.text = ArcadePalette.Signed(perYear, prefix, suffix);
                valueText.color = ArcadePalette.DeltaColor(perYear);
            }
            if (labelText != null) labelText.text = label;
        }
    }
}
