using System.Collections.Generic;
using System.IO;
using System.Linq;
using Evergreen.Systems;
using Evergreen.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Evergreen.EditorTools
{
    /// <summary>
    /// Menu: Evergreen > Build Arcade HUD In Open Scene.
    /// Builds the "C · Arcade" UI concept with real GameObjects you can tweak afterwards:
    /// top meters + date, speed controls, build menu, building tooltip, game-over screen,
    /// plus building definition assets and a small test city. Re-running rebuilds the UI
    /// (building/icon assets are only created if missing, so your edits to them are kept).
    /// Safe to delete this script once the scene is built.
    /// </summary>
    public static class ArcadeHUDBuilder
    {
        const string IconDir = "Assets/Art/UI/Icons/";
        const string ShapeDir = "Assets/Art/UI/Shapes/";
        const string FontDir = "Assets/Art/UI/Fonts/";
        const string DataDir = "Assets/Data";
        const string BuildingDir = "Assets/Data/Buildings";

        const string CanvasName = "Arcade HUD";
        const string CityName = "Test City";

        static Font fontXB, fontB, fontM;
        static Sprite panelSprite, pillSprite, smallSprite;

        static readonly Color PanelColor = ArcadePalette.Panel;
        static readonly Color Card = ArcadePalette.Card;
        static readonly Color Track = ArcadePalette.Track;
        static readonly Color Muted = ArcadePalette.TextMuted;

        // ------------------------------------------------------------------ entry

        [MenuItem("Evergreen/Build Arcade HUD In Open Scene")]
        public static void Build()
        {
            panelSprite = PrepareShape("UI_RoundedPanel.png", 40);
            pillSprite = PrepareShape("UI_Pill.png", 63);
            smallSprite = PrepareShape("UI_RoundedSmall.png", 14);

            Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            fontXB = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "Rubik-ExtraBold.ttf") ?? fallback;
            fontB = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "Rubik-Bold.ttf") ?? fallback;
            fontM = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "Rubik-Medium.ttf") ?? fallback;

            ResourceIcons icons = EnsureResourceIcons();
            List<BuildingDefinition> defs = EnsureDefinitions();

            foreach (string n in new[] { CanvasName, "Simulation HUD", CityName, "Test Buildings" })
            {
                GameObject existing = GameObject.Find(n);
                if (existing != null) Undo.DestroyObjectImmediate(existing);
            }

            EnsureSimulation(out SimulationClock clock, out ResourceManager resources);
            SetupCamera();
            BuildTestCity(defs);
            EnsureEventSystem();
            BuildCanvas(clock, resources, icons, defs);

            BuildPanelRebuilder.FixPillLayouts();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("Evergreen: Arcade HUD built. Save the scene and press Play. Click a building for its tooltip; Space = pause, 1/2/3 = speed.");
        }

        // ------------------------------------------------------------------ assets

        static Sprite PrepareShape(string file, float border)
        {
            string path = ShapeDir + file;
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null)
            {
                Debug.LogError($"Evergreen: missing {path}");
                return null;
            }

            if (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(ti.spriteBorder.x, border))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spriteBorder = new Vector4(border, border, border, border);
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.filterMode = FilterMode.Bilinear;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                ti.SetTextureSettings(settings);
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Sprite Icon(string name)
        {
            Sprite s = AssetDatabase.LoadAllAssetsAtPath(IconDir + name + ".png").OfType<Sprite>().FirstOrDefault();
            if (s == null) Debug.LogWarning($"Evergreen: icon '{name}' not found in {IconDir}");
            return s;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static ResourceIcons EnsureResourceIcons()
        {
            EnsureFolder(DataDir);
            string path = DataDir + "/ResourceIcons.asset";
            var icons = AssetDatabase.LoadAssetAtPath<ResourceIcons>(path);
            if (icons == null)
            {
                icons = ScriptableObject.CreateInstance<ResourceIcons>();
                AssetDatabase.CreateAsset(icons, path);
            }
            if (icons.money == null) icons.money = Icon("coin1");
            if (icons.eco == null) icons.eco = Icon("emerald");
            if (icons.population == null) icons.population = Icon("clipboard"); // placeholder: no people icon in the set yet
            if (icons.happiness == null) icons.happiness = Icon("badgeheart1");
            if (icons.year == null) icons.year = Icon("trophy");
            if (icons.locked == null) icons.locked = Icon("lock_OUT");
            EditorUtility.SetDirty(icons);
            return icons;
        }

        struct Spec
        {
            public string name, icon; public BuildingCategory cat; public float cost; public ResourceYield y; public int life; public int color;
            public Spec(string name, BuildingCategory cat, float cost, float m, float e, float p, float h, int life, string icon, int color)
            { this.name = name; this.cat = cat; this.cost = cost; y = new ResourceYield(m, e, p, h); this.life = life; this.icon = icon; this.color = color; }
        }

        // Placeholder balance numbers, same as the mockups.
        static readonly Spec[] Specs =
        {
            new Spec("Solar Farm",       BuildingCategory.Energy,  1200,  80,   6,  0,  1, 25, "star",          0x3D7DD8),
            new Spec("Wind Turbine",     BuildingCategory.Energy,   900,  60,   4,  0, -1, 20, "battery",       0xF4F7F9),
            new Spec("Coal Plant",       BuildingCategory.Energy,   500, 300, -20,  0, -4, 40, "bomb",          0x4B4746),
            new Spec("Apartments",       BuildingCategory.Housing,  800,  60,  -3, 40,  0, 50, "hammer_OUT",    0xF28F5C),
            new Spec("Green-Roof Homes", BuildingCategory.Housing, 1100,  70,   2, 35,  1, 60, "emerald_stack", 0x4FA34A),
            new Spec("Park",             BuildingCategory.Nature,   300, -40,   8,  0,  5, 80, "emeraldstone",  0x3F9440),
            new Spec("Recycling Center", BuildingCategory.Services, 900, -20,  10,  0,  2, 35, "magnet",        0xF2C744),
            new Spec("Transit Hub",      BuildingCategory.Services,1500,  40,   5,  0,  6, 45, "compass",       0x9A7CF0),
        };

        static List<BuildingDefinition> EnsureDefinitions()
        {
            EnsureFolder(BuildingDir);
            var list = new List<BuildingDefinition>();
            foreach (Spec s in Specs)
            {
                string path = $"{BuildingDir}/{s.name.Replace(" ", "")}.asset";
                var def = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
                if (def == null)
                {
                    def = ScriptableObject.CreateInstance<BuildingDefinition>();
                    def.displayName = s.name;
                    def.category = s.cat;
                    def.cost = s.cost;
                    def.yearlyYield = s.y;
                    def.lifespanYears = s.life;
                    def.icon = Icon(s.icon);
                    def.tileColor = ArcadePalette.Hex(s.color);
                    AssetDatabase.CreateAsset(def, path);
                }
                list.Add(def);
            }
            return list;
        }

        // ------------------------------------------------------------------ scene objects

        static void EnsureSimulation(out SimulationClock clock, out ResourceManager resources)
        {
            clock = Object.FindFirstObjectByType<SimulationClock>();
            resources = Object.FindFirstObjectByType<ResourceManager>();
            if (clock != null && resources != null) return;

            var sim = new GameObject("Simulation");
            Undo.RegisterCreatedObjectUndo(sim, "Build Arcade HUD");
            clock = sim.AddComponent<SimulationClock>();
            resources = sim.AddComponent<ResourceManager>();
            var hotkeys = sim.AddComponent<SimulationHotkeys>();
            SetRefs(resources, ("clock", clock));
            SetRefs(hotkeys, ("clock", clock));
        }

        static void SetupCamera()
        {
            Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (cam == null) return;
            Undo.RecordObject(cam, "Build Arcade HUD");
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ArcadePalette.Hex(0x6FB64F);
        }

        static void BuildTestCity(List<BuildingDefinition> defs)
        {
            var root = new GameObject(CityName);
            Undo.RegisterCreatedObjectUndo(root, "Build Arcade HUD");

            // (definition index, column, row) on a 1-unit grid, kept clear of the HUD.
            int[,] layout =
            {
                { 0, 1, 1 }, { 0, 2, 1 }, { 1, 0, 4 }, { 2, 7, 0 }, { 3, 4, 3 },
                { 3, 5, 3 }, { 4, 4, 4 }, { 5, 1, 3 }, { 5, 7, 4 }, { 6, 6, 1 },
            };
            const float originX = -6.6f, originY = 2.4f;

            for (int i = 0; i < layout.GetLength(0); i++)
            {
                BuildingDefinition def = defs[layout[i, 0]];
                var go = new GameObject(def.displayName);
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(originX + layout[i, 1], originY - layout[i, 2], 0f);

                var tile = go.AddComponent<SpriteRenderer>();
                tile.sprite = smallSprite;
                tile.drawMode = SpriteDrawMode.Sliced;
                tile.size = new Vector2(0.9f, 0.9f);
                tile.color = def.tileColor;

                var col = go.AddComponent<BoxCollider2D>();
                col.size = new Vector2(0.9f, 0.9f);

                if (def.icon != null)
                {
                    var iconGo = new GameObject("Icon");
                    iconGo.transform.SetParent(go.transform, false);
                    var sr = iconGo.AddComponent<SpriteRenderer>();
                    sr.sprite = def.icon;
                    sr.sortingOrder = 1;
                    Vector2 size = def.icon.bounds.size;
                    float scale = 0.62f / Mathf.Max(size.x, size.y);
                    iconGo.transform.localScale = new Vector3(scale, scale, 1f);
                }

                var b = go.AddComponent<BuildingYield>();
                SetRefs(b, ("definition", def), ("tintOnExpire", tile));
            }
        }

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Build Arcade HUD");
        }

        // ------------------------------------------------------------------ canvas

        static void BuildCanvas(SimulationClock clock, ResourceManager resources, ResourceIcons icons, List<BuildingDefinition> defs)
        {
            var canvasGo = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Build Arcade HUD");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            Transform c = canvasGo.transform;

            BuildTopRow(c, clock, resources, icons);
            BuildSpeedControls(c, clock);
            BuildBuildPanel(c, resources, icons, defs);
            BuildingTooltip tooltip = BuildTooltip(c, icons);
            BuildGameOver(c, clock, resources, icons);

            var selector = canvasGo.AddComponent<BuildingClickSelector>();
            SetRefs(selector, ("tooltip", tooltip), ("worldCamera", Camera.main));
        }

        // ---- top row

        static void BuildTopRow(Transform parent, SimulationClock clock, ResourceManager resources, ResourceIcons icons)
        {
            GameObject row = UI("Top Row", parent);
            Place(RT(row), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(1250, 92));
            HLG(row, 12, Pad(0, 0, 0, 0), TextAnchor.UpperLeft);

            // Date card
            Image date = Panel("Date", row.transform, panelSprite, PanelColor, 40f / 22f);
            LE(date.gameObject, 250, 92);
            Chunky(date.gameObject, 6);
            HLG(date.gameObject, 10, Pad(14, 18, 12, 12), TextAnchor.MiddleLeft);
            IconImg("Trophy", date.transform, icons.year, 48);
            GameObject col = UI("Column", date.transform);
            LE(col, -1, -1, 1);
            VLG(col, 8, Pad(0, 0, 0, 0), TextAnchor.MiddleLeft, true, true, true, false);
            GameObject yearRow = UI("Year Row", col.transform);
            HLG(yearRow, 8, Pad(0, 0, 0, 0), TextAnchor.LowerLeft);
            Text yearText = Txt("Year", yearRow.transform, "YEAR 1", fontXB, 28, ArcadePalette.Lime);
            Text goalText = Txt("Goal", yearRow.transform, "/ 20", fontB, 15, Muted);
            GameObject barRow = UI("Progress Row", col.transform);
            HLG(barRow, 8, Pad(0, 0, 0, 0), TextAnchor.MiddleLeft);
            RectTransform yearFill = Bar("Year Bar", barRow.transform, 10, Track, ArcadePalette.Lime, 0.2f, out _, out _);
            Text monthText = Txt("Month", barRow.transform, "JAN", fontB, 13, Muted);

            MeterWidget money = Meter(row.transform, "Money", icons.money, "$1,000", false);
            MeterWidget eco = Meter(row.transform, "Eco", icons.eco, "50", false);
            MeterWidget pop = Meter(row.transform, "Population", icons.population, "100", false);
            MeterWidget happy = Meter(row.transform, "Happiness", icons.happiness, "60%", true);

            var hud = row.AddComponent<SimulationHUD>();
            SetRefs(hud,
                ("clock", clock), ("resources", resources),
                ("yearText", yearText), ("goalText", goalText), ("monthText", monthText), ("yearProgressFill", yearFill),
                ("money", money), ("eco", eco), ("population", pop), ("happiness", happy));
        }

        static MeterWidget Meter(Transform parent, string name, Sprite icon, string value, bool withBar)
        {
            Image card = Panel(name, parent, panelSprite, PanelColor, 40f / 22f);
            LE(card.gameObject, 236, 92);
            Chunky(card.gameObject, 6);
            HLG(card.gameObject, 12, Pad(14, 16, 12, 12), TextAnchor.MiddleLeft);

            IconBadge(card.transform, icon, 58, 6);

            GameObject col = UI("Column", card.transform);
            LE(col, -1, -1, 1);
            VLG(col, withBar ? 4 : 0, Pad(0, 0, 0, 0), TextAnchor.MiddleLeft, true, true, true, false);
            Text valueText = Txt("Value", col.transform, value, fontXB, 26, Color.white);
            RectTransform fill = null;
            if (withBar) fill = Bar("Bar", col.transform, 10, Track, ArcadePalette.Pink, 0.6f, out _, out _);
            Text delta = Txt("Delta", col.transform, "+0 / yr", fontB, 14, ArcadePalette.Lime);

            var meter = card.gameObject.AddComponent<MeterWidget>();
            SetRefs(meter, ("valueText", valueText), ("deltaText", delta), ("fill", fill));
            return meter;
        }

        // ---- speed controls

        static void BuildSpeedControls(Transform parent, SimulationClock clock)
        {
            Image bg = Panel("Speed Controls", parent, pillSprite, PanelColor, 1f);
            // Centered under the playable area (screen minus the build panel).
            Place(RT(bg.gameObject), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f), new Vector2(668, 24), new Vector2(306, 84));
            Chunky(bg.gameObject, 6);
            HLG(bg.gameObject, 10, Pad(10, 10, 10, 10), TextAnchor.MiddleCenter);
            var fitter = bg.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Button pause = RoundButton(bg.transform, "Pause", "II");
            Button s1 = RoundButton(bg.transform, "Speed 1x", "1×");
            Button s2 = RoundButton(bg.transform, "Speed 2x", "2×");
            Button s3 = RoundButton(bg.transform, "Speed 3x", "3×");

            var sc = bg.gameObject.AddComponent<SpeedControls>();
            SetRefs(sc, ("clock", clock), ("pauseButton", pause), ("speed1Button", s1), ("speed2Button", s2), ("speed3Button", s3));
        }

        static Button RoundButton(Transform parent, string name, string label)
        {
            Image img = Panel(name, parent, pillSprite, Track, 1f);
            LE(img.gameObject, 64, 64);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            Text t = Txt("Label", img.transform, label, fontXB, 22, Color.white, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
            return b;
        }

        // ---- build panel

        static void BuildBuildPanel(Transform parent, ResourceManager resources, ResourceIcons icons, List<BuildingDefinition> defs)
        {
            Image panel = Panel("Build Panel", parent, panelSprite, PanelColor, 40f / 28f);
            RectTransform rt = RT(panel.gameObject);
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 0.5f);
            rt.offsetMin = new Vector2(-584, 32);
            rt.offsetMax = new Vector2(-24, -24);
            Chunky(panel.gameObject, 8);
            VLG(panel.gameObject, 16, Pad(22, 22, 22, 22), TextAnchor.UpperLeft, true, true, true, false);

            GameObject header = UI("Header", panel.transform);
            HLG(header, 8, Pad(0, 0, 0, 0), TextAnchor.MiddleLeft);
            Text title = Txt("Title", header.transform, "Build", fontXB, 34, Color.white);
            LE(title.gameObject, -1, -1, 1);
            Txt("Hint", header.transform, "yields per year", fontM, 14, Muted, TextAnchor.MiddleRight);

            GameObject tabRow = UI("Tabs", panel.transform);
            HLG(tabRow, 8, Pad(0, 0, 0, 0), TextAnchor.MiddleLeft);
            var tabs = new List<(Button, bool, BuildingCategory)>
            {
                (Tab(tabRow.transform, "All"), true, BuildingCategory.Energy),
                (Tab(tabRow.transform, "Energy"), false, BuildingCategory.Energy),
                (Tab(tabRow.transform, "Homes"), false, BuildingCategory.Housing),
                (Tab(tabRow.transform, "Nature"), false, BuildingCategory.Nature),
                (Tab(tabRow.transform, "Services"), false, BuildingCategory.Services),
            };

            // Scrollable 2-column catalog
            GameObject catalog = UI("Catalog", panel.transform);
            LE(catalog, -1, -1, -1, 1);
            catalog.AddComponent<RectMask2D>();
            var scroll = catalog.AddComponent<ScrollRect>();
            GameObject content = UI("Content", catalog.transform);
            RectTransform crt = RT(content);
            crt.anchorMin = new Vector2(0, 1);
            crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(0.5f, 1);
            crt.offsetMin = crt.offsetMax = Vector2.zero;
            var grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(252, 200);
            grid.spacing = new Vector2(12, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            var cfit = content.AddComponent<ContentSizeFitter>();
            cfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = crt;
            scroll.viewport = RT(catalog);
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;

            BuildCard template = CardTemplate(content.transform, icons);
            template.gameObject.SetActive(false);

            var menu = panel.gameObject.AddComponent<BuildMenu>();
            var so = new SerializedObject(menu);
            so.FindProperty("resources").objectReferenceValue = resources;
            so.FindProperty("icons").objectReferenceValue = icons;
            so.FindProperty("cardParent").objectReferenceValue = content.transform;
            so.FindProperty("cardTemplate").objectReferenceValue = template;
            SerializedProperty cat = so.FindProperty("catalog");
            cat.arraySize = defs.Count;
            for (int i = 0; i < defs.Count; i++) cat.GetArrayElementAtIndex(i).objectReferenceValue = defs[i];
            SerializedProperty tp = so.FindProperty("tabs");
            tp.arraySize = tabs.Count;
            for (int i = 0; i < tabs.Count; i++)
            {
                SerializedProperty e = tp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("button").objectReferenceValue = tabs[i].Item1;
                e.FindPropertyRelative("showsAll").boolValue = tabs[i].Item2;
                e.FindPropertyRelative("category").enumValueIndex = (int)tabs[i].Item3;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Button Tab(Transform parent, string label)
        {
            Image img = Panel("Tab " + label, parent, pillSprite, Card, 1f);
            HLG(img.gameObject, 0, Pad(18, 18, 11, 11), TextAnchor.MiddleCenter);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            Txt("Label", img.transform, label, fontB, 15, Color.white, TextAnchor.MiddleCenter);
            return b;
        }

        static BuildCard CardTemplate(Transform parent, ResourceIcons icons)
        {
            Image bg = Panel("Card Template", parent, panelSprite, Card, 2f);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var outline = bg.gameObject.AddComponent<Outline>();
            outline.effectColor = ArcadePalette.Lime;
            outline.effectDistance = new Vector2(3, -3);
            outline.enabled = false;
            VLG(bg.gameObject, 8, Pad(14, 14, 14, 14), TextAnchor.UpperLeft, true, true, true, false);

            GameObject top = UI("Top", bg.transform);
            HLG(top, 8, Pad(0, 0, 0, 0), TextAnchor.UpperLeft);
            Image icon = IconBadge(top.transform, null, 58, 6);
            GameObject spacer = UI("Spacer", top.transform);
            LE(spacer, -1, -1, 1);
            Image costPill = Panel("Cost", top.transform, pillSprite, ArcadePalette.Amber, 1f);
            HLG(costPill.gameObject, 0, Pad(12, 12, 6, 6), TextAnchor.MiddleCenter);
            Text cost = Txt("Label", costPill.transform, "$0", fontXB, 16, ArcadePalette.Hex(0x3A2600), TextAnchor.MiddleCenter);

            Text name = Txt("Name", bg.transform, "Building", fontXB, 18, Color.white);

            GameObject chips = UI("Yields", bg.transform);
            HLG(chips, 5, Pad(0, 0, 0, 0), TextAnchor.MiddleLeft);
            Image chipBg = Panel("Chip Template", chips.transform, pillSprite, Track, 1f);
            HLG(chipBg.gameObject, 4, Pad(6, 9, 4, 4), TextAnchor.MiddleCenter);
            Image chipIcon = IconImg("Icon", chipBg.transform, icons.money, 18);
            Text chipText = Txt("Label", chipBg.transform, "+0", fontB, 13, ArcadePalette.Lime);
            var chip = chipBg.gameObject.AddComponent<YieldChip>();
            SetRefs(chip, ("icon", chipIcon), ("label", chipText));

            GameObject flex = UI("Spacer", bg.transform);
            LE(flex, -1, -1, -1, 1);
            Text life = Txt("Lifespan", bg.transform, "0 yr lifespan", fontM, 13, Muted);

            // Locked overlay (shown when unaffordable)
            Image locked = Panel("Locked", bg.transform, panelSprite, new Color(0.06f, 0.07f, 0.07f, 0.55f), 2f);
            locked.raycastTarget = false;
            var lle = locked.gameObject.AddComponent<LayoutElement>();
            lle.ignoreLayout = true;
            Stretch(RT(locked.gameObject));
            Image lockIcon = IconImg("Lock", locked.transform, icons.locked, 56);
            Place(lockIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 56));

            var card = bg.gameObject.AddComponent<BuildCard>();
            SetRefs(card,
                ("button", button), ("icon", icon), ("nameText", name), ("costText", cost), ("costPill", costPill),
                ("lifespanText", life), ("chipRow", chips.transform), ("chipTemplate", chip),
                ("lockedOverlay", locked.gameObject), ("selectedOutline", outline));
            return card;
        }

        // ---- tooltip

        static BuildingTooltip BuildTooltip(Transform parent, ResourceIcons icons)
        {
            GameObject holder = UI("Building Tooltip", parent);
            Stretch(RT(holder));

            Image panel = Panel("Bubble", holder.transform, panelSprite, PanelColor, 40f / 24f);
            Place(RT(panel.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(340, 300));
            Chunky(panel.gameObject, 8);
            VLG(panel.gameObject, 14, Pad(18, 18, 18, 18), TextAnchor.UpperLeft, true, true, true, false);
            var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject header = UI("Header", panel.transform);
            HLG(header, 12, Pad(0, 0, 0, 0), TextAnchor.MiddleLeft);
            Image icon = IconBadge(header.transform, null, 52, 5);
            GameObject names = UI("Names", header.transform);
            LE(names, -1, -1, 1);
            VLG(names, 0, Pad(0, 0, 0, 0), TextAnchor.MiddleLeft, true, true, true, false);
            Text nameText = Txt("Name", names.transform, "Building", fontXB, 21, Color.white);
            Text catText = Txt("Category", names.transform, "Category", fontB, 13, Muted);

            GameObject grid = UI("Yields", panel.transform);
            var g = grid.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(147, 44);
            g.spacing = new Vector2(8, 8);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 2;
            LE(grid, -1, 96);
            Text money = YieldCell(grid.transform, "Money", icons.money);
            Text eco = YieldCell(grid.transform, "Eco", icons.eco);
            Text pop = YieldCell(grid.transform, "Population", icons.population);
            Text happy = YieldCell(grid.transform, "Happiness", icons.happiness);

            GameObject lifeRow = UI("Lifespan Row", panel.transform);
            HLG(lifeRow, 8, Pad(0, 0, 0, 0), TextAnchor.MiddleLeft);
            Text lifeLabel = Txt("Label", lifeRow.transform, "Lifespan", fontB, 14, Color.white);
            LE(lifeLabel.gameObject, -1, -1, 1);
            Text lifeText = Txt("Remaining", lifeRow.transform, "0 yrs left", fontB, 14, Muted, TextAnchor.MiddleRight);
            RectTransform lifeFill = Bar("Lifespan Bar", panel.transform, 14, Track, ArcadePalette.Lime, 0.6f, out _, out _);

            Txt("Hint", panel.transform, "per year · click again to close", fontM, 12, Muted, TextAnchor.MiddleCenter);

            Image arrow = Panel("Arrow", panel.transform, smallSprite, PanelColor, 3f);
            arrow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Place(arrow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24, 24));
            arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);

            var tip = holder.AddComponent<BuildingTooltip>();
            SetRefs(tip,
                ("panel", RT(panel.gameObject)), ("worldCamera", Camera.main),
                ("icon", icon), ("nameText", nameText), ("categoryText", catText),
                ("moneyText", money), ("ecoText", eco), ("populationText", pop), ("happinessText", happy),
                ("lifespanText", lifeText), ("lifespanFill", lifeFill));
            return tip;
        }

        static Text YieldCell(Transform parent, string name, Sprite icon)
        {
            Image cell = Panel(name, parent, smallSprite, Card, 1f);
            HLG(cell.gameObject, 8, Pad(10, 10, 0, 0), TextAnchor.MiddleLeft);
            IconImg("Icon", cell.transform, icon, 26);
            return Txt("Value", cell.transform, "+0", fontXB, 17, Color.white);
        }

        // ---- game over

        static void BuildGameOver(Transform parent, SimulationClock clock, ResourceManager resources, ResourceIcons icons)
        {
            GameObject holder = UI("Game Over", parent);
            Stretch(RT(holder));

            Image overlay = Panel("Overlay", holder.transform, null, new Color(0.06f, 0.08f, 0.07f, 0.6f), 1f);
            Stretch(overlay.rectTransform);

            Image card = Panel("Card", overlay.transform, panelSprite, PanelColor, 40f / 36f);
            Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(760, 700));
            Chunky(card.gameObject, 12);
            VLG(card.gameObject, 20, Pad(44, 44, 84, 44), TextAnchor.UpperCenter, true, true, true, false);
            var fit = card.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Badge that sits on the card's top edge
            Image badge = Panel("Badge", card.transform, pillSprite, PanelColor, 1f);
            badge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Place(badge.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140, 140));
            Image inner = Panel("Inner", badge.transform, pillSprite, ArcadePalette.FailBg, 1f);
            Place(inner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 120));
            Image reason = IconImg("Reason Icon", inner.transform, icons.eco, 76);
            Place(reason.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(76, 76));

            Txt("Kicker", card.transform, "GAME OVER", fontXB, 18, ArcadePalette.Coral, TextAnchor.MiddleCenter);
            Text title = Txt("Title", card.transform, "Eco hit zero!", fontXB, 60, Color.white, TextAnchor.MiddleCenter);
            Text subtitle = Txt("Subtitle", card.transform, "The environment collapsed in Year 1.", fontM, 20, Muted, TextAnchor.MiddleCenter);

            GameObject rows = UI("Meters", card.transform);
            VLG(rows, 8, Pad(0, 0, 8, 8), TextAnchor.UpperLeft, true, true, true, false);
            GameOverMeterRow moneyRow = MeterRow(rows.transform, "Money", icons.money, ArcadePalette.Amber);
            GameOverMeterRow ecoRow = MeterRow(rows.transform, "Eco score", icons.eco, ArcadePalette.Lime);
            GameOverMeterRow popRow = MeterRow(rows.transform, "Population", icons.population, ArcadePalette.Sky);
            GameOverMeterRow happyRow = MeterRow(rows.transform, "Happiness", icons.happiness, ArcadePalette.Pink);

            Text survived = Txt("Survived", card.transform, "Survived 0 of 20 years", fontB, 17, Muted, TextAnchor.MiddleCenter);

            GameObject buttons = UI("Buttons", card.transform);
            HLG(buttons, 14, Pad(0, 0, 6, 0), TextAnchor.MiddleCenter, true, true, true, false);
            Button menu = PillButton(buttons.transform, "Menu", "Menu", Track, Color.white, 20, 1f);
            Button again = PillButton(buttons.transform, "Play Again", "Play again", ArcadePalette.Lime, ArcadePalette.Hex(0x1C3A08), 22, 1.6f);

            var screen = holder.AddComponent<GameOverScreen>();
            SetRefs(screen,
                ("clock", clock), ("resources", resources), ("icons", icons), ("root", overlay.gameObject),
                ("reasonIcon", reason), ("titleText", title), ("subtitleText", subtitle), ("survivedText", survived),
                ("moneyRow", moneyRow), ("ecoRow", ecoRow), ("populationRow", popRow), ("happinessRow", happyRow),
                ("playAgainButton", again), ("menuButton", menu));

            overlay.gameObject.SetActive(false);
        }

        static GameOverMeterRow MeterRow(Transform parent, string label, Sprite icon, Color color)
        {
            Image bg = Panel(label, parent, smallSprite, new Color(0, 0, 0, 0), 0.8f);
            var outline = bg.gameObject.AddComponent<Outline>();
            outline.effectColor = ArcadePalette.Coral;
            outline.effectDistance = new Vector2(3, -3);
            outline.enabled = false;
            HLG(bg.gameObject, 14, Pad(12, 12, 8, 8), TextAnchor.MiddleLeft);

            Image iconImg = IconBadge(bg.transform, icon, 48, 4);
            Text labelText = Txt("Label", bg.transform, label, fontB, 18, Color.white);
            LE(labelText.gameObject, 140, -1);
            RectTransform fill = Bar("Bar", bg.transform, 20, Track, color, 0.5f, out Image track, out Image fillImg);
            Text value = Txt("Value", bg.transform, "0", fontXB, 20, Color.white, TextAnchor.MiddleRight);
            LE(value.gameObject, 110, -1);

            var row = bg.gameObject.AddComponent<GameOverMeterRow>();
            var so = new SerializedObject(row);
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("failOutline").objectReferenceValue = outline;
            so.FindProperty("icon").objectReferenceValue = iconImg;
            so.FindProperty("label").objectReferenceValue = labelText;
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("fillImage").objectReferenceValue = fillImg;
            so.FindProperty("track").objectReferenceValue = track;
            so.FindProperty("value").objectReferenceValue = value;
            so.FindProperty("meterColor").colorValue = color;
            so.ApplyModifiedPropertiesWithoutUndo();
            return row;
        }

        static Button PillButton(Transform parent, string name, string label, Color bg, Color fg, int size, float flex)
        {
            Image img = Panel(name, parent, pillSprite, bg, 1f);
            LE(img.gameObject, -1, 68, flex);
            Chunky(img.gameObject, 6);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            Text t = Txt("Label", img.transform, label, fontXB, size, fg, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
            return b;
        }

        // ------------------------------------------------------------------ UI helpers

        static GameObject UI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static RectTransform RT(GameObject go) => (RectTransform)go.transform;

        static Image Panel(string name, Transform parent, Sprite sprite, Color color, float ppuMultiplier)
        {
            GameObject go = UI(name, parent);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.pixelsPerUnitMultiplier = ppuMultiplier;
            img.color = color;
            return img;
        }

        static Image IconImg(string name, Transform parent, Sprite sprite, float size)
        {
            GameObject go = UI(name, parent);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            RT(go).sizeDelta = new Vector2(size, size);
            LE(go, size, size);
            return img;
        }

        /// <summary>Dark circle with an icon inside. Returns the icon Image.</summary>
        static Image IconBadge(Transform parent, Sprite icon, float size, float inset)
        {
            Image bg = Panel("Icon", parent, pillSprite, Card, 1f);
            bg.raycastTarget = false;
            LE(bg.gameObject, size, size);
            GameObject go = UI("Sprite", bg.transform);
            var img = go.AddComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true;
            img.raycastTarget = false;
            RectTransform rt = RT(go);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return img;
        }

        static Text Txt(string name, Transform parent, string text, Font font, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft)
        {
            GameObject go = UI(name, parent);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Rounded track with a fill child; returns the fill RectTransform (scale it via anchorMax.x).</summary>
        static RectTransform Bar(string name, Transform parent, float height, Color track, Color fill, float t, out Image trackImg, out Image fillImg)
        {
            trackImg = Panel(name, parent, pillSprite, track, 1f);
            trackImg.raycastTarget = false;
            LE(trackImg.gameObject, -1, height, 1);
            fillImg = Panel("Fill", trackImg.transform, pillSprite, fill, 1f);
            fillImg.raycastTarget = false;
            RectTransform rt = fillImg.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(t, 1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static LayoutElement LE(GameObject go, float w = -1, float h = -1, float flexW = -1, float flexH = -1)
        {
            if (!go.TryGetComponent(out LayoutElement le)) le = go.AddComponent<LayoutElement>();
            if (w >= 0) { le.preferredWidth = w; le.minWidth = w; }
            if (h >= 0) { le.preferredHeight = h; le.minHeight = h; }
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
            return le;
        }

        static RectOffset Pad(int l, int r, int t, int b) => new RectOffset(l, r, t, b);

        static HorizontalLayoutGroup HLG(GameObject go, float spacing, RectOffset pad, TextAnchor align,
            bool ctrlW = true, bool ctrlH = true, bool expW = false, bool expH = false)
        {
            var g = go.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing; g.padding = pad; g.childAlignment = align;
            g.childControlWidth = ctrlW; g.childControlHeight = ctrlH;
            g.childForceExpandWidth = expW; g.childForceExpandHeight = expH;
            return g;
        }

        static VerticalLayoutGroup VLG(GameObject go, float spacing, RectOffset pad, TextAnchor align,
            bool ctrlW = true, bool ctrlH = true, bool expW = true, bool expH = false)
        {
            var g = go.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing; g.padding = pad; g.childAlignment = align;
            g.childControlWidth = ctrlW; g.childControlHeight = ctrlH;
            g.childForceExpandWidth = expW; g.childForceExpandHeight = expH;
            return g;
        }

        /// <summary>The chunky drop shadow under every panel.</summary>
        static void Chunky(GameObject go, float distance)
        {
            var s = go.AddComponent<Shadow>();
            s.effectColor = ArcadePalette.ShadowCol;
            s.effectDistance = new Vector2(0, -distance);
        }

        static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void SetRefs(Object target, params (string field, Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in refs)
            {
                SerializedProperty p = so.FindProperty(field);
                if (p == null) { Debug.LogError($"Evergreen: {target.GetType().Name} has no field '{field}'"); continue; }
                p.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
