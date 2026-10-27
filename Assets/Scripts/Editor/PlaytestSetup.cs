using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Evergreen.Systems;
using Evergreen.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Evergreen > Playtest Setup. Safe to run more than once: it rebuilds the building prefabs,
/// links them to their BuildingDefinitions, fills the build menu catalog and wires MainScene
/// (placement system, EventSystem). It never deletes anything already in the scene.
/// </summary>
public static class PlaytestSetup
{
    const string ScenePath = "Assets/Scenes/MainScene.unity";
    const string KitPath = "Assets/Art/Objects/LowPoly_City_01Ready.fbx";
    const string KitMaterialPath = "Assets/Art/Objects/Material.mat";
    const string KitTexturePath = "Assets/Art/Objects/color_2048x2048.jpg";
    const string KitLibraryFolder = "Assets/Prefabs/CityKit";
    const string DataFolder = "Assets/Data/Buildings";
    const string PrefabFolder = "Assets/Prefabs/Buildings";
    const string MaterialFolder = "Assets/Materials/Buildings";

    /// <summary>Grid cells are 20 units, so 1-cell buildings are sized to leave a little gap.</summary>
    const float Footprint = 16f;

    /// <summary>
    /// THE size knob. Grid cell size in world units. The scene's Grid is set to this and every building
    /// is scaled with it (buildings are designed at 20-unit cells, then scaled by CellSize / 20).
    /// Change it and re-run Playtest Setup.
    /// </summary>
    const float CellSize = 30f;
    const float CellGap = 0.3f;
    const float DesignCellSize = 20f;
    static float BuildingScale => CellSize / DesignCellSize;

    /// <summary>Distance between neighbouring cell centres. Used to work out footprints.</summary>
    const float CellPitch = CellSize + CellGap;
    const float FootprintMargin = 1f;

    /// <summary>Kit scale: the 15 m kit bank comes out this many units wide. Everything else keeps its real size relative to it.</summary>
    const float BankWidth = 10.2f;
    static float kitScale = -1f;

    const string ShowcaseScenePath = "Assets/Scenes/CityKitShowcase.unity";

    static GameObject kit;
    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

    [MenuItem("Evergreen/Playtest Setup")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);
        materials.Clear();
        kit = AssetDatabase.LoadAssetAtPath<GameObject>(KitPath);
        kitScale = -1f;
        if (kit == null) Debug.LogWarning("Playtest Setup: city kit FBX not found, using primitive stand-ins.");

        SetupKitTexture();
        int libraryCount = BuildKitLibrary();

        Material ghost = GhostMaterial();

