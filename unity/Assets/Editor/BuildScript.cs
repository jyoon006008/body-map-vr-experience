using UnityEditor;
using UnityEngine;
using System.IO;

public static class BuildScript
{
    [MenuItem("Tools/Build Game")]
    public static void PerformBuilds()
    {
        // Set Microphone Usage Description to prevent macOS Standalone OS X build failure
        PlayerSettings.iOS.microphoneUsageDescription = "Voice chat with the AI therapist requires microphone access.";

        string[] scenes = {
            "Assets/Scenes/BootstrapScene.unity",
            "Assets/Scenes/MapSelectionScene.unity",
            "Assets/Scenes/LoadingScene.unity",
            "Assets/Scenes/VRArtTherapyScene.unity",
            "Assets/Scenes/Env_URP_Garden.unity",
            "Assets/Scenes/Env_URP_Desert.unity"
        };
        
        // Ensure the build target directory exists inside the deploy repository
        string winPath = "C:/Users/junwo/body-map-vr-experience/builds/Windows/BodyMapVR.exe";
        string winDir = Path.GetDirectoryName(winPath);
        if (!Directory.Exists(winDir))
        {
            Directory.CreateDirectory(winDir);
        }

        BuildPlayerOptions winOptions = new BuildPlayerOptions();
        winOptions.scenes = scenes;
        winOptions.locationPathName = winPath;
        winOptions.target = BuildTarget.StandaloneWindows64;
        winOptions.options = BuildOptions.None;
        
        Debug.Log("[BuildScript] Starting StandaloneWindows64 Build...");
        var winReport = BuildPipeline.BuildPlayer(winOptions);
        Debug.Log($"[BuildScript] Windows Build Result: {winReport.summary.result}");


        // StandaloneOSX Build
        string macPath = "C:/Users/junwo/body-map-vr-experience/builds/Mac/BodyMapVR.app";
        string macDir = Path.GetDirectoryName(macPath);
        if (!Directory.Exists(macDir))
        {
            Directory.CreateDirectory(macDir);
        }

        BuildPlayerOptions macOptions = new BuildPlayerOptions();
        macOptions.scenes = scenes;
        macOptions.locationPathName = macPath;
        macOptions.target = BuildTarget.StandaloneOSX;
        macOptions.options = BuildOptions.None;

        Debug.Log("[BuildScript] Starting StandaloneOSX Build...");
        var macReport = BuildPipeline.BuildPlayer(macOptions);
        Debug.Log($"[BuildScript] macOS Build Result: {macReport.summary.result}");
    }
}
