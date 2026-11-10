using System.Collections.Generic;
using UnityEngine;

namespace Evergreen.UI
{
    /// <summary>One city site in the level select. Assets > Create > Evergreen > Level Data.</summary>
    [CreateAssetMenu(fileName = "Level_", menuName = "Evergreen/Level Data")]
    public class LevelData : ScriptableObject
    {
        [Tooltip("Unique id used for unlock tracking.")]
        public string levelId = "level_01";
        public string displayName = "New Site";
        public string difficulty = "Easy";
        [TextArea] public string description;

        [Tooltip("Must match a scene in Build Profiles / Build Settings.")]
        public string sceneName = "MainScene";

        public bool unlockedByDefault;
    }

    /// <summary>Minimal PlayerPrefs-backed unlocks. Swap for a real save system later.</summary>
    public static class LevelProgress
    {
        const string Prefix = "Evergreen_LevelUnlocked_";

        public static bool IsUnlocked(string levelId) => PlayerPrefs.GetInt(Prefix + levelId, 0) == 1;

        public static void Unlock(string levelId)
        {
            PlayerPrefs.SetInt(Prefix + levelId, 1);
            PlayerPrefs.Save();
        }

        public static void ResetAll(IEnumerable<LevelData> levels)
        {
            foreach (var l in levels) PlayerPrefs.DeleteKey(Prefix + l.levelId);
            PlayerPrefs.Save();
        }
    }
}
