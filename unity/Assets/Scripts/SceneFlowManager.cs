using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneFlowManager : MonoBehaviour
{
    public static SceneFlowManager Instance { get; private set; }

    public string mapSelectionSceneName = "MapSelectionScene";
    public string loadingSceneName = "LoadingScene";
    public string vrArtTherapySceneName = "VRArtTherapyScene";

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
        SceneManager.LoadScene(mapSelectionSceneName);
    }

    public void GoToLoading()
    {
        Debug.Log("[SceneFlow] Transitioning to LoadingScene");
        SceneManager.LoadScene(loadingSceneName);
    }

    public void GoToVRArtTherapy()
    {
        Debug.Log("[SceneFlow] Transitioning to VRArtTherapyScene");
        SceneManager.LoadScene(vrArtTherapySceneName);
    }
}
