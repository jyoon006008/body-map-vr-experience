using System;
using UnityEngine;

public class SafePlaceSessionData : MonoBehaviour
{
    public static SafePlaceSessionData Instance { get; private set; }

    [Header("Safe Place")]
    public string sessionId = "";
    public string userName = "";
    [TextArea(3, 6)] public string description = "";
    [TextArea(3, 8)] public string panoramaPrompt = "";
    public string skyboxAssetPath = "";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (string.IsNullOrEmpty(sessionId))
            {
                sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static SafePlaceSessionData GetOrCreate()
    {
        if (Instance != null) return Instance;

        GameObject go = new GameObject("SafePlaceSessionData");
        return go.AddComponent<SafePlaceSessionData>();
    }
}
