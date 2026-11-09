using System.Collections.Generic;
using Evergreen.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace Evergreen.UI
{
    /// <summary>
    /// Spawns clickable happiness pop-ups over buildings the player has placed.
    /// Every simulation tick, each building without a pop-up rolls to spawn one. The chance and the points a
    /// click gives come from the building's happiness rating (HappinessRatings asset): Vomit buildings never
    /// spawn any, Love buildings spawn lots. This is the only way to earn happiness.
    /// Pop-ups pause with the simulation clock. Drop this on any object in a scene that has a
    /// ResourceManager + SimulationClock. Pop-ups go on the HUD canvas, under a container that sits
    /// behind the HUD's own panels so menus cover them.
    /// </summary>
    public class HappinessPopUpManager : MonoBehaviour
    {
        [Header("Canvas")]
        [Tooltip("Canvas to put pop-ups on. Leave empty to use the HUD canvas.")]
        [SerializeField] Canvas hudCanvas;

        [Header("Ratings")]
        [Tooltip("Chance + points per rating, and the emoji shown. Found from the build menu's Resource Icons if empty.")]
        [SerializeField] HappinessRatings ratings;

        [Header("Look")]
        [Tooltip("Shown if the rating has no emoji set.")]
        [SerializeField] Sprite icon;
        [SerializeField] Vector2 size = new Vector2(48f, 48f);
        [Tooltip("World units above the top of the building.")]
        [SerializeField] float heightAboveBuilding = 0.5f;
        [Tooltip("Gentle up/down bob, in pixels. 0 = off.")]
        [SerializeField] float bobPixels = 6f;

        [Header("Spawning")]
        [Tooltip("Most pop-ups on screen at once.")]
        [SerializeField, Min(1)] int maxActive = 3;
        [Tooltip("Seconds (at 1x, not counting pause) before an unclicked pop-up disappears.")]
        [SerializeField, Min(1f)] float lifetimeSeconds = 30f;


        class PopUp
        {
            public BuildingYield building;
            public RectTransform rect;
            public float age;
            public float bobOffset;
            public float points;
        }

        readonly List<PopUp> active = new List<PopUp>();
        readonly HashSet<BuildingYield> buildingsWithPopUp = new HashSet<BuildingYield>();

        ResourceManager resources;
        SimulationClock clock;
        Camera worldCamera;
        RectTransform container;
        Camera canvasCamera; // null for Screen Space - Overlay

        // ---------- Unity lifecycle ----------

        void Start()
        {
            resources = ResourceManager.Instance != null ? ResourceManager.Instance : FindFirstObjectByType<ResourceManager>();
            clock = SimulationClock.Instance != null ? SimulationClock.Instance : FindFirstObjectByType<SimulationClock>();
            worldCamera = Camera.main;

            if (resources == null || clock == null)
            {
                Debug.LogWarning("HappinessPopUpManager needs a ResourceManager and SimulationClock in the scene. Disabled.", this);
                enabled = false;
                return;
            }

            if (ratings == null)
            {
                BuildMenu menu = FindFirstObjectByType<BuildMenu>(FindObjectsInactive.Include);
                if (menu != null && menu.Icons != null) ratings = menu.Icons.happinessRatings;
            }
            if (ratings == null)
            {
                Debug.LogWarning("HappinessPopUpManager: no HappinessRatings asset. Assign one. Disabled.", this);
                enabled = false;
                return;
            }

            if (hudCanvas == null) hudCanvas = FindHudCanvas();
            if (hudCanvas == null)
            {
                Debug.LogWarning("HappinessPopUpManager: no HUD canvas found. Assign one in the Inspector. Disabled.", this);
                enabled = false;
                return;
            }

            CreateContainer();
            clock.Ticked += OnTick;
        }

        void OnDestroy()
        {
            if (clock != null) clock.Ticked -= OnTick;
        }

        void LateUpdate()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            bool running = !clock.IsPaused && !clock.IsHalted;
            float dt = running ? Time.unscaledDeltaTime * (float)clock.Speed : 0f;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                PopUp p = active[i];
                if (p.building == null || p.building.IsExpired || resources.IsGameOver) { Remove(i); continue; }

                p.age += dt;
                if (p.age >= lifetimeSeconds) { Remove(i); continue; }

                Position(p);
            }
        }

        // ---------- Spawning ----------

        void OnTick(SimulationClock _)
        {
            if (resources.IsGameOver || active.Count >= maxActive) return;

            BuildingYield[] buildings = FindObjectsByType<BuildingYield>(FindObjectsSortMode.None);
            Shuffle(buildings); // so the same buildings don't always win when maxActive is hit

            foreach (BuildingYield b in buildings)
            {
                if (active.Count >= maxActive) break;
                if (b.IsExpired || buildingsWithPopUp.Contains(b)) continue;
                HappinessRatings.Level level = ratings.Get(b.HappinessRating);
                if (level == null || level.popUpChancePercent <= 0f || level.pointsPerClick <= 0f) continue;
                if (Random.Range(0f, 100f) < level.popUpChancePercent) Spawn(b, level);
            }
        }

        void Spawn(BuildingYield building, HappinessRatings.Level level)
        {
            var go = new GameObject("HappinessPopUp - " + building.name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(container, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            Sprite sprite = level.emoji != null ? level.emoji : icon;
            image.sprite = sprite;
            image.preserveAspect = true;
            if (sprite == null) image.color = new Color(1f, 0.85f, 0.2f); // plain yellow square if no icon set

            var popUp = new PopUp
            {
                building = building,
                rect = rect,
                bobOffset = Random.Range(0f, Mathf.PI * 2f),
                points = level.pointsPerClick
            };
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => Collect(popUp));

            active.Add(popUp);
            buildingsWithPopUp.Add(building);
            Position(popUp);
        }

        void Collect(PopUp popUp)
        {
            resources.GainHappiness(popUp.points);
            int index = active.IndexOf(popUp);
            if (index >= 0) Remove(index);
        }

        void Remove(int index)
        {
            PopUp p = active[index];
            if (p.rect != null) Destroy(p.rect.gameObject);
            if (p.building != null) buildingsWithPopUp.Remove(p.building);
            active.RemoveAt(index);
        }

        // ---------- Placement on screen ----------

        void Position(PopUp p)
        {
            if (worldCamera == null) return;

            Vector3 world = TopOf(p.building) + Vector3.up * heightAboveBuilding;
            Vector3 screen = worldCamera.WorldToScreenPoint(world);

            bool visible = screen.z > 0f;
            p.rect.gameObject.SetActive(visible);
            if (!visible) return;

            if (bobPixels > 0f) screen.y += Mathf.Sin(Time.unscaledTime * 3f + p.bobOffset) * bobPixels;

            // Works for Overlay and Screen Space - Camera canvases, and with the HUD's CanvasScaler.
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(container, screen, canvasCamera, out Vector2 local))
                p.rect.anchoredPosition = local;
        }

        /// <summary>Top-centre of the building's visible mesh, so tall and short buildings both look right.</summary>
        static Vector3 TopOf(BuildingYield building)
        {
            Renderer[] renderers = building.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return building.transform.position;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }

        // ---------- Setup ----------

        /// <summary>Prefers a root canvas named "HUD", then any root screen-space canvas.</summary>
        static Canvas FindHudCanvas()
        {
            Canvas fallback = null;
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas || c.renderMode == RenderMode.WorldSpace) continue;
                if (c.name == "HUD") return c;
                if (fallback == null || c.sortingOrder > fallback.sortingOrder) fallback = c;
            }
            return fallback;
        }

        /// <summary>Full-screen container as the canvas's first child, so every HUD panel draws on top of the pop-ups.</summary>
        void CreateContainer()
        {
            var go = new GameObject("HappinessPopUps", typeof(RectTransform));
            container = (RectTransform)go.transform;
            container.SetParent(hudCanvas.transform, false);
            container.SetAsFirstSibling();
            container.anchorMin = Vector2.zero;
            container.anchorMax = Vector2.one;
            container.pivot = new Vector2(0.5f, 0.5f);
            container.offsetMin = Vector2.zero;
            container.offsetMax = Vector2.zero;

            canvasCamera = hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : hudCanvas.worldCamera;
        }

        static void Shuffle<T>(T[] items)
        {
            for (int i = items.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
