using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Evergreen.Systems
{
    /// <summary>
    /// Removes a placed building: frees its grid cells (old and new grid systems), optionally refunds
    /// part of its cost, then destroys it. BuildingYield unregisters itself from ResourceManager on disable,
    /// so its yields stop immediately.
    /// </summary>
    public static class BuildingDemolition
    {
        /// <summary>Fires just before the building's GameObject is destroyed.</summary>
        public static event Action<BuildingYield> Demolished;

        /// <summary>Money returned for demolishing this building (refundFraction of its build cost).</summary>
        public static float RefundFor(BuildingYield building, float refundFraction)
        {
            if (building == null || building.Definition == null) return 0f;
            return Mathf.Round(building.Definition.cost * Mathf.Clamp01(refundFraction));
        }

        public static void Demolish(BuildingYield building, ResourceManager resources, float refundFraction = 0f)
        {
            if (building == null) return;

            foreach (GridPlacement placement in Object.FindObjectsByType<GridPlacement>(FindObjectsSortMode.None))
                placement.FreeCells(building);
            foreach (GridPlacementNEW placement in Object.FindObjectsByType<GridPlacementNEW>(FindObjectsSortMode.None))
                placement.FreeCells(building);

            float refund = RefundFor(building, refundFraction);
            if (resources != null && refund > 0f) resources.AddResources(new ResourceYield(refund, 0f, 0f));

            Demolished?.Invoke(building);
            building.gameObject.SetActive(false); // Unregisters yields this frame, before Destroy runs.
            Object.Destroy(building.gameObject);
        }
    }
}
