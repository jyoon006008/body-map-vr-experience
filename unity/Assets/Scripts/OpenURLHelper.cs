using UnityEngine;

public class OpenURLHelper : MonoBehaviour
{
    public string url = "file:///C:/Users/junwo/.gemini/antigravity/scratch/body-map-detector/index.html";

    public void OpenWebClient()
    {
        Application.OpenURL(url);
        Debug.Log($"[OpenURLHelper] Opening URL: {url}");
    }
}
