using System.Collections;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

public class SafePlaceIntroManager : MonoBehaviour
{
    [Header("Tutorial")]
    [TextArea(2, 4)]
    public string[] koreanTutorialPages =
    {
        "이 경험에서는 몸에 그려지는 색과 형태를 통해 감정을 천천히 바라봅니다.",
        "정답을 찾는 시간이 아닙니다. 지금 몸과 마음이 어떤 이미지를 만들고 있는지 함께 살펴보는 시간입니다.",
        "이제 당신만의 safe place를 상상하고, 그곳에서 body mapping 세션을 준비합니다."
    };

    [TextArea(2, 4)]
    public string[] englishTutorialPages =
    {
        "In this experience, you will gently explore emotions through colors and shapes drawn on the body.",
        "There is no correct answer here. We will simply notice what your body and mind are showing today.",
        "Next, we will imagine a safe place and prepare the body mapping session from there."
    };

    [Header("External Safe Place Panorama")]
    public string safePlacePanoramaFileName = "OfficeInterior.jpeg";
    public string safePlacePanoramaFolder = "SafePlacePanoramas";

    [Header("Voice Conversation")]
    public bool voiceConversationEnabled = true;
    public float maxRecordTime = 20f;
    public float pauseThreshold = 1.3f;
    public float rmsStartThreshold = 0.015f;
    public float rmsEndThreshold = 0.008f;
    public float minimumRecordingLength = 1.0f;
    public float postSpeechBuffer = 0.8f;

    [Header("Generated UI")]
    public Canvas canvas;
    public CanvasGroup blackOverlay;
    public RectTransform dialoguePanel;
    public TextMeshProUGUI dialogueText;
    public Button nextButton;
    public TMP_InputField inputField;
    public Button submitButton;
    public TextMeshProUGUI hintText;
    public GameObject aiSphere;
    public TextMeshProUGUI safePlaceInstructionText;
    public float desktopLookSensitivity = 2.2f;

    private int pageIndex;
    private int conversationStep;
    private bool safePlaceReady;
    private bool mapBoardVisible;
    private string smallTalkDayAnswer = "";
    private string smallTalkFeelingAnswer = "";
    private string safePlaceThemeAnswer = "";
    private string safePlaceLightAnswer = "";
    private string safePlaceSensesAnswer = "";
    private float cameraYaw;
    private float cameraPitch;
    private Material safePlaceSkyboxMaterial;
    private Material aiSphereMaterial;
    private Material aiGlowMaterial;
    private GameObject aiGlowSphere;
    private Light aiPointLight;
    private Canvas mapBoardCanvas;
    private AudioSource audioSource;
    private AudioClip micRecordClip;
    private string openAiApiKey = "";
    private bool isAiSpeechActive;
    private bool isListeningForVoice;
    private bool isProcessingVoice;
    private bool hasDetectedVoiceInCurrentClip;
    private float recordStartTime;
    private float lastTimeVoiceDetected;
    private float voiceStartTime = -1f;
    private float voiceEndTime = -1f;
    private float preSpeechBuffer = 0.3f;
    private Coroutine speechCoroutine;
    private bool suppressInputChange;

    void Start()
    {
        SafePlaceSessionData.GetOrCreate();
        LoadOpenAIKey();
        EnsureSceneObjects();
        ShowTutorialPage(0);
    }

    void Update()
    {
        PulseAISphere();
        UpdateDesktopLook();
        HandleVoiceRecording();

        if (safePlaceReady && Input.GetKeyDown(KeyCode.M))
        {
            ToggleMapSelectionBoard();
        }
    }

    private bool IsKorean()
    {
        return GameManager.Instance == null || GameManager.Instance.currentLanguage == "ko";
    }

    private void EnsureSceneObjects()
    {
        EnsureCamera();
        EnsureEventSystem();

        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("SafePlaceCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        if (blackOverlay == null)
        {
            GameObject overlayGO = CreateUIObject("BlackOverlay", canvas.transform);
            var image = overlayGO.AddComponent<Image>();
            image.color = Color.black;
            var rect = overlayGO.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            blackOverlay = overlayGO.AddComponent<CanvasGroup>();
            blackOverlay.alpha = 1f;
        }

        if (dialoguePanel == null)
        {
            GameObject panelGO = CreateUIObject("DialoguePanel", canvas.transform);
            var image = panelGO.AddComponent<Image>();
            image.color = new Color(0.04f, 0.04f, 0.05f, 0.94f);
            dialoguePanel = panelGO.GetComponent<RectTransform>();
            dialoguePanel.anchorMin = new Vector2(0.12f, 0.06f);
            dialoguePanel.anchorMax = new Vector2(0.88f, 0.32f);
            dialoguePanel.offsetMin = Vector2.zero;
            dialoguePanel.offsetMax = Vector2.zero;

            dialogueText = CreateText("DialogueText", panelGO.transform, 30, TextAlignmentOptions.TopLeft);
            var textRect = dialogueText.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.05f, 0.38f);
            textRect.anchorMax = new Vector2(0.95f, 0.9f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            nextButton = CreateButton("NextButton", panelGO.transform, IsKorean() ? "다음" : "Next", 24);
            var btnRect = nextButton.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.78f, 0.08f);
            btnRect.anchorMax = new Vector2(0.95f, 0.28f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;
            nextButton.onClick.AddListener(OnNextPressed);
        }

        if (inputField == null)
        {
            inputField = CreateInputField("SafePlaceInput", dialoguePanel.transform);
            var inputRect = inputField.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0.05f, 0.08f);
            inputRect.anchorMax = new Vector2(0.68f, 0.3f);
            inputRect.offsetMin = Vector2.zero;
            inputRect.offsetMax = Vector2.zero;
            inputField.gameObject.SetActive(false);
            inputField.onValueChanged.AddListener(OnManualInputChanged);
            inputField.onSubmit.AddListener(_ => OnSubmitPressed());

            submitButton = CreateButton("SubmitButton", dialoguePanel.transform, IsKorean() ? "전달" : "Send", 24);
            var submitRect = submitButton.GetComponent<RectTransform>();
            submitRect.anchorMin = new Vector2(0.7f, 0.08f);
            submitRect.anchorMax = new Vector2(0.95f, 0.3f);
            submitRect.offsetMin = Vector2.zero;
            submitRect.offsetMax = Vector2.zero;
            submitButton.onClick.AddListener(OnSubmitPressed);
            submitButton.gameObject.SetActive(false);
        }

        if (hintText == null)
        {
            hintText = CreateText("SafePlaceHint", canvas.transform, 26, TextAlignmentOptions.Center);
            var hintRect = hintText.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.18f, 0.82f);
            hintRect.anchorMax = new Vector2(0.82f, 0.92f);
            hintRect.offsetMin = Vector2.zero;
            hintRect.offsetMax = Vector2.zero;
            hintText.text = "";
        }

