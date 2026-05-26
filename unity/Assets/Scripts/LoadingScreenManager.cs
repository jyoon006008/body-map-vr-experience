using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

public class LoadingScreenManager : MonoBehaviour
{
    [Header("UI Elements")]
    public Slider progressBar;
    public TextMeshProUGUI progressText;
    public TextMeshProUGUI statusText;

    [Header("Loading Configuration")]
    public string targetSceneName = "VRArtTherapyScene";
    public float minLoadingDuration = 1.5f;

    void Start()
    {
        Debug.Log("[LoadingScreen] Loading VRArtTherapyScene");
        StartCoroutine(LoadTargetSceneAsync());
    }

    private IEnumerator LoadTargetSceneAsync()
    {
        float startTime = Time.time;
        AsyncOperation op = SceneManager.LoadSceneAsync(targetSceneName);
        op.allowSceneActivation = false;

        while (!op.isDone)
        {
            float loadProgress = Mathf.Clamp01(op.progress / 0.9f);
            float elapsedTime = Time.time - startTime;
            float timeRatio = Mathf.Clamp01(elapsedTime / minLoadingDuration);
            float actualProgress = Mathf.Min(loadProgress, timeRatio);

            if (progressBar != null)
            {
                progressBar.value = actualProgress;
            }

            int percentage = Mathf.RoundToInt(actualProgress * 100);
            if (progressText != null)
            {
                progressText.text = $"Loading... {percentage}%";
            }

            Debug.Log($"[LoadingScreen] Progress: {percentage}%");

            if (op.progress >= 0.9f && elapsedTime >= minLoadingDuration)
            {
                Debug.Log("[LoadingScreen] VRArtTherapyScene loaded");
                op.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}
