using System.Collections.Generic;
using Evergreen.Systems;
using Evergreen.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Places buildings picked in the build menu onto the 3D grid.
/// Flow: BuildMenu raises StructureSelected -> a see-through ghost follows the mouse, snapped to the grid ->
/// left click spends the cost and spawns the building -> Esc or a right-click (without dragging) cancels.
/// R rotates 90 degrees. Buildings can cover several cells (BuildingDefinition.footprint).
/// Ghost turns red over occupied cells, off the ground, or when the player can't afford it.
/// </summary>
public class GridPlacement : MonoBehaviour
{
    /// <summary>True while a ghost is following the mouse. Other click handlers (tooltips) should stay quiet.</summary>
    public static bool IsPlacing { get; private set; }

    /// <summary>A right-click that moves less than this many pixels counts as "cancel" instead of a camera drag.</summary>
    const float RightClickDragThreshold = 6f;

    [Header("References (found automatically if left empty)")]
    [SerializeField] BuildMenu buildMenu;
    [SerializeField] ResourceManager resources;
    public Grid grid;
    [SerializeField] Camera worldCamera;

    [Header("Placement")]
    [Tooltip("Stay in placement mode after placing, so the player can drop several in a row.")]
    [SerializeField] bool keepPlacingAfterBuild = true;
    [Tooltip("Colliders on these layers can be built on (the ground). Buildings themselves are always ignored.")]
    [SerializeField] LayerMask groundLayers = ~0;
    [SerializeField] float maxRayDistance = 2000f;

    [Header("Ghost look")]
    [Tooltip("See-through material for the ghost. Made by Evergreen > Playtest Setup. A fallback is created if empty.")]
    [SerializeField] Material ghostMaterial;
    [SerializeField] Color validColor = new Color(0.55f, 1f, 0.6f, 0.45f);
    [SerializeField] Color invalidColor = new Color(1f, 0.3f, 0.25f, 0.45f);

    readonly HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();

    BuildingDefinition selected;
    GameObject ghost;
    Renderer[] ghostRenderers;
    MaterialPropertyBlock ghostBlock;
    readonly List<Vector3Int> currentCells = new List<Vector3Int>();
    int quarterTurns;
    Vector3 currentPosition;
    bool currentCellValid;
    bool hasGroundUnderMouse;
    Vector2 rightClickStart;

    // ---------- Unity lifecycle ----------

    void Awake()
    {
        if (grid == null) grid = GetComponent<Grid>();
        if (grid == null) grid = FindFirstObjectByType<Grid>();
        if (buildMenu == null) buildMenu = FindFirstObjectByType<BuildMenu>(FindObjectsInactive.Include);
        if (resources == null) resources = FindFirstObjectByType<ResourceManager>();
        if (worldCamera == null) worldCamera = Camera.main;
        ghostBlock = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        if (buildMenu != null) buildMenu.StructureSelected += OnStructureSelected;
    }

    void OnDisable()
    {
        if (buildMenu != null) buildMenu.StructureSelected -= OnStructureSelected;
        StopPlacing(false);
    }

    void Start()
    {
        if (grid == null)
        {
            Debug.LogWarning("GridPlacement: no Grid found, placement is disabled.", this);
            enabled = false;
            return;
        }
        if (buildMenu == null) Debug.LogWarning("GridPlacement: no BuildMenu found, nothing can be selected.", this);

        // Anything already sitting in the scene blocks its cells.
        foreach (BuildingYield existing in FindObjectsByType<BuildingYield>(FindObjectsSortMode.None))
        {
            Vector2Int size = existing.Definition != null ? existing.Definition.footprint : Vector2Int.one;
            int turns = Mathf.RoundToInt(existing.transform.eulerAngles.y / 90f);
            occupiedCells.UnionWith(BlockCells(WorldToCell(existing.transform.position), Rotated(size, turns)));
        }
    }

    void Update()
    {
        if (selected == null || ghost == null) return;

        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { StopPlacing(true); return; }
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame) quarterTurns = (quarterTurns + 1) % 4;

