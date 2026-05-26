using UnityEngine;
using System.Collections.Generic;

public class MapSelectionManager : MonoBehaviour
{
    [Header("Map Options Configuration")]
    public List<MapOption> mapOptions = new List<MapOption>();

    [Header("UI References")]
    public Transform cardParent;

    private TMPro.TextMeshProUGUI languageToggleText;

    void Start()
    {
        Debug.Log("[MapSelectionManager] Static UI MapSelectionManager initialized.");
        
        // Find language toggle button component dynamically
        GameObject toggleBtnObj = GameObject.Find("ChangeLanguageButton");
        if (toggleBtnObj != null)
        {
            var btn = toggleBtnObj.GetComponent<UnityEngine.UI.Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(ToggleLanguage);
            }
            languageToggleText = toggleBtnObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        }

        ApplyLocalization();
    }

    public void ToggleLanguage()
    {
        if (GameManager.Instance != null)
        {
            string newLang = GameManager.Instance.currentLanguage == "ko" ? "en" : "ko";
            GameManager.Instance.currentLanguage = newLang;
            Debug.Log($"[MapSelectionManager] Toggled language to: {newLang}");
        }
        else
        {
            Debug.LogWarning("[MapSelectionManager] GameManager.Instance is null. Local toggle only.");
        }
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        bool isKorean = true;
        if (GameManager.Instance != null)
        {
            isKorean = GameManager.Instance.currentLanguage == "ko";
        }

        // Translate Title & Subtitle
        GameObject titleObj = GameObject.Find("TitleText");
        if (titleObj != null)
        {
            var titleText = titleObj.GetComponent<TMPro.TextMeshProUGUI>();
            if (titleText != null)
            {
                titleText.text = isKorean ? "환경 선택" : "SELECT ENVIRONMENT";
                if (isKorean) BodyMapAIController.ApplyKoreanFont(titleText);
            }
        }

        GameObject subtitleObj = GameObject.Find("SubtitleText");
        if (subtitleObj != null)
        {
            var subtitleText = subtitleObj.GetComponent<TMPro.TextMeshProUGUI>();
            if (subtitleText != null)
            {
                subtitleText.text = isKorean 
                    ? "VR 미술 치료 세션을 시작할 환경을 선택하세요" 
                    : "Choose an environment to begin your VR Art Therapy session";
                if (isKorean) BodyMapAIController.ApplyKoreanFont(subtitleText);
            }
        }

        // Translate Cards
        foreach (var opt in mapOptions)
        {
            GameObject cardObj = GameObject.Find("Card_" + opt.sceneName);
            if (cardObj != null)
            {
                // Find DisplayName text
                Transform nameTrans = cardObj.transform.Find("DisplayName");
                if (nameTrans != null)
                {
                    var nameText = nameTrans.GetComponent<TMPro.TextMeshProUGUI>();
                    if (nameText != null)
                    {
                        if (opt.sceneName == "Env_URP_Garden")
                            nameText.text = isKorean ? "정원 환경" : "Garden Environment";
                        else if (opt.sceneName == "Env_URP_Desert")
                            nameText.text = isKorean ? "사막 오아시스" : "Desert Oasis";
                        
                        if (isKorean) BodyMapAIController.ApplyKoreanFont(nameText);
                    }
                }

                // Find Description text
                Transform descTrans = cardObj.transform.Find("Description");
                if (descTrans != null)
                {
                    var descText = descTrans.GetComponent<TMPro.TextMeshProUGUI>();
                    if (descText != null)
                    {
                        if (opt.sceneName == "Env_URP_Garden")
                            descText.text = isKorean 
                                ? "나무와 중앙 상호작용 공간이 있는 열린 자연 환경입니다." 
                                : "Open natural environment with trees and central interaction space.";
                        else if (opt.sceneName == "Env_URP_Desert")
                            descText.text = isKorean 
                                ? "차분한 분위기의 드넓은 사막 환경입니다." 
                                : "Wide open desert-like environment with calm atmosphere.";

                        if (isKorean) BodyMapAIController.ApplyKoreanFont(descText);
                    }
                }

                // Find SelectButton Text
                Transform btnTrans = cardObj.transform.Find("SelectButton");
                if (btnTrans != null)
                {
                    Transform btnTextTrans = btnTrans.Find("Text");
                    if (btnTextTrans != null)
                    {
                        var btnText = btnTextTrans.GetComponent<TMPro.TextMeshProUGUI>();
                        if (btnText != null)
                        {
                            btnText.text = isKorean ? "선택" : "Select";
                            if (isKorean) BodyMapAIController.ApplyKoreanFont(btnText);
                        }
                    }
                }
            }
        }

        // Update Language Toggle Button Text
        if (languageToggleText != null)
        {
            languageToggleText.text = isKorean ? "Change Language: English" : "언어 변경: 한국어";
            if (isKorean)
            {
                BodyMapAIController.ApplyKoreanFont(languageToggleText);
            }
        }
    }

    public void SelectMap(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[MapSelectionManager] SelectMap called with null or empty sceneName!");
            return;
        }

        Debug.Log($"[MapSelectionManager] SelectMap called: {sceneName}");

        string displayName = "Unknown Environment";
        if (mapOptions != null)
        {
            foreach (var opt in mapOptions)
            {
                if (opt.sceneName == sceneName)
                {
                    displayName = opt.displayName;
                    break;
                }
            }
        }

        // Fallback display names
        if (displayName == "Unknown Environment")
        {
            if (sceneName == "Env_URP_Garden") displayName = "Garden Environment";
            else if (sceneName == "Env_URP_Desert") displayName = "Desert Oasis";
            else if (sceneName == "Env_URP_Terminal") displayName = "Terminal Hangar";
            else if (sceneName == "Env_URP_Gallery") displayName = "Gallery";
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetSelectedEnvironment(sceneName, displayName);
        }
        else
        {
            Debug.LogWarning("[MapSelectionManager] GameManager.Instance is null!");
        }

        Debug.Log("[MapSelectionManager] Loading scene transition requested");

        if (SceneFlowManager.Instance != null)
        {
            SceneFlowManager.Instance.GoToLoading();
        }
        else
        {
            Debug.Log("[SceneFlow] Transitioning to LoadingScene");
            UnityEngine.SceneManagement.SceneManager.LoadScene("LoadingScene");
        }
    }
}
