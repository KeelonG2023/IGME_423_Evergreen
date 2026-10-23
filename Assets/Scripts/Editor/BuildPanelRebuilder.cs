using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Evergreen.Systems;
using Evergreen.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Evergreen.EditorTools
{
    /// <summary>
    /// Menu: Evergreen > Rebuild Build Panel (keeps your layout).
    ///
    /// 1. Saves a backup copy of the open scene to Assets/Scenes/Backups/ (including unsaved edits).
    /// 2. Finds the existing BuildMenu object and keeps it: its position, size, anchors, scale,
    ///    background Image and Shadow are not touched. Only its CHILDREN are rebuilt, as the
    ///    "folder tabs + click to expand" design, sized to fit whatever width the panel has now.
    /// 3. Swaps the eco icon to recycle and happiness to happyLittle everywhere in the scene.
    /// Nothing else in the scene is changed. Undo (Ctrl+Z) reverts it in one step.
    /// </summary>
    public static class BuildPanelRebuilder
    {
        const string ShapeDir = "Assets/Art/UI/Shapes/";
        const string FontDir = "Assets/Art/UI/Fonts/";
        const string BackupDir = "Assets/Scenes/Backups";
        const string RecyclePath = "Assets/Art/UI/Icons/recycle.png";
        const string HappyPath = "Assets/Art/UI/Emoji/happyLittle.png";
        const string BuildingDir = "Assets/Data/Buildings";

        /// <summary>Raise above 1 to make everything in the panel bigger still.</summary>
        const float ExtraScale = 1.3f;

        // Size multiplier: the panel is laid out as if it were 373px wide, then scaled to its real width,
        // so text and icons fill the panel instead of sitting small in the middle of it.
        static float s = 1f;
        static int R(float px) => Mathf.RoundToInt(px * s);

        static Font fontXB, fontB, fontM;
        static Sprite panelSprite, pillSprite, smallSprite, tabSprite, wellSprite, chevronSprite;

        static readonly Color Panel = ArcadePalette.Panel;
        static readonly Color CardCol = ArcadePalette.Card;
        static readonly Color TrackCol = ArcadePalette.Track;
        static readonly Color Muted = ArcadePalette.TextMuted;
        static readonly Color Well = ArcadePalette.Hex(0x151A19);

        static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>
        {
            { "Solar Farm", "Clean power from sunlight. Costs more up front than coal, but earns steadily and never pollutes." },
            { "Wind Turbine", "Clean power from the wind. A little noisy for neighbors, but cheap to run once it's up." },
            { "Coal Plant", "Cheap to build and very profitable, but it pollutes heavily and residents hate living near it." },
            { "Apartments", "Dense housing that fits lots of people on a small lot." },
            { "Green-Roof Homes", "Plant-covered roofs keep homes cooler and soak up rainwater. Lasts longer than regular housing." },
            { "Park", "Green space that cleans the air and lifts everyone's mood. Needs a little upkeep each year." },
            { "Recycling Center", "Turns the city's waste back into materials. Small running cost, big boost to eco score." },
            { "Transit Hub", "Buses and trains take cars off the road and make commutes easier." },
        };

        [MenuItem("Evergreen/Rebuild Build Panel (keeps your layout)")]
        public static void Rebuild()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Rebuild Build Panel", "Save the scene once first so a backup can be made.", "OK");
                return;
            }

            BuildMenu menu = Object.FindFirstObjectByType<BuildMenu>(FindObjectsInactive.Include);
            if (menu == null)
            {
                EditorUtility.DisplayDialog("Rebuild Build Panel", "No BuildMenu found in the open scene.", "OK");
                return;
            }

            // ---- 1. Backup (saves the current in-editor state, unsaved edits included)
            EnsureFolder(BackupDir);
            string backup = $"{BackupDir}/{scene.name}_before_build_panel_{DateTime.Now:yyyyMMdd_HHmmss}.unity";
            if (!EditorSceneManager.SaveScene(scene, backup, true))
            {
                EditorUtility.DisplayDialog("Rebuild Build Panel", "Couldn't save the backup, so nothing was changed.", "OK");
                return;
            }

            Undo.SetCurrentGroupName("Rebuild Build Panel");
            int undoGroup = Undo.GetCurrentGroup();

            // ---- 2. Assets
            panelSprite = PrepareShape("UI_RoundedPanel.png", 40);
            pillSprite = PrepareShape("UI_Pill.png", 63);
            smallSprite = PrepareShape("UI_RoundedSmall.png", 14);
            tabSprite = PrepareShape("UI_TabTop.png", 28);
            wellSprite = PrepareShape("UI_WellBottom.png", 28);
            chevronSprite = PrepareShape("UI_Chevron.png", 0);

            Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            fontXB = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "Rubik-ExtraBold.ttf") ?? fallback;
            fontB = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "Rubik-Bold.ttf") ?? fallback;
            fontM = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "Rubik-Medium.ttf") ?? fallback;

            var menuSo = new SerializedObject(menu);
            var icons = menuSo.FindProperty("icons").objectReferenceValue as ResourceIcons;
            if (icons == null) icons = AssetDatabase.LoadAssetAtPath<ResourceIcons>("Assets/Data/ResourceIcons.asset");
            if (icons == null)
            {
                EditorUtility.DisplayDialog("Rebuild Build Panel", "No ResourceIcons asset found (Assets/Data/ResourceIcons.asset).", "OK");
                return;
            }

            SwapResourceIcons(icons);
            FillDescriptions();

            List<BuildingDefinition> catalog = ReadCatalog(menuSo);

            // ---- 3. Rebuild the panel's children only
            GameObject panel = menu.gameObject;
            float panelWidth = ((RectTransform)panel.transform).rect.width;
            s = Mathf.Clamp(panelWidth / 373f, 1f, 2.5f) * ExtraScale;
            for (int i = panel.transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(panel.transform.GetChild(i).gameObject);

            if (!panel.TryGetComponent(out VerticalLayoutGroup vlg)) vlg = Undo.AddComponent<VerticalLayoutGroup>(panel);
            Undo.RecordObject(vlg, "Rebuild Build Panel");
            vlg.padding = Pad(16, 16, 20, 16);
            vlg.spacing = 0;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Header: title + current money
            GameObject header = UI("Header", panel.transform);
            HLG(header, 8, Pad(4, 4, 0, 14), TextAnchor.MiddleLeft);
            Text title = Txt("Title", header.transform, "Build", fontXB, 30, Color.white);
            LE(title.gameObject, -1, -1, 1);
            Image moneyPill = Img("Money", header.transform, smallSprite, CardCol, 1f);
            HLG(moneyPill.gameObject, 6, Pad(6, 12, 5, 5), TextAnchor.MiddleCenter);
            IconImg("Coin", moneyPill.transform, icons.money, 22);
            Text moneyText = Txt("Amount", moneyPill.transform, "$0", fontXB, 15, ArcadePalette.Amber);

            // Folder tabs
            GameObject tabRow = UI("Tabs", panel.transform);
            HLG(tabRow, 3, Pad(0, 0, 0, 0), TextAnchor.LowerLeft, true, true, true, false);
            LE(tabRow, -1, 46);
            var tabs = new List<(Button button, bool all, BuildingCategory cat, LayoutElement le, GameObject stripe)>();
            AddTab(tabs, tabRow.transform, "All", true, BuildingCategory.Energy);
            AddTab(tabs, tabRow.transform, "Energy", false, BuildingCategory.Energy);
            AddTab(tabs, tabRow.transform, "Homes", false, BuildingCategory.Housing);
            AddTab(tabs, tabRow.transform, "Nature", false, BuildingCategory.Nature);
            AddTab(tabs, tabRow.transform, "Services", false, BuildingCategory.Services);

            // List well (scrolls)
            Image well = Img("List", panel.transform, wellSprite, Well, 2f);
            LE(well.gameObject, -1, -1, -1, 1);
            well.gameObject.AddComponent<RectMask2D>();
            var scroll = well.gameObject.AddComponent<ScrollRect>();
            GameObject content = UI("Content", well.transform);
            RectTransform crt = RT(content);
            crt.anchorMin = new Vector2(0, 1);
            crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(0.5f, 1);
            crt.offsetMin = crt.offsetMax = Vector2.zero;
            VLG(content, 8, Pad(10, 10, 10, 10), TextAnchor.UpperLeft);
            var fit = content.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = crt;
            scroll.viewport = RT(well.gameObject);
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;

            BuildCard template = CardTemplate(content.transform, icons);
            template.gameObject.SetActive(false);

            // ---- 4. Wire the existing BuildMenu
            menuSo.Update();
            menuSo.FindProperty("icons").objectReferenceValue = icons;
            menuSo.FindProperty("cardParent").objectReferenceValue = content.transform;
            menuSo.FindProperty("cardTemplate").objectReferenceValue = template;
            menuSo.FindProperty("moneyText").objectReferenceValue = moneyText;
            SerializedProperty cat = menuSo.FindProperty("catalog");
            cat.arraySize = catalog.Count;
            for (int i = 0; i < catalog.Count; i++) cat.GetArrayElementAtIndex(i).objectReferenceValue = catalog[i];
            SerializedProperty tp = menuSo.FindProperty("tabs");
            tp.arraySize = tabs.Count;
            for (int i = 0; i < tabs.Count; i++)
            {
                SerializedProperty e = tp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("button").objectReferenceValue = tabs[i].button;
                e.FindPropertyRelative("showsAll").boolValue = tabs[i].all;
                e.FindPropertyRelative("category").enumValueIndex = (int)tabs[i].cat;
                e.FindPropertyRelative("layout").objectReferenceValue = tabs[i].le;
                e.FindPropertyRelative("activeIndicator").objectReferenceValue = tabs[i].stripe;
            }
            menuSo.FindProperty("tabActive").colorValue = Well;
            menuSo.FindProperty("tabActiveLabel").colorValue = ArcadePalette.Lime;
            menuSo.FindProperty("tabInactive").colorValue = CardCol;
            menuSo.FindProperty("tabInactiveLabel").colorValue = Muted;
            menuSo.FindProperty("activeTabHeight").floatValue = 46f * s;
            menuSo.FindProperty("inactiveTabHeight").floatValue = 40f * s;
            menuSo.ApplyModifiedProperties();

            FixPillLayouts();
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = panel;
            Debug.Log($"Evergreen: build panel rebuilt. Backup of the previous scene: {backup}. Save the scene (Ctrl+S) to keep the change, or Ctrl+Z to undo.");
        }

        // ------------------------------------------------------------------ pill fix

        /// <summary>
        /// A sliced Image reports its sprite's border size as its preferred size to layout groups.
        /// The pill sprite's borders are 126px, so any pill that sizes itself to its content
        /// (has a layout group) ballooned into a big circle. Swap those to sprites with small
        /// borders: Unity shrinks the borders to fit, so the ends still come out round.
        /// </summary>
        [MenuItem("Evergreen/Fix Stretched Pills In Scene")]
        public static void FixPillsMenu()
        {
            int n = FixPillLayouts();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"Evergreen: fixed {n} stretched pill(s). Save the scene to keep it.");
        }

        public static int FixPillLayouts()
        {
            Sprite pill = AssetDatabase.LoadAssetAtPath<Sprite>(ShapeDir + "UI_Pill.png");
            Sprite small = PrepareShape("UI_RoundedSmall.png", 14);
            Sprite large = PrepareShape("UI_RoundedPanel.png", 40);
            if (pill == null || small == null || large == null) return 0;

            int count = 0;
            foreach (Image img in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (img.sprite != pill || !img.TryGetComponent(out LayoutGroup _)) continue;
                Undo.RecordObject(img, "Fix Stretched Pills");
                // Self-sizing containers (speed controls) are tall, so they get the bigger corners.
                img.sprite = img.TryGetComponent(out ContentSizeFitter _) ? large : small;
                img.pixelsPerUnitMultiplier = 1f;
                count++;
            }
            return count;
        }

        // ------------------------------------------------------------------ content

        static void AddTab(List<(Button, bool, BuildingCategory, LayoutElement, GameObject)> tabs, Transform parent, string label, bool all, BuildingCategory cat)
        {
            Image img = Img("Tab " + label, parent, tabSprite, CardCol, 2f);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            LayoutElement le = LE(img.gameObject, 10, 40, 1);

            Text t = Txt("Label", img.transform, label, fontB, 11, Muted, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);

            Image stripe = Img("Active Stripe", img.transform, pillSprite, ArcadePalette.Lime, 1f);
            stripe.raycastTarget = false;
            RectTransform srt = stripe.rectTransform;
            srt.anchorMin = new Vector2(0, 1);
            srt.anchorMax = new Vector2(1, 1);
            srt.pivot = new Vector2(0.5f, 1);
            srt.offsetMin = new Vector2(10 * s, -3 * s);
            srt.offsetMax = new Vector2(-10 * s, 0);
            stripe.gameObject.SetActive(false);

            tabs.Add((b, all, cat, le, stripe.gameObject));
        }

        static BuildCard CardTemplate(Transform parent, ResourceIcons icons)
        {
            Image bg = Img("Card Template", parent, panelSprite, CardCol, 2.5f);
            var outline = bg.gameObject.AddComponent<Outline>();
            outline.effectColor = ArcadePalette.Lime;
            outline.effectDistance = new Vector2(3 * s, -3 * s);
            outline.enabled = false;
            VLG(bg.gameObject, 8, Pad(14, 14, 12, 12), TextAnchor.UpperLeft);

            // Header (click to expand)
            Image headerHit = Img("Header", bg.transform, null, new Color(0, 0, 0, 0), 1f);
            var headerButton = headerHit.gameObject.AddComponent<Button>();
            headerButton.targetGraphic = headerHit;
            headerButton.transition = Selectable.Transition.None;
            var nav = headerButton.navigation; nav.mode = Navigation.Mode.None; headerButton.navigation = nav;
            HLG(headerHit.gameObject, 12, Pad(0, 0, 0, 0), TextAnchor.MiddleLeft);

            Image badge = Img("Badge", headerHit.transform, pillSprite, TrackCol, 1f);
            badge.raycastTarget = false;
            LE(badge.gameObject, 52, 52);
            Image icon = Child("Icon", badge.transform, null, 6);
            Image lockImg = Child("Lock", badge.transform, icons.locked, 0);
            Place(lockImg.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(6, -6), new Vector2(26, 26));

            GameObject names = UI("Names", headerHit.transform);
            LE(names, 10, -1, 1); // small preferred width so the name wraps instead of squeezing the cost pill
            VLG(names, 2, Pad(0, 0, 0, 0), TextAnchor.MiddleLeft);
            var namesLayout = names.GetComponent<VerticalLayoutGroup>();
            namesLayout.childForceExpandWidth = false; // cost pill hugs its text
            Text nameText = Txt("Name", names.transform, "Building", fontXB, 17, Color.white);
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap; // long names wrap to two lines
            LE(nameText.gameObject, 10, -1, 1);

            // Cost sits under the name so the name gets the full row width.
            Image costPill = Img("Cost", names.transform, smallSprite, ArcadePalette.Amber, 1f);
            costPill.raycastTarget = false;
            HLG(costPill.gameObject, 0, Pad(12, 12, 5, 5), TextAnchor.MiddleCenter);
            Text costText = Txt("Label", costPill.transform, "$0", fontXB, 15, ArcadePalette.Hex(0x3A2600), TextAnchor.MiddleCenter);
            Text lifeText = Txt("Lifespan", names.transform, "0 yr lifespan", fontM, 12, Muted);

            Image chevron = Img("Chevron", headerHit.transform, chevronSprite, Muted, 1f);
            chevron.type = Image.Type.Simple;
            chevron.raycastTarget = false;
            LE(chevron.gameObject, 16, 16);

            // Collapsed yield chips
            GameObject chips = UI("Chips", bg.transform);
            HLG(chips, 6, Pad(0, 0, 0, 0), TextAnchor.MiddleLeft);
            Image chipBg = Img("Chip Template", chips.transform, smallSprite, TrackCol, 1.2f);
            chipBg.raycastTarget = false;
            HLG(chipBg.gameObject, 4, Pad(5, 9, 3, 3), TextAnchor.MiddleCenter);
            Image chipIcon = IconImg("Icon", chipBg.transform, icons.money, 18);
            Text chipText = Txt("Label", chipBg.transform, "+0", fontXB, 13, ArcadePalette.Lime);
            var chip = chipBg.gameObject.AddComponent<YieldChip>();
            SetRefs(chip, ("icon", chipIcon), ("label", chipText));

            // Expanded details
            GameObject details = UI("Details", bg.transform);
            VLG(details, 12, Pad(0, 0, 4, 2), TextAnchor.UpperLeft);

            Text desc = Txt("Description", details.transform, "Description", fontM, 14, Muted);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.lineSpacing = 1.1f;

            Txt("Per Year", details.transform, "EVERY YEAR", fontB, 11, Muted);
            GameObject row1 = UI("Yields Row 1", details.transform);
            HLG(row1, 6, Pad(0, 0, 0, 0), TextAnchor.MiddleLeft, true, true, true, false);
            YieldTile money = Tile(row1.transform, "Money");
            YieldTile eco = Tile(row1.transform, "Eco");
            GameObject row2 = UI("Yields Row 2", details.transform);
            HLG(row2, 6, Pad(0, 0, 0, 0), TextAnchor.MiddleLeft, true, true, true, false);
            YieldTile pop = Tile(row2.transform, "Population");
            YieldTile happy = Tile(row2.transform, "Happiness");

            Image box = Img("Lifetime", details.transform, smallSprite, Panel, 1.2f);
            box.raycastTarget = false;
            VLG(box.gameObject, 6, Pad(12, 12, 10, 10), TextAnchor.UpperLeft);
            Text lifeHeader = Txt("Header", box.transform, "OVER ITS LIFESPAN", fontB, 11, Muted);
            Text totals = Txt("Totals", box.transform, "", fontXB, 15, Color.white);
            totals.horizontalOverflow = HorizontalWrapMode.Wrap;
            Text payback = Txt("Payback", box.transform, "", fontB, 13, ArcadePalette.Lime);
            payback.horizontalOverflow = HorizontalWrapMode.Wrap;

            Image placeBg = Img("Place Button", details.transform, pillSprite, ArcadePalette.Lime, 1f);
            LE(placeBg.gameObject, -1, 48);
            var placeShadow = placeBg.gameObject.AddComponent<Shadow>();
            placeShadow.effectColor = ArcadePalette.Hex(0x5E9A2E);
            placeShadow.effectDistance = new Vector2(0, -5 * s);
            var placeButton = placeBg.gameObject.AddComponent<Button>();
            placeButton.targetGraphic = placeBg;
            var pnav = placeButton.navigation; pnav.mode = Navigation.Mode.None; placeButton.navigation = pnav;
            Text placeLabel = Txt("Label", placeBg.transform, "Place", fontXB, 17, ArcadePalette.Hex(0x1C3A08), TextAnchor.MiddleCenter);
            Stretch(placeLabel.rectTransform);

            details.SetActive(false);

            var card = bg.gameObject.AddComponent<BuildCard>();
            SetRefs(card,
                ("button", headerButton), ("icon", icon), ("nameText", nameText), ("costText", costText),
                ("costPill", costPill), ("lifespanText", lifeText), ("lockedOverlay", lockImg.gameObject),
                ("chevron", chevron.rectTransform), ("selectedOutline", outline),
                ("chipRow", chips.transform), ("chipTemplate", chip),
                ("details", details), ("descriptionText", desc),
                ("moneyTile", money), ("ecoTile", eco), ("populationTile", pop), ("happinessTile", happy),
                ("lifetimeHeader", lifeHeader), ("lifetimeTotals", totals), ("paybackText", payback),
                ("placeButton", placeButton), ("placeBackground", placeBg), ("placeLabel", placeLabel));
            return card;
        }

        static YieldTile Tile(Transform parent, string name)
        {
            Image bg = Img(name, parent, smallSprite, TrackCol, 1.2f);
            bg.raycastTarget = false;
            LE(bg.gameObject, 10, -1, 1);
            HLG(bg.gameObject, 6, Pad(8, 8, 7, 7), TextAnchor.MiddleLeft);
            Image icon = IconImg("Icon", bg.transform, null, 22);
            GameObject col = UI("Text", bg.transform);
            VLG(col, 0, Pad(0, 0, 0, 0), TextAnchor.MiddleLeft);
            Text value = Txt("Value", col.transform, "+0", fontXB, 16, ArcadePalette.Lime);
            Text label = Txt("Label", col.transform, "per yr", fontM, 11, Muted);
            var tile = bg.gameObject.AddComponent<YieldTile>();
            SetRefs(tile, ("icon", icon), ("valueText", value), ("labelText", label));
            return tile;
        }

        // ------------------------------------------------------------------ assets

        static void SwapResourceIcons(ResourceIcons icons)
        {
            Sprite recycle = PrepareIcon(RecyclePath, false);
            Sprite happy = PrepareIcon(HappyPath, true);
            Sprite oldEco = icons.eco, oldHappy = icons.happiness;

            Undo.RecordObject(icons, "Rebuild Build Panel");
            if (recycle != null) icons.eco = recycle;
            if (happy != null) icons.happiness = happy;
            EditorUtility.SetDirty(icons);

            // Swap the same sprites wherever they were already placed (top bar, tooltip, game over).
            foreach (Image img in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (recycle != null && oldEco != null && img.sprite == oldEco)
                {
                    Undo.RecordObject(img, "Rebuild Build Panel");
                    img.sprite = recycle;
                }
                else if (happy != null && oldHappy != null && img.sprite == oldHappy)
                {
                    Undo.RecordObject(img, "Rebuild Build Panel");
                    img.sprite = happy;
                }
            }
        }

        static Sprite PrepareIcon(string path, bool pixelArt)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null)
            {
                Debug.LogWarning($"Evergreen: {path} not found; icon left as is.");
                return null;
            }
            bool dirty = false;
            if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; dirty = true; }
            // recycle.png was auto-sliced into many sprites; one sprite for the whole image.
            if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (pixelArt && ti.filterMode != FilterMode.Point)
            {
                ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                dirty = true;
            }
            if (dirty) ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void FillDescriptions()
        {
            if (!AssetDatabase.IsValidFolder(BuildingDir)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:BuildingDefinition", new[] { BuildingDir }))
            {
                var def = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || !string.IsNullOrEmpty(def.description)) continue;
                if (Descriptions.TryGetValue(def.displayName, out string text))
                {
                    Undo.RecordObject(def, "Rebuild Build Panel");
                    def.description = text;
                    EditorUtility.SetDirty(def);
                }
            }
        }

        static List<BuildingDefinition> ReadCatalog(SerializedObject menuSo)
        {
            var list = new List<BuildingDefinition>();
            SerializedProperty cat = menuSo.FindProperty("catalog");
            for (int i = 0; i < cat.arraySize; i++)
                if (cat.GetArrayElementAtIndex(i).objectReferenceValue is BuildingDefinition d) list.Add(d);

            if (list.Count == 0 && AssetDatabase.IsValidFolder(BuildingDir))
                list = AssetDatabase.FindAssets("t:BuildingDefinition", new[] { BuildingDir })
                    .Select(g => AssetDatabase.LoadAssetAtPath<BuildingDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(d => d != null).ToList();
            return list;
        }

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

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ UI helpers

        static GameObject UI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Rebuild Build Panel");
            go.transform.SetParent(parent, false);
            return go;
        }

        static RectTransform RT(GameObject go) => (RectTransform)go.transform;

        static Image Img(string name, Transform parent, Sprite sprite, Color color, float ppuMultiplier)
        {
            GameObject go = UI(name, parent);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.pixelsPerUnitMultiplier = ppuMultiplier / s; // keeps corner radius in proportion
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
            RT(go).sizeDelta = new Vector2(size * s, size * s);
            LE(go, size, size);
            return img;
        }

        /// <summary>Icon image stretched inside its parent with an inset (not part of any layout).</summary>
        static Image Child(string name, Transform parent, Sprite sprite, float inset)
        {
            GameObject go = UI(name, parent);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            RectTransform rt = RT(go);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset * s, inset * s);
            rt.offsetMax = new Vector2(-inset * s, -inset * s);
            return img;
        }

        static Text Txt(string name, Transform parent, string text, Font font, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft)
        {
            GameObject go = UI(name, parent);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = R(size);
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        static LayoutElement LE(GameObject go, float w = -1, float h = -1, float flexW = -1, float flexH = -1)
        {
            if (!go.TryGetComponent(out LayoutElement le)) le = go.AddComponent<LayoutElement>();
            if (w >= 0) { le.preferredWidth = w * s; le.minWidth = Mathf.Min(w, 10) * s; }
            if (h >= 0) { le.preferredHeight = h * s; le.minHeight = h * s; }
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
            return le;
        }

        static RectOffset Pad(int l, int r, int t, int b) => new RectOffset(R(l), R(r), R(t), R(b));

        static void HLG(GameObject go, float spacing, RectOffset pad, TextAnchor align,
            bool ctrlW = true, bool ctrlH = true, bool expW = false, bool expH = false)
        {
            var g = go.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing * s; g.padding = pad; g.childAlignment = align;
            g.childControlWidth = ctrlW; g.childControlHeight = ctrlH;
            g.childForceExpandWidth = expW; g.childForceExpandHeight = expH;
        }

        static void VLG(GameObject go, float spacing, RectOffset pad, TextAnchor align)
        {
            var g = go.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing * s; g.padding = pad; g.childAlignment = align;
            g.childControlWidth = true; g.childControlHeight = true;
            g.childForceExpandWidth = true; g.childForceExpandHeight = false;
        }

        static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = pos * s; rt.sizeDelta = size * s;
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