        if (mouse != null)
        {
            // Right mouse is also camera drag-pan, so only a short tap cancels.
            if (mouse.rightButton.wasPressedThisFrame) rightClickStart = mouse.position.ReadValue();
            if (mouse.rightButton.wasReleasedThisFrame &&
                (mouse.position.ReadValue() - rightClickStart).sqrMagnitude < RightClickDragThreshold * RightClickDragThreshold)
            {
                StopPlacing(true);
                return;
            }
        }

        bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        UpdateGhost(mouse, pointerOverUi);

        if (mouse != null && mouse.leftButton.wasPressedThisFrame && !pointerOverUi)
            TryPlace();
    }

    // ---------- Selection ----------

    void OnStructureSelected(BuildingDefinition definition)
    {
        if (definition == selected) return;
        StopPlacing(false);
        if (definition == null) return;

        if (definition.prefab == null)
        {
            Debug.LogWarning($"GridPlacement: '{definition.displayName}' has no prefab. Run Evergreen > Playtest Setup.", definition);
            buildMenu?.ClearSelection();
            return;
        }

        selected = definition;
        ghost = CreateGhost(definition.prefab);
        IsPlacing = true;
    }

    /// <summary>Removes the ghost. If <paramref name="tellMenu"/> is set, the build menu's selection is cleared too.</summary>
    void StopPlacing(bool tellMenu)
    {
        if (ghost != null) Destroy(ghost);
        ghost = null;
        ghostRenderers = null;
        selected = null;
        IsPlacing = false;

        // ClearSelection raises StructureSelected(null), which lands back in OnStructureSelected and returns early.
        if (tellMenu && buildMenu != null) buildMenu.ClearSelection();
    }

    // ---------- Ghost ----------

    GameObject CreateGhost(GameObject prefab)
    {
        // Spawn under an inactive holder so the building's scripts never run (no yield registration).
        var holder = new GameObject("GhostHolder");
        holder.SetActive(false);
        GameObject g = Instantiate(prefab, holder.transform);
        g.name = "Ghost - " + prefab.name;

        foreach (Collider c in g.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        foreach (Collider2D c in g.GetComponentsInChildren<Collider2D>(true)) DestroyImmediate(c);
        foreach (MonoBehaviour b in g.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(b is Spinner)) DestroyImmediate(b); // Spinner is harmless and looks nice on the ghost.

        Material mat = ghostMaterial != null ? ghostMaterial : CreateFallbackGhostMaterial();
        ghostRenderers = g.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in ghostRenderers)
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        g.transform.SetParent(null, false);
        Destroy(holder);
        g.SetActive(true);
        SetGhostColor(validColor);
        return g;
    }

    void UpdateGhost(Mouse mouse, bool pointerOverUi)
    {
        hasGroundUnderMouse = mouse != null && worldCamera != null && TryGetGroundPoint(mouse.position.ReadValue(), out currentPosition);

        // Hide the ghost while the mouse is over the HUD or off the map.
        ghost.SetActive(hasGroundUnderMouse && !pointerOverUi);
        if (!hasGroundUnderMouse) return;

        Vector2Int size = Rotated(selected.footprint, quarterTurns);
        currentCells.Clear();
        currentCells.AddRange(BlockCells(WorldToCell(currentPosition), size));

        // Centre of the block = halfway between its first and last cell centres.
        Vector3 first = grid.GetCellCenterWorld(currentCells[0]);
        Vector3 last = grid.GetCellCenterWorld(currentCells[currentCells.Count - 1]);
        Vector3 center = (first + last) * 0.5f;
        currentPosition = new Vector3(center.x, currentPosition.y, center.z);
        ghost.transform.SetPositionAndRotation(currentPosition, Quaternion.Euler(0f, quarterTurns * 90f, 0f));

        bool affordable = resources == null || resources.CanAfford(selected.cost);
        bool free = true;
        foreach (Vector3Int cell in currentCells)
        {
            if (occupiedCells.Contains(cell) || !HasGroundAt(grid.GetCellCenterWorld(cell))) { free = false; break; }
        }
        currentCellValid = free && affordable;
        SetGhostColor(currentCellValid ? validColor : invalidColor);
    }

    void SetGhostColor(Color color)
    {
        if (ghostRenderers == null) return;
        ghostBlock.SetColor("_BaseColor", color);
        ghostBlock.SetColor("_Color", color);
        foreach (Renderer r in ghostRenderers) r.SetPropertyBlock(ghostBlock);
    }

    static Material CreateFallbackGhostMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var mat = new Material(shader) { name = "Ghost (runtime)" };
        ConfigureTransparent(mat);
        return mat;
    }

    /// <summary>Turns a URP Unlit/Lit material into an alpha-blended one. Shared with the editor setup.</summary>
    public static void ConfigureTransparent(Material mat)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    // ---------- Placing ----------

    void TryPlace()
    {
        if (!hasGroundUnderMouse || !currentCellValid) return;
        if (resources != null && !resources.TrySpend(selected.cost)) return;

        GameObject building = Instantiate(selected.prefab, currentPosition, Quaternion.Euler(0f, quarterTurns * 90f, 0f));
        building.name = selected.displayName;

        BuildingYield yield = building.GetComponent<BuildingYield>();
        if (yield == null) yield = building.AddComponent<BuildingYield>();
        yield.ApplyDefinition(selected);

        occupiedCells.UnionWith(currentCells);

        bool canAffordAnother = resources == null || resources.CanAfford(selected.cost);
        if (!keepPlacingAfterBuild || !canAffordAnother) StopPlacing(true);
    }

    /// <summary>Frees the cells a building covers, e.g. when it is demolished.</summary>
    public void FreeCells(BuildingYield building)
    {
        Vector2Int size = building.Definition != null ? building.Definition.footprint : Vector2Int.one;
        int turns = Mathf.RoundToInt(building.transform.eulerAngles.y / 90f);
        foreach (Vector3Int cell in BlockCells(WorldToCell(building.transform.position), Rotated(size, turns)))
            occupiedCells.Remove(cell);
    }

    // ---------- Helpers ----------

    Vector3Int WorldToCell(Vector3 world)
    {
        Vector3Int cell = grid.WorldToCell(world);
        // The grid is laid flat (XZY swizzle), so the "height" cell axis is irrelevant. Zero it so
        // a hit on a slightly raised patch of ground still maps to the same cell.
        cell.z = 0;
        return cell;
    }

    static Vector2Int Rotated(Vector2Int size, int turns)
    {
        size = Vector2Int.Max(size, Vector2Int.one);
        return turns % 2 == 0 ? size : new Vector2Int(size.y, size.x);
    }

    /// <summary>Cells of a size.x by size.y block centred (as closely as possible) on the given cell.</summary>
    static List<Vector3Int> BlockCells(Vector3Int around, Vector2Int size)
    {
        var cells = new List<Vector3Int>(size.x * size.y);
        int startX = around.x - (size.x - 1) / 2;
        int startY = around.y - (size.y - 1) / 2;
        for (int y = 0; y < size.y; y++)
        for (int x = 0; x < size.x; x++)
            cells.Add(new Vector3Int(startX + x, startY + y, 0));
        return cells;
    }

    /// <summary>True if there's buildable ground straight below this point (keeps big buildings off the map edge).</summary>
    bool HasGroundAt(Vector3 point)
    {
        var ray = new Ray(point + Vector3.up * 500f, Vector3.down);
        foreach (RaycastHit hit in Physics.RaycastAll(ray, 1000f, groundLayers, QueryTriggerInteraction.Ignore))
            if (hit.collider.GetComponentInParent<BuildingYield>() == null) return true;
        return false;
    }

    /// <summary>First ground hit under the mouse, skipping buildings (and their colliders).</summary>
    bool TryGetGroundPoint(Vector2 screenPosition, out Vector3 point)
    {
        Ray ray = worldCamera.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, maxRayDistance, groundLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<BuildingYield>() != null) continue;
            point = hit.point;
            return true;
        }
        point = default;
        return false;
    }
}
