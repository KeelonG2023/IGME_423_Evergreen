using Evergreen.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Evergreen.UI
{
    /// <summary>
    /// Click a placed building (any BuildingYield with a collider) to select it: it gets a highlight
    /// outline and its tooltip opens. Click it again, click empty ground, or press Escape to deselect.
    /// </summary>
    public class BuildingClickSelector : MonoBehaviour
    {
        [SerializeField] BuildingTooltip tooltip;
        [SerializeField] Camera worldCamera;

        [Tooltip("Only colliders on these layers count as buildings.")]
        [SerializeField] LayerMask buildingLayers = ~0;

        [Header("Highlight outline")]
        [Tooltip("Evergreen/BuildingOutline shader. Found by name if left empty.")]
        [SerializeField] Shader outlineShader;
        [SerializeField] Color outlineColor = new Color(1f, 0.85f, 0.2f, 1f);
        [Tooltip("Outline thickness in world units.")]
        [SerializeField, Min(0f)] float outlineWidth = 1f;

        /// <summary>The currently selected building, or null.</summary>
        public BuildingYield Selected { get; private set; }

        Material outlineMaterial;
        Material maskMaterial;
        BuildingOutline selectedOutline;

        void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (tooltip == null) tooltip = FindFirstObjectByType<BuildingTooltip>(FindObjectsInactive.Include);
            if (outlineShader == null) outlineShader = Shader.Find("Evergreen/BuildingOutline");

            if (outlineShader != null)
            {
                // Same shader, two materials: the mask (stencil only) draws first, then the outline.
                maskMaterial = new Material(outlineShader) { name = "Building Outline Mask (runtime)" };
                maskMaterial.SetShaderPassEnabled("UniversalForward", false);
                maskMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry + 5;

                outlineMaterial = new Material(outlineShader) { name = "Building Outline (runtime)" };
                outlineMaterial.SetShaderPassEnabled("SRPDefaultUnlit", false);
                outlineMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry + 10;
                outlineMaterial.SetColor("_OutlineColor", outlineColor);
            }
            else
            {
                Debug.LogWarning("BuildingClickSelector: Evergreen/BuildingOutline shader not found, selection outline is disabled.", this);
            }
        }

        void OnDestroy()
        {
            if (outlineMaterial != null) Destroy(outlineMaterial);
            if (maskMaterial != null) Destroy(maskMaterial);
        }

        void Update()
        {
            // Building was destroyed, or something else closed the tooltip.
            if (Selected == null && selectedOutline != null) Deselect();
            else if (Selected != null && tooltip != null && tooltip.Target != Selected) Deselect();

            // Picking a new building to place clears the current selection.
            if (GridPlacement.IsPlacing)
            {
                if (Selected != null) Deselect();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Deselect();

            Mouse mouse = Mouse.current;
            if (mouse == null || worldCamera == null) return;
            if (!mouse.leftButton.wasPressedThisFrame) return;

            // Clicks on the HUD itself shouldn't change the selection.
            bool clickedOnUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (clickedOnUi) return;

            BuildingYield clickedBuilding = FindBuildingUnderMouse(mouse);

            // Clicking empty ground or the already-selected building deselects.
            if (clickedBuilding == null || clickedBuilding == Selected) Deselect();
            else Select(clickedBuilding);
        }

        public void Select(BuildingYield building)
        {
            if (building == null) { Deselect(); return; }
            if (selectedOutline != null) selectedOutline.Hide();

            Selected = building;

            if (outlineMaterial != null)
            {
                // Re-applied on every select so Inspector tweaks show up during Play mode.
                outlineMaterial.SetColor("_OutlineColor", outlineColor);
                outlineMaterial.SetFloat("_OutlineWidth", outlineWidth);
                selectedOutline = building.GetComponent<BuildingOutline>();
                if (selectedOutline == null) selectedOutline = building.gameObject.AddComponent<BuildingOutline>();
                selectedOutline.Show(outlineMaterial, maskMaterial);
            }

            if (tooltip != null) tooltip.Show(building);
        }

        public void Deselect()
        {
            if (selectedOutline != null) selectedOutline.Hide();
            selectedOutline = null;
            Selected = null;
            if (tooltip != null && tooltip.Target != null) tooltip.Hide();
        }

        BuildingYield FindBuildingUnderMouse(Mouse mouse)
        {
            Vector2 mouseScreenPosition = mouse.position.ReadValue();

            // 3D buildings: raycast from the camera.
            Ray ray = worldCamera.ScreenPointToRay(mouseScreenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 5000f, buildingLayers, QueryTriggerInteraction.Ignore))
            {
                BuildingYield hitBuilding = hit.collider.GetComponentInParent<BuildingYield>();
                if (hitBuilding != null) return hitBuilding;
            }

            // 2D fallback (sprites with Collider2D).

            // The z value is the distance from the camera to the 2D world plane (z = 0).
            float distanceToWorldPlane = -worldCamera.transform.position.z;
            Vector3 mouseWorldPosition = worldCamera.ScreenToWorldPoint(
                new Vector3(mouseScreenPosition.x, mouseScreenPosition.y, distanceToWorldPlane));

            Collider2D hitCollider = Physics2D.OverlapPoint(mouseWorldPosition, buildingLayers);
            return hitCollider != null ? hitCollider.GetComponentInParent<BuildingYield>() : null;
        }
    }
}
