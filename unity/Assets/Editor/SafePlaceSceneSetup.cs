using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SafePlaceSceneSetup
{
    private const string ScenePath = "Assets/Scenes/SafePlaceIntroScene.unity";

    [MenuItem("Tools/VR Art Therapy/Create Safe Place Intro Scene")]
    public static void CreateSafePlaceIntroScene()
    {
        Directory.CreateDirectory("Assets/Scenes");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject cameraGO = new GameObject("Main Camera");
        Camera camera = cameraGO.AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.Skybox;
        cameraGO.transform.position = new Vector3(0f, 0f, -2f);

        GameObject lightGO = new GameObject("Directional Light");
        Light light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.65f;
        lightGO.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

        GameObject managerGO = new GameObject("SafePlaceIntroManager");
        managerGO.AddComponent<SafePlaceIntroManager>();

        EnsurePersistentManagers();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath, 1);
        Debug.Log($"[SafePlaceSceneSetup] Created and registered {ScenePath}");
    }

    [MenuItem("Tools/VR Art Therapy/Register Safe Place Intro Scene In Build")]
    public static void RegisterSafePlaceIntroScene()
    {
        AddSceneToBuildSettings(ScenePath, 1);
    }

    private static void EnsurePersistentManagers()
    {
        if (Object.FindFirstObjectByType<GameManager>() == null)
        {
            new GameObject("GameManager").AddComponent<GameManager>();
        }

        if (Object.FindFirstObjectByType<SceneFlowManager>() == null)
        {
            new GameObject("SceneFlowManager").AddComponent<SceneFlowManager>();
        }
    }

    private static void AddSceneToBuildSettings(string scenePath, int preferredIndex)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == scenePath);

        EditorBuildSettingsScene newScene = new EditorBuildSettingsScene(scenePath, true);
        int insertIndex = Mathf.Clamp(preferredIndex, 0, scenes.Count);
        scenes.Insert(insertIndex, newScene);

        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
    }
}
