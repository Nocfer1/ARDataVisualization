using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;

public static class CreateTEMARScene
{
    private const int stationCount = 8;
    private const float stationSpacing = 0.045f;
    private const float sectionWidth = 0.12f;
    private static float[] layerThicknesses = { 0.025f, 0.035f, 0.05f, 0.07f };
    private static float[] resistivities = { 25f, 80f, 220f, 600f };
    private const float minResistivity = 10f;
    private const float maxResistivity = 1000f;
    private const float modelScale = 1f;

    private const string ScenePath = "Assets/Scenes/TEMARScene.unity";
    
    [MenuItem("TEM AR/Create or refresh AR scene")]
    public static void Create()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var sessionObject = new GameObject("AR Session");
        sessionObject.AddComponent<ARSession>();

        var origin = new GameObject("XR Origin (AR)");
        var xrOrigin = origin.AddComponent<XROrigin>();
        var planeManager = origin.AddComponent<ARPlaneManager>();
        planeManager.requestedDetectionMode = UnityEngine.XR.ARSubsystems.PlaneDetectionMode.Horizontal;
        var raycastManager = origin.AddComponent<ARRaycastManager>();

        var cameraObject = new GameObject("AR Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(origin.transform, false);
        cameraObject.transform.localPosition = Vector3.zero;
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        cameraObject.AddComponent<ARCameraManager>();
        cameraObject.AddComponent<ARCameraBackground>();
        xrOrigin.Camera = camera;

        var controller = origin.AddComponent<TEMARPlacementController>();
        var serialized = new SerializedObject(controller);
        serialized.FindProperty("raycastManager").objectReferenceValue = raycastManager;
        serialized.FindProperty("arCamera").objectReferenceValue = camera;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        var lightObject = new GameObject("Directional Light");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        EditorSceneManager.SaveScene(scene, ScenePath);
        SetBuildScenes();
        ConfigureAndroidPlayer();
        Debug.Log("TEM AR scene created at " + ScenePath);
    }

    private static void SetBuildScenes()
    {
        var existing = EditorBuildSettings.scenes;
        var scenes = new EditorBuildSettingsScene[existing.Length + 1];
        scenes[0] = new EditorBuildSettingsScene(ScenePath, true);
        int index = 1;
        foreach (var item in existing)
            if (item.path != ScenePath) scenes[index++] = item;
        if (index != scenes.Length) System.Array.Resize(ref scenes, index);
        EditorBuildSettings.scenes = scenes;
    }

    private static void ConfigureAndroidPlayer()
    {
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard);
    }
    
    [MenuItem("TEM AR/Create demo model")]
    private static void CreateDemoModel()
    {
        Transform modelRoot = new GameObject("TEM model (synthetic demo)").transform;
        float totalDepth = 0f;
        foreach (float thickness in layerThicknesses) totalDepth += thickness;
        float startX = -((stationCount - 1) * stationSpacing) * 0.5f;

        for (int station = 0; station < stationCount; station++)
        {
            float x = startX + station * stationSpacing;
            float variation = 0.78f + 0.22f * Mathf.Sin(station * 0.9f);
            float y = 0f;
            for (int layer = 0; layer < layerThicknesses.Length; layer++)
            {
                float thickness = layerThicknesses[layer];
                float rho = resistivities[Mathf.Min(layer, resistivities.Length - 1)] * variation;
                GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = $"Station_{station + 1}_Layer_{layer + 1}";
                block.transform.SetParent(modelRoot, false);
                block.transform.localPosition = new Vector3(x, -(y + thickness * 0.5f), 0f);
                block.transform.localScale = new Vector3(stationSpacing * 0.96f, thickness * 0.96f, sectionWidth);
                Renderer renderer = block.GetComponent<Renderer>();
                renderer.material.color = ResistivityColor(rho);
                y += thickness;
            }
        }
    }
    
    private static Color ResistivityColor(float value)
    {
        float min = Mathf.Max(minResistivity, 0.0001f);
        float max = Mathf.Max(maxResistivity, min + 0.0001f);
        float t = Mathf.InverseLerp(Mathf.Log10(min), Mathf.Log10(max), Mathf.Log10(Mathf.Max(value, 0.0001f)));
        return Color.Lerp(new Color(0.05f, 0.28f, 0.85f), new Color(0.95f, 0.2f, 0.08f), t);
    }
}
