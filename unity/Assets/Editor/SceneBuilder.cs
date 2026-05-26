using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SceneBuilder
{
    [MenuItem("Tools/Build All")]
    public static void BuildAll()
    {
        BuildScene();
        BuildStandalonePlayer();
    }

    [MenuItem("Tools/Build Body Mapping Scene")]
    public static void BuildScene()
    {
        Debug.Log("[SceneBuilder] Starting scene generation...");

        // 1. Create a new scene with Default GameObjects (Camera & Light)
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // 2. Destroy default camera to avoid conflicts
        GameObject defaultCam = GameObject.FindWithTag("MainCamera");
        if (defaultCam != null)
        {
            GameObject.DestroyImmediate(defaultCam);
        }

        // Load StarterAssets FPS controller prefabs
        GameObject playerCapsulePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarterAssets/FirstPersonController/Prefabs/PlayerCapsule.prefab");
        GameObject playerFollowCameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarterAssets/FirstPersonController/Prefabs/PlayerFollowCamera.prefab");
        GameObject mainCameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarterAssets/FirstPersonController/Prefabs/MainCamera.prefab");

        if (playerCapsulePrefab == null || playerFollowCameraPrefab == null || mainCameraPrefab == null)
        {
            Debug.LogError("[SceneBuilder] Failed to load StarterAssets prefabs from Assets/StarterAssets/FirstPersonController/Prefabs/. Please ensure StarterAssets is fully imported.");
            return;
        }

        // Instantiate FPS Player Controller
        GameObject playerCapsule = PrefabUtility.InstantiatePrefab(playerCapsulePrefab) as GameObject;
        GameObject playerFollowCamera = PrefabUtility.InstantiatePrefab(playerFollowCameraPrefab) as GameObject;
        GameObject mainCamera = PrefabUtility.InstantiatePrefab(mainCameraPrefab) as GameObject;

        playerCapsule.transform.position = new Vector3(0f, -1.8f, -6f);
        playerCapsule.transform.rotation = Quaternion.identity;

        // Link CinemachineVirtualCamera target
        Transform cameraRoot = playerCapsule.transform.Find("PlayerCameraRoot");
        if (cameraRoot != null)
        {
            var virtualCam = playerFollowCamera.GetComponent<Cinemachine.CinemachineVirtualCamera>();
            if (virtualCam != null)
            {
                virtualCam.Follow = cameraRoot;
            }
            else
            {
                Debug.LogWarning("[SceneBuilder] PlayerFollowCamera does not have CinemachineVirtualCamera component!");
            }
        }

        // 2.5. Create standard floor plane with URP lit solid white material
        GameObject floorObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floorObj.name = "FloorPlane";
        floorObj.transform.position = new Vector3(0f, -1.8f, 0f);
        floorObj.transform.localScale = new Vector3(5f, 1f, 5f); // 50x50 meters

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");
        Material floorMat = new Material(litShader);
        floorMat.color = Color.white; // Pure white plane
        floorMat.SetFloat("_Smoothness", 0.0f); // Fully rough plane

        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }
        AssetDatabase.CreateAsset(floorMat, "Assets/Materials/FloorMaterial.mat");
        floorObj.GetComponent<Renderer>().material = floorMat;

        // 3. Create the SpriteRenderer GameObject representing the avatar cutout
        GameObject avatarObj = GameObject.Find("BodyMap_Transparent");
        if (avatarObj == null)
        {
            avatarObj = new GameObject("BodyMap_Transparent");
        }
        avatarObj.transform.position = new Vector3(0f, 0.2f, 0f);
        SpriteRenderer spriteRenderer = avatarObj.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = avatarObj.AddComponent<SpriteRenderer>();
        }

        // 4. Create the Manager and attach Receiver
        GameObject managerObj = new GameObject("BodyMapManager");
        BodyMapReceiver receiver = managerObj.AddComponent<BodyMapReceiver>();
        receiver.avatarSpriteRenderer = spriteRenderer;

        // 5. Create the Canvas and UI
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // --- InstructionPanel (Center Card) ---
        GameObject instructionPanelObj = new GameObject("InstructionPanel");
        instructionPanelObj.transform.SetParent(canvasObj.transform, false);
        Image instructionPanelImage = instructionPanelObj.AddComponent<Image>();
        instructionPanelImage.color = new Color(0.08f, 0.08f, 0.1f, 0.95f); // High opacity for startup card

        RectTransform instructionPanelRect = instructionPanelObj.GetComponent<RectTransform>();
        instructionPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        instructionPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        instructionPanelRect.pivot = new Vector2(0.5f, 0.5f);
        instructionPanelRect.anchoredPosition = new Vector2(0f, 0f);
        instructionPanelRect.sizeDelta = new Vector2(500f, 220f);

        GameObject instructionTextObj = new GameObject("InstructionText");
        instructionTextObj.transform.SetParent(instructionPanelObj.transform, false);
        TextMeshProUGUI instructionTmpText = instructionTextObj.AddComponent<TextMeshProUGUI>();
        instructionTmpText.fontSize = 22;
        instructionTmpText.color = Color.white;
        instructionTmpText.alignment = TextAlignmentOptions.Center;
        instructionTmpText.text = "Please upload the scan of your body map";
        ApplyFont(instructionTmpText);

        RectTransform instructionTextRect = instructionTextObj.GetComponent<RectTransform>();
        instructionTextRect.anchorMin = new Vector2(0f, 0.4f);
        instructionTextRect.anchorMax = new Vector2(1f, 1f);
        instructionTextRect.offsetMin = new Vector2(20f, 20f);
        instructionTextRect.offsetMax = new Vector2(-20f, -20f);

        // Button inside InstructionPanel
        GameObject buttonObj = new GameObject("OpenUploaderButton");
        buttonObj.transform.SetParent(instructionPanelObj.transform, false);
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.15f, 0.35f, 0.75f, 1f); // Action blue
        Button button = buttonObj.AddComponent<Button>();

        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 30f);
        buttonRect.sizeDelta = new Vector2(280f, 50f);

        GameObject buttonTextObj = new GameObject("ButtonText");
        buttonTextObj.transform.SetParent(buttonObj.transform, false);
        TextMeshProUGUI buttonTmpText = buttonTextObj.AddComponent<TextMeshProUGUI>();
        buttonTmpText.fontSize = 16;
        buttonTmpText.color = Color.white;
        buttonTmpText.alignment = TextAlignmentOptions.Center;
        buttonTmpText.text = "Open Body Map Uploader";
        ApplyFont(buttonTmpText);

        RectTransform buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
        buttonTextRect.anchorMin = new Vector2(0f, 0f);
        buttonTextRect.anchorMax = new Vector2(1f, 1f);
        buttonTextRect.offsetMin = Vector2.zero;
        buttonTextRect.offsetMax = Vector2.zero;

        // Attach Helper and Hook button event
        OpenURLHelper urlHelper = managerObj.AddComponent<OpenURLHelper>();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, urlHelper.OpenWebClient);

        // Hook Text to Receiver (deleted panels are set to null)
        receiver.detailText = null;
        receiver.statusText = null;
        receiver.instructionPanel = instructionPanelObj;
        receiver.noticePanel = null;
        receiver.noticeText = null;
        receiver.detailPanel = null;

        // 6. Setup EventSystem if missing
        if (GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
#if ENABLE_INPUT_SYSTEM
            eventSystemObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
        }

        // 7. Save Scene
        string scenePath = "Assets/Scenes/BodyMappingScene.unity";
        bool saveSuccess = EditorSceneManager.SaveScene(scene, scenePath);
        
        if (saveSuccess)
        {
            Debug.Log($"[SceneBuilder] Scene successfully saved to: {scenePath}");
        }
        else
        {
            Debug.LogError("[SceneBuilder] Failed to save the scene!");
        }
    }

    [MenuItem("Tools/Build Standalone Player")]
    public static void BuildStandalonePlayer()
    {
        Debug.Log("[SceneBuilder] Starting Standalone Player build...");
        string[] scenes = { "Assets/Scenes/BodyMappingScene.unity" };
        string buildPath = "Build/BodyMapTest.exe";

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = scenes;
        buildPlayerOptions.locationPathName = buildPath;
        buildPlayerOptions.target = BuildTarget.StandaloneWindows64;
        buildPlayerOptions.options = BuildOptions.None;

        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        var summary = report.summary;

        if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"[SceneBuilder] Build succeeded: {summary.totalSize} bytes");
        }
        else if (summary.result == UnityEditor.Build.Reporting.BuildResult.Failed)
        {
            Debug.LogError($"[SceneBuilder] Build failed! Errors: {summary.totalErrors}");
        }
    }

    [MenuItem("Tools/Inspect Sample Scene")]
    public static void InspectSampleScene()
    {
        Debug.Log("[SceneBuilder] Opening SampleScene...");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        var rootObjects = scene.GetRootGameObjects();
        foreach (var go in rootObjects)
        {
            Debug.Log($"[SceneBuilder] Root GameObject: {go.name}");
        }
    }

    private static void ApplyFont(TextMeshProUGUI tmpText)
    {
        if (tmpText == null) return;
        TMP_FontAsset krFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/NotoSansKR.asset");
        if (krFont != null)
        {
            tmpText.font = krFont;
            tmpText.fontSharedMaterial = krFont.material;
        }
    }
}
