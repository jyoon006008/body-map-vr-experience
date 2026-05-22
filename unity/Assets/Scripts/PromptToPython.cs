using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Diagnostics;
using System.Collections;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Text;
using System.Threading.Tasks;

public class PromptToPython : MonoBehaviour
{
    [Header("UI")]
    public TMP_InputField inputField;
    public CanvasGroup inputCanvasGroup;
    public GameObject dimOverlay;
    public TextMeshProUGUI loadingText;

    [Header("Slideshow UI")]
    public GameObject imageOptionPanel;
    public RawImage optionPreview;
    public Button leftButton;
    public Button rightButton;
    public Button selectButton;

    [Header("Player Control")]
    public MonoBehaviour playerController;
    public CharacterController characterController;

    [Header("Spawner")]
    public AutoMeshSpawner meshSpawner;

    [Header("Ground")]
    public GroundTextureApplier groundTextureApplier;

    [Header("Background")]
    public BackgroundDomeApplier backgroundDomeApplier;

    [Header("Paths")]
    public string windowsSceneOptionsDir = @"C:\AI\scene_options";
    public string wslSceneOptionsDir = "/mnt/c/AI/scene_options";

    [Header("WSL Command")]
    public string baseCommand =
        "cd ~/ai_pipeline && source ~/hunyuan3d/bin/activate && python automation.py";

    private bool uiMode = false;
    private bool isGenerating = false;
    private bool imageOptionsReady = false;

    private string currentPrompt = "";
    private int currentImageIndex = 0;
    private const int imageCount = 4;

