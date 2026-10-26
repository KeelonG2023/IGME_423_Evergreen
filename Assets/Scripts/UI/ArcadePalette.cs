using UnityEngine;

namespace Evergreen.UI
{
    /// <summary>Colors and number formatting shared by the Arcade HUD.</summary>
    public static class ArcadePalette
    {
        // Backgrounds, darkest to lightest.
        public static readonly Color ShadowCol = Hex(0x0F1312);
        public static readonly Color Panel = Hex(0x1E2422);
        public static readonly Color Card = Hex(0x2B3330);
        public static readonly Color Track = Hex(0x38403D); // Empty part of a progress bar.
        public static readonly Color FailBg = Hex(0x3A2826); // Reddish card behind a failed meter.

        public static readonly Color TextMuted = Hex(0xC9D2CE);

        // Accents.
        public static readonly Color Lime = Hex(0x9BE15D);  // Gains, eco.
        public static readonly Color Amber = Hex(0xFFB547); // Money, warnings.
        public static readonly Color Sky = Hex(0x5CC8FF);   // Population.
        public static readonly Color Pink = Hex(0xFF7AA8);  // Happiness.
        public static readonly Color Coral = Hex(0xFF8C7A); // Losses, danger.

        /// <summary>Anything closer to zero than this is treated as zero (avoids "+0" from float noise).</summary>
        const float ZeroThreshold = 0.0001f;

        /// <summary>Builds a color from a web-style hex number, e.g. Hex(0x9BE15D).</summary>
        public static Color Hex(int rgb, float alpha = 1f)
        {
            float red = ((rgb >> 16) & 0xFF) / 255f;
            float green = ((rgb >> 8) & 0xFF) / 255f;
            float blue = (rgb & 0xFF) / 255f;
            return new Color(red, green, blue, alpha);
        }

        public static bool IsNearlyZero(float value) => Mathf.Abs(value) < ZeroThreshold;

        /// <summary>Green for gains, coral for losses, muted for zero.</summary>
        public static Color DeltaColor(float value)
        {
            if (value > ZeroThreshold) return Lime;
            if (value < -ZeroThreshold) return Coral;
            return TextMuted;
        }

        /// <summary>"+80", "−20", "0" with an optional prefix/suffix ("$", "%"). Rounds to a whole number.</summary>
        public static string Signed(float value, string prefix = "", string suffix = "")
        {
            int rounded = Mathf.RoundToInt(value);
            string sign = rounded > 0 ? "+" : rounded < 0 ? "−" : ""; // Real minus sign, not a hyphen.
            return $"{sign}{prefix}{Mathf.Abs(rounded):N0}{suffix}";
        }
    }
}
