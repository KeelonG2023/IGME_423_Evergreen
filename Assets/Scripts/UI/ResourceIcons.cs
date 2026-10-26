using UnityEngine;

namespace Evergreen.UI
{
    /// <summary>
    /// One place to swap the icon used for each resource across the whole HUD.
    /// Create via Assets > Create > Evergreen > Resource Icons.
    /// </summary>
    [CreateAssetMenu(menuName = "Evergreen/Resource Icons", fileName = "ResourceIcons")]
    public class ResourceIcons : ScriptableObject
    {
        public Sprite money;
        public Sprite eco;
        public Sprite population;
        public Sprite happiness;
        public Sprite year;

        [Tooltip("Shown on build cards the player can't afford.")]
        public Sprite locked;
    }
}
