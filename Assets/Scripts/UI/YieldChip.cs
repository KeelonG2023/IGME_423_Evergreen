using UnityEngine;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>Small pill on a collapsed build card: resource icon + signed per-year amount.</summary>
    public class YieldChip : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] Text label;

        /// <summary>Just an icon (e.g. a happiness emoji), no number.</summary>
        public void SetIconOnly(Sprite sprite)
        {
            if (icon != null) icon.sprite = sprite;
            if (label != null) label.gameObject.SetActive(false);
        }

        public void Set(Sprite resourceIcon, float perYear, string prefix = "", string suffix = "")
        {
            if (icon != null) icon.sprite = resourceIcon;
            if (label != null)
            {
                label.text = ArcadePalette.Signed(perYear, prefix, suffix);
                label.color = ArcadePalette.DeltaColor(perYear);
            }
        }
    }
}
