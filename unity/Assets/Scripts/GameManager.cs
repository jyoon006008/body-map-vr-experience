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

    [Header("Background Music")]
    private AudioSource bgmAudioSource;

    public void PlayBackgroundMusic()
    {
        if (bgmAudioSource != null && bgmAudioSource.isPlaying) return;

        if (bgmAudioSource == null)
        {
            bgmAudioSource = gameObject.AddComponent<AudioSource>();
        }

        // Start asynchronous loading to prevent main thread blocking (lag)
        StartCoroutine(LoadAndPlayBgmAsync());
    }

    private System.Collections.IEnumerator LoadAndPlayBgmAsync()
    {
        ResourceRequest request = Resources.LoadAsync<AudioClip>("Audio/bgtherapy");
        yield return request;

        AudioClip clip = request.asset as AudioClip;
        if (clip != null && bgmAudioSource != null)
        {
            bgmAudioSource.clip = clip;
            bgmAudioSource.loop = true;
            bgmAudioSource.volume = 0.20f; // Set to 20% volume
            bgmAudioSource.spatialBlend = 0f; // 2D Stereo
            bgmAudioSource.playOnAwake = false;
            bgmAudioSource.Play();
            Debug.Log("[GameManager] Background music (bgtherapy) started playing asynchronously.");
        }
        else if (clip == null)
        {
            Debug.LogError("[GameManager] Background music file 'Resources/Audio/bgtherapy' not found asynchronously!");
        }
    }
}
