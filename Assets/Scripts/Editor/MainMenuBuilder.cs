using System.Collections.Generic;
using System.IO;
using Evergreen.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Editor-only. Evergreen > Build Main Menu Scene lays out the title + level select UI directly in
/// Assets/Scenes/MainMenu.unity (every object, including one button per level, is saved in the scene),
/// creates starter LevelData assets, wires references, and adds MainMenu as scene 0 in the build list.
/// Nothing is instantiated at runtime. Safe to re-run: it replaces the old menu canvas.
/// </summary>
public static class MainMenuBuilder
{
    const string ScenePath = "Assets/Scenes/MainMenu.unity";
    const string LevelFolder = "Assets/Data/Levels";
    const string CanvasName = "MainMenuCanvas";

    static readonly Color Bg = ArcadePalette.Hex(0x0F1F17);
    static readonly Color Card = ArcadePalette.Hex(0x173323);
    static readonly Color TextCol = ArcadePalette.Hex(0xEAF2E3);
    static readonly Color Muted = ArcadePalette.Hex(0xB9CDB4);
    static readonly Color Accent = ArcadePalette.Lime;

    static Font font;

    [MenuItem("Evergreen/Build Main Menu Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var old = GameObject.Find(CanvasName);
        if (old != null) Object.DestroyImmediate(old);
        EnsureEventSystem();
        StyleCamera();

        List<LevelData> levels = EnsureLevels();

        // ---- Canvas ----
        var canvasGo = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)canvasGo.transform;

        Stretch(Img(root, "Background", Bg).rectTransform);

        // ---- Title panel ----
        var title = Panel(root, "TitlePanel");
        var titleText = Label(title, "Title", "EVERGREEN", 180, FontStyle.Bold, TextCol);
        Place(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(1600, 220));
        var subtitle = Label(title, "Subtitle", "A SUSTAINABLE CITY BUILDER", 30, FontStyle.Bold, Accent);
        Place(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1200, 50));
        var begin = Label(title, "ClickToBegin", "CLICK TO BEGIN", 36, FontStyle.Bold, TextCol);
        Place(begin.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -200), new Vector2(800, 70));

        // ---- Level select panel ----
        var select = Panel(root, "LevelSelectPanel");
        var header = Label(select, "Header", "Choose a City Site", 64, FontStyle.Bold, TextCol);
        Place(header.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(1200, 90));

        var back = MakeButton(select, "BackButton", "BACK", Card, TextCol, 26);
        Place((RectTransform)back.transform, new Vector2(0, 1), new Vector2(80, -60), new Vector2(200, 64), new Vector2(0, 1));

        var grid = new GameObject("LevelGrid", typeof(RectTransform), typeof(GridLayoutGroup)).GetComponent<RectTransform>();
        grid.SetParent(select, false);
        Place(grid, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1680, 300));
        var gl = grid.GetComponent<GridLayoutGroup>();
        gl.cellSize = new Vector2(390, 260);
        gl.spacing = new Vector2(40, 40);
        gl.childAlignment = TextAnchor.MiddleCenter;
        gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gl.constraintCount = 4;

        var buttons = new List<LevelSelectButton>();
        for (int i = 0; i < levels.Count; i++) buttons.Add(CreateLevelButton(grid, levels[i], i + 1));

        var footer = Img(select, "Footer", Card);
        Place(footer.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 130), new Vector2(1680, 160));
        var selName = Label(footer.rectTransform, "SelectedName", levels[0].displayName, 40, FontStyle.Bold, TextCol);
        selName.alignment = TextAnchor.MiddleLeft;
        Place(selName.rectTransform, new Vector2(0, 0.5f), new Vector2(40, 28), new Vector2(1100, 56), new Vector2(0, 0.5f));
        var selDesc = Label(footer.rectTransform, "SelectedDescription", levels[0].description, 24, FontStyle.Normal, Muted);
        selDesc.alignment = TextAnchor.MiddleLeft;
        Place(selDesc.rectTransform, new Vector2(0, 0.5f), new Vector2(40, -28), new Vector2(1150, 44), new Vector2(0, 0.5f));
        var start = MakeButton(footer.rectTransform, "StartButton", "START BUILDING", Accent, Bg, 30);
        Place((RectTransform)start.transform, new Vector2(1, 0.5f), new Vector2(-40, 0), new Vector2(380, 84), new Vector2(1, 0.5f));

        // ---- Controller ----
        var controller = canvasGo.AddComponent<MainMenuController>();
        var so = new SerializedObject(controller);
        so.FindProperty("titlePanel").objectReferenceValue = title.GetComponent<CanvasGroup>();
        so.FindProperty("levelSelectPanel").objectReferenceValue = select.GetComponent<CanvasGroup>();
        so.FindProperty("clickToBeginText").objectReferenceValue = begin;
        so.FindProperty("backButton").objectReferenceValue = back;
        so.FindProperty("startButton").objectReferenceValue = start;
        so.FindProperty("selectedNameText").objectReferenceValue = selName;
        so.FindProperty("selectedDescriptionText").objectReferenceValue = selDesc;
        var list = so.FindProperty("levelButtons");
        list.arraySize = buttons.Count;
        for (int i = 0; i < buttons.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        select.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EnsureBuildSettings();
        Debug.Log("[Evergreen] Main menu built in " + ScenePath);
    }

    // ---------- Level buttons (saved in the scene) ----------

    static LevelSelectButton CreateLevelButton(RectTransform parent, LevelData data, int number)
    {
        var go = new GameObject($"LevelButton_{number:00}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(LevelSelectButton));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);

        var bg = go.GetComponent<Image>();
        bg.color = Card;
        var outline = go.GetComponent<Outline>();
        outline.effectColor = Accent;
        outline.effectDistance = new Vector2(5, -5);
        outline.enabled = false;

        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colors.disabledColor = Color.white;
        btn.colors = colors;

        var num = Label(rt, "Number", number.ToString("00"), 24, FontStyle.Bold, Muted);
        num.alignment = TextAnchor.MiddleLeft;
        Place(num.rectTransform, new Vector2(0, 1), new Vector2(24, -20), new Vector2(120, 36), new Vector2(0, 1));

        var diff = Label(rt, "Difficulty", data.difficulty.ToUpperInvariant(), 22, FontStyle.Bold, Accent);
        diff.alignment = TextAnchor.MiddleRight;
        Place(diff.rectTransform, new Vector2(1, 1), new Vector2(-24, -20), new Vector2(200, 36), new Vector2(1, 1));

        var name = Label(rt, "Name", data.displayName, 40, FontStyle.Bold, TextCol);
        name.alignment = TextAnchor.MiddleLeft;
        Place(name.rectTransform, new Vector2(0, 0.5f), new Vector2(24, -10), new Vector2(340, 110), new Vector2(0, 0.5f));

        var lockImg = Img(rt, "LockOverlay", new Color(0.04f, 0.08f, 0.06f, 0.7f));
        Stretch(lockImg.rectTransform);
        var lockText = Label(lockImg.rectTransform, "LockLabel", "LOCKED", 34, FontStyle.Bold, Muted);
        Stretch(lockText.rectTransform);
        lockImg.gameObject.SetActive(!data.unlockedByDefault && number != 1);

        var so = new SerializedObject(go.GetComponent<LevelSelectButton>());
        so.FindProperty("level").objectReferenceValue = data;
        so.FindProperty("background").objectReferenceValue = bg;
        so.FindProperty("nameText").objectReferenceValue = name;
        so.FindProperty("difficultyText").objectReferenceValue = diff;
        so.FindProperty("lockOverlay").objectReferenceValue = lockImg.gameObject;
        so.FindProperty("selectionOutline").objectReferenceValue = outline;
        so.ApplyModifiedPropertiesWithoutUndo();

        return go.GetComponent<LevelSelectButton>();
    }

    // ---------- Assets ----------

    static List<LevelData> EnsureLevels()
    {
        EnsureFolder(LevelFolder);
        var defs = new[]
        {
            ("level_01", "Riverside", "Easy", "Fertile banks and steady rain. Learn the basics of green building.", true),
            ("level_02", "Coastal Flats", "Normal", "Rising tides threaten low ground. Plan for flooding early.", false),
            ("level_03", "Highland Valley", "Hard", "Cold winters and steep terrain. Heating costs add up fast.", false),
            ("level_04", "Desert Basin", "Expert", "Scarce water, brutal heat. Every drop and shade tree counts.", false),
        };

        var result = new List<LevelData>();
        foreach (var d in defs)
        {
            string path = $"{LevelFolder}/Level_{d.Item1.Substring(6)}_{d.Item2.Replace(" ", "")}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<LevelData>();
                asset.levelId = d.Item1;
                asset.displayName = d.Item2;
                asset.difficulty = d.Item3;
                asset.description = d.Item4;
                asset.unlockedByDefault = d.Item5;
                asset.sceneName = "MainScene";
                AssetDatabase.CreateAsset(asset, path);
            }
            result.Add(asset);
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // ---------- Scene setup ----------

    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    static void StyleCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Bg;
    }

    static void EnsureBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ---------- UI helpers ----------

    static RectTransform Panel(RectTransform parent, string name)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform), typeof(CanvasGroup)).transform;
        rt.SetParent(parent, false);
        Stretch(rt);
        return rt;
    }

    static Image Img(RectTransform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static Text Label(RectTransform parent, string name, string text, int size, FontStyle style, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static Button MakeButton(RectTransform parent, string name, string label, Color fill, Color textColor, int size)
    {
        var img = Img(parent, name, fill);
        var btn = img.gameObject.AddComponent<Button>();
        Stretch(Label(img.rectTransform, "Label", label, size, FontStyle.Bold, textColor).rectTransform);
        return btn;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
