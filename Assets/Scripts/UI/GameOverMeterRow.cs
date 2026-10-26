using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>One resource row on the game-over card. The row that hit zero is highlighted in coral.</summary>
    public class GameOverMeterRow : MonoBehaviour
    {
        static readonly Color FailedTrack = ArcadePalette.Hex(0x2A1E1C);
        static readonly Color FadedIcon = new Color(1f, 1f, 1f, 0.45f);
        static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);

        [Tooltip("Turns reddish when this resource caused the game over.")]
        [SerializeField] Image background;
        [SerializeField] Outline failOutline;
        [SerializeField] Image icon;

        [FormerlySerializedAs("label")]
        [SerializeField] Text labelText;

        [Header("Bar")]
        [Tooltip("Stretched horizontally from 0 to 1.")]
        [FormerlySerializedAs("fill")]
        [SerializeField] RectTransform fillBar;
        [FormerlySerializedAs("fillImage")]
        [SerializeField] Image fillBarImage;
        [Tooltip("The empty part of the bar behind the fill.")]
        [SerializeField] Image track;
        [SerializeField] Color meterColor = Color.white;

        [FormerlySerializedAs("value")]
        [SerializeField] Text valueText;

        /// <param name="formattedValue">The final reading, e.g. "$1,250".</param>
        /// <param name="fillAmount">0-1 bar fill. Ignored (shown empty) if the row failed.</param>
        /// <param name="failed">True if this resource hit zero.</param>
        public void Set(string formattedValue, float fillAmount, bool failed)
        {
            Color textColor = failed ? ArcadePalette.Coral : Color.white;

            if (valueText != null)
            {
                valueText.text = formattedValue;
                valueText.color = textColor;
            }
            if (labelText != null) labelText.color = textColor;

            if (fillBar != null)
            {
                float shownFill = failed ? 0f : Mathf.Clamp01(fillAmount);
                fillBar.anchorMax = new Vector2(shownFill, 1f);
            }
            if (fillBarImage != null) fillBarImage.color = meterColor;
            if (track != null) track.color = failed ? FailedTrack : ArcadePalette.Track;

            if (background != null) background.color = failed ? ArcadePalette.FailBg : Transparent;
            if (failOutline != null) failOutline.enabled = failed;
            if (icon != null) icon.color = failed ? FadedIcon : Color.white;
        }
    }
}
