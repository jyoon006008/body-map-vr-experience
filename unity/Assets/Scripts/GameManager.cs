using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Selected Environment Info")]
    [SerializeField] private string selectedEnvironmentSceneName = "";
    [SerializeField] private string selectedEnvironmentDisplayName = "";

    [Header("Language Setting")]
    public string currentLanguage = "ko"; // "ko" or "en"

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[SceneFlow] GameManager initialized and set to DontDestroyOnLoad.");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetSelectedEnvironment(string sceneName, string displayName)
    {
        selectedEnvironmentSceneName = sceneName;
        selectedEnvironmentDisplayName = displayName;
        Debug.Log($"[GameManager] Selected environment: {sceneName}");
    }

    public string GetSelectedEnvironmentSceneName() => selectedEnvironmentSceneName;
    public string GetSelectedEnvironmentDisplayName() => selectedEnvironmentDisplayName;
}
