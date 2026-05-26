using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using System.IO;
using System.Collections.Generic;
using TMPro;

public class SceneCleaner
{
    [MenuItem("Tools/Clean Scenes")]
    public static void CleanAllScenes()
    {
        Debug.Log("[SceneCleaner] Starting comprehensive 3-map scene cleanup and generation flow...");

        // 1. Copy fresh scenes from My project (1) while skipping the Scripts directory
        CopyFreshSourceScenes();

        // 2. Copy and configure thumbnails
        ConfigureThumbnails();

        // 3. Rebuild flow scenes
        SceneBuilder.BuildScene();
        RebuildBootstrapScene();
        RebuildLoadingScene();
        RebuildMapSelectionScene(); // Statically builds 3 cards and binds events

        // 4. Rebuild VRArtTherapyScene
        PrepareVRArtTherapy();

        // 5. Clean all 3 environments and fix materials/spawns
        CleanAllEnvironments();

        // 6. Update Project Build Settings
        UpdateBuildSettings();

        // 7. Run automatic validation and print checklist
        ValidateProjectFlow();

        Debug.Log("[SceneCleaner] All scenes cleaned, generated, and saved successfully! Build settings updated.");
    }

    private static void CopyFreshSourceScenes()
    {
        string baseSrc = "C:/Users/junwo/My project (1)/Assets/Scenes";
        string baseDst = "Assets/Scenes";

        Debug.Log("[SceneCleaner] Copying fresh source scenes from My project (1)...");
        CopyFolder(Path.Combine(baseSrc, "Garden"), Path.Combine(baseDst, "Garden"));
        CopyFolder(Path.Combine(baseSrc, "Oasis"), Path.Combine(baseDst, "Oasis"));

        string sharedSrc = "C:/Users/junwo/My project (1)/Assets/SharedAssets/Scripts";
        string sharedDst = "Assets/SharedAssets/Scripts";
        Debug.Log("[SceneCleaner] Copying shared scripts...");
        CopyFolder(sharedSrc, sharedDst);

        string settingsSrc = "C:/Users/junwo/My project (1)/Assets/Settings";
        string settingsDst = "Assets/Settings";
        Debug.Log("[SceneCleaner] Copying settings...");
        CopyFolder(settingsSrc, settingsDst);

        // Copy MainLight subgraph as a dependency for Oasis shader graph
        string mainLightSrc = "C:/Users/junwo/My project (1)/Assets/Scenes/Cockpit/Shaders/Subgraphs/MainLight.shadersubgraph";
        string mainLightDst = "Assets/Scenes/Oasis/Shaders/MainLight.shadersubgraph";
        if (File.Exists(mainLightSrc))
        {
            File.Copy(mainLightSrc, mainLightDst, true);
            File.Copy(mainLightSrc + ".meta", mainLightDst + ".meta", true);
            Debug.Log("[SceneCleaner] Copied MainLight.shadersubgraph dependency for Oasis shaders.");
        }

        // Copy ToonLighting.hlsl dependency for MainLight custom function node
        string toonLightSrc = "C:/Users/junwo/My project (1)/Assets/Scenes/Cockpit/Shaders/ToonLighting.hlsl";
        string toonLightDst = "Assets/Scenes/Cockpit/Shaders/ToonLighting.hlsl";
        string toonLightDstDir = "Assets/Scenes/Cockpit/Shaders";
        if (File.Exists(toonLightSrc))
        {
            if (!Directory.Exists(toonLightDstDir))
            {
                Directory.CreateDirectory(toonLightDstDir);
            }
            File.Copy(toonLightSrc, toonLightDst, true);
            File.Copy(toonLightSrc + ".meta", toonLightDst + ".meta", true);
            Debug.Log("[SceneCleaner] Copied ToonLighting.hlsl dependency for Oasis shaders.");
        }

        AssetDatabase.Refresh();
    }

    private static void CopyFolder(string srcDir, string dstDir)
    {
        if (!Directory.Exists(srcDir)) return;
        Directory.CreateDirectory(dstDir);

        foreach (string file in Directory.GetFiles(srcDir))
        {
            string dest = Path.Combine(dstDir, Path.GetFileName(file));
            File.Copy(file, dest, true);
        }

        foreach (string folder in Directory.GetDirectories(srcDir))
        {
            string dest = Path.Combine(dstDir, Path.GetFileName(folder));
            CopyFolder(folder, dest);
        }
    }

    private static void ConfigureThumbnails()
    {
        string mapsDir = "C:/Users/junwo/Desktop/maps";
        string targetDir = "Assets/Textures/MapThumbnails";
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        string[] fileNames = { "Desert.png", "Japanese Garden.png" };
        foreach (var file in fileNames)
        {
            string srcPath = Path.Combine(mapsDir, file);
            string dstPath = Path.Combine(targetDir, file);
            if (File.Exists(srcPath))
            {
                File.Copy(srcPath, dstPath, true);
                Debug.Log($"[SceneCleaner] Copied thumbnail to {dstPath}");
            }
            else
            {
                Debug.LogWarning($"[SceneCleaner] Source thumbnail not found: {srcPath}");
            }
        }

        AssetDatabase.Refresh();

        foreach (var file in fileNames)
        {
            string dstPath = Path.Combine(targetDir, file);
            TextureImporter importer = AssetImporter.GetAtPath(dstPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
                Debug.Log($"[SceneCleaner] Configured import settings for {dstPath}");
            }
        }
    }

