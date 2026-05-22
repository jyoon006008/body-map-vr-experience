using UnityEngine;

public class OpenURLHelper : MonoBehaviour
{
    public string url = "https://jyoon006008.github.io/body-map-vr-experience/web/";

    public void OpenWebClient()
    {
        Debug.Log($"[OpenURLHelper] OpenWebClient triggered. Target URL: {url}");

        bool opened = false;

        #if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        try
        {
            Debug.Log("[OpenURLHelper] macOS standalone detected. Attempting launch using native process command: 'open'...");
            System.Diagnostics.Process.Start("open", url);
            Debug.Log("[OpenURLHelper] Native 'open' process command executed successfully.");
            opened = true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[OpenURLHelper] Native macOS process start failed: {ex.Message}. Will attempt fallback to standard Application.OpenURL.");
        }
        #endif

        if (!opened)
        {
            try
            {
                string escapedUrl = System.Uri.EscapeUriString(url);
                Debug.Log($"[OpenURLHelper] Calling Application.OpenURL with: {escapedUrl}");
                Application.OpenURL(escapedUrl);
                Debug.Log("[OpenURLHelper] Application.OpenURL call completed.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[OpenURLHelper] Application.OpenURL failed: {e.Message}");
            }
        }
    }
}