        // Existing definitions keep their numbers; only their prefab (and footprint) is filled in.
        // New ones are created once with starter numbers, then they're yours to rebalance.
        // City kit buildings are used exactly as split out (Prefabs/CityKit), nothing added to them.
        // Only the wind turbine and solar farm are built from primitives: the kit has no clean energy.
        var catalog = new List<BuildingDefinition>
        {
            // Energy
            UseKit(LoadDefinition("CoalPlant"), "Buildings", "factorybuilding_a"),
            Link("WindTurbine", BuildWindTurbine()),
            Link(NewDefinition("SolarFarm", "Solar Farm", BuildingCategory.Energy, "WindTurbine",
                    "Rows of panels that turn sunlight into steady, clean income. Pricey up front, almost no upkeep.",
                    cost: 1200f, yield: new ResourceYield(110f, 3f, 0f, 0f), lifespan: 25,
                    tint: new Color(0.35f, 0.55f, 0.95f)), BuildSolarFarm()),
            // Housing
            UseKit(NewDefinition("House", "House", BuildingCategory.Housing, "Apartments",
                    "A single family home. Cheap and cosy, but it takes a lot of land per person.",
                    cost: 400f, yield: new ResourceYield(20f, -1f, 10f, 1f), lifespan: 40,
                    tint: new Color(0.95f, 0.75f, 0.5f)), "Buildings", "house_01"),
            UseKit(LoadDefinition("Apartments"), "Buildings", "highlivingbuilding_a"),
            UseKit(NewDefinition("GreenApartments", "Green Apartments", BuildingCategory.Housing, "Apartments",
                    "Better insulated apartments. Costs more, but lower bills and happier tenants.",
                    cost: 1300f, yield: new ResourceYield(70f, 1f, 40f, 2f), lifespan: 60,
                    tint: new Color(0.45f, 0.8f, 0.4f)), "Buildings", "highlivingbuilding_d"),
            // Nature
            UseKit(NewDefinition("Trees", "Trees", BuildingCategory.Nature, "Park",
                    "Plant a tree. Costs almost nothing, cleans the air, and pays you back more every year it grows.",
                    cost: 50f, yield: new ResourceYield(-2f, 1f, 0f, 0.5f), lifespan: 60,
                    tint: new Color(0.3f, 0.65f, 0.3f)), "Nature", "tree"),
            UseKit(NewDefinition("PocketPark", "Pocket Park", BuildingCategory.Nature, "Park",
                    "A small square of green between buildings. Cheap, and people love it.",
                    cost: 150f, yield: new ResourceYield(-15f, 3f, 0f, 2f), lifespan: 80,
                    tint: new Color(0.4f, 0.75f, 0.35f)), "Nature", "park_c"),
            UseKit(LoadDefinition("Park"), "Nature", "park_a"),
            // Services
            UseKit(NewDefinition("School", "School", BuildingCategory.Services, "Apartments",
                    "Costs money to run, but families move in and the city gets happier.",
                    cost: 1200f, yield: new ResourceYield(-60f, 0f, 15f, 4f), lifespan: 60,
                    tint: new Color(0.95f, 0.85f, 0.4f)), "Buildings", "school_a"),
            UseKit(NewDefinition("Hospital", "Hospital", BuildingCategory.Services, "Apartments",
                    "Expensive to run, but a healthy city grows and stays happy.",
                    cost: 1800f, yield: new ResourceYield(-90f, 0f, 20f, 6f), lifespan: 60,
                    tint: new Color(0.95f, 0.5f, 0.5f)), "Buildings", "hospital_a"),
            UseKit(NewDefinition("OfficeTower", "Office Tower", BuildingCategory.Services, "CoalPlant",
                    "Big business brings big tax money, plus traffic and a bit of smog.",
                    cost: 2000f, yield: new ResourceYield(250f, -6f, 0f, 0f), lifespan: 60,
                    tint: new Color(0.6f, 0.7f, 0.85f)), "Buildings", "skyscraper_a"),
        };
        RemoveUnusedGeneratedPrefabs("WindTurbine", "SolarFarm");
        catalog.RemoveAll(d => d == null);

