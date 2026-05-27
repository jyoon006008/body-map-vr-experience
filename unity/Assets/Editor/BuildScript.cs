using UnityEditor;
using UnityEngine;
using System.IO;

public static class BuildScript
{
    private static readonly string[] BuildScenes = {
        "Assets/Scenes/BootstrapScene.unity",
        "Assets/Scenes/SafePlaceIntroScene.unity",
        "Assets/Scenes/MapSelectionScene.unity",
        "Assets/Scenes/LoadingScene.unity",
        "Assets/Scenes/VRArtTherapyScene.unity",
        "Assets/Scenes/Env_URP_Garden.unity",
        "Assets/Scenes/Env_URP_Desert.unity"
    };

    [MenuItem("Tools/Build Game")]
    public static void PerformBuilds()
    {
        // Set Microphone Usage Description to prevent macOS Standalone OS X build failure
        PlayerSettings.iOS.microphoneUsageDescription = "Voice chat with the AI therapist requires microphone access.";

        // Ensure the build target directory exists inside the deploy repository
        string winPath = "C:/Users/junwo/body-map-vr-experience/builds/Windows/BodyMapVR.exe";
        string winDir = Path.GetDirectoryName(winPath);
        if (!Directory.Exists(winDir))
        {
            Directory.CreateDirectory(winDir);
        }

        BuildPlayerOptions winOptions = new BuildPlayerOptions();
        winOptions.scenes = BuildScenes;
        winOptions.locationPathName = winPath;
        winOptions.target = BuildTarget.StandaloneWindows64;
        winOptions.options = BuildOptions.None;
        
        Debug.Log("[BuildScript] Starting StandaloneWindows64 Build...");
        var winReport = BuildPipeline.BuildPlayer(winOptions);
        Debug.Log($"[BuildScript] Windows Build Result: {winReport.summary.result}");
        CopyAPIKeysNextToBuild(winDir);


        // StandaloneOSX Build
        string macPath = "C:/Users/junwo/body-map-vr-experience/builds/Mac/BodyMapVR.app";
        string macDir = Path.GetDirectoryName(macPath);
        if (!Directory.Exists(macDir))
        {
            Directory.CreateDirectory(macDir);
        }

        BuildPlayerOptions macOptions = new BuildPlayerOptions();
        macOptions.scenes = BuildScenes;
        macOptions.locationPathName = macPath;
        macOptions.target = BuildTarget.StandaloneOSX;
        macOptions.options = BuildOptions.None;

        Debug.Log("[BuildScript] Starting StandaloneOSX Build...");
        var macReport = BuildPipeline.BuildPlayer(macOptions);
        Debug.Log($"[BuildScript] macOS Build Result: {macReport.summary.result}");
        CopyAPIKeysNextToBuild(macDir);
        AddMacMicrophoneUsageDescription(macPath);
    }

    [MenuItem("Tools/Build Mac App Only")]
    public static void PerformMacBuild()
    {
        PlayerSettings.iOS.microphoneUsageDescription = "Voice chat with the AI therapist requires microphone access.";

        string macDir = "G:/내 드라이브/카이스트";
        if (!Directory.Exists(macDir))
        {
            macDir = "C:/Users/junwo/body-map-vr-experience/builds/Mac";
            Debug.LogWarning($"[BuildScript] Google Drive target not found. Falling back to {macDir}");
        }

        if (!Directory.Exists(macDir))
        {
            Directory.CreateDirectory(macDir);
        }

        string macPath = Path.Combine(macDir, "BodyMapVR.app");
        BuildPlayerOptions macOptions = new BuildPlayerOptions();
        macOptions.scenes = BuildScenes;
        macOptions.locationPathName = macPath;
        macOptions.target = BuildTarget.StandaloneOSX;
        macOptions.options = BuildOptions.None;

        Debug.Log("[BuildScript] Starting StandaloneOSX Build...");
        var macReport = BuildPipeline.BuildPlayer(macOptions);
        Debug.Log($"[BuildScript] macOS Build Result: {macReport.summary.result}");
        CopyAPIKeysNextToBuild(macDir);
        AddMacMicrophoneUsageDescription(macPath);
    }

    private static void CopyAPIKeysNextToBuild(string targetDir)
    {
        string sourcePath = Path.GetFullPath(Path.Combine(Application.dataPath, "../api_keys.json"));
        if (!File.Exists(sourcePath))
        {
            Debug.LogWarning($"[BuildScript] api_keys.json not found at {sourcePath}. Built app will need api_keys.json next to it.");
            return;
        }

        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        string targetPath = Path.Combine(targetDir, "api_keys.json");
        File.Copy(sourcePath, targetPath, true);
        Debug.Log($"[BuildScript] Copied api_keys.json to {targetPath}");
    }

    private static void AddMacMicrophoneUsageDescription(string appPath)
    {
        string plistPath = Path.Combine(appPath, "Contents/Info.plist");
        if (!File.Exists(plistPath))
        {
            Debug.LogWarning($"[BuildScript] macOS Info.plist not found at {plistPath}");
            return;
        }

        string plist = File.ReadAllText(plistPath);
        if (plist.Contains("<key>NSMicrophoneUsageDescription</key>"))
        {
            Debug.Log("[BuildScript] NSMicrophoneUsageDescription already exists in macOS Info.plist.");
            return;
        }

        string entry =
            "\t<key>NSMicrophoneUsageDescription</key>\n" +
            "\t<string>Voice chat with the AI therapist requires microphone access.</string>\n";
        int dictEndIndex = plist.LastIndexOf("</dict>");
        if (dictEndIndex < 0)
        {
            Debug.LogWarning("[BuildScript] Could not find </dict> in macOS Info.plist.");
            return;
        }

        plist = plist.Insert(dictEndIndex, entry);
        File.WriteAllText(plistPath, plist);
        Debug.Log("[BuildScript] Added NSMicrophoneUsageDescription to macOS Info.plist.");
    }
}
