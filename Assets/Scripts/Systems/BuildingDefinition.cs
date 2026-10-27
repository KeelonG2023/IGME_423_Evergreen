using UnityEngine;

namespace Evergreen.Systems
{
    /// <summary>
    /// Data for one buildable structure: what the build menu shows and what a placed
    /// building yields. Create via Assets > Create > Evergreen > Building Definition.
    /// </summary>
    [CreateAssetMenu(menuName = "Evergreen/Building Definition", fileName = "NewBuilding")]
    public class BuildingDefinition : ScriptableObject
    {
        [Header("Display")]
        public string displayName = "New Building";

        [Tooltip("Which build menu tab this appears under.")]
        public BuildingCategory category;

        public Sprite icon;

        [Tooltip("Color used for the placeholder tile / card accents.")]
        public Color tileColor = Color.white;

        [Tooltip("One or two sentences shown when the build card is expanded.")]
        [TextArea(2, 4)] public string description;

        [Header("Economy")]
        [Tooltip("One-time money cost to place it.")]
        [Min(0f)] public float cost = 500f;

        [Tooltip("Per in-game year. Negative = upkeep / damage.")]
        public ResourceYield yearlyYield;

        [Tooltip("Years before the building expires and stops yielding. 0 = never.")]
        [Min(0)] public int lifespanYears = 30;

        [Header("Placement")]
        [Tooltip("Grid cells it covers (x by z) before rotating. Evergreen > Playtest Setup fills this from the model's size.")]
        public Vector2Int footprint = Vector2Int.one;

        [Tooltip("Prefab the placement system spawns. Should have a BuildingYield and a Collider2D.")]
        public GameObject prefab;
    }
}
