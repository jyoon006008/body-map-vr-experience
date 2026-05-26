using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class EnvironmentLoader : MonoBehaviour
{
    [Header("Target References")]
    [SerializeField] private BodyMapReceiver bodyMapReceiver;
    [SerializeField] private Transform playerTransform;

    private string loadedEnvSceneName = "";

    void Awake()
    {
        if (bodyMapReceiver == null)
        {
            bodyMapReceiver = FindFirstObjectByType<BodyMapReceiver>();
        }

        if (bodyMapReceiver != null)
        {
            bodyMapReceiver.enabled = false;
            Debug.Log("[EnvironmentLoader] BodyMapReceiver disabled initially to wait for environment loading.");
        }

        if (playerTransform == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
    }

    void Start()
    {
        string selectedEnv = "";
        if (GameManager.Instance != null)
        {
            selectedEnv = GameManager.Instance.GetSelectedEnvironmentSceneName();
        }

        if (string.IsNullOrEmpty(selectedEnv))
        {
            selectedEnv = "Env_URP_Garden";
            Debug.LogWarning("[EnvironmentLoader] No environment scene selected in GameManager. Falling back to Env_URP_Garden.");
        }

        Debug.Log($"[EnvironmentLoader] Selected environment: {selectedEnv}");
        StartCoroutine(LoadEnvironmentAdditive(selectedEnv));
    }

    private IEnumerator LoadEnvironmentAdditive(string sceneName)
    {
        Debug.Log($"[EnvironmentLoader] Loading selected environment additively: {sceneName}");
        
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (op == null)
        {
            Debug.LogError($"[EnvironmentLoader] Failed to start additive loading for scene: {sceneName}. Make sure it is in Build Settings!");
            if (bodyMapReceiver != null)
            {
                bodyMapReceiver.enabled = true;
            }
            yield break;
        }

        while (!op.isDone)
        {
            yield return null;
        }

        loadedEnvSceneName = sceneName;
        Debug.Log($"[EnvironmentLoader] Environment loaded: {sceneName}");

        Scene loadedScene = SceneManager.GetSceneByName(sceneName);
        if (loadedScene.IsValid())
        {
            SceneManager.SetActiveScene(loadedScene);
            DisableDuplicateComponentsInLoadedScene(loadedScene);
        }

        var spawnPoint = FindFirstObjectByType<EnvironmentSpawnPoint>();
        if (spawnPoint != null)
        {
            Debug.Log($"[EnvironmentLoader] Spawn point found: {spawnPoint.gameObject.name}");
            if (playerTransform != null)
            {
                playerTransform.position = spawnPoint.transform.position;
                playerTransform.rotation = spawnPoint.transform.rotation;
                Debug.Log($"[EnvironmentLoader] Player moved to spawn: {playerTransform.position}");
            }
        }
        else
        {
            Debug.LogWarning("[EnvironmentLoader] EnvironmentSpawnPoint not found in loaded environment scene!");
        }

        if (bodyMapReceiver != null)
        {
            bodyMapReceiver.enabled = true;
            Debug.Log($"[EnvironmentLoader] Body mapping enabled: {bodyMapReceiver.enabled}");
        }
    }

    private void DisableDuplicateComponentsInLoadedScene(Scene loadedScene)
    {
        Debug.Log($"[EnvironmentLoader] Scanning loaded scene '{loadedScene.name}' to disable conflicting player, camera, and UI elements.");
        GameObject[] rootObjects = loadedScene.GetRootGameObjects();
        foreach (var obj in rootObjects)
        {
            string name = obj.name.ToLower();
            
            // If it's the player, player capsule, or camera, disable the entire GameObject
            if (name.Contains("player") || 
                name.Contains("controller") || 
                name.Contains("camera") || 
                name.Contains("canvas") || 
                name.Contains("eventsystem") ||
                name.Contains("rig") ||
                name.Contains("ui"))
            {
                obj.SetActive(false);
                Debug.Log($"[EnvironmentLoader] Disabled duplicate root GameObject: {obj.name}");
                continue;
            }

            // Otherwise check components individually inside hierarchy
            Camera[] cams = obj.GetComponentsInChildren<Camera>(true);
            foreach (var cam in cams)
            {
                cam.enabled = false;
                Debug.Log($"[EnvironmentLoader] Disabled duplicate Camera component in: {cam.gameObject.name}");
            }

            AudioListener[] listeners = obj.GetComponentsInChildren<AudioListener>(true);
            foreach (var l in listeners)
            {
                l.enabled = false;
                Debug.Log($"[EnvironmentLoader] Disabled duplicate AudioListener component in: {l.gameObject.name}");
            }

            Canvas[] canvases = obj.GetComponentsInChildren<Canvas>(true);
            foreach (var canv in canvases)
            {
                canv.enabled = false;
                Debug.Log($"[EnvironmentLoader] Disabled duplicate Canvas component in: {canv.gameObject.name}");
            }

            UnityEngine.EventSystems.EventSystem[] evSystems = obj.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true);
            foreach (var ev in evSystems)
            {
                ev.enabled = false;
                Debug.Log($"[EnvironmentLoader] Disabled duplicate EventSystem component in: {ev.gameObject.name}");
            }

            // Disable PlayableDirectors (cinematic timelines) in loaded scene
            UnityEngine.Playables.PlayableDirector[] directors = obj.GetComponentsInChildren<UnityEngine.Playables.PlayableDirector>(true);
            foreach (var dir in directors)
            {
                dir.Stop();
                dir.enabled = false;
                Debug.Log($"[EnvironmentLoader] Disabled duplicate PlayableDirector in: {dir.gameObject.name}");
            }

            // Disable Cinemachine virtual cameras and brains
            Cinemachine.CinemachineVirtualCamera[] vCams = obj.GetComponentsInChildren<Cinemachine.CinemachineVirtualCamera>(true);
            foreach (var vCam in vCams)
            {
                vCam.enabled = false;
                Debug.Log($"[EnvironmentLoader] Disabled duplicate CinemachineVirtualCamera in: {vCam.gameObject.name}");
            }

            Cinemachine.CinemachineBrain[] brains = obj.GetComponentsInChildren<Cinemachine.CinemachineBrain>(true);
            foreach (var brain in brains)
            {
                brain.enabled = false;
                Debug.Log($"[EnvironmentLoader] Disabled duplicate CinemachineBrain in: {brain.gameObject.name}");
            }

            // Disable MediaSceneLoader, PlayerManager, SceneLoader and other scripts that drive flythrough transitions
            MediaSceneLoader[] mediaLoaders = obj.GetComponentsInChildren<MediaSceneLoader>(true);
            foreach (var ml in mediaLoaders)
            {
                ml.enabled = false;
                Debug.Log($"[EnvironmentLoader] Disabled duplicate MediaSceneLoader in: {ml.gameObject.name}");
            }

            PlayerManager[] playerManagers = obj.GetComponentsInChildren<PlayerManager>(true);
            foreach (var pm in playerManagers)
            {
                pm.enabled = false;
                Debug.Log($"[EnvironmentLoader] Disabled duplicate PlayerManager in: {pm.gameObject.name}");
            }

            SceneLoader[] sceneLoaders = obj.GetComponentsInChildren<SceneLoader>(true);
            foreach (var sl in sceneLoaders)
            {
                sl.enabled = false;
                Debug.Log($"[EnvironmentLoader] Disabled duplicate SceneLoader in: {sl.gameObject.name}");
            }

            SceneTransitionManager[] stManagers = obj.GetComponentsInChildren<SceneTransitionManager>(true);
            foreach (var stm in stManagers)
            {
                stm.enabled = false;
                Debug.Log($"[EnvironmentLoader] Disabled duplicate SceneTransitionManager in: {stm.gameObject.name}");
            }
        }
    }

    public void UnloadCurrentEnvironment()
    {
        if (!string.IsNullOrEmpty(loadedEnvSceneName))
        {
            Debug.Log("[EnvironmentLoader] Unloading environment scene: " + loadedEnvSceneName);
            SceneManager.UnloadSceneAsync(loadedEnvSceneName);
            loadedEnvSceneName = "";
        }
    }
}
