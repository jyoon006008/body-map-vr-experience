// EDITOR ONLY
// Manages NotoSansKR TMP Font Asset creation.
//
// Strategy:
//   - TMP_FontAsset.CreateFontAsset() in Dynamic mode produces an asset with
//     NO pre-baked atlas texture (atlas is populated at runtime on demand).
//   - Unity 6 serializes this correctly: atlasTextures[0] is a valid 2048x2048
//     Texture2D object (even if initially blank), so UnassignedReferenceException
//     should NOT occur for Dynamic assets saved after Unity 6.x.
//   - If the saved asset is still broken (atlas null), we delete it so the
//     fallback path in GetKoreanFontAsset() takes over.
//
// Run:   Tools > Recreate NotoSansKR TMP Font Asset
//        (also runs automatically once on project open)
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;

[InitializeOnLoad]
public static class TMP_FontAssetCreator
{
    private const string TTF_PATH   = "Assets/Resources/Fonts/NotoSansKR-Regular.ttf";
    private const string ASSET_PATH = "Assets/Resources/Fonts/NotoSansKR.asset";

    static TMP_FontAssetCreator()
    {
        EditorApplication.delayCall += ValidateOrCreateFontAsset;
    }

    // ─── Menu item (manual trigger) ───────────────────────────────────────────
    [MenuItem("Tools/Recreate NotoSansKR TMP Font Asset")]
    public static void ForceRecreate()
    {
        // Delete existing (potentially broken) asset
        if (System.IO.File.Exists(ASSET_PATH))
        {
            AssetDatabase.DeleteAsset(ASSET_PATH);
            AssetDatabase.Refresh();
            Debug.Log("[Font] Deleted old NotoSansKR.asset for recreation.");
        }
        ValidateOrCreateFontAsset();
    }

    // ─── Core logic ───────────────────────────────────────────────────────────
    public static void ValidateOrCreateFontAsset()
    {
        // Step 1: Check if a valid asset already exists
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ASSET_PATH);
        if (existing != null)
        {
            if (IsAssetValid(existing))
            {
                Debug.Log("[Font] NotoSansKR.asset is valid. No action needed.");
                return;
            }
            else
            {
                // Existing asset is broken — delete it
                Debug.LogWarning("[Font] NotoSansKR.asset exists but atlas is broken. Deleting and recreating...");
                AssetDatabase.DeleteAsset(ASSET_PATH);
                AssetDatabase.Refresh();
            }
        }

        // Step 2: Load source TTF
        Font sourceTTF = AssetDatabase.LoadAssetAtPath<Font>(TTF_PATH);
        if (sourceTTF == null)
        {
            Debug.LogError("[Font] Cannot find NotoSansKR-Regular.ttf at: " + TTF_PATH);
            return;
        }

        // Step 3: Create TMP_FontAsset
        // Dynamic mode: atlas is populated at runtime — no pre-bake needed.
        // Sampling 90, padding 9, atlas 2048x2048
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceTTF,
            samplingPointSize: 90,
            atlasPadding: 9,
            renderMode: UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
            atlasWidth: 2048,
            atlasHeight: 2048,
            atlasPopulationMode: AtlasPopulationMode.Dynamic
        );

        if (fontAsset == null)
        {
            Debug.LogError("[Font] TMP_FontAsset.CreateFontAsset() returned null. Cannot create NotoSansKR.asset.");
            return;
        }

        // Step 4: Force Distance Field shader
        Shader dfShader = Shader.Find("TextMeshPro/Distance Field");
        if (dfShader == null) dfShader = Shader.Find("TextMeshPro/Mobile/Distance Field");
        if (fontAsset.material != null && dfShader != null)
        {
            fontAsset.material.shader = dfShader;
            Debug.Log("[Font] Shader set to: " + dfShader.name);
        }

        // Step 5: Save main asset
        fontAsset.name = "NotoSansKR";
        AssetDatabase.CreateAsset(fontAsset, ASSET_PATH);

        // Step 6: Save material as sub-asset (required for correct serialization)
        if (fontAsset.material != null)
        {
            fontAsset.material.name = "NotoSansKR Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, ASSET_PATH);
        }

        // Step 7: Save atlas texture as sub-asset (critical for Unity to serialize it)
        if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
        {
            fontAsset.atlasTextures[0].name = "NotoSansKR Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], ASSET_PATH);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Step 8: Validate the saved asset
        TMP_FontAsset saved = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ASSET_PATH);
        if (saved == null)
        {
            Debug.LogError("[Font] NotoSansKR.asset could not be loaded after save.");
            return;
        }

        if (!IsAssetValid(saved))
        {
            Debug.LogWarning("[Font] NotoSansKR.asset was saved but atlas validation failed. " +
                             "The OS fallback font will be used instead. " +
                             "To fix: open Unity, select NotoSansKR-Regular.ttf, " +
                             "right-click > Create > TextMeshPro > Font Asset manually.");
            // Delete broken asset so runtime uses OS fallback cleanly
            AssetDatabase.DeleteAsset(ASSET_PATH);
            AssetDatabase.Refresh();
            return;
        }

        Debug.Log("[Font] Loaded: NotoSansKR  (saved at " + ASSET_PATH + ")");
        Debug.Log("[Font] Shader: " + (saved.material != null ? saved.material.shader.name : "null"));
        Debug.Log("[Font] Atlas: " + (saved.atlasTextures != null && saved.atlasTextures.Length > 0 ? "OK" : "MISSING"));
    }

    // ─── Validation ───────────────────────────────────────────────────────────
    private static bool IsAssetValid(TMP_FontAsset asset)
    {
        if (asset == null) return false;
        if (asset.material == null)
        {
            Debug.LogWarning("[Font] Validation FAILED: NotoSansKR.asset has no material.");
            return false;
        }
        // For Dynamic assets, atlasTextures may be empty initially but must not be null
        if (asset.atlasTextures == null)
        {
            Debug.LogWarning("[Font] Validation FAILED: NotoSansKR.asset atlasTextures is null.");
            return false;
        }
        return true;
    }
}
#endif
