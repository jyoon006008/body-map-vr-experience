using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

public class SceneFlowManager : MonoBehaviour
{
    public static SceneFlowManager Instance { get; private set; }

    public string mapSelectionSceneName = "MapSelectionScene";
    public string safePlaceIntroSceneName = "SafePlaceIntroScene";
    public string loadingSceneName = "LoadingScene";
    public string vrArtTherapySceneName = "VRArtTherapyScene";

    private const string MapSelectionScenePath = "Assets/Scenes/MapSelectionScene.unity";
    private const string SafePlaceIntroScenePath = "Assets/Scenes/SafePlaceIntroScene.unity";
    private const string LoadingScenePath = "Assets/Scenes/LoadingScene.unity";
    private const string VRArtTherapyScenePath = "Assets/Scenes/VRArtTherapyScene.unity";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[SceneFlow] SceneFlowManager initialized.");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (SceneManager.GetActiveScene().name == "BootstrapScene")
        {
            Debug.Log("[SceneFlow] BootstrapScene loaded. Waiting for language selection.");
        }
    }

    public void GoToMapSelection()
    {
        Debug.Log("[SceneFlow] Transitioning to MapSelectionScene");
        TryLoadScene(mapSelectionSceneName, MapSelectionScenePath);
    }

    public void GoToSafePlaceIntro()
    {
        Debug.Log("[SceneFlow] Transitioning to SafePlaceIntroScene");
        if (!TryLoadScene(safePlaceIntroSceneName, SafePlaceIntroScenePath))
        {
            Debug.LogWarning("[SceneFlow] SafePlaceIntroScene is not in the active Build Profile. Starting the safe place intro inside the current scene.");
            StartSafePlaceIntroInCurrentScene();
        }
    }

    private void StartSafePlaceIntroInCurrentScene()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas != null)
            {
                canvas.gameObject.SetActive(false);
            }
        }

        if (FindFirstObjectByType<SafePlaceIntroManager>() == null)
        {
            GameObject safePlaceIntro = new GameObject("SafePlaceIntroManager_RuntimeFallback");
            safePlaceIntro.AddComponent<SafePlaceIntroManager>();
        }
    }

    public void GoToLoading()
    {
        Debug.Log("[SceneFlow] Transitioning to LoadingScene");
        TryLoadScene(loadingSceneName, LoadingScenePath);
    }

    public void GoToVRArtTherapy()
    {
        Debug.Log("[SceneFlow] Transitioning to VRArtTherapyScene");
        TryLoadScene(vrArtTherapySceneName, VRArtTherapyScenePath);
    }

    private bool TryLoadScene(string sceneName, string scenePath)
    {
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SceneManager.LoadScene(sceneName);
            return true;
        }

#if UNITY_EDITOR
        if (Application.isEditor)
        {
            Debug.LogWarning($"[SceneFlow] {sceneName} is not in the active Build Profile. Loading by asset path for Editor play mode.");
            EditorSceneManager.LoadSceneInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
            return true;
        }
#endif

        Debug.LogError($"[SceneFlow] {sceneName} could not be loaded. Add it to File > Build Profiles before making a player build.");
        return false;
    }
}