    void Start()
    {
        DisableDimOverlayRaycast();

        SetPromptUI(false);

        if (loadingText != null)
            loadingText.gameObject.SetActive(false);

        if (imageOptionPanel != null)
            imageOptionPanel.SetActive(false);

        if (leftButton != null)
            leftButton.onClick.AddListener(ShowPreviousImage);

        if (rightButton != null)
            rightButton.onClick.AddListener(ShowNextImage);

        if (selectButton != null)
            selectButton.onClick.AddListener(SelectCurrentImage);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab) && !isGenerating)
        {
            uiMode = !uiMode;
            SetPromptUI(uiMode);

            if (imageOptionsReady)
            {
                ShowSelectionUI(true);
            }
        }

        if (uiMode && inputField != null && inputField.isFocused)
        {
            bool ctrlPressed =
                Input.GetKey(KeyCode.LeftControl) ||
                Input.GetKey(KeyCode.RightControl);

            if (Input.GetKeyDown(KeyCode.Return))
            {
                if (ctrlPressed)
                {
                    inputField.text += "\n";
                    inputField.caretPosition = inputField.text.Length;
                }
                else
                {
                    currentPrompt = inputField.text;
                    StartCoroutine(GenerateImageOptions(currentPrompt));
                }
            }
        }
    }

    void SetPromptUI(bool visible)
    {
        uiMode = visible;

        if (inputCanvasGroup != null)
        {
            inputCanvasGroup.alpha = visible ? 1f : 0f;
            inputCanvasGroup.interactable = true;
            inputCanvasGroup.blocksRaycasts = true;
        }

        if (dimOverlay != null)
            dimOverlay.SetActive(visible || imageOptionsReady || isGenerating);

        if (playerController != null)
            playerController.enabled = !visible && !imageOptionsReady && !isGenerating;

        if (characterController != null)
            characterController.enabled = !visible && !imageOptionsReady && !isGenerating;

        if (visible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (inputField != null)
            {
                inputField.gameObject.SetActive(true);
                inputField.interactable = true;
                inputField.readOnly = false;
                inputField.Select();
                inputField.ActivateInputField();
            }
        }
        else
        {
            if (!imageOptionsReady && !isGenerating)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (inputField != null)
            {
                inputField.DeactivateInputField();
                inputField.interactable = false;
                inputField.readOnly = true;
            }
        }
    }

    void ShowSelectionUI(bool show)
    {
        if (imageOptionPanel != null)
            imageOptionPanel.SetActive(show);

        if (dimOverlay != null)
            dimOverlay.SetActive(show || uiMode || isGenerating);

        if (show)
        {
            if (playerController != null)
                playerController.enabled = false;

            if (characterController != null)
                characterController.enabled = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    IEnumerator GenerateImageOptions(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            UnityEngine.Debug.LogWarning("Prompt is empty.");
            yield break;
        }

        isGenerating = true;
        imageOptionsReady = false;

        if (inputField != null)
            inputField.DeactivateInputField();

        if (dimOverlay != null)
            dimOverlay.SetActive(true);

        if (loadingText != null)
        {
            loadingText.gameObject.SetActive(true);
            loadingText.text = "Generating image options...";
        }

        if (imageOptionPanel != null)
            imageOptionPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        string escapedPrompt = prompt.Replace("\"", "\\\"");
        string command = baseCommand + " --generate \\\"" + escapedPrompt + "\\\"";

        yield return RunWSLCommand(command);

        currentImageIndex = 0;
        LoadCurrentImage();

        if (loadingText != null)
            loadingText.gameObject.SetActive(false);

        imageOptionsReady = true;
        isGenerating = false;

        ShowSelectionUI(true);
        UpdateSlideButtons();
    }

    void LoadCurrentImage()
    {
        string path = Path.Combine(
            windowsSceneOptionsDir,
            $"scene_{currentImageIndex}.png"
        );

        if (!File.Exists(path))
        {
            UnityEngine.Debug.LogWarning("Image option not found: " + path);
            return;
        }

        byte[] bytes = File.ReadAllBytes(path);

        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(bytes);

        if (optionPreview != null)
            optionPreview.texture = texture;
    }

    void ShowPreviousImage()
    {
        if (!imageOptionsReady)
            return;

        if (currentImageIndex <= 0)
            return;

        currentImageIndex--;
        LoadCurrentImage();
        UpdateSlideButtons();
    }

    void ShowNextImage()
    {
        if (!imageOptionsReady)
            return;

        if (currentImageIndex >= imageCount - 1)
            return;

        currentImageIndex++;
        LoadCurrentImage();
        UpdateSlideButtons();
    }

    void UpdateSlideButtons()
    {
        SetButtonVisible(leftButton, currentImageIndex > 0);
        SetButtonVisible(rightButton, currentImageIndex < imageCount - 1);
    }

    void SetButtonVisible(Button button, bool visible)
    {
        if (button == null)
            return;

        CanvasGroup cg = button.GetComponent<CanvasGroup>();

        if (cg == null)
            cg = button.gameObject.AddComponent<CanvasGroup>();

        cg.alpha = visible ? 1f : 0f;
        cg.interactable = visible;
        cg.blocksRaycasts = visible;
    }

    void SelectCurrentImage()
    {
        if (isGenerating)
            return;

        if (!imageOptionsReady)
            return;

        string selectedPath =
            $"{wslSceneOptionsDir}/scene_{currentImageIndex}.png";

        StartCoroutine(ProcessSelectedImage(selectedPath));
    }

    IEnumerator ProcessSelectedImage(string selectedImagePath)
    {
        isGenerating = true;
        imageOptionsReady = false;

        if (imageOptionPanel != null)
            imageOptionPanel.SetActive(false);

        if (loadingText != null)
        {
            loadingText.gameObject.SetActive(true);
            loadingText.text = "Generating 3D mesh...";
        }

        if (dimOverlay != null)
            dimOverlay.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        string escapedPrompt = currentPrompt.Replace("\"", "\\\"");
        string command =
            baseCommand +
            " --selected " +
            selectedImagePath +
            " \\\"" +
            escapedPrompt +
            "\\\"";

        yield return RunWSLCommand(command);

#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif

        if (meshSpawner != null)
            meshSpawner.SpawnLatestMesh();

        if (groundTextureApplier != null)
            groundTextureApplier.ApplyGroundTexture();

        if (backgroundDomeApplier != null)
            backgroundDomeApplier.ApplyBackgroundTexture();

        if (loadingText != null)
            loadingText.gameObject.SetActive(false);

        if (dimOverlay != null)
            dimOverlay.SetActive(false);

        SetPromptUI(false);

        if (playerController != null)
            playerController.enabled = true;

        if (characterController != null)
            characterController.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        uiMode = false;
        isGenerating = false;

        UnityEngine.Debug.Log("Selected image pipeline finished.");
    }

    IEnumerator RunWSLCommand(string command)
    {
        StringBuilder outputBuilder = new StringBuilder();
        StringBuilder errorBuilder = new StringBuilder();

        Task<int> task = Task.Run(() =>
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "wsl.exe";
            psi.Arguments = "bash -lc \"" + command + "\"";
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;

            using (Process process = new Process())
            {
                process.StartInfo = psi;
                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                outputBuilder.Append(output);
                errorBuilder.Append(error);

                return process.ExitCode;
            }
        });

        float dotTimer = 0f;
        int dotCount = 0;
        string baseLoadingText = loadingText != null ? loadingText.text.TrimEnd('.') : "";

        while (!task.IsCompleted)
        {
            dotTimer += Time.deltaTime;

            if (dotTimer >= 0.4f)
            {
                dotTimer = 0f;
                dotCount = (dotCount + 1) % 4;

                if (loadingText != null)
                    loadingText.text = baseLoadingText + new string('.', dotCount);
            }

            yield return null;
        }

        if (task.Result != 0)
        {
            UnityEngine.Debug.LogError(
                "WSL command failed with exit code: " + task.Result
            );

            if (errorBuilder.Length > 0)
                UnityEngine.Debug.LogError(errorBuilder.ToString());
        }
    }

    void DisableDimOverlayRaycast()
    {
        if (dimOverlay == null)
            return;

        Image image = dimOverlay.GetComponent<Image>();

        if (image != null)
            image.raycastTarget = false;
    }
}