using UnityEngine;

namespace Evergreen.UI
{
    /// <summary>One place to swap the icon used for each resource across the whole HUD.</summary>
    [CreateAssetMenu(menuName = "Evergreen/Resource Icons", fileName = "ResourceIcons")]
    public class ResourceIcons : ScriptableObject
    {
        public Sprite money;
        public Sprite eco;
        public Sprite population;
        public Sprite happiness;
        public Sprite year;
        public Sprite locked;
    }
}