        AssetDatabase.SaveAssets();
        SetupScene(catalog, ghost);
        AssetDatabase.SaveAssets();
        Debug.Log($"Playtest Setup done: {catalog.Count} buildings in the build menu, {libraryCount} city kit prefabs in {KitLibraryFolder}. " +
                  "Press Play, pick a card, click Place, then click the ground (R rotates).");
    }

    // ---------------------------------------------------------------- Scene

    static void SetupScene(List<BuildingDefinition> catalog, Material ghost)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var all = scene.GetRootGameObjects();
        T Find<T>() where T : Component => all.SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault();

        BuildMenu menu = Find<BuildMenu>();
        ResourceManager resources = Find<ResourceManager>();
        Grid grid = Find<Grid>();
        GridPlacement placement = Find<GridPlacement>();
        Camera cam = all.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c => c.CompareTag("MainCamera"))
                     ?? Find<Camera>();

        if (menu == null) Debug.LogError("Playtest Setup: no BuildMenu in MainScene (is the HUD prefab there?).");
        if (grid == null) Debug.LogError("Playtest Setup: no Grid in MainScene (is BuildingGrid there?).");

        if (grid != null)
        {
            var gridSo = new SerializedObject(grid);
            gridSo.FindProperty("m_CellSize").vector3Value = new Vector3(CellSize, CellSize, 0f);
            gridSo.FindProperty("m_CellGap").vector3Value = new Vector3(CellGap, CellGap, 0f);
            gridSo.ApplyModifiedPropertiesWithoutUndo();
        }

        if (placement == null && grid != null) placement = Undo.AddComponent<GridPlacement>(grid.gameObject);

        if (placement != null)
        {
            var so = new SerializedObject(placement);
            so.FindProperty("buildMenu").objectReferenceValue = menu;
            so.FindProperty("resources").objectReferenceValue = resources;
            so.FindProperty("grid").objectReferenceValue = grid;
            so.FindProperty("worldCamera").objectReferenceValue = cam;
            so.FindProperty("ghostMaterial").objectReferenceValue = ghost;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        if (menu != null)
        {
            var so = new SerializedObject(menu);
            SerializedProperty list = so.FindProperty("catalog");
            list.arraySize = catalog.Count;
            for (int i = 0; i < catalog.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = catalog[i];
            if (so.FindProperty("resources").objectReferenceValue == null)
                so.FindProperty("resources").objectReferenceValue = resources;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // UI buttons do nothing without an EventSystem.
        if (Find<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            SceneManager.MoveGameObjectToScene(es, scene);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // ---------------------------------------------------------------- Definitions

    static BuildingDefinition Link(string assetName, GameObject prefab) => Link(LoadDefinition(assetName), prefab);

    static BuildingDefinition Link(BuildingDefinition def, GameObject prefab)
    {
        if (def == null || prefab == null) return def;

        // If you've pointed this definition at a different prefab (e.g. one from CityKit), keep your choice.
        string currentPath = def.prefab != null ? AssetDatabase.GetAssetPath(def.prefab) : null;
        if (currentPath != null && !currentPath.StartsWith(PrefabFolder + "/"))
        {
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(prefab));
            def.footprint = FootprintOf(def.prefab);
            EditorUtility.SetDirty(def);
            return def;
        }
        def.prefab = prefab;
        def.footprint = FootprintOf(prefab);
        EditorUtility.SetDirty(def);

        // Point the prefab's BuildingYield at its definition so name / yields / lifespan come from the data.
        GameObject contents = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));
        var so = new SerializedObject(contents.GetComponent<BuildingYield>());
        so.FindProperty("definition").objectReferenceValue = def;
        so.FindProperty("displayName").stringValue = def.displayName;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(contents, AssetDatabase.GetAssetPath(prefab));
        PrefabUtility.UnloadPrefabContents(contents);
        return def;
    }

    /// <summary>
    /// Points a definition at a CityKit prefab, untouched. Keeps a prefab you've since swapped in yourself
    /// (anything outside Prefabs/Buildings), so re-running never overwrites your picks.
    /// </summary>
    static BuildingDefinition UseKit(BuildingDefinition def, string category, string kitNode)
    {
        if (def == null) return null;
        string path = $"{KitLibraryFolder}/{category}/{SafeFileName(kitNode)}.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"Playtest Setup: {path} not found (run Evergreen > City Kit > Split Into Prefabs).");
            return def;
        }

        // Only replace an empty slot or one of this script's old generated prefabs.
        string currentPath = def.prefab != null ? AssetDatabase.GetAssetPath(def.prefab) : null;
        if (currentPath == null || currentPath.StartsWith(PrefabFolder + "/")) def.prefab = prefab;
        def.footprint = FootprintOf(def.prefab);
        EditorUtility.SetDirty(def);
        return def;
    }

    /// <summary>Deletes building prefabs this script made earlier that nothing uses any more.</summary>
    static void RemoveUnusedGeneratedPrefabs(params string[] keep)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!keep.Contains(Path.GetFileNameWithoutExtension(path))) AssetDatabase.DeleteAsset(path);
        }
    }

    static BuildingDefinition LoadDefinition(string assetName)
    {
        var def = AssetDatabase.LoadAssetAtPath<BuildingDefinition>($"{DataFolder}/{assetName}.asset");
        if (def == null) Debug.LogWarning($"Playtest Setup: {DataFolder}/{assetName}.asset not found, skipped.");
        return def;
    }

    /// <summary>Creates the definition the first time only. After that it's yours to rebalance in the Inspector.</summary>
    static BuildingDefinition NewDefinition(string assetName, string displayName, BuildingCategory category, string iconFrom,
        string description, float cost, ResourceYield yield, int lifespan, Color tint)
    {
        string path = $"{DataFolder}/{assetName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
        if (existing != null) return existing;

        var def = ScriptableObject.CreateInstance<BuildingDefinition>();
        def.displayName = displayName;
        def.category = category;
        def.description = description;
        def.cost = cost;
        def.yearlyYield = yield;
        def.lifespanYears = lifespan;
        def.tileColor = tint;
        BuildingDefinition iconSource = AssetDatabase.LoadAssetAtPath<BuildingDefinition>($"{DataFolder}/{iconFrom}.asset");
        if (iconSource != null) def.icon = iconSource.icon;
        AssetDatabase.CreateAsset(def, path);
        return def;
    }

    // ---------------------------------------------------------------- Buildings


    static GameObject BuildWindTurbine()
    {
        GameObject root = new GameObject("WindTurbine");
        const float towerHeight = 30f;

        GameObject pad = Primitive(PrimitiveType.Cylinder, "Pad", "Concrete", root.transform);
        pad.transform.localScale = new Vector3(7f, 0.3f, 7f);
        pad.transform.localPosition = new Vector3(0, 0.3f, 0);

        GameObject tower = Primitive(PrimitiveType.Cylinder, "Tower", "Turbine", root.transform);
        tower.transform.localScale = new Vector3(1.6f, towerHeight * 0.5f, 1.6f);
        tower.transform.localPosition = new Vector3(0, towerHeight * 0.5f, 0);

        GameObject nacelle = Primitive(PrimitiveType.Cube, "Nacelle", "Turbine", root.transform);
        nacelle.transform.localScale = new Vector3(2.2f, 2.2f, 5f);
        nacelle.transform.localPosition = new Vector3(0, towerHeight, 0.4f);

        var rotor = new GameObject("Rotor");
        rotor.transform.SetParent(root.transform, false);
        rotor.transform.localPosition = new Vector3(0, towerHeight, -2.4f);
        var spinner = rotor.AddComponent<Spinner>();
        var spinnerSo = new SerializedObject(spinner);
        spinnerSo.FindProperty("degreesPerSecond").floatValue = 70f;
        spinnerSo.ApplyModifiedPropertiesWithoutUndo();

        GameObject hub = Primitive(PrimitiveType.Sphere, "Hub", "Turbine", rotor.transform);
        hub.transform.localScale = Vector3.one * 2f;

        for (int i = 0; i < 3; i++)
        {
            var arm = new GameObject("BladeArm");
            arm.transform.SetParent(rotor.transform, false);
            arm.transform.localRotation = Quaternion.Euler(0, 0, i * 120f);
            GameObject blade = Primitive(PrimitiveType.Cube, "Blade", "Turbine", arm.transform);
            blade.transform.localScale = new Vector3(1f, 8.6f, 0.3f);
            blade.transform.localPosition = new Vector3(0, 5f, 0); // tip ~9.3 from hub: fits a 20-unit cell
        }
        return SavePrefab(root);
    }

    static GameObject BuildSolarFarm()
    {
        GameObject root = new GameObject("SolarFarm");
        GameObject ground = Primitive(PrimitiveType.Cube, "Gravel", "Gravel", root.transform);
        ground.transform.localScale = new Vector3(Footprint + 2f, 0.3f, Footprint + 2f);
        ground.transform.localPosition = new Vector3(0, 0.15f, 0);

        const int rows = 3, cols = 3;
        const float spacingX = 5.8f, spacingZ = 5.6f;
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            var mount = new GameObject("Panel");
            mount.transform.SetParent(root.transform, false);
            mount.transform.localPosition = new Vector3((c - 1) * spacingX, 0, (r - 1) * spacingZ);

            GameObject post = Primitive(PrimitiveType.Cylinder, "Post", "Metal", mount.transform);
            post.transform.localScale = new Vector3(0.35f, 1f, 0.35f);
            post.transform.localPosition = new Vector3(0, 1f, 0);

            GameObject panel = Primitive(PrimitiveType.Cube, "Panel", "Solar", mount.transform);
            panel.transform.localScale = new Vector3(5f, 0.2f, 3.6f);
            panel.transform.localPosition = new Vector3(0, 2.1f, 0);
            panel.transform.localRotation = Quaternion.Euler(-25f, 0, 0);
        }
        return SavePrefab(root);
    }



    // ---------------------------------------------------------------- City kit

    /// <summary>The kit's material had a broken texture link and the atlas was imported as a Sprite. Fix both.</summary>
    static void SetupKitTexture()
    {
        if (AssetImporter.GetAtPath(KitTexturePath) is TextureImporter importer)
        {
            bool changed = importer.textureType != TextureImporterType.Default || !importer.mipmapEnabled || !importer.sRGBTexture;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            if (changed) importer.SaveAndReimport();
        }

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(KitTexturePath);
        var material = AssetDatabase.LoadAssetAtPath<Material>(KitMaterialPath);
        if (texture == null || material == null)
        {
            Debug.LogWarning("Playtest Setup: city kit texture or material missing, kit will be untextured.");
            return;
        }
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        material.mainTexture = texture;
        material.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(material);

        // Make sure the FBX's material slot points at this material (it already should).
        if (AssetImporter.GetAtPath(KitPath) is ModelImporter model)
        {
            var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), "Material");
            if (!model.GetExternalObjectMap().TryGetValue(id, out Object mapped) || mapped != material)
            {
                model.AddRemap(id, material);
                model.SaveAndReimport();
                kit = AssetDatabase.LoadAssetAtPath<GameObject>(KitPath);
            }
        }
    }

    [MenuItem("Evergreen/City Kit/Split Into Prefabs")]
    public static void SplitKitMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        kit = AssetDatabase.LoadAssetAtPath<GameObject>(KitPath);
        kitScale = -1f;
        materials.Clear();
        if (kit == null) { Debug.LogError($"City Kit: {KitPath} not found."); return; }
        SetupKitTexture();
        int count = BuildKitLibrary();
        AssetDatabase.SaveAssets();
        Debug.Log($"City Kit: {count} textured prefabs in {KitLibraryFolder}. Open {ShowcaseScenePath} to browse them.");
    }

    /// <summary>
    /// Saves every distinct object in the city kit as its own textured prefab, sorted into
    /// CityKit/Buildings, Nature, Roads, Vehicles, Props and Terrain. Duplicates in the FBX
    /// (name.001, name.002...) are skipped. Each prefab sits on its base, has a click collider
    /// and a BuildingYield, so any of them can be dropped into a BuildingDefinition's prefab slot.
    /// Also lays them all out in a labelled showcase scene.
    /// </summary>
    static int BuildKitLibrary()
    {
        if (kit == null) return 0;
        EnsureFolder(KitLibraryFolder);
        RemoveOldFlatLibrary();

        var seen = new HashSet<string>();
        var made = new List<(string category, GameObject prefab)>();
        foreach (Transform node in kit.GetComponentsInChildren<Transform>(true))
        {
            if (node == kit.transform || node.GetComponent<MeshFilter>() == null) continue;
            string baseName = Regex.Replace(node.name, @"\.\d+$", "");
            if (!seen.Add(baseName)) continue;

            string category = CategoryOf(baseName);
            string folder = $"{KitLibraryFolder}/{category}";
            EnsureFolder(folder);

            GameObject model = CopyKitNode(node, baseName);
            GameObject root = new GameObject(SafeFileName(baseName));
            FitKit(model, root);
            made.Add((category, SavePrefab(root, folder, baseName)));
        }

        // Remove prefabs left in a folder they no longer belong to (e.g. after a category fix).
        var madePaths = new HashSet<string>(made.Select(m => AssetDatabase.GetAssetPath(m.prefab)));
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { KitLibraryFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!madePaths.Contains(path)) AssetDatabase.DeleteAsset(path);
        }

        BuildShowcaseScene(made);
        return made.Count;
    }

    static GameObject CopyKitNode(Transform src, string name)
    {
        GameObject copy = Object.Instantiate(src.gameObject);
        copy.name = name;
        copy.transform.SetPositionAndRotation(Vector3.zero, src.rotation);
        copy.transform.localScale = src.lossyScale;
        foreach (Collider c in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        return copy;
    }

    static string CategoryOf(string n)
    {
        n = n.ToLowerInvariant();
        if (Regex.IsMatch(n, @"^(road|parking|bridge|streetsign|crosswalk|sidewalk)")) return "Roads";
        if (Regex.IsMatch(n, @"^(car_|jeep|truck|tractor|bus(_|$)|airballoon|cart|bike|boat)")) return "Vehicles";
        if (Regex.IsMatch(n, @"^(green_|water|sand|landscape|land$|wall$)")) return "Terrain";
        if (Regex.IsMatch(n, @"^(tree|park_|bush|grass|lawn$|basketballcourt)")) return "Nature";
        if (Regex.IsMatch(n, @"(_mesh$|^props|^bench|^barrel|^fence|^crate|^mailbox|^gate|^logs|^timber|^lawn_mower|^table|^chair|^lamp|^farmstractures)")) return "Props";
        return "Buildings";
    }

    static string SafeFileName(string n)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
        return n.Replace('.', '_');
    }

    /// <summary>The first version saved a hand-picked set straight into CityKit/. Clear those so nothing is doubled up.</summary>
    static void RemoveOldFlatLibrary()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { KitLibraryFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetDirectoryName(path).Replace('\\', '/') == KitLibraryFolder) AssetDatabase.DeleteAsset(path);
        }
    }

    /// <summary>One row per category, every prefab labelled with its name. For browsing only.</summary>
    static void BuildShowcaseScene(List<(string category, GameObject prefab)> made)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.GetComponent<Renderer>().sharedMaterial = Mat("Gravel");

        const float gap = 6f;
        float z = 0f, widestRow = 0f;
        foreach (var group in made.GroupBy(m => m.category))
        {
            float x = 0f, rowDepth = 0f;
            Label($"{group.Key} ({group.Count()})", new Vector3(-30f, 0.5f, z), 6f, Color.black);
            foreach (var (_, prefab) in group)
            {
                var box = prefab.GetComponent<BoxCollider>();
                Vector3 size = box != null ? Vector3.Scale(box.size, prefab.transform.localScale) : Vector3.one * 10f;
                size = Vector3.Min(size, Vector3.one * 400f); // huge terrain pieces shouldn't push the row out forever
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.position = new Vector3(x + size.x * 0.5f, 0f, z + size.z * 0.5f);
                Label(prefab.name, new Vector3(x + size.x * 0.5f, size.y + 2f, z), 2.5f, Color.white);
                x += size.x + gap;
                rowDepth = Mathf.Max(rowDepth, size.z);
            }
            widestRow = Mathf.Max(widestRow, x);
            z += rowDepth + gap * 4f;
        }
        ground.transform.position = new Vector3(widestRow * 0.5f - 40f, -0.01f, z * 0.5f - 20f);
        ground.transform.localScale = new Vector3(widestRow / 10f + 12f, 1f, z / 10f + 8f);

        EditorSceneManager.SaveScene(scene, ShowcaseScenePath);
    }

    static void Label(string text, Vector3 position, float size, Color color)
    {
        var go = new GameObject("Label - " + text);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
        var tm = go.AddComponent<TextMesh>();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            tm.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
        tm.text = text;
        tm.characterSize = size * 0.1f;
        tm.fontSize = 64;
        tm.anchor = TextAnchor.LowerCenter;
        tm.color = color;
    }

    /// <summary>Uniform scale for the whole kit, so a skyscraper stays bigger than a house.</summary>
    static float KitScale()
    {
        if (kitScale > 0f) return kitScale;
        kitScale = 1f;
        GameObject bank = KitModel("bank");
        if (bank != null)
        {
            Bounds b = RendererBounds(bank);
            float widest = Mathf.Max(b.size.x, b.size.z);
            if (widest > 0.0001f) kitScale = BankWidth / widest;
            Object.DestroyImmediate(bank);
        }
        return kitScale;
    }

    /// <summary>Like Fit, but with the kit-wide scale instead of squeezing into one cell.</summary>
    static void FitKit(GameObject model, GameObject root)
    {
        float scale = KitScale();
        model.transform.SetParent(root.transform, true);
        model.transform.localScale *= scale;
        Bounds b = RendererBounds(model);
        model.transform.position += new Vector3(-b.center.x, -b.min.y, -b.center.z);
    }

    /// <summary>Grid cells (x by z) a prefab needs, from its collider.</summary>
    static Vector2Int FootprintOf(GameObject prefab)
    {
        var box = prefab.GetComponent<BoxCollider>();
        if (box == null) return Vector2Int.one;
        Vector3 size = Vector3.Scale(box.size, prefab.transform.localScale); // localScale = BuildingScale
        return new Vector2Int(
            Mathf.Max(1, Mathf.CeilToInt((size.x + FootprintMargin * BuildingScale) / CellPitch)),
            Mathf.Max(1, Mathf.CeilToInt((size.z + FootprintMargin * BuildingScale) / CellPitch)));
    }

    // ---------------------------------------------------------------- Building helpers

    /// <summary>Copies one object (and its children) out of the city kit FBX, keeping how it looks in the kit.</summary>
    static GameObject KitModel(string nodeName)
    {
        if (kit == null) return null;
        Transform[] nodes = kit.GetComponentsInChildren<Transform>(true);
        Transform src = nodes.FirstOrDefault(t => t.name == nodeName)
                        ?? nodes.FirstOrDefault(t => t.name.StartsWith(nodeName));
        if (src == null)
        {
            Debug.LogWarning($"Playtest Setup: '{nodeName}' not found in the city kit, using a box instead.");
            return null;
        }
        GameObject copy = Object.Instantiate(src.gameObject);
        copy.name = nodeName;
        copy.transform.SetPositionAndRotation(Vector3.zero, src.rotation);
        copy.transform.localScale = src.lossyScale;
        foreach (Collider c in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        return copy;
    }

    /// <summary>Parents the model under root, scales it to fit the footprint, and sits it on the root's origin.</summary>
    static void Fit(GameObject model, GameObject root, float footprint)
    {
        model.transform.SetParent(root.transform, true);
        Bounds b = RendererBounds(model);
        float widest = Mathf.Max(b.size.x, b.size.z);
        if (widest > 0.0001f) model.transform.localScale *= footprint / widest;
        b = RendererBounds(model);
        model.transform.position += new Vector3(-b.center.x, -b.min.y, -b.center.z) + root.transform.position;
    }

    static GameObject SavePrefab(GameObject root, string folder = PrefabFolder, string displayName = null)
    {
        // One box collider over the whole thing: used for clicking (tooltip) and to block re-placement raycasts.
        Bounds b = RendererBounds(root);
        var box = root.AddComponent<BoxCollider>();
        box.center = b.center - root.transform.position;
        box.size = b.size;

        // Everything is modelled for 20-unit cells; scale the whole building to the current cell size.
        root.transform.localScale = Vector3.one * BuildingScale;
        BuildingYield yield = root.AddComponent<BuildingYield>();
        if (displayName != null)
        {
            var so = new SerializedObject(yield);
            so.FindProperty("displayName").stringValue = displayName;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        string path = $"{folder}/{root.name}.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject Primitive(PrimitiveType type, string name, string material, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = Mat(material);
        go.transform.SetParent(parent, false);
        return go;
    }

    static GameObject Box(string name, string material, Vector3 size)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = Mat(material);
        go.transform.localScale = size;
        return go;
    }

    static Bounds RendererBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    // ---------------------------------------------------------------- Materials

    static Material Mat(string name)
    {
        if (materials.TryGetValue(name, out Material cached)) return cached;

        (Color color, float smoothness) = name switch
        {
            "Turbine" => (new Color(0.94f, 0.95f, 0.96f), 0.4f),
            "Metal" => (new Color(0.55f, 0.57f, 0.6f), 0.5f),
            "Solar" => (new Color(0.08f, 0.16f, 0.38f), 0.85f),
            "Gravel" => (new Color(0.62f, 0.6f, 0.55f), 0.1f),
            "Grass" => (new Color(0.36f, 0.66f, 0.3f), 0.1f),
            "Leaves" => (new Color(0.25f, 0.55f, 0.25f), 0.1f),
            "Brick" => (new Color(0.55f, 0.3f, 0.25f), 0.15f),
            _ => (new Color(0.75f, 0.75f, 0.72f), 0.2f), // Concrete
        };

        string path = $"{MaterialFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(lit);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.color = color;
        mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        materials[name] = mat;
        return mat;
    }

    static Material GhostMaterial()
    {
        string path = $"{MaterialFolder}/Ghost.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            mat = new Material(unlit);
            AssetDatabase.CreateAsset(mat, path);
        }
        GridPlacement.ConfigureTransparent(mat);
        mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.45f));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
