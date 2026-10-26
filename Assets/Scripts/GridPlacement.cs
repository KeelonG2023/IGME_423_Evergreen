using System.Collections.Generic;
using UnityEngine;

public class GridPlacement : MonoBehaviour
{
    public GameObject objectToPlace;
    public GameObject ghostObject;

    // GRID SETTINGS
    public Grid grid;
    public float gridSize = 1f;



    private List<Vector3Int> occupiedSpaces = new List<Vector3Int>();
    [SerializeField] private Vector3Int currentCellPos;
    private void Start()
    {
        grid = FindFirstObjectByType<Grid>();

        if(grid == null)
        {
            Debug.LogWarning("Grid not detected, disabling placement system");
            this.enabled = false;
            return;
        }

        CreateGhostObject();
    }

    private void Update()
    {
        if(ghostObject != null)
        {
            UpdateGhostPosition();

            if (Input.GetMouseButtonDown(0))
            {
                Place();
            }
        }
        
    }

    void CreateGhostObject()
    {
        ghostObject = Instantiate(objectToPlace);
        Collider col = ghostObject.GetComponent<Collider>();
        if (col) col.enabled = false;

        Renderer[] renderers = ghostObject.GetComponents<Renderer>();

        foreach (Renderer r in renderers)
        {
            Material mat = r.material;
            Color color = new Color(1, 1, 0.9f, 0.3f);
            mat.color = color;

            mat.SetFloat("_Mode", 2);
            mat.SetInt("_ScrBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }

    }

    void UpdateGhostPosition()
    {
        Ray cast = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(cast, out RaycastHit hit))
        {
            Debug.DrawRay(cast.origin, cast.direction);
            Vector3 pos = hit.point;

            Vector3Int cellPos = grid.WorldToCell(pos);
            currentCellPos = cellPos;
            Vector3 adjustedPos = grid.GetCellCenterWorld(cellPos);

            print(adjustedPos);
            ghostObject.transform.position = adjustedPos;

            if (occupiedSpaces.Contains(cellPos)) 
            { 
                SetGhostColor(Color.red);
            }
            else
            {
                SetGhostColor(new Color(0.9f, 1f, 1f, 0.3f));
            }

        }
    }


    void SetGhostColor(Color col)
    {
        Renderer[] renderers = ghostObject.GetComponents<Renderer>();

        foreach (Renderer r in renderers)
        {
            Material mat = r.material;
            mat.color = col;
        }
    }

    void Place()
    {
        Vector3 placePos = grid.GetCellCenterWorld(currentCellPos);
        if (!occupiedSpaces.Contains(currentCellPos))
        {
            Instantiate(objectToPlace, placePos, Quaternion.identity);
            occupiedSpaces.Add(currentCellPos);
        }
    }
}
