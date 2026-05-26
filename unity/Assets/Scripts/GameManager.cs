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

        AudioClip clip = Resources.Load<AudioClip>("Audio/Therapy_Music");
        if (clip != null)
        {
            bgmAudioSource.clip = clip;
            bgmAudioSource.loop = true;
            bgmAudioSource.volume = 0.22f; // Gentle background volume
            bgmAudioSource.spatialBlend = 0f; // 2D Stereo
            bgmAudioSource.playOnAwake = false;
            bgmAudioSource.Play();
            Debug.Log("[GameManager] Background music (Therapy_Music) started playing.");
        }
        else
        {
            Debug.LogError("[GameManager] Background music file 'Resources/Audio/Therapy_Music' not found! Make sure Therapy_Music.mp3 is placed inside Assets/Resources/Audio folder.");
        }
    }
}