        if (safePlaceInstructionText == null)
        {
            safePlaceInstructionText = CreateText("SafePlaceInstruction", canvas.transform, 30, TextAlignmentOptions.Center);
            var instructionRect = safePlaceInstructionText.GetComponent<RectTransform>();
            instructionRect.anchorMin = new Vector2(0.14f, 0.05f);
            instructionRect.anchorMax = new Vector2(0.86f, 0.14f);
            instructionRect.offsetMin = Vector2.zero;
            instructionRect.offsetMax = Vector2.zero;
            safePlaceInstructionText.gameObject.SetActive(false);
        }

        if (aiSphere == null)
        {
            CreateAISphere();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
    }

    private void EnsureCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraGO = new GameObject("Main Camera");
            camera = cameraGO.AddComponent<Camera>();
            cameraGO.tag = "MainCamera";
            cameraGO.transform.position = new Vector3(0f, 0f, -2f);
        }

        if (FindFirstObjectByType<AudioListener>() == null)
        {
            camera.gameObject.AddComponent<AudioListener>();
        }

        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        Vector3 euler = camera.transform.eulerAngles;
        cameraYaw = euler.y;
        cameraPitch = euler.x > 180f ? euler.x - 360f : euler.x;
        RenderSettings.skybox = null;
        RenderSettings.ambientLight = Color.black;
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void CreateAISphere()
    {
        aiSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        aiSphere.name = "AI Safe Place Sphere";
        aiSphere.transform.position = new Vector3(0f, 0f, 3f);
        aiSphere.transform.localScale = Vector3.one * 0.55f;

        var renderer = aiSphere.GetComponent<Renderer>();
        aiSphereMaterial = CreateTransparentSphereMaterial(0f);
        renderer.material = aiSphereMaterial;

        aiGlowSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        aiGlowSphere.name = "AI Safe Place Glow";
        aiGlowSphere.transform.SetParent(aiSphere.transform, false);
        aiGlowSphere.transform.localScale = Vector3.one * 2.9f;
        Destroy(aiGlowSphere.GetComponent<Collider>());
        aiGlowMaterial = CreateGlowMaterial(0f);
        aiGlowSphere.GetComponent<Renderer>().material = aiGlowMaterial;

        aiPointLight = aiSphere.AddComponent<Light>();
        aiPointLight.type = LightType.Point;
        aiPointLight.color = new Color(0.45f, 0.95f, 1f);
        aiPointLight.range = 5.2f;
        aiPointLight.intensity = 0f;

        aiSphere.SetActive(false);
    }

    private Material CreateTransparentSphereMaterial(float alpha)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        SetTransparentMaterial(mat);
        Color color = new Color(0.55f, 0.96f, 1f, alpha);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);
        mat.SetColor("_EmissionColor", new Color(0.22f, 1f, 1f, alpha * 2f));
        mat.EnableKeyword("_EMISSION");
        return mat;
    }

    private Material CreateGlowMaterial(float alpha)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");

        Material mat = new Material(shader);
        SetTransparentMaterial(mat);
        Color color = new Color(0.2f, 0.92f, 1f, alpha);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);
        return mat;
    }

    private void SetTransparentMaterial(Material mat)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    private void SetSphereAlpha(float alpha)
    {
        if (aiSphereMaterial != null)
        {
            Color color = new Color(0.55f, 0.96f, 1f, alpha);
            aiSphereMaterial.SetColor("_BaseColor", color);
            aiSphereMaterial.SetColor("_Color", color);
            aiSphereMaterial.SetColor("_EmissionColor", new Color(0.22f, 1f, 1f, alpha * 2f));
        }

        if (aiGlowMaterial != null)
        {
            Color glow = new Color(0.2f, 0.92f, 1f, alpha * 0.18f);
            aiGlowMaterial.SetColor("_BaseColor", glow);
            aiGlowMaterial.SetColor("_Color", glow);
        }

        if (aiPointLight != null)
        {
            aiPointLight.intensity = alpha * 1.25f;
        }
    }

    private void PulseAISphere()
    {
        if (aiSphere == null || !aiSphere.activeSelf) return;

        float pulse = (Mathf.Sin(Time.time * 1.45f) + 1f) * 0.5f;
        float glowScale = Mathf.Lerp(2.65f, 3.25f, pulse);
        if (aiGlowSphere != null)
        {
            aiGlowSphere.transform.localScale = Vector3.one * glowScale;
        }

        if (aiPointLight != null)
        {
            aiPointLight.intensity = Mathf.Lerp(0.95f, 1.55f, pulse);
            aiPointLight.range = Mathf.Lerp(4.7f, 5.8f, pulse);
        }

        if (aiGlowMaterial != null)
        {
            float alpha = Mathf.Lerp(0.1f, 0.22f, pulse);
            Color glow = new Color(0.2f, 0.92f, 1f, alpha);
            aiGlowMaterial.SetColor("_BaseColor", glow);
            aiGlowMaterial.SetColor("_Color", glow);
        }
    }

    private void UpdateDesktopLook()
    {
        if (!safePlaceReady || mapBoardVisible || Camera.main == null)
        {
            SetCursorFree();
            return;
        }

        SetCursorLocked();

        cameraYaw += Input.GetAxis("Mouse X") * desktopLookSensitivity;
        cameraPitch -= Input.GetAxis("Mouse Y") * desktopLookSensitivity;
        cameraPitch = Mathf.Clamp(cameraPitch, -65f, 65f);
        Camera.main.transform.rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
    }

    private void SetCursorLocked()
    {
        if (Cursor.lockState == CursorLockMode.Locked && !Cursor.visible) return;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void SetCursorFree()
    {
        if (Cursor.lockState == CursorLockMode.None && Cursor.visible) return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ShowTutorialPage(int index)
    {
        string[] pages = IsKorean() ? koreanTutorialPages : englishTutorialPages;
        pageIndex = Mathf.Clamp(index, 0, pages.Length - 1);
        dialogueText.text = pages[pageIndex];
        nextButton.GetComponentInChildren<TextMeshProUGUI>().text =
            pageIndex >= pages.Length - 1 ? (IsKorean() ? "시작" : "Start") : (IsKorean() ? "다음" : "Next");
    }

    private void OnNextPressed()
    {
        StopVoiceRecording();
        StopCurrentSpeech();

        string[] pages = IsKorean() ? koreanTutorialPages : englishTutorialPages;
        if (pageIndex < pages.Length - 1)
        {
            ShowTutorialPage(pageIndex + 1);
            return;
        }

        StartCoroutine(BeginSafePlaceDialogue());
    }

    private IEnumerator BeginSafePlaceDialogue()
    {
        dialoguePanel.gameObject.SetActive(false);
        blackOverlay.alpha = 0f;
        blackOverlay.gameObject.SetActive(false);

        aiSphere.SetActive(true);
        SetSphereAlpha(0f);
        yield return FadeSphere(0f, 1f, 1.4f);

        conversationStep = 0;
        dialoguePanel.gameObject.SetActive(true);
        nextButton.gameObject.SetActive(false);
        string greeting = IsKorean()
            ? "안녕하세요. 저는 이 여정을 함께할 AI companion입니다. 성함을 알려주실 수 있을까요?"
            : "Hello. I am the AI companion for this journey. What is your name?";
        inputField.text = "";
        SpeakAndPrepareForAnswer(greeting);
    }

    private void OnSubmitPressed()
    {
        if (isAiSpeechActive)
        {
            Debug.Log("[SafePlaceVoice] Submit ignored while AI speech is still playing.");
            return;
        }

        StopVoiceRecording();

        string answer = inputField.text.Trim();
        if (string.IsNullOrEmpty(answer)) return;

        var data = SafePlaceSessionData.GetOrCreate();
        if (conversationStep == 0)
        {
            data.userName = answer;
            conversationStep = 1;
            inputField.text = "";
            string dayQuestion = IsKorean()
                ? $"만나서 반가워요, {data.userName}님. 오늘 하루는 어땠나요?"
                : $"It is nice to meet you, {data.userName}. How has your day been?";
            SpeakAndPrepareForAnswer(dayQuestion);
            return;
        }

        if (conversationStep == 1)
        {
            smallTalkDayAnswer = answer;
            string feelingQuestion = IsKorean()
                ? "그 하루를 떠올렸을 때, 지금 마음에 가장 남아 있는 감정은 무엇인가요?"
                : "When you think back on the day, what feeling is staying with you the most right now?";
            StartCoroutine(RespondToSmallTalkThenContinue(answer, feelingQuestion, 2));
            return;
        }

        if (conversationStep == 2)
        {
            smallTalkFeelingAnswer = answer;
            string transition = IsKorean()
                ? "이야기해 주셔서 감사합니다. 본격적으로 자신의 감정에 대해 알아보는 시간을 가지기 전에, 편안함을 느낄 수 있는 공간을 상상해 보는 시간을 가져 보겠습니다."
                : "Thank you for telling me. Before we begin exploring your emotions more directly, let's take a moment to imagine a space where you can feel comfortable.";
            StartCoroutine(RespondToSmallTalkThenStartSafePlace(answer, transition, data.userName));
            return;
        }

        if (conversationStep == 3)
        {
            safePlaceThemeAnswer = answer;
            conversationStep = 4;
            inputField.text = "";
            string lightQuestion = IsKorean()
                ? "그 공간을 채울 빛과 색감은 어떤 느낌이면 좋을까요? 시간대나 날씨를 말씀해 주셔도 좋습니다."
                : "What kind of light and color should fill that space? You can also describe the time of day or weather.";
            SpeakAndPrepareForAnswer(lightQuestion);
            return;
        }

        if (conversationStep == 4)
        {
            safePlaceLightAnswer = answer;
            conversationStep = 5;
            inputField.text = "";
            string sensesQuestion = IsKorean()
                ? "그 공간에서 은은하게 느껴졌으면 하는 향기와, 멀리서 들려왔으면 하는 소리는 어떤 것인가요? 단어 몇 개로 편하게 말씀해 주세요."
                : "What gentle scent would you like to feel in that space, and what distant sound would make it feel calm? A few simple words are enough.";
            SpeakAndPrepareForAnswer(sensesQuestion);
            return;
        }

        safePlaceSensesAnswer = answer;
        data.description = BuildSafePlaceDescription();
        data.panoramaPrompt = BuildPanoramaPrompt(data.description);
        inputField.gameObject.SetActive(false);
        submitButton.gameObject.SetActive(false);
        StartCoroutine(LoadExternalSafePlacePanorama());
    }

    private void OnManualInputChanged(string value)
    {
        if (suppressInputChange) return;

        if (value.Contains("\n") || value.Contains("\r"))
        {
            string cleaned = value.Replace("\r", "").Replace("\n", "");
            suppressInputChange = true;
            inputField.text = cleaned;
            inputField.caretPosition = inputField.text.Length;
            inputField.ForceLabelUpdate();
            suppressInputChange = false;
            OnSubmitPressed();
            return;
        }

        if (!isListeningForVoice) return;
        if (!HasManualInput()) return;

        Debug.Log("[SafePlaceVoice] Manual typing detected. Voice recording stopped.");
        StopVoiceRecording();
    }

    private IEnumerator BeginSafePlaceQuestionSequence(string userName)
    {
        conversationStep = 3;
        inputField.text = "";
        inputField.gameObject.SetActive(false);
        submitButton.gameObject.SetActive(false);

        yield return new WaitForSeconds(0.5f);

        string themeQuestion = IsKorean()
            ? "어떤 공간인지 말씀해 주실 수 있을까요?"
            : "Can you tell me what kind of space it is?";
        SpeakAndPrepareForAnswer(themeQuestion);
    }

    private IEnumerator SpeakTransitionThenStartSafePlace(string transition, string userName)
    {
        inputField.text = "";
        inputField.gameObject.SetActive(false);
        submitButton.gameObject.SetActive(false);

        yield return StartCoroutine(SpeakLineRoutine(transition, false));
        yield return StartCoroutine(BeginSafePlaceQuestionSequence(userName));
    }

    private IEnumerator RespondToSmallTalkThenContinue(string userAnswer, string nextQuestion, int nextStep)
    {
        inputField.text = "";
        inputField.gameObject.SetActive(false);
        submitButton.gameObject.SetActive(false);
        dialoguePanel.gameObject.SetActive(false);

        string reply = null;
        yield return StartCoroutine(ProcessSmallTalkGPT(userAnswer, nextQuestion, false, text => reply = text));
        conversationStep = nextStep;
        SpeakAndPrepareForAnswer(string.IsNullOrWhiteSpace(reply) ? nextQuestion : reply);
    }

    private IEnumerator RespondToSmallTalkThenStartSafePlace(string userAnswer, string transition, string userName)
    {
        inputField.text = "";
        inputField.gameObject.SetActive(false);
        submitButton.gameObject.SetActive(false);
        dialoguePanel.gameObject.SetActive(false);

        string reply = null;
        yield return StartCoroutine(ProcessSmallTalkGPT(userAnswer, transition, true, text => reply = text));
        yield return StartCoroutine(SpeakLineRoutine(string.IsNullOrWhiteSpace(reply) ? transition : reply, false));
        yield return StartCoroutine(BeginSafePlaceQuestionSequence(userName));
    }

    private string BuildSmallTalkReply(string userAnswer, string nextLine, bool isTransition)
    {
        string normalized = userAnswer.ToLowerInvariant();
        bool asksBack = normalized.Contains("?") || normalized.Contains("너는") || normalized.Contains("당신은") || normalized.Contains("ai는") || normalized.Contains("you");

        if (IsKorean())
        {
            string reaction = asksBack
                ? "저는 여기에서 차분히 함께하고 있습니다. 말씀해 주신 그대로 천천히 따라가 볼게요."
                : "그렇게 느끼셨군요. 말씀해 주신 느낌을 그대로 두고 천천히 살펴보겠습니다.";

            return reaction + " " + nextLine;
        }

        string englishReaction = asksBack
            ? "I am here with you calmly. I will follow what you shared as it is."
            : "That is how it felt for you. We can stay with that feeling gently.";

        return englishReaction + " " + nextLine;
    }
    private string BuildSafePlaceDescription()
    {
        return "Basic theme / composition: " + safePlaceThemeAnswer + "\n" +
               "Light and color / emotional tone: " + safePlaceLightAnswer + "\n" +
               "Visualized scent and sound: " + safePlaceSensesAnswer;
    }

    private string BuildPanoramaPrompt(string description)
    {
        return "A calming 360 equirectangular panorama safe place for VR art therapy, immersive but gentle, " +
               "soft natural light, quiet atmosphere, no people, no text, seamless horizon, based on: " + description;
    }

    private void SpeakAndPrepareForAnswer(string text)
    {
        StopCurrentSpeech();
        speechCoroutine = StartCoroutine(SpeakLineRoutine(text, true));
    }

    private void SpeakWithoutListening(string text)
    {
        StopCurrentSpeech();
        speechCoroutine = StartCoroutine(SpeakLineRoutine(text, false));
    }

    private void StopCurrentSpeech()
    {
        if (speechCoroutine != null)
        {
            StopCoroutine(speechCoroutine);
            speechCoroutine = null;
        }

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        isAiSpeechActive = false;
    }

    private IEnumerator SpeakLineRoutine(string text, bool listenAfter)
    {
        StopVoiceRecording();
        inputField.gameObject.SetActive(false);
        submitButton.gameObject.SetActive(false);

        if (voiceConversationEnabled && !string.IsNullOrEmpty(openAiApiKey))
        {
            dialoguePanel.gameObject.SetActive(false);
            AudioClip clip = null;
            yield return StartCoroutine(ProcessTTS(text, c => clip = c));
            if (clip != null && audioSource != null)
            {
                dialoguePanel.gameObject.SetActive(true);
                dialogueText.text = text;
                audioSource.clip = clip;
                audioSource.volume = 1f;
                audioSource.spatialBlend = 0f;
                isAiSpeechActive = true;
                audioSource.Play();
                Debug.Log("[SafePlaceVoice] TTS playback started.");

                while (audioSource != null && audioSource.isPlaying)
                {
                    yield return null;
                }

                isAiSpeechActive = false;
                yield return new WaitForSeconds(0.15f);
                Debug.Log("[SafePlaceVoice] TTS playback ended.");
            }
            else
            {
                dialoguePanel.gameObject.SetActive(true);
                dialogueText.text = text;
                Debug.LogWarning("[SafePlaceVoice] TTS clip or AudioSource is null. Continuing with text only.");
            }
        }
        else if (voiceConversationEnabled)
        {
            dialoguePanel.gameObject.SetActive(true);
            dialogueText.text = text;
            Debug.LogWarning("[SafePlaceVoice] OpenAI API key is empty. Continuing with text only.");
        }
        else
        {
            dialoguePanel.gameObject.SetActive(true);
            dialogueText.text = text;
        }

        if (listenAfter)
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                yield return new WaitUntil(() => audioSource == null || !audioSource.isPlaying);
            }

            inputField.text = "";
            inputField.gameObject.SetActive(true);
            submitButton.gameObject.SetActive(true);
            inputField.Select();
            StartVoiceRecording();
        }
    }

    private void StartVoiceRecording()
    {
        if (!voiceConversationEnabled || string.IsNullOrEmpty(openAiApiKey) || isListeningForVoice || isProcessingVoice) return;
        if (HasManualInput()) return;
        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            Debug.LogWarning("[SafePlaceVoice] No microphone detected.");
            return;
        }

        isListeningForVoice = true;
        hasDetectedVoiceInCurrentClip = false;
        voiceStartTime = -1f;
        voiceEndTime = -1f;
        recordStartTime = Time.time;
        lastTimeVoiceDetected = Time.time;
        micRecordClip = Microphone.Start(null, false, (int)maxRecordTime, 16000);
        Debug.Log("[SafePlaceVoice] Listening started.");
    }

    private void StopVoiceRecording()
    {
        if (!isListeningForVoice) return;

        isListeningForVoice = false;
        if (Microphone.IsRecording(null))
        {
            Microphone.End(null);
        }
    }

    private void HandleVoiceRecording()
    {
        if (!isListeningForVoice || isProcessingVoice || micRecordClip == null) return;

        float currentVol = GetMicVolume();
        if (currentVol > rmsStartThreshold)
        {
            lastTimeVoiceDetected = Time.time;
            if (!hasDetectedVoiceInCurrentClip)
            {
                hasDetectedVoiceInCurrentClip = true;
                voiceStartTime = Time.time - recordStartTime;
                Debug.Log("[SafePlaceVoice] Speech detected.");
            }
            voiceEndTime = -1f;
        }

        if (hasDetectedVoiceInCurrentClip)
        {
            if (currentVol < rmsEndThreshold)
            {
                if (voiceEndTime < 0f)
                {
                    voiceEndTime = Time.time - recordStartTime;
                }

                if (Time.time - lastTimeVoiceDetected > pauseThreshold)
                {
                    StopVoiceRecordingAndProcess();
                }
            }
            else
            {
                lastTimeVoiceDetected = Time.time;
                voiceEndTime = -1f;
            }
        }

        if (Time.time - recordStartTime >= maxRecordTime)
        {
            StopVoiceRecordingAndProcess();
        }
    }

    private void StopVoiceRecordingAndProcess()
    {
        if (isProcessingVoice || micRecordClip == null) return;

        int micPos = Microphone.GetPosition(null);
        isListeningForVoice = false;
        if (Microphone.IsRecording(null))
        {
            Microphone.End(null);
        }

        if (HasManualInput())
        {
            Debug.Log("[SafePlaceVoice] Manual text exists. Discarding voice clip.");
            return;
        }

        float recordDuration = Time.time - recordStartTime;
        float speechDuration = 0f;
        if (hasDetectedVoiceInCurrentClip && voiceStartTime >= 0f)
        {
            float endMarker = voiceEndTime >= 0f ? voiceEndTime : recordDuration;
            speechDuration = endMarker - voiceStartTime;
        }

        if (speechDuration < minimumRecordingLength || !hasDetectedVoiceInCurrentClip)
        {
            Debug.Log("[SafePlaceVoice] Recorded voice was too short. Listening again.");
            StartVoiceRecording();
            return;
        }

        float croppedStart = Mathf.Max(0f, voiceStartTime - preSpeechBuffer);
        float croppedEnd = Mathf.Min(recordDuration, (voiceEndTime >= 0f ? voiceEndTime : recordDuration) + postSpeechBuffer);
        float croppedDuration = croppedEnd - croppedStart;
        int startSample = Mathf.RoundToInt(croppedStart * micRecordClip.frequency);
        int sampleCount = Mathf.RoundToInt(croppedDuration * micRecordClip.frequency);
        int totalSamplesAvailable = Mathf.Min(micPos, micRecordClip.samples);

        if (startSample >= totalSamplesAvailable)
        {
            StartVoiceRecording();
            return;
        }

        if (startSample + sampleCount > totalSamplesAvailable)
        {
            sampleCount = totalSamplesAvailable - startSample;
        }

        if (sampleCount <= 0)
        {
            StartVoiceRecording();
            return;
        }

        float[] croppedSamples = new float[sampleCount * micRecordClip.channels];
        micRecordClip.GetData(croppedSamples, startSample);
        byte[] wavBytes = ConvertSamplesToWav(croppedSamples, micRecordClip.frequency, micRecordClip.channels);
        inputField.gameObject.SetActive(true);
        inputField.text = IsKorean() ? "음성을 인식하고 있습니다..." : "Recognizing your voice...";
        inputField.ForceLabelUpdate();
        StartCoroutine(ProcessVoiceAnswer(wavBytes));
    }

    private IEnumerator ProcessVoiceAnswer(byte[] wavBytes)
    {
        isProcessingVoice = true;
        string transcript = null;
        yield return StartCoroutine(ProcessSTT(wavBytes, text => transcript = text));
        isProcessingVoice = false;

        if (HasManualInput())
        {
            Debug.Log("[SafePlaceVoice] Manual text exists. Ignoring STT transcript.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(transcript))
        {
            Debug.LogWarning("[SafePlaceVoice] Whisper returned an empty transcript. Listening again.");
            inputField.text = "";
            StartVoiceRecording();
            yield break;
        }

        if (IsLikelySTTHallucination(transcript))
        {
            Debug.LogWarning("[SafePlaceVoice] Ignored likely STT hallucination: " + transcript);
            inputField.text = "";
            StartVoiceRecording();
            yield break;
        }

        inputField.text = transcript.Trim();
        inputField.caretPosition = inputField.text.Length;
        inputField.ForceLabelUpdate();
        Debug.Log("[SafePlaceVoice] Transcript recognized: " + inputField.text);
        yield return new WaitForSeconds(1.0f);
        OnSubmitPressed();
    }

    private IEnumerator ProcessSTT(byte[] wavBytes, System.Action<string> onComplete)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", wavBytes, "audio.wav", "audio/wav");
        form.AddField("model", "whisper-1");
        form.AddField("language", GameManager.Instance != null && GameManager.Instance.currentLanguage == "en" ? "en" : "ko");
        form.AddField("response_format", "verbose_json");

        using (UnityWebRequest whisperReq = UnityWebRequest.Post("https://api.openai.com/v1/audio/transcriptions", form))
        {
            whisperReq.SetRequestHeader("Authorization", "Bearer " + openAiApiKey);
            yield return whisperReq.SendWebRequest();

            if (whisperReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[SafePlaceVoice] Whisper API error: " + whisperReq.error + " / " + whisperReq.downloadHandler.text);
                onComplete?.Invoke(null);
                yield break;
            }

            OpenAIWhisperVerboseResponse whisperRes = null;
            try
            {
                whisperRes = JsonUtility.FromJson<OpenAIWhisperVerboseResponse>(whisperReq.downloadHandler.text);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[SafePlaceVoice] Failed to parse STT response: " + ex.Message);
            }

            onComplete?.Invoke(whisperRes != null ? whisperRes.text : null);
        }
    }

    private bool HasManualInput()
    {
        if (inputField == null) return false;

        string text = inputField.text;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (IsVoiceStatusText(text)) return false;

        return true;
    }

    private bool IsVoiceStatusText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        string trimmed = text.Trim();
        return trimmed.Contains("Recognizing your voice") ||
               trimmed.Contains("voice") ||
               trimmed.Contains("인식");
    }

    private bool IsLikelySTTHallucination(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        string normalized = text.Trim().ToLowerInvariant();
        return normalized.Contains("시청해주셔서 감사합니다") ||
               normalized.Contains("시청해 주셔서 감사합니다") ||
               normalized.Contains("thank you for watching") ||
               normalized.Contains("thanks for watching");
    }

    private IEnumerator ProcessSmallTalkGPT(string userAnswer, string requiredNextLine, bool isTransition, System.Action<string> onComplete)
    {
        if (string.IsNullOrEmpty(openAiApiKey))
        {
            onComplete?.Invoke(requiredNextLine);
            yield break;
        }

        string languageInstruction = IsKorean()
            ? "한국어 존댓말로만 답하세요."
            : "Reply in warm, natural English.";
        string purposeInstruction = isTransition
            ? "The next line must gently transition into imagining a safe place before body mapping."
            : "The next line must end by asking the required follow-up question.";
        string systemPrompt =
            "You are a calm AI companion for a VR art therapy session. " +
            "Understand the user's sentence semantically, not by keyword matching. " +
            "Respond directly to the user's latest message with empathy and light conversational presence. " +
            "Do not reverse the user's meaning: if they say the day was not good, do not say it was good. " +
            "If the emotional meaning is unclear, acknowledge uncertainty gently instead of judging it. " +
            "If the user asks you a casual question, answer it briefly as an AI companion, then continue. " +
            "Do not mention body mapping data, safe place data, prompts, JSON, or implementation details. " +
            "Keep the whole response to 1 or 2 short sentences and make the required next line feel natural. " +
            languageInstruction + " " + purposeInstruction;
        string userPrompt =
            "User's latest message: " + userAnswer + "\n" +
            "Required next line or question to include naturally at the end: " + requiredNextLine;

        string messagesJson =
            "{\"role\":\"system\",\"content\":\"" + EscapeJsonString(systemPrompt) + "\"}," +
            "{\"role\":\"user\",\"content\":\"" + EscapeJsonString(userPrompt) + "\"}";
        string jsonPayload = "{\"model\":\"gpt-4o-mini\",\"messages\":[" + messagesJson + "],\"temperature\":0.7,\"max_tokens\":140}";

        using (UnityWebRequest chatReq = new UnityWebRequest("https://api.openai.com/v1/chat/completions", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            chatReq.uploadHandler = new UploadHandlerRaw(bodyRaw);
            chatReq.downloadHandler = new DownloadHandlerBuffer();
            chatReq.SetRequestHeader("Authorization", "Bearer " + openAiApiKey);
            chatReq.SetRequestHeader("Content-Type", "application/json");

            yield return chatReq.SendWebRequest();

            if (chatReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[SafePlaceVoice] Small talk GPT error: " + chatReq.error + " / " + chatReq.downloadHandler.text);
                onComplete?.Invoke(requiredNextLine);
                yield break;
            }

            OpenAIChatResponse chatRes = null;
            try
            {
                chatRes = JsonUtility.FromJson<OpenAIChatResponse>(chatReq.downloadHandler.text);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[SafePlaceVoice] Failed to parse small talk GPT response: " + ex.Message);
            }

            if (chatRes == null || chatRes.choices == null || chatRes.choices.Length == 0 || chatRes.choices[0].message == null)
            {
                onComplete?.Invoke(requiredNextLine);
                yield break;
            }

            onComplete?.Invoke(chatRes.choices[0].message.content);
        }
    }

    private IEnumerator ProcessTTS(string text, System.Action<AudioClip> onComplete)
    {
        string escapedText = EscapeJsonString(text);
        string jsonPayload = $"{{\"model\":\"tts-1\",\"input\":\"{escapedText}\",\"voice\":\"nova\",\"response_format\":\"mp3\"}}";

        using (UnityWebRequest request = new UnityWebRequest("https://api.openai.com/v1/audio/speech", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", "Bearer " + openAiApiKey);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[SafePlaceVoice] TTS API error: " + request.error);
                onComplete?.Invoke(null);
                yield break;
            }

            string tempPath = Path.Combine(Application.temporaryCachePath, "safe_place_tts_output.mp3");
            File.WriteAllBytes(tempPath, request.downloadHandler.data);
            string fileUrl = "file:///" + tempPath.Replace("\\", "/");

            using (UnityWebRequest audioLoader = UnityWebRequestMultimedia.GetAudioClip(fileUrl, AudioType.MPEG))
            {
                yield return audioLoader.SendWebRequest();
                if (audioLoader.result == UnityWebRequest.Result.Success)
                {
                    onComplete?.Invoke(DownloadHandlerAudioClip.GetContent(audioLoader));
                }
                else
                {
                    Debug.LogError("[SafePlaceVoice] Failed to load TTS audio: " + audioLoader.error);
                    onComplete?.Invoke(null);
                }
            }
        }
    }

    private byte[] ConvertSamplesToWav(float[] samples, int frequency, int channels)
    {
        using (MemoryStream stream = new MemoryStream())
        {
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + samples.Length * 2);
                writer.Write(new char[4] { 'W', 'A', 'V', 'E' });
                writer.Write(new char[4] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)channels);
                writer.Write(frequency);
                writer.Write(frequency * channels * 2);
                writer.Write((short)(channels * 2));
                writer.Write((short)16);
                writer.Write(new char[4] { 'd', 'a', 't', 'a' });
                writer.Write(samples.Length * 2);

                for (int i = 0; i < samples.Length; i++)
                {
                    short value = (short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767);
                    writer.Write(value);
                }
            }
            return stream.ToArray();
        }
    }

    private float GetMicVolume()
    {
        if (!Microphone.IsRecording(null) || micRecordClip == null) return 0f;

        int sampleWindow = 128;
        float[] samples = new float[sampleWindow];
        int micPosition = Microphone.GetPosition(null);
        int startPosition = micPosition - sampleWindow;
        if (startPosition < 0) return 0f;

        micRecordClip.GetData(samples, startPosition);
        float sum = 0f;
        for (int i = 0; i < sampleWindow; i++)
        {
            sum += samples[i] * samples[i];
        }
        return Mathf.Sqrt(sum / sampleWindow);
    }

    private void LoadOpenAIKey()
    {
        string path = Path.Combine(Application.dataPath, "../api_keys.json");
        if (!File.Exists(path))
        {
            Debug.LogWarning("[SafePlaceVoice] api_keys.json not found. Voice conversation will fall back to text input.");
            return;
        }

        string json = File.ReadAllText(path);
        APIKeys keys = JsonUtility.FromJson<APIKeys>(json);
        openAiApiKey = keys != null && keys.OpenAI_API_Key != null ? keys.OpenAI_API_Key.Trim() : "";
        if (string.IsNullOrEmpty(openAiApiKey) || openAiApiKey == "YOUR_OPENAI_API_KEY_HERE")
        {
            Debug.LogWarning("[SafePlaceVoice] OpenAI API key is empty or placeholder. Voice conversation will fall back to text input.");
            openAiApiKey = "";
            return;
        }

        Debug.Log("[SafePlaceVoice] OpenAI API key loaded.");
    }

    private string EscapeJsonString(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    private IEnumerator LoadExternalSafePlacePanorama()
    {
        yield return StartCoroutine(SpeakLineRoutine(IsKorean()
            ? "좋습니다. safe place 이미지를 불러오는 중입니다."
            : "Good. I am loading your safe place panorama.", false));

        yield return new WaitForSeconds(0.45f);

        blackOverlay.gameObject.SetActive(true);
        yield return FadeCanvasGroup(blackOverlay, 0f, 1f, 0.55f);

        string panoramaPath = GetStreamingAssetPath(safePlacePanoramaFolder, safePlacePanoramaFileName);
        Texture2D panorama = LoadTextureFromFile(panoramaPath);

        if (panorama == null)
        {
            dialogueText.text = IsKorean()
                ? "safe place 이미지를 찾지 못했습니다. StreamingAssets/SafePlacePanoramas 폴더를 확인해 주세요."
                : "I could not find the safe place image. Please check StreamingAssets/SafePlacePanoramas.";
            blackOverlay.alpha = 0.85f;
            dialoguePanel.gameObject.SetActive(true);
            yield break;
        }

        ApplyPanoramaSkybox(panorama);
        if (SafePlaceSessionData.Instance != null)
        {
            SafePlaceSessionData.Instance.skyboxAssetPath = panoramaPath;
        }

        dialoguePanel.gameObject.SetActive(false);
        safePlaceReady = true;
        safePlaceInstructionText.gameObject.SetActive(true);
        safePlaceInstructionText.text = IsKorean()
            ? "M 키를 눌러 body mapping 세션 환경을 선택하세요."
            : "Press M to choose the body mapping session environment.";
        hintText.text = IsKorean() ? "M: 세션 환경 선택" : "M: Session Environment";
        aiSphere.transform.position = new Vector3(0f, -0.2f, 2.4f);

        yield return FadeCanvasGroup(blackOverlay, 1f, 0f, 1.1f);
        blackOverlay.gameObject.SetActive(false);
    }

    private string GetStreamingAssetPath(string folder, string fileName)
    {
        return Path.Combine(Application.streamingAssetsPath, folder, fileName);
    }

    private Texture2D LoadTextureFromFile(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError("[SafePlaceIntro] Panorama file not found: " + path);
            return null;
        }

        byte[] bytes = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes))
        {
            Debug.LogError("[SafePlaceIntro] Failed to load panorama image: " + path);
            return null;
        }

        texture.wrapMode = TextureWrapMode.Clamp;
        texture.name = Path.GetFileNameWithoutExtension(path);
        return texture;
    }

    private void ApplyPanoramaSkybox(Texture2D panorama)
    {
        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.clearFlags = CameraClearFlags.Skybox;
        }

        Shader shader = Shader.Find("Skybox/Panoramic");
        if (shader == null)
        {
            Debug.LogError("[SafePlaceIntro] Skybox/Panoramic shader was not found.");
            return;
        }

        safePlaceSkyboxMaterial = new Material(shader);
        safePlaceSkyboxMaterial.SetTexture("_MainTex", panorama);
        safePlaceSkyboxMaterial.SetFloat("_Exposure", 1f);
        RenderSettings.skybox = safePlaceSkyboxMaterial;
        RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.6f);
        DynamicGI.UpdateEnvironment();
    }

    private void ToggleMapSelectionBoard()
    {
        if (mapBoardCanvas == null)
        {
            CreateMapSelectionBoard();
        }

        mapBoardVisible = !mapBoardVisible;
        mapBoardCanvas.gameObject.SetActive(mapBoardVisible);
        if (aiSphere != null)
        {
            aiSphere.SetActive(!mapBoardVisible);
        }

        if (mapBoardVisible)
        {
            SetCursorFree();
            StopVoiceRecording();
            PositionBoardInFrontOfCamera();
            safePlaceInstructionText.gameObject.SetActive(false);
            hintText.text = IsKorean() ? "body mapping 세션 환경을 선택하세요. M: 닫기" : "Choose a body mapping session environment. M: Close";
        }
        else
        {
            safePlaceInstructionText.gameObject.SetActive(true);
            hintText.text = IsKorean() ? "M: 세션 환경 선택" : "M: Session Environment";
        }
    }

    private void CreateMapSelectionBoard()
    {
        GameObject boardGO = new GameObject("FloatingEnvironmentBoard", typeof(Canvas), typeof(GraphicRaycaster));
        mapBoardCanvas = boardGO.GetComponent<Canvas>();
        mapBoardCanvas.renderMode = RenderMode.WorldSpace;
        mapBoardCanvas.worldCamera = Camera.main;

        RectTransform boardRect = boardGO.GetComponent<RectTransform>();
        boardRect.sizeDelta = new Vector2(1240f, 700f);
        boardRect.localScale = Vector3.one * 0.0022f;

        GameObject bgGO = CreateUIObject("BoardBackground", boardGO.transform);
        Image bg = bgGO.AddComponent<Image>();
        bg.color = new Color(0.055f, 0.055f, 0.065f, 0.96f);
        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        TextMeshProUGUI title = CreateText("TitleText", boardGO.transform, 46, TextAlignmentOptions.Center);
        title.text = IsKorean() ? "환경 선택" : "SELECT ENVIRONMENT";
        title.color = new Color(0f, 0.95f, 0.86f);
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.08f, 0.81f);
        titleRect.anchorMax = new Vector2(0.92f, 0.92f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        TextMeshProUGUI subtitle = CreateText("SubtitleText", boardGO.transform, 22, TextAlignmentOptions.Center);
        subtitle.text = IsKorean()
            ? "body mapping 세션을 진행할 환경을 선택하세요"
            : "Choose an environment for the body mapping session";
        subtitle.color = new Color(0.72f, 0.72f, 0.82f);
        RectTransform subtitleRect = subtitle.GetComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0.08f, 0.75f);
        subtitleRect.anchorMax = new Vector2(0.92f, 0.80f);
        subtitleRect.offsetMin = Vector2.zero;
        subtitleRect.offsetMax = Vector2.zero;

        GameObject cardsGO = CreateUIObject("Cards", boardGO.transform);
        RectTransform cardsRect = cardsGO.GetComponent<RectTransform>();
        cardsRect.anchorMin = new Vector2(0.09f, 0.11f);
        cardsRect.anchorMax = new Vector2(0.91f, 0.70f);
        cardsRect.offsetMin = Vector2.zero;
        cardsRect.offsetMax = Vector2.zero;
        var layout = cardsGO.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 64f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        CreateEnvironmentCard(
            cardsGO.transform,
            IsKorean() ? "정원 환경" : "Garden Environment",
            IsKorean() ? "나무와 중앙 상호작용 공간이 있는 body mapping 세션 환경입니다." : "Open natural environment with trees and central interaction space.",
            "Env_URP_Garden",
            "Japanese Garden.png");

        CreateEnvironmentCard(
            cardsGO.transform,
            IsKorean() ? "사막 오아시스" : "Desert Oasis",
            IsKorean() ? "차분한 분위기의 넓은 사막 오아시스 body mapping 세션 환경입니다." : "Wide open desert-like environment with calm atmosphere.",
            "Env_URP_Desert",
            "Desert.png");

        mapBoardCanvas.gameObject.SetActive(false);
    }

    private void CreateEnvironmentCard(Transform parent, string title, string description, string sceneName, string thumbnailFileName)
    {
        GameObject cardGO = CreateUIObject("Card_" + sceneName, parent);
        Image cardBg = cardGO.AddComponent<Image>();
        cardBg.color = new Color(0.11f, 0.11f, 0.14f, 1f);
        RectTransform cardRect = cardGO.GetComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(420f, 430f);
        var layoutElement = cardGO.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 420f;
        layoutElement.preferredHeight = 430f;

        GameObject previewGO = CreateUIObject("PreviewImage", cardGO.transform);
        RawImage preview = previewGO.AddComponent<RawImage>();
        preview.color = Color.white;
        preview.texture = LoadThumbnail(thumbnailFileName);
        RectTransform previewRect = previewGO.GetComponent<RectTransform>();
        previewRect.anchorMin = new Vector2(0.045f, 0.52f);
        previewRect.anchorMax = new Vector2(0.955f, 0.91f);
        previewRect.offsetMin = Vector2.zero;
        previewRect.offsetMax = Vector2.zero;

        TextMeshProUGUI nameText = CreateText("DisplayName", cardGO.transform, 27, TextAlignmentOptions.Center);
        nameText.text = title;
        RectTransform nameRect = nameText.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.04f, 0.385f);
        nameRect.anchorMax = new Vector2(0.96f, 0.50f);
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;

        TextMeshProUGUI descText = CreateText("Description", cardGO.transform, 16, TextAlignmentOptions.Center);
        descText.text = description;
        descText.color = new Color(0.78f, 0.78f, 0.84f);
        RectTransform descRect = descText.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0.08f, 0.19f);
        descRect.anchorMax = new Vector2(0.92f, 0.35f);
        descRect.offsetMin = Vector2.zero;
        descRect.offsetMax = Vector2.zero;

        Button selectButton = CreateButton("SelectButton", cardGO.transform, IsKorean() ? "선택" : "Select", 21);
        RectTransform btnRect = selectButton.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.04f, 0.055f);
        btnRect.anchorMax = new Vector2(0.96f, 0.17f);
        btnRect.offsetMin = Vector2.zero;
        btnRect.offsetMax = Vector2.zero;
        selectButton.onClick.AddListener(() => SelectEnvironment(sceneName, title));
    }

    private Texture2D LoadThumbnail(string thumbnailFileName)
    {
        string path = Path.Combine(Application.streamingAssetsPath, "MapThumbnails", thumbnailFileName);
        Texture2D texture = LoadTextureFromFile(path);
        if (texture != null) return texture;

        path = Path.Combine(Application.dataPath, "Textures", "MapThumbnails", thumbnailFileName);
        return LoadTextureFromFile(path);
    }

    private void ToggleLanguageOnBoard()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.currentLanguage = GameManager.Instance.currentLanguage == "ko" ? "en" : "ko";
        }

        if (mapBoardCanvas != null)
        {
            Destroy(mapBoardCanvas.gameObject);
            mapBoardCanvas = null;
            mapBoardVisible = false;
            ToggleMapSelectionBoard();
        }
    }

    private void PositionBoardInFrontOfCamera()
    {
        Camera cam = Camera.main;
        if (cam == null || mapBoardCanvas == null) return;

        Transform board = mapBoardCanvas.transform;
        board.position = cam.transform.position + cam.transform.forward * 2.75f;
        board.rotation = Quaternion.LookRotation(board.position - cam.transform.position, Vector3.up);
    }

    private void SelectEnvironment(string sceneName, string displayName)
    {
        SetCursorFree();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetSelectedEnvironment(sceneName, displayName);
        }

        if (SceneFlowManager.Instance != null)
        {
            SceneFlowManager.Instance.GoToLoading();
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("LoadingScene");
        }
    }

    private IEnumerator FadeSphere(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetSphereAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        SetSphereAlpha(to);
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        float elapsed = 0f;
        group.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        group.alpha = to;
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = CreateUIObject(name, parent);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = "";
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        BodyMapAIController.ApplyKoreanFont(text);
        return text;
    }

    private Button CreateButton(string name, Transform parent, string label, int fontSize)
    {
        GameObject go = CreateUIObject(name, parent);
        var image = go.AddComponent<Image>();
        image.color = new Color(0.03f, 0.64f, 0.72f, 1f);
        var button = go.AddComponent<Button>();
        var text = CreateText("Text", go.transform, fontSize, TextAlignmentOptions.Center);
        text.text = label;
        text.overflowMode = TextOverflowModes.Ellipsis;
        var rect = text.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return button;
    }

    private TMP_InputField CreateInputField(string name, Transform parent)
    {
        GameObject go = CreateUIObject(name, parent);
        var image = go.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.96f);

        var input = go.AddComponent<TMP_InputField>();
        input.lineType = TMP_InputField.LineType.MultiLineSubmit;
        input.richText = false;
        input.characterLimit = 0;

        GameObject viewportGO = CreateUIObject("Text Area", go.transform);
        viewportGO.AddComponent<RectMask2D>();
        RectTransform viewportRect = viewportGO.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.03f, 0.1f);
        viewportRect.anchorMax = new Vector2(0.97f, 0.9f);
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        input.textViewport = viewportRect;

        var text = CreateText("Text", viewportGO.transform, 22, TextAlignmentOptions.TopLeft);
        text.color = Color.black;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        var textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        input.textComponent = text;

        var placeholder = CreateText("Placeholder", viewportGO.transform, 22, TextAlignmentOptions.TopLeft);
        placeholder.color = new Color(0f, 0f, 0f, 0.38f);
        placeholder.text = IsKorean() ? "여기에 답을 입력하세요" : "Type your answer here";
        var placeholderRect = placeholder.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;
        input.placeholder = placeholder;

        return input;
    }

    [System.Serializable]
    private class APIKeys
    {
        public string OpenAI_API_Key;
    }

    [System.Serializable]
    private class OpenAIWhisperSegment
    {
        public string text;
        public float avg_logprob;
    }

    [System.Serializable]
    private class OpenAIWhisperVerboseResponse
    {
        public string text;
        public OpenAIWhisperSegment[] segments;
    }

    [System.Serializable]
    private class OpenAIChatMessage
    {
        public string role;
        public string content;
    }

    [System.Serializable]
    private class OpenAIChatChoice
    {
        public OpenAIChatMessage message;
    }

    [System.Serializable]
    private class OpenAIChatResponse
    {
        public OpenAIChatChoice[] choices;
    }
}
