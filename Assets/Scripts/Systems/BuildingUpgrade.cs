using UnityEngine;

namespace Evergreen.Systems
{
    /// <summary>
    /// One add-on a placed building can buy, e.g. "Green Insulation". Upgrades aren't a tree:
    /// any building whose BuildingDefinition lists this asset can install it, in any order, once.
    /// The same asset can be shared by several building types (Apartments and Hospital can both
    /// list Green Insulation). Create via Assets > Create > Evergreen > Building Upgrade.
    /// </summary>
    [CreateAssetMenu(menuName = "Evergreen/Building Upgrade", fileName = "NewUpgrade")]
    public class BuildingUpgrade : ScriptableObject
    {
        [Header("Display")]
        public string displayName = "New Upgrade";

        [Tooltip("Optional. If empty, the icon of the resource it changes most is used.")]
        public Sprite icon;

        [Tooltip("One or two sentences shown when the upgrade card is expanded.")]
        [TextArea(2, 4)] public string description;

        [Header("Economy")]
        [Tooltip("One-time money cost to install it.")]
        [Min(0f)] public float cost = 250f;

        [Tooltip("Added to the building's yearly yield once installed. Negative = extra upkeep / damage.")]
        public ResourceYield yearlyChange;

        [Tooltip("Steps added to the building's happiness rating (e.g. +1 = Happy -> Love). Kept within Vomit-Love.")]
        [Range(-4, 4)] public int happinessRatingChange;

        [Header("Research")]
        [Tooltip("If ticked, the upgrade menu hides this until it's researched in the skill tree.")]
        public bool requiresResearch;
    }
}
