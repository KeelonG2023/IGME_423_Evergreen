using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Evergreen > Lighting Setup. Gives MainScene a bright, readable daytime look for the playtest:
/// warm sun with soft long-range shadows, a cool fill light, sky ambient, procedural sky, light haze,
/// post-processing (tonemapping, colour, soft bloom, vignette) and anti-aliasing.
/// Safe to re-run; it reuses the lights / volume it made before. Doesn't remove anything from the scene.
/// Tweak the look afterwards on the "Sun" light and Assets/Settings/Lighting/PlaytestVolume.
/// </summary>
public static class LightingSetup
{
    const string ScenePath = "Assets/Scenes/MainScene.unity";
    const string Folder = "Assets/Settings/Lighting";
    const string KitMaterialPath = "Assets/Art/Objects/Material.mat";

    [MenuItem("Evergreen/Lighting Setup")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureFolder(Folder);

        ConfigurePipeline();
        SoftenKitMaterial();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();

        // --- Lights
        Light sun = FindOrCreate<Light>(roots, "Sun", scene);
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.94f, 0.84f);
        sun.intensity = 1.45f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.75f;
        sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        RenderSettings.sun = sun;

        Light fill = FindOrCreate<Light>(roots, "Fill Light", scene);
        fill.type = LightType.Directional;
        fill.color = new Color(0.62f, 0.73f, 1f);
        fill.intensity = 0.35f;
        fill.shadows = LightShadows.None;
        fill.transform.rotation = Quaternion.Euler(30f, 150f, 0f);

        // --- Ambient, sky, fog
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.58f, 0.68f, 0.84f);
        RenderSettings.ambientEquatorColor = new Color(0.6f, 0.62f, 0.6f);
        RenderSettings.ambientGroundColor = new Color(0.34f, 0.31f, 0.27f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.skybox = SkyMaterial();
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.74f, 0.82f, 0.9f);
        RenderSettings.fogStartDistance = 400f;
        RenderSettings.fogEndDistance = 950f;

        // --- Post-processing volume
        Volume volume = FindOrCreate<Volume>(roots, "Global Volume", scene);
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.sharedProfile = VolumeProfileAsset();

        // --- Camera: sky background, post-processing on, anti-aliasing
        Camera cam = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c => c.CompareTag("MainCamera"))
                     ?? roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.Skybox;
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(cam);
        }
        else Debug.LogWarning("Lighting Setup: no camera found in MainScene.");

        // --- Ground: soft grass green so buildings stand out
        GameObject ground = roots.FirstOrDefault(r => r.name == "Plane (1)");
        if (ground != null && ground.TryGetComponent(out Renderer groundRenderer))
        {
            groundRenderer.sharedMaterial = LitMaterial("Ground", new Color(0.45f, 0.62f, 0.34f), 0.05f);
            groundRenderer.shadowCastingMode = ShadowCastingMode.Off;
            groundRenderer.receiveShadows = true;
        }

        DynamicGI.UpdateEnvironment();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Lighting Setup done. Tweak the 'Sun' light and Assets/Settings/Lighting/PlaytestVolume to taste.");
    }

    /// <summary>Shadows reach the whole map from the playtest camera height (~140 up) and are soft, plus MSAA.</summary>
    static void ConfigurePipeline()
    {
        var asset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (asset == null) { Debug.LogWarning("Lighting Setup: no URP asset in Graphics settings."); return; }

        var so = new SerializedObject(asset);
        void SetFloat(string name, float v) { var p = so.FindProperty(name); if (p != null) p.floatValue = v; }
        void SetInt(string name, int v) { var p = so.FindProperty(name); if (p != null) p.intValue = v; }
        void SetBool(string name, bool v) { var p = so.FindProperty(name); if (p != null) p.boolValue = v; }

        SetFloat("m_ShadowDistance", 600f);
        SetInt("m_ShadowCascadeCount", 4);
        SetBool("m_SoftShadowsSupported", true);
        SetInt("m_MainLightShadowmapResolution", 4096);
        SetFloat("m_ShadowDepthBias", 1f);
        SetFloat("m_ShadowNormalBias", 1f);
        SetInt("m_MSAA", 4);
        SetBool("m_SupportsHDR", true);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }

    /// <summary>The city kit material was half-glossy, which looks plastic under a sun. Make it matte.</summary>
    static void SoftenKitMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(KitMaterialPath);
        if (mat == null) return;
        mat.SetFloat("_Smoothness", 0.12f);
        mat.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(mat);
    }

    static VolumeProfile VolumeProfileAsset()
    {
        string path = $"{Folder}/PlaytestVolume.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        T Get<T>() where T : VolumeComponent
        {
            if (profile.TryGet(out T existing)) return existing;
            T added = profile.Add<T>(false);
            added.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(added, profile);
            return added;
        }

        var tone = Get<Tonemapping>();
        tone.mode.Override(TonemappingMode.Neutral);

        var color = Get<ColorAdjustments>();
        color.postExposure.Override(0.15f);
        color.contrast.Override(12f);
        color.saturation.Override(15f);

        var white = Get<WhiteBalance>();
        white.temperature.Override(6f);

        var bloom = Get<Bloom>();
        bloom.threshold.Override(1.1f);
        bloom.intensity.Override(0.25f);
        bloom.scatter.Override(0.6f);

        var vignette = Get<Vignette>();
        vignette.intensity.Override(0.18f);
        vignette.smoothness.Override(0.45f);

        EditorUtility.SetDirty(profile);
        return profile;
    }

    static Material SkyMaterial()
    {
        string path = $"{Folder}/Sky.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetFloat("_SunSize", 0.035f);
        mat.SetFloat("_AtmosphereThickness", 0.85f);
        mat.SetColor("_SkyTint", new Color(0.55f, 0.65f, 0.82f));
        mat.SetColor("_GroundColor", new Color(0.5f, 0.55f, 0.5f));
        mat.SetFloat("_Exposure", 1.2f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material LitMaterial(string name, Color color, float smoothness)
    {
        string path = $"{Folder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.color = color;
        mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static T FindOrCreate<T>(GameObject[] roots, string name, Scene scene) where T : Component
    {
        GameObject go = roots.FirstOrDefault(r => r.name == name);
        if (go == null)
        {
            go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
        }
        return go.TryGetComponent(out T existing) ? existing : go.AddComponent<T>();
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
