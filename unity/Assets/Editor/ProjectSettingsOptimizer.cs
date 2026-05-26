using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ProjectSettingsOptimizer
{
    static ProjectSettingsOptimizer()
    {
        OptimizeSettings();
    }

    [MenuItem("Tools/Optimize Project Settings")]
    public static void OptimizeSettings()
    {
        // Enforce release build configuration in the editor build settings
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.allowDebugging = false;
        EditorUserBuildSettings.connectProfiler = false;
        EditorUserBuildSettings.buildWithDeepProfilingSupport = false;

        Debug.Log("[ProjectSettingsOptimizer] Build Settings enforced: Release Mode (Development Build=OFF, Debugging=OFF, Profiling=OFF).");
    }
}
