using System;
using UnityEngine;

namespace Evergreen.Systems
{
    /// <summary>How much people like a building, 1 (vomit) to 5 (heart). Decides its happiness pop-ups.</summary>
    public enum HappinessRating
    {
        Vomit = 1,
        Disgusted = 2,
        HappyLittle = 3,
        Happy = 4,
        Love = 5
    }

    /// <summary>
    /// The tuning table for the five happiness ratings: emoji shown on cards, how often a building of that
    /// rating spawns a pop-up, and how many points clicking it gives. Happiness is only earned through pop-ups.
    /// Create via Assets > Create > Evergreen > Happiness Ratings.
    /// </summary>
    [CreateAssetMenu(menuName = "Evergreen/Happiness Ratings", fileName = "HappinessRatings")]
    public class HappinessRatings : ScriptableObject
    {
        [Serializable]
        public class Level
        {
            public string label;
            public Sprite emoji;

            [Tooltip("Chance (0-100) per tick that a building with this rating spawns a pop-up.")]
            [Range(0f, 100f)] public float popUpChancePercent;

            [Tooltip("Happiness points for clicking that pop-up.")]
            [Min(0f)] public float pointsPerClick;
        }

        [Tooltip("Exactly five entries, in order: Vomit, Disgusted, Happy Little, Happy, Love.")]
        [SerializeField] Level[] levels =
        {
            new Level { label = "Vomit",        popUpChancePercent = 0f,  pointsPerClick = 0f },
            new Level { label = "Disgusted",    popUpChancePercent = 5f,  pointsPerClick = 1f },
            new Level { label = "Happy Little", popUpChancePercent = 10f, pointsPerClick = 2f },
            new Level { label = "Happy",        popUpChancePercent = 15f, pointsPerClick = 3f },
            new Level { label = "Love",         popUpChancePercent = 25f, pointsPerClick = 5f },
        };

        public Level Get(HappinessRating rating)
        {
            if (levels == null || levels.Length == 0) return null;
            return levels[Mathf.Clamp((int)rating - 1, 0, levels.Length - 1)];
        }

        public Sprite EmojiFor(HappinessRating rating) => Get(rating)?.emoji;
        public string LabelFor(HappinessRating rating) => Get(rating)?.label ?? rating.ToString();

        /// <summary>Base rating plus any number of steps, kept inside 1-5.</summary>
        public static HappinessRating Clamp(int value) =>
            (HappinessRating)Mathf.Clamp(value, (int)HappinessRating.Vomit, (int)HappinessRating.Love);

        void OnValidate()
        {
            if (levels != null && levels.Length != 5)
                Debug.LogWarning("HappinessRatings should have exactly 5 levels (Vomit to Love).", this);
        }
    }
}
