using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Data for one build tile definition. A tile of this gameObject type will be placed if the types of adjacent tiles matches the types specified.
/// </summary>
[CreateAssetMenu(menuName = "Evergreen/Building Tile Definition", fileName = "BuildTileDefinition")]
public class BuildTileDefinition : ScriptableObject
{
    [Tooltip("The object that is to be placed if the conditons are met.")]
    public GameObject tile;

    [Tooltip("List of adjacent grid spaces that must be filled with objects of similar type in order for the correspodning GameObject to be placed")]
    public List<Vector2Int> axes;

    [Tooltip("Orientation, in XYZ")]
    public Vector3 Orientation;

}
