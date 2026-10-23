using UnityEngine;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>Small pill: resource icon + signed per-year amount.</summary>
    public class YieldChip : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] Text label;

        public void Set(Sprite sprite, float perYear, string prefix = "", string suffix = "")
        {
            if (icon != null) icon.sprite = sprite;
            if (label != null)
            {
                label.text = ArcadePalette.Signed(perYear, prefix, suffix);
                label.color = ArcadePalette.DeltaColor(perYear);
            }
        }
    }
}
