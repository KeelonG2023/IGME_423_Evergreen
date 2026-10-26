using Evergreen.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>
    /// Speech-bubble card that floats above a placed building and shows its yields
    /// and remaining lifespan. Call Show/Hide (BuildingClickSelector does this on click).
    /// </summary>
    public class BuildingTooltip : MonoBehaviour
    {
        [Tooltip("The bubble itself. Defaults to this object.")]
        [SerializeField] RectTransform panel;
        [SerializeField] Camera worldCamera;

        [Header("Content")]
        [SerializeField] Image icon;
        [SerializeField] Text nameText;
        [SerializeField] Text categoryText;
        [SerializeField] Text moneyText;
        [SerializeField] Text ecoText;
        [SerializeField] Text populationText;
        [SerializeField] Text happinessText;
        [SerializeField] Text lifespanText;
        [Tooltip("Bar that shrinks as the building ages.")]
        [SerializeField] RectTransform lifespanFill;

        [Header("Positioning")]
        [Tooltip("Gap in canvas pixels between the top of the building and the bubble's arrow.")]
        [SerializeField] float gap = 18f;
        [Tooltip("Closest the bubble may get to the screen edge, in canvas pixels.")]
        [SerializeField] float screenMargin = 16f;

        /// <summary>The building currently shown, or null when hidden.</summary>
        public BuildingYield Target { get; private set; }

        Canvas canvas;
        RectTransform canvasRect;

        // ---------- Unity lifecycle ----------

        void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            canvasRect = canvas != null ? (RectTransform)canvas.transform : null;
            if (worldCamera == null) worldCamera = Camera.main;
            if (panel == null) panel = (RectTransform)transform;
            panel.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            // The building was destroyed while the tooltip was open.
            if (Target == null)
            {
                if (panel.gameObject.activeSelf) Hide();
                return;
            }

            // Refresh every frame so the lifespan bar ticks down live and the bubble follows the camera.
            RefreshContent();
            FollowTarget();
        }

        // ---------- Public API ----------

        public void Show(BuildingYield building)
        {
            Target = building;
            if (building == null)
            {
                Hide();
                return;
            }

            panel.gameObject.SetActive(true);
            RefreshContent();
            FollowTarget();
        }

        public void Hide()
        {
            Target = null;
            if (panel != null) panel.gameObject.SetActive(false);
        }

        // ---------- Content ----------

        void RefreshContent()
        {
            BuildingYield building = Target;
            ResourceYield yearly = building.YearlyYield;

            if (icon != null)
            {
                icon.sprite = building.Icon;
                icon.enabled = building.Icon != null;
            }
            if (nameText != null) nameText.text = building.DisplayName;
            if (categoryText != null) categoryText.text = GetCategoryLabel(building);

            SetSignedText(moneyText, yearly.money, "$", "");
            SetSignedText(ecoText, yearly.eco, "", " eco");
            SetSignedText(populationText, yearly.population, "", " pop");
            SetSignedText(happinessText, yearly.happiness, "", "%");

            RefreshLifespan(building);
        }

        static string GetCategoryLabel(BuildingYield building)
        {
            if (building.IsExpired) return "Expired";
            return building.Definition != null ? building.Definition.category.ToString() : "Building";
        }

        void RefreshLifespan(BuildingYield building)
        {
            float yearsRemaining = building.YearsRemaining;

            if (float.IsInfinity(yearsRemaining))
            {
                if (lifespanText != null) lifespanText.text = "Lasts forever";
                if (lifespanFill != null) lifespanFill.anchorMax = new Vector2(1f, 1f);
                return;
            }

            int wholeYearsLeft = Mathf.CeilToInt(yearsRemaining);
            if (lifespanText != null)
                lifespanText.text = building.IsExpired
                    ? "Expired"
                    : $"{wholeYearsLeft} yr{(wholeYearsLeft == 1 ? "" : "s")} left";

            if (lifespanFill != null)
            {
                float fractionLeft = yearsRemaining / Mathf.Max(1, building.LifespanYears);
                lifespanFill.anchorMax = new Vector2(Mathf.Clamp01(fractionLeft), 1f);
            }
        }

        static void SetSignedText(Text textField, float value, string prefix, string suffix)
        {
            if (textField == null) return;
            textField.text = ArcadePalette.Signed(value, prefix, suffix);
            textField.color = ArcadePalette.DeltaColor(value);
        }

        // ---------- Positioning ----------

        /// <summary>Places the bubble just above the building, kept inside the screen.</summary>
        void FollowTarget()
        {
            if (canvasRect == null || worldCamera == null) return;

            Vector3 buildingTopWorld = GetTopCenterOfBuilding(Target);
            Vector2 buildingTopScreen = worldCamera.WorldToScreenPoint(buildingTopWorld);

            // Overlay canvases don't use a camera for UI conversion.
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, buildingTopScreen, uiCamera, out Vector2 buildingTopOnCanvas))
                return;

            // Panel pivot is bottom-center, so this puts the arrow just above the building.
            Vector2 bubblePosition = buildingTopOnCanvas + new Vector2(0f, gap);

            // Keep the bubble on screen.
            Vector2 halfCanvasSize = canvasRect.rect.size * 0.5f;
            Vector2 panelSize = panel.rect.size;
            bubblePosition.x = Mathf.Clamp(bubblePosition.x,
                -halfCanvasSize.x + panelSize.x * 0.5f + screenMargin,
                halfCanvasSize.x - panelSize.x * 0.5f - screenMargin);
            bubblePosition.y = Mathf.Clamp(bubblePosition.y,
                -halfCanvasSize.y + screenMargin,
                halfCanvasSize.y - panelSize.y - screenMargin);

            panel.anchoredPosition = bubblePosition;
        }

        /// <summary>Top-center of the building's collider, or its pivot if it has no collider.</summary>
        static Vector3 GetTopCenterOfBuilding(BuildingYield building)
        {
            Collider2D buildingCollider = building.GetComponentInChildren<Collider2D>();
            if (buildingCollider == null) return building.transform.position;

            Bounds bounds = buildingCollider.bounds;
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }
    }
}
