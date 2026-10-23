using UnityEngine;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>One resource row on the game-over card. The row that hit zero is highlighted.</summary>
    public class GameOverMeterRow : MonoBehaviour
    {
        [SerializeField] Image background;
        [SerializeField] Outline failOutline;
        [SerializeField] Image icon;
        [SerializeField] Text label;
        [SerializeField] RectTransform fill;
        [SerializeField] Image fillImage;
        [SerializeField] Image track;
        [SerializeField] Text value;
        [SerializeField] Color meterColor = Color.white;

        public void Set(string valueText, float fill01, bool failed)
        {
            if (value != null)
            {
                value.text = valueText;
                value.color = failed ? ArcadePalette.Coral : Color.white;
            }
            if (label != null) label.color = failed ? ArcadePalette.Coral : Color.white;
            if (fill != null) fill.anchorMax = new Vector2(failed ? 0f : Mathf.Clamp01(fill01), 1f);
            if (fillImage != null) fillImage.color = meterColor;
            if (background != null) background.color = failed ? ArcadePalette.FailBg : new Color(0f, 0f, 0f, 0f);
            if (failOutline != null) failOutline.enabled = failed;
            if (icon != null) icon.color = failed ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
            if (track != null) track.color = failed ? ArcadePalette.Hex(0x2A1E1C) : ArcadePalette.Track;
        }
    }
}
