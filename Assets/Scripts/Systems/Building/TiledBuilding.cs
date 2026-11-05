using System.Collections.Generic;
using Evergreen.Systems;
using NUnit.Framework;
using UnityEngine;


/// <summary>
/// Data for one buildable structure: what the build menu shows and what a placed
/// building yields. Identical to BuildingDefinition, but contains definitions for multiple tiles. Create via Assets > Create > Evergreen > Tiled Building Definition.
/// </summary>
[CreateAssetMenu(menuName = "Evergreen/Tiled Building Definition", fileName = "NewBuilding")]
public class TiledBuilding : BuildingDefinition
{
    [Tooltip("The type of tile this is. This will be checked against surrounding tiles to see if building update is nessecary.")]
    public string type;

    public List<BuildTileDefinition> tiles;
}
