using UnityEngine;
using System.IO;

public class BackgroundDomeApplier : MonoBehaviour
{
    [Header("Texture")]
    public string backgroundTexturePath =
        @"C:\AI\grounded_sam_output\regenerated_background.png";

    void Start()
    {
        ApplyBackgroundTexture();
    }

    [ContextMenu("Apply Background Texture")]
    public void ApplyBackgroundTexture()
    {
        if (!File.Exists(backgroundTexturePath))
        {
            Debug.LogWarning("Background texture not found: " + backgroundTexturePath);
            return;
        }

        byte[] bytes = File.ReadAllBytes(backgroundTexturePath);
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(bytes);
        texture.wrapMode = TextureWrapMode.Clamp;

        // Skybox/Panoramic은 빌트인 셰이더라 URP 관계없이 동작
        Material skyMat = new Material(Shader.Find("Skybox/Panoramic"));
        skyMat.SetTexture("_MainTex", texture);
        skyMat.SetFloat("_Rotation", 0f);

        RenderSettings.skybox = skyMat;
        DynamicGI.UpdateEnvironment();

        Debug.Log("Skybox background applied");
    }
}