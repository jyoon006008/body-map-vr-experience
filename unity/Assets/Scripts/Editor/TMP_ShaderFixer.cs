// EDITOR ONLY — auto-fixes TMP material shaders on project open.
// Replaces any TMP_SDF-URP Lit shader with TextMeshPro/Distance Field
// to eliminate the 18119M shader variant compile time.
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;

[InitializeOnLoad]
public static class TMP_ShaderFixer
{
    static TMP_ShaderFixer()
    {
        // Defer until editor is fully loaded
        EditorApplication.delayCall += FixTMPShaders;
    }

    [MenuItem("Tools/Fix TMP Shaders (Remove URP Lit)")]
    public static void FixTMPShaders()
    {
        Shader distanceFieldShader = Shader.Find("TextMeshPro/Distance Field");
        if (distanceFieldShader == null)
        {
            Debug.LogError("[TMP_ShaderFixer] Could not find 'TextMeshPro/Distance Field' shader.");
            return;
        }

        int fixedCount = 0;

        // Find all TMP_FontAsset objects in the project
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fontAsset == null) continue;

            // Fix the main material
            if (fontAsset.material != null)
            {
                string shaderName = fontAsset.material.shader != null ? fontAsset.material.shader.name : "null";
                if (shaderName != "TextMeshPro/Distance Field" &&
                    shaderName != "TextMeshPro/Mobile/Distance Field")
                {
                    fontAsset.material.shader = distanceFieldShader;
                    EditorUtility.SetDirty(fontAsset.material);
                    fixedCount++;
                    Debug.Log($"[TMP_ShaderFixer] Fixed shader on '{fontAsset.name}' material (was: {shaderName})");
                }
            }
        }

        // Also fix all standalone .mat files that use URP Lit TMP shader
        string[] matGuids = AssetDatabase.FindAssets("t:Material");
        foreach (string guid in matGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // Only process TMP-related materials
            if (!path.Contains("TextMesh Pro") && !path.Contains("TMP") && !path.Contains("Fonts")) continue;

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null) continue;

            string shaderName = mat.shader.name;
            if (shaderName.Contains("SRP") || shaderName.Contains("URP Lit") || shaderName.Contains("TMP_SDF-URP"))
            {
                mat.shader = distanceFieldShader;
                EditorUtility.SetDirty(mat);
                fixedCount++;
                Debug.Log($"[TMP_ShaderFixer] Fixed mat shader: {path} (was: {shaderName})");
            }
        }

        if (fixedCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TMP_ShaderFixer] Fixed {fixedCount} TMP material(s). URP Lit shader removed.");
        }
        else
        {
            Debug.Log("[TMP_ShaderFixer] No URP Lit TMP materials found. All good.");
        }
    }
}
#endif
