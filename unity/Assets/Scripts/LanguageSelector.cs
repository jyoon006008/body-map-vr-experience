using UnityEngine;

public class LanguageSelector : MonoBehaviour
{
    public void SelectLanguage(string languageCode)
    {
        Debug.Log($"[LanguageSelector] SelectLanguage: {languageCode}");
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.currentLanguage = languageCode;
            Debug.Log($"[LanguageSelector] GameManager.Instance.currentLanguage set to {languageCode}");
        }
        else
        {
            Debug.LogWarning("[LanguageSelector] GameManager.Instance is null!");
        }

        if (SceneFlowManager.Instance != null)
        {
            SceneFlowManager.Instance.GoToMapSelection();
        }
        else
        {
            Debug.LogWarning("[LanguageSelector] SceneFlowManager.Instance is null! Falling back to SceneManager.");
            UnityEngine.SceneManagement.SceneManager.LoadScene("MapSelectionScene");
        }
    }
}
