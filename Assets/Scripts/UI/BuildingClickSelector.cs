using Evergreen.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Evergreen.UI
{
    /// <summary>
    /// Click a placed building (any BuildingYield with a Collider2D) to open its tooltip.
    /// Click it again, click empty ground, or press Escape to close.
    /// </summary>
    public class BuildingClickSelector : MonoBehaviour
    {
        [SerializeField] BuildingTooltip tooltip;
        [SerializeField] Camera worldCamera;

        [Tooltip("Only colliders on these layers count as buildings.")]
        [SerializeField] LayerMask buildingLayers = ~0;

        void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (tooltip == null) tooltip = FindFirstObjectByType<BuildingTooltip>(FindObjectsInactive.Include);
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && tooltip != null)
                tooltip.Hide();

            Mouse mouse = Mouse.current;
            if (mouse == null || tooltip == null || worldCamera == null) return;
            if (!mouse.leftButton.wasPressedThisFrame) return;

            // While a building is being placed, left click belongs to the placement system.
            if (GridPlacement.IsPlacing) return;

            // Clicks on the HUD itself shouldn't close or change the tooltip.
            bool clickedOnUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (clickedOnUi) return;

            BuildingYield clickedBuilding = FindBuildingUnderMouse(mouse);

            // Clicking empty ground or the already-open building closes the tooltip.
            if (clickedBuilding == null || clickedBuilding == tooltip.Target) tooltip.Hide();
            else tooltip.Show(clickedBuilding);
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
