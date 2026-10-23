using UnityEngine;

namespace Evergreen.UI
{
    /// <summary>Colors and number formatting shared by the Arcade HUD.</summary>
    public static class ArcadePalette
    {
        public static readonly Color Panel = Hex(0x1E2422);
        public static readonly Color Card = Hex(0x2B3330);
        public static readonly Color Track = Hex(0x38403D);
        public static readonly Color ShadowCol = Hex(0x0F1312);
        public static readonly Color TextMuted = Hex(0xC9D2CE);

        public static readonly Color Lime = Hex(0x9BE15D);
        public static readonly Color Amber = Hex(0xFFB547);
        public static readonly Color Sky = Hex(0x5CC8FF);
        public static readonly Color Pink = Hex(0xFF7AA8);
        public static readonly Color Coral = Hex(0xFF8C7A);
        public static readonly Color FailBg = Hex(0x3A2826);

        public static Color Hex(int rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

        /// <summary>Green for gains, coral for losses, muted for zero.</summary>
        public static Color DeltaColor(float v) =>
            v > 0.0001f ? Lime : v < -0.0001f ? Coral : TextMuted;

        /// <summary>"+80", "−20", "0" with an optional prefix/suffix ("$", "%").</summary>
        public static string Signed(float v, string prefix = "", string suffix = "")
        {
            int n = Mathf.RoundToInt(v);
            string sign = n > 0 ? "+" : n < 0 ? "−" : "";
            return $"{sign}{prefix}{Mathf.Abs(n):N0}{suffix}";
        }
    }
}
