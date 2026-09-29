using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;

public static class CreateTEMARScene
{
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
}
