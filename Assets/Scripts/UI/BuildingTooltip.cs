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
        [SerializeField] RectTransform lifespanFill;

        [Header("Positioning")]
        [Tooltip("Gap in canvas pixels between the top of the building and the bubble's arrow.")]
        [SerializeField] float gap = 18f;
        [SerializeField] float screenMargin = 16f;

        public BuildingYield Target { get; private set; }

        Canvas canvas;
        RectTransform canvasRect;

        void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            canvasRect = canvas != null ? (RectTransform)canvas.transform : null;
            if (worldCamera == null) worldCamera = Camera.main;
            if (panel == null) panel = (RectTransform)transform;
            panel.gameObject.SetActive(false);
        }

        public void Show(BuildingYield building)
        {
            Target = building;
            if (building == null) { Hide(); return; }
            panel.gameObject.SetActive(true);
            Refresh();
            Reposition();
        }

        public void Hide()
        {
            Target = null;
            if (panel != null) panel.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (Target == null)
            {
                if (panel.gameObject.activeSelf) Hide();
                return;
            }
            Refresh();
            Reposition();
        }

        void Refresh()
        {
            BuildingYield b = Target;
            ResourceYield y = b.YearlyYield;

            if (icon != null)
            {
                icon.sprite = b.Icon;
                icon.enabled = b.Icon != null;
            }
            if (nameText != null) nameText.text = b.DisplayName;
            if (categoryText != null)
                categoryText.text = b.IsExpired ? "Expired" : (b.Definition != null ? b.Definition.category.ToString() : "Building");

            Set(moneyText, y.money, "$", "");
            Set(ecoText, y.eco, "", " eco");
            Set(populationText, y.population, "", " pop");
            Set(happinessText, y.happiness, "", "%");

            float remaining = b.YearsRemaining;
            if (float.IsInfinity(remaining))
            {
                if (lifespanText != null) lifespanText.text = "Lasts forever";
                if (lifespanFill != null) lifespanFill.anchorMax = new Vector2(1f, 1f);
            }
            else
            {
                int yrs = Mathf.CeilToInt(remaining);
                if (lifespanText != null) lifespanText.text = b.IsExpired ? "Expired" : $"{yrs} yr{(yrs == 1 ? "" : "s")} left";
                if (lifespanFill != null)
                    lifespanFill.anchorMax = new Vector2(Mathf.Clamp01(remaining / Mathf.Max(1, b.LifespanYears)), 1f);
            }
        }

        static void Set(Text t, float v, string prefix, string suffix)
        {
            if (t == null) return;
            t.text = ArcadePalette.Signed(v, prefix, suffix);
            t.color = ArcadePalette.DeltaColor(v);
        }

        void Reposition()
        {
            if (canvasRect == null || worldCamera == null) return;

            // Top-center of the building in world space.
            Vector3 world = Target.transform.position;
            Collider2D col = Target.GetComponentInChildren<Collider2D>();
            if (col != null) world = new Vector3(col.bounds.center.x, col.bounds.max.y, col.bounds.center.z);

            Vector2 screen = worldCamera.WorldToScreenPoint(world);
            Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCam, out Vector2 local))
                return;

            // Panel pivot is bottom-center, so this puts the arrow just above the building.
            Vector2 pos = local + new Vector2(0f, gap);

            // Keep the bubble on screen.
            Vector2 half = canvasRect.rect.size * 0.5f;
            Vector2 size = panel.rect.size;
            pos.x = Mathf.Clamp(pos.x, -half.x + size.x * 0.5f + screenMargin, half.x - size.x * 0.5f - screenMargin);
            pos.y = Mathf.Clamp(pos.y, -half.y + screenMargin, half.y - size.y - screenMargin);
            panel.anchoredPosition = pos;
        }
    }
}
