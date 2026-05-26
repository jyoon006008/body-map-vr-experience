using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapSelectionManager : MonoBehaviour
{
    [Header("Map Options Configuration")]
    public List<MapOption> mapOptions = new List<MapOption>();

    [Header("UI References")]
    public Transform cardParent;

    private TextMeshProUGUI languageToggleText;

    void Start()
    {
        Debug.Log("[MapSelectionManager] Static UI MapSelectionManager initialized.");

        GameObject toggleBtnObj = GameObject.Find("ChangeLanguageButton");
        if (toggleBtnObj != null)
        {
            var btn = toggleBtnObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(ToggleLanguage);
            }
            languageToggleText = toggleBtnObj.GetComponentInChildren<TextMeshProUGUI>();
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
        bool isKorean = GameManager.Instance == null || GameManager.Instance.currentLanguage == "ko";

        SetText("TitleText", isKorean ? "환경 선택" : "SELECT ENVIRONMENT", isKorean);
        SetText(
            "SubtitleText",
            isKorean ? "body mapping 세션을 진행할 환경을 선택하세요" : "Choose an environment for the body mapping session",
            isKorean);

        foreach (var opt in mapOptions)
        {
            GameObject cardObj = GameObject.Find("Card_" + opt.sceneName);
            if (cardObj == null) continue;

            SetChildText(cardObj.transform, "DisplayName", GetDisplayName(opt.sceneName, isKorean), isKorean);
            SetChildText(cardObj.transform, "Description", GetDescription(opt.sceneName, isKorean), isKorean);

            Transform btnTrans = cardObj.transform.Find("SelectButton");
            if (btnTrans != null)
            {
                SetChildText(btnTrans, "Text", isKorean ? "선택" : "Select", isKorean);
            }
        }

        if (languageToggleText != null)
        {
            languageToggleText.text = isKorean ? "언어 변경: English" : "Change Language: 한국어";
            if (isKorean) BodyMapAIController.ApplyKoreanFont(languageToggleText);
        }
    }

    private void SetText(string objectName, string value, bool applyKoreanFont)
    {
        GameObject obj = GameObject.Find(objectName);
        if (obj == null) return;

        var text = obj.GetComponent<TextMeshProUGUI>();
        if (text == null) return;

        text.text = value;
        if (applyKoreanFont) BodyMapAIController.ApplyKoreanFont(text);
    }

    private void SetChildText(Transform root, string childName, string value, bool applyKoreanFont)
    {
        Transform child = root.Find(childName);
        if (child == null) return;

        var text = child.GetComponent<TextMeshProUGUI>();
        if (text == null) return;

        text.text = value;
        if (applyKoreanFont) BodyMapAIController.ApplyKoreanFont(text);
    }

    private string GetDisplayName(string sceneName, bool isKorean)
    {
        if (sceneName == "Env_URP_Garden") return isKorean ? "정원 환경" : "Garden Environment";
        if (sceneName == "Env_URP_Desert") return isKorean ? "사막 오아시스" : "Desert Oasis";
        if (sceneName == "Env_URP_Terminal") return isKorean ? "터미널 격납고" : "Terminal Hangar";
        if (sceneName == "Env_URP_Gallery") return isKorean ? "갤러리" : "Gallery";
        return isKorean ? "환경" : "Environment";
    }

    private string GetDescription(string sceneName, bool isKorean)
    {
        if (sceneName == "Env_URP_Garden")
        {
            return isKorean
                ? "나무와 중앙 상호작용 공간이 있는 body mapping 세션 환경입니다."
                : "Open natural environment with trees and central interaction space.";
        }

        if (sceneName == "Env_URP_Desert")
        {
            return isKorean
                ? "차분한 분위기의 넓은 사막 오아시스 body mapping 세션 환경입니다."
                : "Wide open desert-like environment with calm atmosphere.";
        }

        return isKorean ? "body mapping 세션을 진행할 수 있는 환경입니다." : "An environment for the body mapping session.";
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

        if (displayName == "Unknown Environment")
        {
            displayName = GetDisplayName(sceneName, false);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetSelectedEnvironment(sceneName, displayName);
        }
        else
        {
            Debug.LogWarning("[MapSelectionManager] GameManager.Instance is null!");
        }

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
