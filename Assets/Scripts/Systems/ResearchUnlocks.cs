using System.Collections.Generic;
using UnityEngine;

namespace Evergreen.Systems
{
    /// <summary>
    /// Remembers which research-gated upgrades the player has unlocked in the skill tree.
    /// An upgrade with requiresResearch ticked stays out of the upgrade menu until the
    /// skill tree calls Unlock for it. Every other upgrade is always available.
    /// </summary>
    public static class ResearchUnlocks
    {
        static readonly HashSet<BuildingUpgrade> unlocked = new HashSet<BuildingUpgrade>();

        /// <summary>True if the upgrade menu may offer this upgrade.</summary>
        public static bool IsAvailable(BuildingUpgrade upgrade)
        {
            if (upgrade == null) return false;
            return !upgrade.requiresResearch || unlocked.Contains(upgrade);
        }

        public static bool IsUnlocked(BuildingUpgrade upgrade) => upgrade != null && unlocked.Contains(upgrade);

        public static void Unlock(BuildingUpgrade upgrade)
        {
            if (upgrade != null) unlocked.Add(upgrade);
        }

        // Clears unlocks between play sessions, even with domain reload turned off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => unlocked.Clear();
    }
}
