using UnityEngine;
using Evergreen.Systems;
using System.Collections.Generic;

/// <summary>
/// Put this on any building. Disables BuildingYield functions while it cannot find adjacent buildings 
/// </summary>
public class RequireAdjacency : MonoBehaviour
{
    public string requiredObject;

    BuildingYield building;
    private void Start()
    {
        building = GetComponent<BuildingYield>();

        if (building == null || requiredObject == string.Empty )
        {
            Debug.LogWarning("This object contains RequireAdjacency, but it does not have a BuildingYield and/or required building. Disabling RequireAdjacency.");
            this.gameObject.SetActive( false );
            return;
        }
       PlaceableGrid.GridUpdate += CheckForAdjacency;
        //call function once at when object starts 
       if (PlaceableGrid.Instance != null ) CheckForAdjacency(PlaceableGrid.Instance);
    }


    /// <summary>
    /// Stops production of a building if it isn't next to the object it needs.
    /// </summary>
   void CheckForAdjacency(PlaceableGrid grid)
   {
        List<GridCell> cells = grid.GetAdjacency(building.footprint);
        foreach (GridCell cell in cells)
        {
            if(cell.gameobject.name == requiredObject)
            {
                building.producing = true;
                return;
            }
        }
        building.producing = false;
   }
}
