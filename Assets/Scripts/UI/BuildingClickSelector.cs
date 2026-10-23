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
        [SerializeField] LayerMask buildingLayers = ~0;

        void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (tooltip == null) tooltip = FindFirstObjectByType<BuildingTooltip>(FindObjectsInactive.Include);
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && tooltip != null) tooltip.Hide();

            Mouse mouse = Mouse.current;
            if (mouse == null || tooltip == null || worldCamera == null) return;
            if (!mouse.leftButton.wasPressedThisFrame) return;

            // Clicks on the HUD itself shouldn't close or change the tooltip.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Vector2 screen = mouse.position.ReadValue();
            Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -worldCamera.transform.position.z));
            Collider2D hit = Physics2D.OverlapPoint(world, buildingLayers);
            BuildingYield building = hit != null ? hit.GetComponentInParent<BuildingYield>() : null;

            if (building == null || building == tooltip.Target) tooltip.Hide();
            else tooltip.Show(building);
        }
    }
}
