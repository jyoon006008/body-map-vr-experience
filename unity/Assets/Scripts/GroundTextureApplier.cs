using UnityEngine;
using System.IO;

public class GroundTextureApplier : MonoBehaviour
{
    [Header("Target")]
    public Renderer targetRenderer;

    [Header("Texture Path")]
    public string groundTexturePath = @"C:\AI\grounded_sam_output\ground.png";

    [Header("Material Settings")]
    public float textureTiling = 1f;

    private Material runtimeMaterial;

    void Start()
    {
        ApplyGroundTexture();
    }

    [ContextMenu("Apply Ground Texture")]
    public void ApplyGroundTexture()
    {
        if (targetRenderer == null)
        {
            Debug.LogError("Target Renderer is missing.");
            return;
        }

        if (!File.Exists(groundTexturePath))
        {
            Debug.LogWarning("Ground texture not found: " + groundTexturePath);
            return;
        }

        byte[] bytes = File.ReadAllBytes(groundTexturePath);

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(bytes);

        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        if (runtimeMaterial == null)
        {
            runtimeMaterial = new Material(Shader.Find("Standard"));
            targetRenderer.material = runtimeMaterial;
        }

        runtimeMaterial.mainTexture = texture;
        runtimeMaterial.mainTextureScale = new Vector2(textureTiling, textureTiling);

        Debug.Log("Applied ground texture: " + groundTexturePath);
    }
}