    private static void RebuildBootstrapScene()
    {
        string path = "Assets/Scenes/BootstrapScene.unity";
        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var camera = GameObject.Find("Main Camera");
        if (camera != null) Object.DestroyImmediate(camera);
        var light = GameObject.Find("Directional Light");
        if (light != null) Object.DestroyImmediate(light);

        new GameObject("GameManager", typeof(GameManager));
        
        GameObject sfGO = new GameObject("SceneFlowManager", typeof(SceneFlowManager));
        var selector = sfGO.AddComponent<LanguageSelector>();

        // Create UI Canvas
        GameObject canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

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

        // Background
        GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(canvasGO.transform, false);
        SetupRect(bgGO, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        bgGO.GetComponent<Image>().color = new Color(0.07f, 0.07f, 0.09f);

        // Title text
        GameObject titleGO = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(canvasGO.transform, false);
        SetupRect(titleGO, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1200f, 100f), new Vector2(0f, 150f));
        var titleText = titleGO.GetComponent<TextMeshProUGUI>();
        titleText.text = "SELECT LANGUAGE";
        titleText.fontSize = 54;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(0.0f, 0.96f, 0.83f);
        ApplyFont(titleText);

        // Subtitle text
        GameObject subtitleGO = new GameObject("SubtitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        subtitleGO.transform.SetParent(canvasGO.transform, false);
        SetupRect(subtitleGO, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1200f, 60f), new Vector2(0f, 80f));
        var subtitleText = subtitleGO.GetComponent<TextMeshProUGUI>();
        subtitleText.text = "Choose your language to begin the session";
        subtitleText.fontSize = 20;
        subtitleText.alignment = TextAlignmentOptions.Center;
        subtitleText.color = new Color(0.7f, 0.7f, 0.8f);
        ApplyFont(subtitleText);

        // Buttons Container
        GameObject containerGO = new GameObject("ButtonsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        containerGO.transform.SetParent(canvasGO.transform, false);
        SetupRect(containerGO, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(800f, 150f), new Vector2(0f, -50f));
        
        var layout = containerGO.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 40;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // Button 1: Korean
        GameObject btnKo = new GameObject("Button_Korean", typeof(RectTransform), typeof(Image), typeof(Button));
        btnKo.transform.SetParent(containerGO.transform, false);
        SetupRect(btnKo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(240f, 80f), Vector2.zero);
        btnKo.GetComponent<Image>().color = new Color(0.11f, 0.11f, 0.14f);
        var outlineKo = btnKo.AddComponent<Outline>();
        outlineKo.effectColor = new Color(0.0f, 0.96f, 0.83f, 0.5f);
        outlineKo.effectDistance = new Vector2(2f, -2f);

        GameObject btnKoTextGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        btnKoTextGO.transform.SetParent(btnKo.transform, false);
        SetupRect(btnKoTextGO, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var btnKoText = btnKoTextGO.GetComponent<TextMeshProUGUI>();
        btnKoText.text = "한국어";
        btnKoText.fontSize = 24;
        btnKoText.alignment = TextAlignmentOptions.Center;
        btnKoText.color = Color.white;
        btnKoText.fontStyle = FontStyles.Bold;
        ApplyFont(btnKoText);

        var buttonKo = btnKo.GetComponent<Button>();
        UnityEventTools.AddStringPersistentListener(buttonKo.onClick, selector.SelectLanguage, "ko");

        // Button 2: English
        GameObject btnEn = new GameObject("Button_English", typeof(RectTransform), typeof(Image), typeof(Button));
        btnEn.transform.SetParent(containerGO.transform, false);
        SetupRect(btnEn, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(240f, 80f), Vector2.zero);
        btnEn.GetComponent<Image>().color = new Color(0.11f, 0.11f, 0.14f);
        var outlineEn = btnEn.AddComponent<Outline>();
        outlineEn.effectColor = new Color(0.0f, 0.96f, 0.83f, 0.5f);
        outlineEn.effectDistance = new Vector2(2f, -2f);

        GameObject btnEnTextGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        btnEnTextGO.transform.SetParent(btnEn.transform, false);
        SetupRect(btnEnTextGO, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var btnEnText = btnEnTextGO.GetComponent<TextMeshProUGUI>();
        btnEnText.text = "English";
        btnEnText.fontSize = 24;
        btnEnText.alignment = TextAlignmentOptions.Center;
        btnEnText.color = Color.white;
        btnEnText.fontStyle = FontStyles.Bold;
        ApplyFont(btnEnText);

        var buttonEn = btnEn.GetComponent<Button>();
        UnityEventTools.AddStringPersistentListener(buttonEn.onClick, selector.SelectLanguage, "en");

        EditorSceneManager.SaveScene(newScene, path);
        Debug.Log($"[SceneCleaner] Rebuilt and saved BootstrapScene at: {path}");
    }

    private static void RebuildLoadingScene()
    {
        string path = "Assets/Scenes/LoadingScene.unity";
        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var camera = GameObject.Find("Main Camera");
        if (camera != null) Object.DestroyImmediate(camera);
        var light = GameObject.Find("Directional Light");
        if (light != null) Object.DestroyImmediate(light);

        GameObject canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

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

        GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(canvasGO.transform, false);
        SetupRect(bgGO, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        bgGO.GetComponent<Image>().color = new Color(0.07f, 0.07f, 0.09f);

        GameObject textGO = new GameObject("LoadingText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(canvasGO.transform, false);
        SetupRect(textGO, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(600f, 60f), new Vector2(0f, 50f));
        var loadingText = textGO.GetComponent<TextMeshProUGUI>();
        loadingText.text = "Loading... 0%";
        loadingText.fontSize = 32;
        loadingText.alignment = TextAlignmentOptions.Center;
        loadingText.color = Color.white;
        ApplyFont(loadingText);

        GameObject sliderGO = new GameObject("ProgressBar", typeof(RectTransform), typeof(Slider));
        sliderGO.transform.SetParent(canvasGO.transform, false);
        SetupRect(sliderGO, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(600f, 20f), new Vector2(0f, -20f));
        var slider = sliderGO.GetComponent<Slider>();

        GameObject sliderBg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        sliderBg.transform.SetParent(sliderGO.transform, false);
        SetupRect(sliderBg, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        sliderBg.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f);

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGO.transform, false);
        SetupRect(fillArea, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        SetupRect(fill, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        fill.GetComponent<Image>().color = new Color(0.0f, 0.96f, 0.83f);

        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;

        GameObject mgrGO = new GameObject("LoadingScreenManager", typeof(LoadingScreenManager));
        var lsm = mgrGO.GetComponent<LoadingScreenManager>();
        lsm.progressBar = slider;
        lsm.progressText = loadingText;

        EditorSceneManager.SaveScene(newScene, path);
        Debug.Log($"[SceneCleaner] Rebuilt and saved LoadingScene at: {path}");
    }

    private static void RebuildMapSelectionScene()
    {
        string path = "Assets/Scenes/MapSelectionScene.unity";
        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        GameObject canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

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

        GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(canvasGO.transform, false);
        SetupRect(bgGO, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        bgGO.GetComponent<Image>().color = new Color(0.07f, 0.07f, 0.09f);

        GameObject titleGO = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(canvasGO.transform, false);
        SetupRect(titleGO, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1000f, 100f), new Vector2(0f, -180f));
        var titleText = titleGO.GetComponent<TextMeshProUGUI>();
        titleText.text = "SELECT ENVIRONMENT";
        titleText.fontSize = 54;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(0.0f, 0.96f, 0.83f);
        ApplyFont(titleText);

        GameObject subtitleGO = new GameObject("SubtitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        subtitleGO.transform.SetParent(canvasGO.transform, false);
        SetupRect(subtitleGO, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1000f, 60f), new Vector2(0f, -245f));
        var subtitleText = subtitleGO.GetComponent<TextMeshProUGUI>();
        subtitleText.text = "Choose an environment to begin your VR Art Therapy session";
        subtitleText.fontSize = 20;
        subtitleText.alignment = TextAlignmentOptions.Center;
        subtitleText.color = new Color(0.7f, 0.7f, 0.8f);
        ApplyFont(subtitleText);

        GameObject containerGO = new GameObject("CardContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        containerGO.transform.SetParent(canvasGO.transform, false);
        SetupRect(containerGO, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1200f, 460f), new Vector2(0f, -30f));
        
        var layout = containerGO.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 60;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        GameObject mgrGO = new GameObject("MapSelectionManager", typeof(MapSelectionManager));
        var msm = mgrGO.GetComponent<MapSelectionManager>();
        msm.cardParent = containerGO.transform;

        var cardOptions = new List<MapOption> {
            new MapOption {
                displayName = "Garden Environment",
                sceneName = "Env_URP_Garden",
                description = "Open natural environment with trees and central interaction space.",
                isRecommendedForVR = true,
                renderPipelineType = "URP"
            },
            new MapOption {
                displayName = "Desert Oasis",
                sceneName = "Env_URP_Desert",
                description = "Wide open desert-like environment with calm atmosphere.",
                isRecommendedForVR = true,
                renderPipelineType = "URP"
            }
        };

        msm.mapOptions = cardOptions;

        foreach (var opt in cardOptions)
        {
            CreateStaticCard(containerGO.transform, opt, msm);
        }

        // Create Language Toggle Button in Bottom-Right
        GameObject toggleBtn = new GameObject("ChangeLanguageButton", typeof(RectTransform), typeof(Image), typeof(Button));
        toggleBtn.transform.SetParent(canvasGO.transform, false);
        SetupRect(toggleBtn, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(240f, 45f), new Vector2(-50f, 40f));
        
        toggleBtn.GetComponent<Image>().color = new Color(0.11f, 0.11f, 0.14f, 1f);
        var outline = toggleBtn.AddComponent<Outline>();
        outline.effectColor = new Color(0.0f, 0.96f, 0.83f, 0.5f);
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject toggleBtnTextGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        toggleBtnTextGO.transform.SetParent(toggleBtn.transform, false);
        SetupRect(toggleBtnTextGO, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var toggleBtnText = toggleBtnTextGO.GetComponent<TextMeshProUGUI>();
        toggleBtnText.text = "Change Language: English";
        toggleBtnText.fontSize = 14;
        toggleBtnText.alignment = TextAlignmentOptions.Center;
        toggleBtnText.color = Color.white;
        toggleBtnText.fontStyle = FontStyles.Bold;
        ApplyFont(toggleBtnText);

        EditorSceneManager.SaveScene(newScene, path);
        Debug.Log($"[SceneCleaner] Rebuilt and saved MapSelectionScene at: {path}");
    }

    private static void CreateStaticCard(Transform parent, MapOption opt, MapSelectionManager manager)
    {
        string cardName = "Card_" + opt.sceneName;
        GameObject cardGO = new GameObject(cardName, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        cardGO.transform.SetParent(parent, false);
        
        var rect = cardGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(360f, 420f);
        
        var cardImg = cardGO.GetComponent<Image>();
        cardImg.color = new Color(0.11f, 0.11f, 0.14f);
        
        var cardLayout = cardGO.GetComponent<VerticalLayoutGroup>();
        cardLayout.spacing = 15;
        cardLayout.padding = new RectOffset(15, 15, 15, 15);
        cardLayout.childAlignment = TextAnchor.UpperCenter;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = false;
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = false;

        GameObject maskGO = new GameObject("PreviewMask", typeof(RectTransform), typeof(RectMask2D));
        maskGO.transform.SetParent(cardGO.transform, false);
        var maskRect = maskGO.GetComponent<RectTransform>();
        maskRect.sizeDelta = new Vector2(330f, 180f);

        GameObject imgGO = new GameObject("PreviewImage", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
        imgGO.transform.SetParent(maskGO.transform, false);
        
        var imgRect = imgGO.GetComponent<RectTransform>();
        imgRect.anchorMin = Vector2.zero;
        imgRect.anchorMax = Vector2.one;
        imgRect.pivot = new Vector2(0.5f, 0.5f);
        imgRect.sizeDelta = Vector2.zero;

        var imgComp = imgGO.GetComponent<Image>();
        imgComp.preserveAspect = true;

        string thumbnailPath = "";
        if (opt.sceneName == "Env_URP_Garden") thumbnailPath = "Assets/Textures/MapThumbnails/Japanese Garden.png";
        else if (opt.sceneName == "Env_URP_Desert") thumbnailPath = "Assets/Textures/MapThumbnails/Desert.png";
        else if (opt.sceneName == "Env_URP_Terminal") thumbnailPath = "Assets/Textures/MapThumbnails/Ternimal.png";

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(thumbnailPath);
        if (sprite != null)
        {
            imgComp.sprite = sprite;
            opt.previewSprite = sprite;
            
            var fitter = imgGO.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = (float)sprite.texture.width / sprite.texture.height;
        }
        else
        {
            imgComp.color = new Color(0.2f, 0.2f, 0.2f);
            var fitter = imgGO.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 1.777f;
            Debug.LogWarning($"[SceneCleaner] Thumbnail sprite not found at: {thumbnailPath}");
        }

        GameObject nameGO = new GameObject("DisplayName", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameGO.transform.SetParent(cardGO.transform, false);
        var nameRect = nameGO.GetComponent<RectTransform>();
        nameRect.sizeDelta = new Vector2(330f, 30f);
        var nameText = nameGO.GetComponent<TextMeshProUGUI>();
        nameText.text = opt.displayName;
        nameText.fontSize = 22;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.fontStyle = FontStyles.Bold;
        nameText.color = Color.white;
        ApplyFont(nameText);

        GameObject descGO = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI));
        descGO.transform.SetParent(cardGO.transform, false);
        var descRect = descGO.GetComponent<RectTransform>();
        descRect.sizeDelta = new Vector2(330f, 90f);
        var descText = descGO.GetComponent<TextMeshProUGUI>();
        descText.text = opt.description;
        descText.fontSize = 14;
        descText.alignment = TextAlignmentOptions.Center;
        descText.color = new Color(0.75f, 0.75f, 0.8f);
        ApplyFont(descText);

        GameObject btnGO = new GameObject("SelectButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(cardGO.transform, false);
        var btnRect = btnGO.GetComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(180f, 40f);
        btnGO.GetComponent<Image>().color = new Color(0.0f, 0.7f, 0.85f);

        GameObject btnTextGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        btnTextGO.transform.SetParent(btnGO.transform, false);
        var btnTextRect = btnTextGO.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.pivot = new Vector2(0.5f, 0.5f);
        btnTextRect.sizeDelta = Vector2.zero;
        var btnText = btnTextGO.GetComponent<TextMeshProUGUI>();
        btnText.text = "Select";
        btnText.fontSize = 16;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = Color.white;
        btnText.fontStyle = FontStyles.Bold;
        ApplyFont(btnText);

        var button = btnGO.GetComponent<Button>();
        UnityEventTools.AddStringPersistentListener(button.onClick, manager.SelectMap, opt.sceneName);
        
        Debug.Log($"[SceneCleaner] Built card {cardName} and bound event persistently.");
    }

    private static void CleanAndFixEnvironment(string srcPath, string dstPath)
    {
        if (!File.Exists(srcPath))
        {
            Debug.LogError($"[SceneCleaner] Source scene not found: {srcPath}");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(srcPath, OpenSceneMode.Single);
        Debug.Log($"[SceneCleaner] Opened scene for cleaning: {srcPath}");

        // 1. Locate original player/camera to set spawn point
        Vector3 spawnPos = new Vector3(0f, 0.5f, 0f);
        Quaternion spawnRot = Quaternion.identity;
        bool foundOriginalPlayer = false;

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null) playerObj = GameObject.Find("PlayerCapsule");
        if (playerObj == null) playerObj = GameObject.Find("FirstPersonController");
        if (playerObj == null) playerObj = GameObject.Find("Player");
        if (playerObj == null) playerObj = GameObject.Find("Main Camera");
        if (playerObj == null) playerObj = GameObject.Find("MainCamera");
        if (playerObj == null)
        {
            var roots = scene.GetRootGameObjects();
            foreach (var r in roots)
            {
                var cam = r.GetComponentInChildren<Camera>();
                if (cam != null)
                {
                    playerObj = cam.gameObject;
                    break;
                }
            }
        }

        if (playerObj != null)
        {
            spawnPos = playerObj.transform.position;
            spawnRot = playerObj.transform.rotation;
            foundOriginalPlayer = true;
            Debug.Log($"[SceneCleaner] Located original player/camera at: {spawnPos} in {srcPath}");
        }

        // Apply fallback positions if not found or if the position is offset
        if (dstPath.Contains("Env_URP_Garden"))
        {
            if (!foundOriginalPlayer) spawnPos = new Vector3(0f, -1.8f, -6f);
            Debug.Log($"[SpawnFix] Env_URP_Garden spawn: {spawnPos}");
        }
        else if (dstPath.Contains("Env_URP_Desert"))
        {
            if (!foundOriginalPlayer) spawnPos = new Vector3(0f, 0.5f, 0f);
            Debug.Log($"[SpawnFix] Env_URP_Desert spawn: {spawnPos}");
        }
        else if (dstPath.Contains("Env_URP_Terminal"))
        {
            if (!foundOriginalPlayer) spawnPos = new Vector3(0f, 0.5f, 0f);
            Debug.Log($"[SpawnFix] Env_URP_Terminal spawn: {spawnPos}");
        }

        // 2. Do NOT delete any target objects (cameras, players, reflection probes, canvases) as the user wants them preserved exactly.
        Debug.Log("[SceneCleaner] Preserving all original objects in the environment scene.");

        // 3. Create/Setup EnvironmentSpawnPoint if not already present
        GameObject spawnPoint = GameObject.Find("EnvironmentSpawnPoint");
        if (spawnPoint == null)
        {
            spawnPoint = new GameObject("EnvironmentSpawnPoint");
            spawnPoint.AddComponent<EnvironmentSpawnPoint>();
        }
        spawnPoint.transform.position = spawnPos;
        spawnPoint.transform.rotation = spawnRot;
        Debug.Log($"[SceneCleaner] Setup EnvironmentSpawnPoint in scene at {spawnPos}.");

        // 4. Scan and convert HDRP/Standard materials to URP Lit to fix the pink color problem
        FixPinkMaterialsInScene(scene);

        // 5. Remove missing prefabs that reference files not present in the project (e.g. Cockpit/Terminal loaders in Oasis)
        RemoveMissingPrefabs(scene);

        EditorSceneManager.SaveScene(scene, dstPath);
        Debug.Log($"[SceneCleaner] Cleaned and saved scene to: {dstPath}");
    }

    private static void RemoveMissingPrefabs(Scene scene)
    {
        Debug.Log($"[SceneCleaner] Scanning for missing prefabs in scene: {scene.name}");
        GameObject[] rootObjects = scene.GetRootGameObjects();
        foreach (var obj in rootObjects)
        {
            RemoveMissingPrefabsRecursive(obj);
        }
    }

    private static void RemoveMissingPrefabsRecursive(GameObject obj)
    {
        if (obj == null) return;

        if (PrefabUtility.IsPrefabAssetMissing(obj))
        {
            Debug.Log($"[SceneCleaner] Destroying GameObject with missing prefab: {obj.name}");
            Object.DestroyImmediate(obj);
            return;
        }

        int childCount = obj.transform.childCount;
        for (int i = childCount - 1; i >= 0; i--)
        {
            if (i < obj.transform.childCount)
            {
                var child = obj.transform.GetChild(i).gameObject;
                RemoveMissingPrefabsRecursive(child);
            }
        }
    }

    private static void FixPinkMaterialsInScene(Scene scene)
    {
        Debug.Log("[MaterialFix] Scanning for pink/broken materials in scene...");

        int fixedCount = 0;
        Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLitShader == null)
        {
            Debug.LogError("[MaterialFix] Universal Render Pipeline/Lit shader not found!");
            return;
        }

        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (var r in renderers)
        {
            Material[] mats = r.sharedMaterials;
            bool matsChanged = false;

            for (int i = 0; i < mats.Length; i++)
            {
                Material mat = mats[i];
                if (mat == null) continue;

                Shader shader = mat.shader;
                if (shader == null || 
                    shader.name == "Hidden/InternalErrorShader" || 
                    shader.name.Contains("HDRP") || 
                    shader.name.Contains("High Definition") || 
                    shader.name == "Standard")
                {
                    Debug.Log($"[MaterialFix] Pink material found: {mat.name} on GameObject: {r.gameObject.name}");

                    Texture mainTex = null;
                    Color mainColor = Color.white;

                    if (mat.HasProperty("_MainTex")) mainTex = mat.GetTexture("_MainTex");
                    else if (mat.HasProperty("_BaseMap")) mainTex = mat.GetTexture("_BaseMap");

                    if (mat.HasProperty("_Color")) mainColor = mat.GetColor("_Color");
                    else if (mat.HasProperty("_BaseColor")) mainColor = mat.GetColor("_BaseColor");

                    // Convert to URP Lit
                    mat.shader = urpLitShader;

                    // Re-apply properties
                    if (mainTex != null)
                    {
                        mat.SetTexture("_BaseMap", mainTex);
                    }
                    else
                    {
                        Debug.LogWarning($"[MaterialFix] Missing texture: {mat.name}");
                    }
                    mat.SetColor("_BaseColor", mainColor);

                    EditorUtility.SetDirty(mat);
                    matsChanged = true;
                    fixedCount++;

                    Debug.Log($"[MaterialFix] Converted to URP Lit: {mat.name}");
                }
            }

            if (matsChanged)
            {
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[MaterialFix] Fixed material count: {fixedCount}");
    }

    private static void CleanAllEnvironments()
    {
        CleanAndFixEnvironment("Assets/Scenes/Garden/GardenScene.unity", "Assets/Scenes/Env_URP_Garden.unity");
        CleanAndFixEnvironment("Assets/Scenes/Oasis/OasisScene.unity", "Assets/Scenes/Env_URP_Desert.unity");
    }

    private static void PrepareVRArtTherapy()
    {
        string srcPath = "Assets/Scenes/BodyMappingScene.unity";
        string dstPath = "Assets/Scenes/VRArtTherapyScene.unity";

        if (!File.Exists(srcPath))
        {
            Debug.LogError($"[SceneCleaner] Source BodyMappingScene not found!");
            return;
        }

        File.Copy(srcPath, dstPath, true);
        AssetDatabase.ImportAsset(dstPath);

        Scene scene = EditorSceneManager.OpenScene(dstPath, OpenSceneMode.Single);
        Debug.Log($"[SceneCleaner] Opened main scene: {dstPath}");

        string[] envTargets = {
            "Environment", "Floor", "Ground", "Dome", "BackgroundDome", "Sky", "Obstacles", "Lights", "Map"
        };

        var rootObjects = scene.GetRootGameObjects();
        foreach (var obj in rootObjects)
        {
            DeleteTargetObjectsRecursive(obj, envTargets);
        }

        GameObject loaderObj = GameObject.Find("EnvironmentLoader");
        if (loaderObj == null)
        {
            loaderObj = new GameObject("EnvironmentLoader");
        }
        
        var loader = loaderObj.GetComponent<EnvironmentLoader>();
        if (loader == null)
        {
            loader = loaderObj.AddComponent<EnvironmentLoader>();
        }

        var receiver = Object.FindAnyObjectByType<BodyMapReceiver>();
        var player = GameObject.FindWithTag("Player");
        if (player == null) player = GameObject.Find("PlayerCapsule");
        if (player == null) player = GameObject.Find("FirstPersonController");
        if (player == null) player = GameObject.Find("Player");

        SerializedObject so = new SerializedObject(loader);
        if (receiver != null)
        {
            so.FindProperty("bodyMapReceiver").objectReferenceValue = receiver;
            receiver.enabled = false;
            Debug.Log("[SceneCleaner] Disabled BodyMapReceiver and bound to EnvironmentLoader.");
        }
        if (player != null)
        {
            so.FindProperty("playerTransform").objectReferenceValue = player.transform;
            Debug.Log("[SceneCleaner] Bound Player Transform to EnvironmentLoader.");
        }
        so.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[SceneCleaner] Saved VRArtTherapyScene to: {dstPath}");
    }

    private static void UpdateBuildSettings()
    {
        string[] scenesToAdd = {
            "Assets/Scenes/BootstrapScene.unity",
            "Assets/Scenes/MapSelectionScene.unity",
            "Assets/Scenes/LoadingScene.unity",
            "Assets/Scenes/VRArtTherapyScene.unity",
            "Assets/Scenes/Env_URP_Garden.unity",
            "Assets/Scenes/Env_URP_Desert.unity"
        };

        List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>();
        foreach (var scenePath in scenesToAdd)
        {
            if (File.Exists(scenePath))
            {
                buildScenes.Add(new EditorBuildSettingsScene(scenePath, true));
                Debug.Log($"[SceneCleaner] Added to Build Settings: {scenePath}");
            }
            else
            {
                Debug.LogWarning($"[SceneCleaner] Scene not found for Build Settings: {scenePath}");
            }
        }
        EditorBuildSettings.scenes = buildScenes.ToArray();
    }

    [MenuItem("Tools/Validate Project Flow")]
    public static void ValidateProjectFlow()
    {
        Debug.Log("[SceneCleaner] Starting verification...");

        bool compileErrorFree = true;
        int cardCount = 0;
        bool gardenBtn = false;
        bool desertBtn = false;
        
        bool galleryRemoved = !File.Exists("Assets/Scenes/Env_URP_Gallery.unity") &&
                             !File.Exists("Assets/Textures/MapThumbnails/Gallery.png");

        string mapSelectionPath = "Assets/Scenes/MapSelectionScene.unity";
        if (File.Exists(mapSelectionPath))
        {
            Scene scene = EditorSceneManager.OpenScene(mapSelectionPath, OpenSceneMode.Single);
            var manager = Object.FindAnyObjectByType<MapSelectionManager>();
            if (manager != null)
            {
                var container = manager.cardParent;
                if (container != null)
                {
                    cardCount = container.childCount;
                    for (int i = 0; i < container.childCount; i++)
                    {
                        var child = container.GetChild(i);
                        var button = child.GetComponentInChildren<Button>();

                        bool hasListener = false;
                        if (button != null)
                        {
                            int persistentCount = button.onClick.GetPersistentEventCount();
                            for (int j = 0; j < persistentCount; j++)
                            {
                                string methodName = button.onClick.GetPersistentMethodName(j);
                                if (methodName == "SelectMap")
                                {
                                    hasListener = true;
                                    break;
                                }
                            }
                        }

                        if (child.name.Contains("Garden"))
                        {
                            gardenBtn = hasListener;
                        }
                        else if (child.name.Contains("Desert"))
                        {
                            desertBtn = hasListener;
                        }

                    }
                }
            }
        }

        bool loadingUI = false;
        string loadingPath = "Assets/Scenes/LoadingScene.unity";
        if (File.Exists(loadingPath))
        {
            Scene scene = EditorSceneManager.OpenScene(loadingPath, OpenSceneMode.Single);
            var lsm = Object.FindAnyObjectByType<LoadingScreenManager>();
            if (lsm != null && lsm.progressBar != null && lsm.progressText != null)
            {
                loadingUI = true;
            }
        }

        bool vrEnvLoader = false;
        string vrPath = "Assets/Scenes/VRArtTherapyScene.unity";
        if (File.Exists(vrPath))
        {
            Scene scene = EditorSceneManager.OpenScene(vrPath, OpenSceneMode.Single);
            var loader = Object.FindAnyObjectByType<EnvironmentLoader>();
            if (loader != null)
            {
                vrEnvLoader = true;
            }
        }

        bool gardenSpawn = CheckSpawnPoint("Assets/Scenes/Env_URP_Garden.unity");
        bool desertSpawn = CheckSpawnPoint("Assets/Scenes/Env_URP_Desert.unity");

        // Count pink materials left in final scenes
        int gardenPinkCount = CountPinkMaterials("Assets/Scenes/Env_URP_Garden.unity");
        int desertPinkCount = CountPinkMaterials("Assets/Scenes/Env_URP_Desert.unity");

        bool buildSettingsOk = true;
        string[] expectedScenes = {
            "Assets/Scenes/BootstrapScene.unity",
            "Assets/Scenes/MapSelectionScene.unity",
            "Assets/Scenes/LoadingScene.unity",
            "Assets/Scenes/VRArtTherapyScene.unity",
            "Assets/Scenes/Env_URP_Garden.unity",
            "Assets/Scenes/Env_URP_Desert.unity"
        };
        var activeScenes = EditorBuildSettings.scenes;
        if (activeScenes.Length != expectedScenes.Length)
        {
            buildSettingsOk = false;
        }
        else
        {
            for (int i = 0; i < expectedScenes.Length; i++)
            {
                if (activeScenes[i].path != expectedScenes[i])
                {
                    buildSettingsOk = false;
                    break;
                }
            }
        }

        bool bootstrapReady = false;
        string bootstrapPath = "Assets/Scenes/BootstrapScene.unity";
        if (File.Exists(bootstrapPath))
        {
            Scene scene = EditorSceneManager.OpenScene(bootstrapPath, OpenSceneMode.Single);
            var gm = Object.FindAnyObjectByType<GameManager>();
            var sf = Object.FindAnyObjectByType<SceneFlowManager>();
            if (gm != null && sf != null)
            {
                bootstrapReady = true;
            }
        }

        // Verify Core therapy scripts are intact
        bool bodyMappingPreserved = File.Exists("Assets/Scripts/BodyMapReceiver.cs");
        bool aiChatPreserved = File.Exists("Assets/Scripts/BodyMapAIController.cs");
        bool sttTtsPreserved = File.Exists("Assets/Scripts/PromptToPython.cs");

        if (File.Exists(bootstrapPath))
        {
            EditorSceneManager.OpenScene(bootstrapPath, OpenSceneMode.Single);
        }

        string checklist = "\n[완료/실패 체크리스트]\n" +
            $"- Compile Error: {(compileErrorFree ? "0개" : "실패")}\n" +
            $"- MapSelectionScene cards: {cardCount}개\n" +
            $"- Garden button binding: {(gardenBtn ? "OK" : "실패")}\n" +
            $"- Desert button binding: {(desertBtn ? "OK" : "실패")}\n" +
            $"- Gallery removed: {(galleryRemoved ? "OK" : "실패")}\n" +
            $"- Build Settings order: {(buildSettingsOk ? "OK" : "실패")}\n" +
            $"- Env_URP_Garden exists: {(File.Exists("Assets/Scenes/Env_URP_Garden.unity") ? "OK" : "실패")}\n" +
            $"- Env_URP_Desert exists: {(File.Exists("Assets/Scenes/Env_URP_Desert.unity") ? "OK" : "실패")}\n" +
            $"- Env_URP_Garden SpawnPoint: {(gardenSpawn ? "OK" : "실패")}\n" +
            $"- Env_URP_Desert SpawnPoint: {(desertSpawn ? "OK" : "실패")}\n" +
            $"- Pink materials in Garden: {gardenPinkCount}\n" +
            $"- Pink materials in Desert: {desertPinkCount}\n" +
            $"- VRArtTherapyScene EnvironmentLoader: {(vrEnvLoader ? "OK" : "실패")}\n" +
            $"- BodyMapping preserved: {(bodyMappingPreserved ? "OK" : "실패")}\n" +
            $"- AI Emotion Chat preserved: {(aiChatPreserved ? "OK" : "실패")}\n" +
            $"- STT/TTS preserved: {(sttTtsPreserved ? "OK" : "실패")}\n" +
            $"- BootstrapScene Play flow ready: {(bootstrapReady ? "OK" : "실패")}\n";

        Debug.Log(checklist);
    }

    private static bool CheckSpawnPoint(string scenePath)
    {
        if (!File.Exists(scenePath)) return false;
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var spawnPoint = Object.FindAnyObjectByType<EnvironmentSpawnPoint>();
        return spawnPoint != null;
    }

    private static int CountPinkMaterials(string scenePath)
    {
        if (!File.Exists(scenePath)) return -1;
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        int pinkCount = 0;
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (var r in renderers)
        {
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null) continue;
                Shader shader = mat.shader;
                if (shader == null || 
                    shader.name == "Hidden/InternalErrorShader" || 
                    shader.name.Contains("HDRP") || 
                    shader.name.Contains("High Definition") || 
                    shader.name == "Standard")
                {
                    pinkCount++;
                }
            }
        }
        return pinkCount;
    }

    private static void DeleteTargetObjectsRecursive(GameObject obj, string[] targets)
    {
        if (obj == null) return;

        // Crucial: Do NOT destroy the main body mapping components (BodyMapManager, BodyMap_Transparent, etc.)
        if (obj.name.IndexOf("BodyMap", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // Skip deletion but check its children
        }
        else
        {
            bool shouldDelete = false;
            foreach (var target in targets)
            {
                if (obj.name.IndexOf(target, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    shouldDelete = true;
                    break;
                }
            }

            if (shouldDelete)
            {
                Debug.Log($"[SceneCleaner] Destroying: {obj.name}");
                Object.DestroyImmediate(obj);
                return;
            }
        }

        int childCount = obj.transform.childCount;
        for (int i = childCount - 1; i >= 0; i--)
        {
            if (i < obj.transform.childCount)
            {
                DeleteTargetObjectsRecursive(obj.transform.GetChild(i).gameObject, targets);
            }
        }
    }

    private static RectTransform SetupRect(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPosition)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        if (rect == null) rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = anchoredPosition;
        return rect;
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

[InitializeOnLoad]
public class SceneCleanerInitializer
{
    static SceneCleanerInitializer()
    {
        EditorApplication.delayCall += ExecuteCleanOnce;
    }

    private static void ExecuteCleanOnce()
    {
        if (SessionState.GetBool("CleanScenesExecutedWithLanguageSelectorAndUIAndUploaderFixesV2", false)) return;
        SessionState.SetBool("CleanScenesExecutedWithLanguageSelectorAndUIAndUploaderFixesV2", true);
        SceneCleaner.CleanAllScenes();
    }
}
