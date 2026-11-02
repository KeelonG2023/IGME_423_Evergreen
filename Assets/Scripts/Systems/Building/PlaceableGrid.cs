using System.Collections.Generic;
using Evergreen.Systems;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;


/// <summary>
/// Defines the grid that objects appear on
/// </summary>
public class PlaceableGrid : MonoBehaviour
{
    [SerializeField] private int width;
    [SerializeField] private int height;
    private GridCell[,] grid;

    [SerializeField] private bool DrawGrid;

    private void Start()
    {
        grid = new GridCell[width, height];

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                grid[i, j] = new GridCell();
            }

        }
    }

    public void SetBuilding(BuildingDefinition building, List<Vector3Int> allBuildingPos)
    {
        foreach (Vector3Int pos in allBuildingPos)
        {
            (int x, int y) = WorldToGridPosition(pos);

            grid[x, y].SetBuilding(building);
        }
    }

    /// <summary>
    /// Checks to see if any of the given building spots are occupied.
    /// </summary>
    /// <param name="allBuildingPos">List of grid positions to check.</param>
    /// <returns></returns>
    public bool CanBuild(List<Vector3Int> allBuildingPos)
    {
        foreach (Vector3Int pos in allBuildingPos)
        {
            int x = pos.x;
            int y = pos.z;
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                return false;
            }

            if (!grid[x, y].IsEmpty()) return false;
        }
        return true;
    }

    private (int x, int y) WorldToGridPosition(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt((worldPos - transform.position).x / GridPlacementNEW.cellSize);
        int y = Mathf.FloorToInt((worldPos - transform.position).z / GridPlacementNEW.cellSize);

        return (x, y);
    }

    public Vector3Int WorldToGridPositionVector(Vector3 worldPos)
    {
        (int x, int y) = WorldToGridPosition(worldPos); 
        return new Vector3Int(x, 0, y);
    }

    public Vector3 GridToWorldPosition(Vector3Int gridPos)
    {
        float cellSize = GridPlacementNEW.cellSize;
        float centerAdjust = cellSize / 2f;
        float x = gridPos.x;
        float y = gridPos.z;

        

        return gameObject.transform.position + new Vector3((x * cellSize) + centerAdjust, gridPos.y, (y * cellSize) + centerAdjust);
    }


    //visualize the grid in the world
    private void OnDrawGizmos()
    {
        if (!DrawGrid) return;
        Gizmos.color = Color.white;

        if (GridPlacementNEW.cellSize <= 0 || width <= 0 || height <= 0) return;

        Vector3 origin = transform.position;

        for (int y = 0; y <= height; y++)
        {
            Vector3 start = origin + new Vector3(0, 0.01f, y * GridPlacementNEW.cellSize);
            Vector3 end = origin + new Vector3(width * GridPlacementNEW.cellSize, 0.01f, y * GridPlacementNEW.cellSize);
            Gizmos.DrawLine(start, end);
        }
        for (int x = 0; x <= width; x++)
        {
            Vector3 start = origin + new Vector3(x * GridPlacementNEW.cellSize, 0.01f, 0);
            Vector3 end = origin + new Vector3(x * GridPlacementNEW.cellSize, 0.01f, height * GridPlacementNEW.cellSize);
            Gizmos.DrawLine(start, end);
        }

    }
}

public class GridCell
{
    private BuildingDefinition building;

    public void SetBuilding(BuildingDefinition build)
    {
        building = build;
    }

    public bool IsEmpty()
    {
        return (building == null);
    }
}

