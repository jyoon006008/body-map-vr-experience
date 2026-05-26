using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;

public class BodyMapAIController : MonoBehaviour
{
    public static BodyMapAIController Instance { get; private set; }

    [Header("API Config (api_keys.json)")]
    [SerializeField] private string openAiApiKey = "";
    [SerializeField] private string tripoApiKey = "";

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;

    [Header("Font & Camera Settings")]
    [SerializeField] private TMP_FontAsset koreanFontAsset;
    [SerializeField] private Camera targetCamera;

    private Canvas hudCanvas = null;
    private bool introStarted = false;

#if UNITY_EDITOR
    private bool enableDebugHUD = false;
    private GameObject debugPanelObj;
    private TextMeshProUGUI debugText;
    private float fpsMeasurePeriod = 0.5f;
    private int fpsAccumulator = 0;
    private float fpsNextPeriod = 0f;
    private int currentFps = 0;
#endif
    private AudioClip micRecordClip;
    private float maxRecordTime = 20f; // max 20 seconds recording
    private float pauseThreshold = 1.3f;
    private float rmsStartThreshold = 0.015f;
    private float rmsEndThreshold = 0.008f;
    private float minimumRecordingLength = 1.0f;
    private float postSpeechBuffer = 0.8f;

    // UI elements dynamically built
    private GameObject chatPanelObj;
    private Transform chatContentTrans;
    private StarterAssets.StarterAssetsInputs playerInputs;

    // State Tracking
    private bool isUiActive = false;
    private bool isRecording = false;
    private float recordStartTime = 0f;
    private float lastTimeVoiceDetected = 0f;
    private bool hasDetectedVoiceInCurrentClip = false;
    private InteractiveRegion3D lastSelectedRegion = null;
    private BodyMapReceiver mapReceiver;
    private Coroutine listeningLoopCoroutine;
    private bool isProcessingUserAnswer = false;
    private bool aiAskedQuestion = false;

    // Concurrency and Thinking States
    private bool isThinking = false;
    private bool isGenerating3D = false;

    public enum ConversationState
    {
        Idle,
        AIThinking,
        AISpeaking,
        WaitingForUser,
        UserSpeaking,
        ProcessingUserAnswer,
        TransitioningStep
    }

    private ConversationState conversationState = ConversationState.Idle;

    private float interruptionTimer = 0f;
    private bool isPotentialInterrupted = false;
    private GameObject thinkingBubbleObj;
    private Coroutine thinkingAnimationCoroutine;
    private Coroutine aiResponseCoroutine;

    // Dialogue State Machine
    public enum ReflectionState
    {
        Idle,
        InitialIntro,
        AwaitingSelection,
        RegionReflection,
        PromptGeneration,
        TransitionTo3D
    }

    private ReflectionState reflectionState = ReflectionState.Idle;

    [System.Serializable]
    public class EmotionProfile
    {
        public string emotionDescription = "";
        public string shapeDescription = "";
        public string textureDescription = "";
        public string movementDescription = "";
        public string relationshipDescription = "";
        public string finalPrompt = "";
    }

    // Region Memory Structure
    [System.Serializable]
    public class RegionMemory
    {
        public int regionId;
        public string colorName;
        public string bodyLocation;
        public string describedEmotion = "";
        public string describedShape = "";
        public string describedSurface = "";
        public string describedMovement = "";
        public RegionGenerationState generationState = RegionGenerationState.NotStarted;
        public EmotionInteractionState interactionState = EmotionInteractionState.Observe;
        public List<MessageData> fullConversation = new List<MessageData>();
        public string workingPrompt = "";
        public string finalPrompt = "";
        public string generatedModelUrl = "";
        public string localModelPath = "";
        // Conversation turn tracking for auto-conclude
        public int aiQuestionCount = 0;       // how many times AI has asked a question
        public int infoGatheredCount = 0;     // how many info categories gathered
        public EmotionProfile emotionProfile = new EmotionProfile();
    }

    private Dictionary<int, RegionMemory> regionMemories = new Dictionary<int, RegionMemory>();

    [System.Serializable]
    public struct MessageData
    {
        public string role;
        public string content;
    }

    [System.Serializable]
    public class ConversationAnalysisResult
    {
        public string assistantResponse;
        public bool hasEnoughInfo;
        public int categoryCount;
        public string generatedPrompt;
        public bool shouldConclude;
    }

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        // centralize audioSource acquisition
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.volume = 1f;
        audioSource.spatialBlend = 0f;
        audioSource.loop = false;

        // self-healing recovery for inspector assignments
        if (koreanFontAsset == null)
        {
            Debug.LogWarning("[Recovery] Missing Korean font asset. Attempting Resources load...");
            koreanFontAsset = Resources.Load<TMP_FontAsset>("Fonts/NotoSansKR");
            if (koreanFontAsset == null)
            {
                Debug.LogError("[Recovery] Failed to load NotoSansKR font asset from Resources/Fonts/.");
            }
        }

        if (targetCamera == null)
        {
            targetCamera = FindFirstObjectByType<Camera>();
            if (targetCamera == null)
            {
                Debug.LogWarning("[Recovery] No Camera found in scene!");
            }
        }
    }

    public bool IsUIActive() => isUiActive;

    private string GetLocalizedString(string key)
    {
        bool isKorean = true;
        if (GameManager.Instance != null)
        {
            isKorean = GameManager.Instance.currentLanguage == "ko";
        }

        switch (key)
        {
            case "already_prepared":
                return isKorean ? "이 감정은 이미 준비되었습니다." : "This emotion is already prepared.";
            case "intro_system":
                return isKorean 
                    ? "You are an empathetic Art Therapy assistant. Ask warm reflective questions about the user's artwork. Keep responses strictly under 1-2 sentences. Discuss in Korean."
                    : "You are an empathetic Art Therapy assistant. Ask warm reflective questions about the user's artwork. Keep responses strictly under 1-2 sentences. Discuss in English.";
            case "intro_msg":
                return isKorean
                    ? "다양한 색상과 형태를 사용하셨군요. 먼저 전체 그림이 어떤 느낌인지 설명해 주실 수 있을까요?"
                    : "You have used various colors and shapes. First, could you describe what the overall picture feels like?";
            case "greeting_first":
                return isKorean
                    ? "선택하신 감정 영역에 대해 대화를 나눠볼까요? 이 부분은 어떤 감정에 가까운지 말씀해주세요."
                    : "Shall we talk about the emotional region you selected? What kind of emotion is this part close to?";
            case "greeting_resume":
                return isKorean
                    ? "이전에 대화하던 감정 영역입니다. 이 부분에 대해 계속 대화해 볼까요?"
                    : "This is the emotional region we were talking about earlier. Shall we continue talking about this part?";
            case "thinking":
                return isKorean ? "생각 중" : "thinking";
            case "silence_timeout":
                return isKorean
                    ? "이 부분은 잠시 여기 두고 다음으로 넘어가도 괜찮아요."
                    : "You can leave this part here for a moment and move on to the next.";
            case "low_confidence_retry":
                return isKorean
                    ? "제가 조금 놓친 부분이 있을 수도 있어요. 마지막 부분을 한 번만 다시 이야기해 주실 수 있을까요?"
                    : "I might have missed a bit. Could you tell me that last part one more time?";
            case "low_confidence_fallback_system":
                return isKorean
                    ? "사용자 표현이 불완전하거나 모호해도 억지 해석하지 말고, 현재 표현을 있는 그대로 받아들이며 부드럽게 탐색을 이어가세요."
                    : "Even if the user's expression is incomplete or ambiguous, do not interpret it forcefully. Accept the current expression as it is and continue exploring gently.";
            case "suggest_select_region":
                return isKorean
                    ? "감사합니다. 이제 각 부분에 대해 하나씩 이야기해 보고 싶어요. 화면에서 이야기하고 싶은 감정 영역을 직접 선택해 주세요."
                    : "Thank you. Now I'd like to talk about each part one by one. Please select the emotional region you'd like to talk about directly on the screen.";
            case "closing_msg":
                return isKorean
                    ? "좋아요. 지금 이야기해 주신 것만으로도 이 감정의 모습이 꽤 선명해진 것 같아요."
                    : "Great. I think the shape of this emotion has become quite clear just from what you've shared.";
            case "earlier_collapsed":
                return isKorean
                    ? "이전 대화가 접혔습니다..."
                    : "Earlier conversation collapsed...";
            case "profile_captured_text":
                return isKorean
                    ? "감정 프로필이 성공적으로 캡처되었습니다."
                    : "Emotion profile captured successfully.";
            case "chat_title":
                return isKorean
                    ? "💬 AI 감정 대화"
                    : "💬 AI Emotion Chat";
            default:
                return "";
        }
    }

    void Start()
    {
        mapReceiver = GetComponent<BodyMapReceiver>();
        playerInputs = FindObjectOfType<StarterAssets.StarterAssetsInputs>();

        LoadAPIKeys();
        CreateSiriSphere(); // Setup 3D Breathing Emotional Orb
        
        try
        {
            CreateChatUI();
            Debug.Log("[BodyMapAI] Chat UI created successfully.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[BodyMapAI] Chat UI creation failed: " + e);
        }

#if UNITY_EDITOR
        CreateDebugUI();
#endif
    }

    void Update()
    {
        // E key: toggle HUD (works in all cursor states)
        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("[BodyMapAI] E pressed. Toggle AI HUD.");
            ToggleAIUI();
        }

        // Check if region selection changed to update UI panels
        UpdateActiveSelectionState();

        if (isUiActive)
        {
            UpdateMicrophoneState();
            HandleVoiceRecording();
            HandleInterruption();
        }

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F3))
        {
            enableDebugHUD = !enableDebugHUD;
            if (debugPanelObj != null)
            {
                debugPanelObj.SetActive(enableDebugHUD);
            }
        }
#endif
    }

    void LateUpdate()
    {
        if (chatPanelObj != null && chatPanelObj.activeSelf)
        {
            var panelRect = chatPanelObj.GetComponent<RectTransform>();
            SafeAreaClamp(panelRect, 20f);
        }
#if UNITY_EDITOR
        UpdateDebugUI();
#endif
    }

    private void SafeAreaClamp(RectTransform rectTrans, float margin)
    {
        if (rectTrans == null) return;
        Canvas canvas = rectTrans.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        Rect safeArea = Screen.safeArea;

        Vector3[] corners = new Vector3[4];
        rectTrans.GetWorldCorners(corners);

        Camera cam = canvas.worldCamera;
        Vector2 screenMin = Vector2.zero;
        Vector2 screenMax = Vector2.zero;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay || cam == null)
        {
            screenMin = corners[0];
            screenMax = corners[2];
        }
        else
        {
            screenMin = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            screenMax = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        }

        float width = screenMax.x - screenMin.x;
        float height = screenMax.y - screenMin.y;

        float minX = safeArea.xMin + margin;
        float maxX = safeArea.xMax - margin;
        float minY = safeArea.yMin + margin;
        float maxY = safeArea.yMax - margin;

        Vector2 currentCenter = (screenMin + screenMax) * 0.5f;
        float halfW = width * 0.5f;
        float halfH = height * 0.5f;

        float clampedX = Mathf.Clamp(currentCenter.x, minX + halfW, maxX - halfW);
        float clampedY = Mathf.Clamp(currentCenter.y, minY + halfH, maxY - halfH);

        if (clampedX != currentCenter.x || clampedY != currentCenter.y)
        {
            Vector2 clampedCenterScreen = new Vector2(clampedX, clampedY);
            Vector3 worldPoint;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay || cam == null)
            {
                worldPoint = clampedCenterScreen;
            }
            else
            {
                RectTransformUtility.ScreenPointToWorldPointInRectangle(rectTrans, clampedCenterScreen, cam, out worldPoint);
            }
            rectTrans.position = new Vector3(worldPoint.x, worldPoint.y, rectTrans.position.z);
        }
    }


    private void ToggleAIUI()
    {
        isUiActive = !isUiActive;
        Debug.Log($"[BodyMapAI] HUD active: {isUiActive}");

        if (chatPanelObj != null)
        {
            chatPanelObj.SetActive(isUiActive);
            Debug.Log($"[BodyMapAI] AIChatPanel active: {isUiActive}");
        }
        else
        {
            Debug.LogWarning("[BodyMapAI] chatPanelObj is null at ToggleAIUI!");
        }

        var orb = BreathingEmotionalOrb.Instance;
        if (orb != null)
        {
            orb.gameObject.SetActive(isUiActive);
            Debug.Log($"[BodyMapAI] BreathingEmotionalOrb active: {isUiActive}");
        }
        else
        {
            Debug.LogWarning("[BodyMapAI] BreathingEmotionalOrb.Instance is null! Orb was not created.");
        }

        // Lock / Unlock cursor based on active state
        if (playerInputs != null)
        {
            if (isUiActive)
            {
                playerInputs.cursorLocked = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                playerInputs.cursorLocked = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        if (isUiActive)
        {
            lastTimeVoiceDetected = Time.time;
            hasDetectedVoiceInCurrentClip = false;
            isProcessingUserAnswer = false;
            aiAskedQuestion = false;
            SetConversationState(ConversationState.WaitingForUser);
        }
        else
        {
            SetConversationState(ConversationState.Idle);
            if (isRecording)
            {
                isRecording = false;
                Microphone.End(null);
            }
        }
    }

    private void UpdateActiveSelectionState()
    {
        if (mapReceiver == null) return;
        if (!isUiActive) return; // Only update selection when active
        if (aiAskedQuestion && conversationState != ConversationState.WaitingForUser) return;

        InteractiveRegion3D activeRegion = mapReceiver.selectedRegionNormal;
        if (activeRegion != lastSelectedRegion)
        {
            if (activeRegion != null)
            {
                // Check if target is completed or in dialogue
                if (regionMemories.ContainsKey(activeRegion.id) &&
                    (regionMemories[activeRegion.id].generationState == RegionGenerationState.Generating ||
                     regionMemories[activeRegion.id].generationState == RegionGenerationState.Generated ||
                     regionMemories[activeRegion.id].generationState == RegionGenerationState.ReadyForGeneration))
                {
                    mapReceiver.selectedRegionNormal = lastSelectedRegion;
                    mapReceiver.ShowNotice("이 감정은 이미 준비되었습니다.");
                    return;
                }

                lastSelectedRegion = activeRegion;
                StartRegionReflection(activeRegion);
            }
            else
            {
                lastSelectedRegion = null;
            }
        }
    }

         public void OnImportCompleted()
    {
        if (introStarted) return;
        introStarted = true;

        Debug.Log("[BodyMapAIController] Received import completed event.");
        reflectionState = ReflectionState.InitialIntro;
        isUiActive = true;

        if (chatPanelObj != null)
            chatPanelObj.SetActive(true);

        if (BreathingEmotionalOrb.Instance != null)
        {
            BreathingEmotionalOrb.Instance.gameObject.SetActive(true);
        }

        if (playerInputs != null)
        {
            playerInputs.cursorLocked = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Initialize intro memory to track question turns
        if (!regionMemories.ContainsKey(-1))
        {
            RegionMemory introMem = new RegionMemory();
            introMem.regionId = -1;
            introMem.fullConversation = new List<MessageData> {
                new MessageData { role = "system", content = GetLocalizedString("intro_system") }
            };
            regionMemories[-1] = introMem;
        }
        regionMemories[-1].aiQuestionCount = 1;
        aiAskedQuestion = true;

        // Trigger step 1 intro dialog request
        string introMsg = GetLocalizedString("intro_msg");
        SetConversationState(ConversationState.AIThinking);
        
        if (aiResponseCoroutine != null) StopCoroutine(aiResponseCoroutine);
        aiResponseCoroutine = StartCoroutine(PlayIntroFlow(introMsg));
    }

    private IEnumerator PlayIntroFlow(string introMsg)
    {
        AudioClip clip = null;
        yield return StartCoroutine(ProcessTTS(introMsg, (c) => clip = c));
        yield return StartCoroutine(PlayTTSAndReturnToListening(clip, introMsg));
    }

    private void StartRegionReflection(InteractiveRegion3D region)
    {
        reflectionState = ReflectionState.RegionReflection;
        isProcessingUserAnswer = false;

        if (!regionMemories.ContainsKey(region.id))
        {
            RegionMemory mem = new RegionMemory();
            mem.regionId = region.id;
            mem.colorName = region.colorName;
            mem.bodyLocation = region.bodyLocation;
            mem.generationState = RegionGenerationState.Reflecting;
            mem.fullConversation = CreateSystemPromptForRegion(region);
            regionMemories[region.id] = mem;
        }

        var regionMem = regionMemories[region.id];

        if (!isUiActive)
        {
            ToggleAIUI();
        }

        UpdateChatDisplayForRegion(region.id);

        var history = regionMem.fullConversation;
        if (history.Count == 1) // only system prompt
        {
            string greeting = GetLocalizedString("greeting_first");
            history.Add(new MessageData { role = "assistant", content = greeting });
            regionMem.aiQuestionCount = 1;
            aiAskedQuestion = true;
            
            SetConversationState(ConversationState.AIThinking);
            if (aiResponseCoroutine != null) StopCoroutine(aiResponseCoroutine);
            aiResponseCoroutine = StartCoroutine(PlayGreetingFlow(greeting));
        }
        else
        {
            string resumeMsg = GetLocalizedString("greeting_resume");
            aiAskedQuestion = true;
            SetConversationState(ConversationState.AIThinking);
            if (aiResponseCoroutine != null) StopCoroutine(aiResponseCoroutine);
            aiResponseCoroutine = StartCoroutine(PlayGreetingFlow(resumeMsg));
        }
    }

    private IEnumerator PlayGreetingFlow(string greeting)
    {
        AudioClip clip = null;
        yield return StartCoroutine(ProcessTTS(greeting, (c) => clip = c));
        yield return StartCoroutine(PlayTTSAndReturnToListening(clip, greeting));
    }

    private void SetConversationState(ConversationState newState)
    {
        ConversationState oldState = conversationState;
        conversationState = newState;
        Debug.Log($"[ConversationState] {oldState} -> {newState}");

        if (BreathingEmotionalOrb.Instance != null)
        {
            try
            {
                BreathingEmotionalOrb.Instance.SetState((OrbState)newState);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[SetConversationState] Error casting to OrbState: " + e.Message);
            }
        }

        // Manage thinking indicator bubble
        if (newState == ConversationState.AIThinking || newState == ConversationState.ProcessingUserAnswer)
        {
            if (thinkingBubbleObj != null)
            {
                Destroy(thinkingBubbleObj);
            }
            if (thinkingAnimationCoroutine != null)
            {
                StopCoroutine(thinkingAnimationCoroutine);
            }
            
            thinkingBubbleObj = new GameObject("ThinkingBubble", typeof(RectTransform));
            thinkingBubbleObj.transform.SetParent(chatContentTrans, false);
            var textComp = thinkingBubbleObj.AddComponent<TextMeshProUGUI>();
            textComp.fontSize = 15;
            textComp.color = Color.gray;
            ApplyKoreanFont(textComp);
            
            thinkingAnimationCoroutine = StartCoroutine(AnimateThinkingDots(textComp));
        }
        else
        {
            if (thinkingAnimationCoroutine != null)
            {
                StopCoroutine(thinkingAnimationCoroutine);
                thinkingAnimationCoroutine = null;
            }
            if (thinkingBubbleObj != null)
            {
                Destroy(thinkingBubbleObj);
                thinkingBubbleObj = null;
            }
        }
    }

    private IEnumerator AnimateThinkingDots(TextMeshProUGUI textComp)
    {
        int dotCount = 1;
        while (textComp != null)
        {
            string dots = new string('.', dotCount);
            string thinkingText = GetLocalizedString("thinking");
            textComp.text = $"<b>AI:</b> ...{thinkingText}{dots}";
            dotCount = (dotCount % 3) + 1;
            
            Canvas.ForceUpdateCanvases();
            var scrollRect = chatPanelObj?.GetComponentInChildren<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 0f;
            }
            
            yield return new WaitForSeconds(0.4f);
        }
    }

    private void UpdateMicrophoneState()
    {
        if (!isUiActive)
        {
            if (isRecording)
            {
                isRecording = false;
                Microphone.End(null);
            }
            return;
        }

        bool needMic = (conversationState == ConversationState.WaitingForUser || 
                        conversationState == ConversationState.UserSpeaking || 
                        conversationState == ConversationState.AISpeaking);

        if (needMic && !isRecording)
        {
            StartVoiceRecording();
        }
        else if (!needMic && isRecording)
        {
            isRecording = false;
            Microphone.End(null);
        }
    }

    private float voiceStartTime = -1f;
    private float voiceEndTime = -1f;
    private float preSpeechBuffer = 0.3f;

    private void StartVoiceRecording()
    {
        isRecording = true;
        recordStartTime = Time.time;
        hasDetectedVoiceInCurrentClip = false;
        voiceStartTime = -1f;
        voiceEndTime = -1f;

        micRecordClip = Microphone.Start(null, false, (int)maxRecordTime, 16000);
        Debug.Log("[Voice] Listening started");
        Debug.Log("[STT] StartListening");
    }

    private void HandleVoiceRecording()
    {
        if (!isRecording) return;

        float currentVol = GetMicVolume();

        if (conversationState == ConversationState.WaitingForUser || conversationState == ConversationState.UserSpeaking)
        {
            if (currentVol > rmsStartThreshold) // rms > 0.015f
            {
                lastTimeVoiceDetected = Time.time;
                if (!hasDetectedVoiceInCurrentClip)
                {
                    hasDetectedVoiceInCurrentClip = true;
                    voiceStartTime = Time.time - recordStartTime;
                    SetConversationState(ConversationState.UserSpeaking);
                    Debug.Log("[Voice] Speech detected");
                }
                voiceEndTime = -1f;
            }

            if (hasDetectedVoiceInCurrentClip)
            {
                if (currentVol < rmsEndThreshold) // rms < 0.008f
                {
                    if (voiceEndTime < 0f)
                    {
                        voiceEndTime = Time.time - recordStartTime;
                    }

                    if (Time.time - lastTimeVoiceDetected > pauseThreshold) // 1.3s
                    {
                        Debug.Log("[Voice] Speech finalized");
                        StopVoiceRecordingAndProcess();
                    }
                }
                else
                {
                    lastTimeVoiceDetected = Time.time;
                    voiceEndTime = -1f;
                }
            }

            if (Time.time - lastTimeVoiceDetected > 90f)
            {
                HandleSilenceTimeout();
            }

            if (Time.time - recordStartTime >= maxRecordTime)
            {
                Debug.Log("[Voice] Max recording time reached (20s). Finalizing speech.");
                StopVoiceRecordingAndProcess();
            }
        }
    }

    private void HandleInterruption()
    {
        if (conversationState != ConversationState.AISpeaking) return;
        if (!isRecording) return;

        float currentVol = GetMicVolume();

        if (currentVol > 0.02f) // rms > 0.02f
        {
            if (!isPotentialInterrupted)
            {
                isPotentialInterrupted = true;
                interruptionTimer = 0f;
                if (audioSource != null) audioSource.volume = 0.25f;
                if (BreathingEmotionalOrb.Instance != null)
                {
                    BreathingEmotionalOrb.Instance.TriggerInterruptionPulse();
                }
                Debug.Log("[Voice] Potential interruption");
            }
            else
            {
                interruptionTimer += Time.deltaTime;
                if (interruptionTimer >= 0.6f)
                {
                    Debug.Log("[Voice] Confirmed interruption");
                    Debug.Log("[TTS] Playback interrupted");

                    if (audioSource != null)
                    {
                        audioSource.Stop();
                    }

                    if (aiResponseCoroutine != null)
                    {
                        StopCoroutine(aiResponseCoroutine);
                        aiResponseCoroutine = null;
                    }

                    isPotentialInterrupted = false;
                    interruptionTimer = 0f;
                    isProcessingUserAnswer = false;

                    // Stop current recording temporarily to reset and restart cleanly
                    Microphone.End(null);
                    isRecording = false;

                    SetConversationState(ConversationState.UserSpeaking);
                    StartVoiceRecording();

                    hasDetectedVoiceInCurrentClip = true;
                    voiceStartTime = Time.time - recordStartTime;
                    lastTimeVoiceDetected = Time.time;
                    voiceEndTime = -1f;
                }
            }
        }
        else
        {
            if (isPotentialInterrupted)
            {
                isPotentialInterrupted = false;
                interruptionTimer = 0f;
                if (audioSource != null) audioSource.volume = 1.0f;
                if (BreathingEmotionalOrb.Instance != null)
                {
                    BreathingEmotionalOrb.Instance.SetState(OrbState.AISpeaking);
                }
            }
        }

        if (audioSource != null && !audioSource.isPlaying && !isPotentialInterrupted)
        {
            SetConversationState(ConversationState.WaitingForUser);
        }
    }

    private void StopVoiceRecordingAndProcess()
    {
        if (isProcessingUserAnswer) return;
        isRecording = false;
        int micPos = Microphone.GetPosition(null);
        Microphone.End(null);
        Debug.Log("[STT] StopListening");

        float recordDuration = Time.time - recordStartTime;
        float speechDuration = 0f;
        if (hasDetectedVoiceInCurrentClip && voiceStartTime >= 0f)
        {
            float endMarker = (voiceEndTime >= 0f) ? voiceEndTime : recordDuration;
            speechDuration = endMarker - voiceStartTime;
        }

        if (speechDuration < minimumRecordingLength || !hasDetectedVoiceInCurrentClip)
        {
            SetConversationState(ConversationState.WaitingForUser);
            return;
        }

        float croppedStart = Mathf.Max(0f, voiceStartTime - preSpeechBuffer);
        float croppedEnd = Mathf.Min(recordDuration, ((voiceEndTime >= 0f) ? voiceEndTime : recordDuration) + postSpeechBuffer);
        float croppedDuration = croppedEnd - croppedStart;

        int startSample = Mathf.RoundToInt(croppedStart * micRecordClip.frequency);
        int sampleCount = Mathf.RoundToInt(croppedDuration * micRecordClip.frequency);

        int totalSamplesAvailable = Mathf.Min(micPos, micRecordClip.samples);
        if (startSample >= totalSamplesAvailable)
        {
            SetConversationState(ConversationState.WaitingForUser);
            return;
        }
        if (startSample + sampleCount > totalSamplesAvailable)
        {
            sampleCount = totalSamplesAvailable - startSample;
        }

        if (sampleCount <= 0)
        {
            SetConversationState(ConversationState.WaitingForUser);
            return;
        }

        SetConversationState(ConversationState.ProcessingUserAnswer);
        isProcessingUserAnswer = true;
        if (BreathingEmotionalOrb.Instance != null)
        {
            BreathingEmotionalOrb.Instance.TriggerVisualAcknowledgment();
        }

        float[] croppedSamples = new float[sampleCount * micRecordClip.channels];
        micRecordClip.GetData(croppedSamples, startSample);
        byte[] wavBytes = ConvertSamplesToWav(croppedSamples, micRecordClip.frequency, micRecordClip.channels);

        if (aiResponseCoroutine != null) StopCoroutine(aiResponseCoroutine);
        aiResponseCoroutine = StartCoroutine(SpeechToTextAndRespondFlow(wavBytes, lastSelectedRegion));
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

    private void HandleSilenceTimeout()
    {
        lastTimeVoiceDetected = Time.time;
        hasDetectedVoiceInCurrentClip = false;

        if (reflectionState == ReflectionState.RegionReflection && lastSelectedRegion != null)
        {
            string msg = GetLocalizedString("silence_timeout");
            
            SetConversationState(ConversationState.AIThinking);
            if (aiResponseCoroutine != null) StopCoroutine(aiResponseCoroutine);
            aiResponseCoroutine = StartCoroutine(PlayGreetingFlow(msg));

            reflectionState = ReflectionState.AwaitingSelection;
            lastSelectedRegion = null;
            if (mapReceiver != null) mapReceiver.DeselectActiveRegion();
        }
    }

    [System.Serializable]
    public class OpenAIWhisperSegment
    {
        public string text;
        public float avg_logprob;
    }

    [System.Serializable]
    public class OpenAIWhisperVerboseResponse
    {
        public string text;
        public OpenAIWhisperSegment[] segments;
    }

    [System.Serializable]
    public class GPTJsonResponse
    {
        public string assistantResponse = "";
        public string emotionDescription = "";
        public string shapeDescription = "";
        public string textureDescription = "";
        public string movementDescription = "";
        public string relationshipDescription = "";
        public string finalPrompt = "";
        public bool shouldConclude = false;
    }

    private IEnumerator ProcessSTT(byte[] wavBytes, System.Action<string, float> onComplete)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", wavBytes, "audio.wav", "audio/wav");
        form.AddField("model", "whisper-1");
        string sttLanguage = "ko";
        if (GameManager.Instance != null && GameManager.Instance.currentLanguage == "en")
        {
            sttLanguage = "en";
        }
        form.AddField("language", sttLanguage);
        form.AddField("response_format", "verbose_json");

        using (UnityWebRequest whisperReq = UnityWebRequest.Post("https://api.openai.com/v1/audio/transcriptions", form))
        {
            whisperReq.SetRequestHeader("Authorization", "Bearer " + openAiApiKey);
            yield return whisperReq.SendWebRequest();

            if (whisperReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Whisper API Error] {whisperReq.error}");
                onComplete?.Invoke(null, 0f);
                yield break;
            }

            string jsonRes = whisperReq.downloadHandler.text;
            OpenAIWhisperVerboseResponse whisperRes = null;
            try
            {
                whisperRes = JsonUtility.FromJson<OpenAIWhisperVerboseResponse>(jsonRes);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[STT] Failed to parse Whisper verbose response: " + ex.Message);
            }

            if (whisperRes == null || string.IsNullOrEmpty(whisperRes.text))
            {
                onComplete?.Invoke(null, 0f);
                yield break;
            }

            float confidence = 1.0f;
            if (whisperRes.segments != null && whisperRes.segments.Length > 0)
            {
                float sumLogprob = 0f;
                for (int i = 0; i < whisperRes.segments.Length; i++)
                {
                    sumLogprob += whisperRes.segments[i].avg_logprob;
                }
                float avgLogprob = sumLogprob / whisperRes.segments.Length;
                confidence = Mathf.Exp(avgLogprob);
            }

            Debug.Log($"[STT Final] {whisperRes.text}");
            Debug.Log($"[STT Confidence] {confidence:F4}");

            if (confidence < 0.75f)
            {
                Debug.Log("[STT] Low confidence detected");
            }

            onComplete?.Invoke(whisperRes.text, confidence);
        }
    }

    private IEnumerator ProcessGPT(List<MessageData> history, bool useJsonMode, System.Action<string> onComplete)
    {
        Debug.Log("[GPT] Request started");

        string messagesJson = "";
        for (int i = 0; i < history.Count; i++)
        {
            messagesJson += $"{{\"role\":\"{history[i].role}\",\"content\":\"{EscapeJsonString(history[i].content)}\"}}";
            if (i < history.Count - 1) messagesJson += ",";
        }

        string jsonPayload;
        if (useJsonMode)
        {
            jsonPayload = $"{{\"model\":\"gpt-4o-mini\",\"response_format\":{{\"type\":\"json_object\"}},\"messages\":[{messagesJson}],\"temperature\":0.7}}";
        }
        else
        {
            jsonPayload = $"{{\"model\":\"gpt-4o-mini\",\"messages\":[{messagesJson}],\"temperature\":0.7}}";
        }

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
                Debug.LogError($"[GPT Error] {chatReq.error}");
                onComplete?.Invoke(null);
                yield break;
            }

            OpenAIChatResponse chatRes = null;
            try
            {
                chatRes = JsonUtility.FromJson<OpenAIChatResponse>(chatReq.downloadHandler.text);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[GPT] Failed to parse: " + ex.Message);
            }

            if (chatRes == null || chatRes.choices == null || chatRes.choices.Length == 0)
            {
                onComplete?.Invoke(null);
                yield break;
            }

            string rawAiResponse = chatRes.choices[0].message.content;
            Debug.Log("[GPT] Response complete");
            onComplete?.Invoke(rawAiResponse);
        }
    }

    private IEnumerator ProcessTTS(string text, System.Action<AudioClip> onComplete)
    {
        Debug.Log("[TTS] Generate once");

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

            if (request.result == UnityWebRequest.Result.Success)
            {
                byte[] data = request.downloadHandler.data;
                if (data != null && data.Length > 0)
                {
                    string tempPath = Path.Combine(Application.temporaryCachePath, "tts_output.mp3");
                    try
                    {
                        File.WriteAllBytes(tempPath, data);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[TTS] Failed to save temp: {ex.Message}");
                    }

                    string fileUrl = "file:///" + tempPath.Replace("\\", "/");
                    using (UnityWebRequest audioLoader = UnityWebRequestMultimedia.GetAudioClip(fileUrl, AudioType.MPEG))
                    {
                        yield return audioLoader.SendWebRequest();

                        if (audioLoader.result == UnityWebRequest.Result.Success)
                        {
                            AudioClip clip = DownloadHandlerAudioClip.GetContent(audioLoader);
                            onComplete?.Invoke(clip);
                        }
                        else
                        {
                            Debug.LogError($"[TTS] Failed to load audio from temp file: {audioLoader.error}");
                            onComplete?.Invoke(null);
                        }
                    }
                }
                else
                {
                    onComplete?.Invoke(null);
                }
            }
            else
            {
                Debug.LogError($"[TTS API Request Failed] {request.error}");
                onComplete?.Invoke(null);
            }
        }
    }

    private IEnumerator PlayTTSAndReturnToListening(AudioClip clip, string text = "")
    {
        if (clip == null)
        {
            SetConversationState(ConversationState.WaitingForUser);
            yield break;
        }

        if (!string.IsNullOrEmpty(text))
        {
            AddChatMessage("AI", text, Color.white);
        }

        audioSource.clip = clip;
        audioSource.volume = 1f;
        audioSource.spatialBlend = 0f;
        audioSource.Play();

        SetConversationState(ConversationState.AISpeaking);
        Debug.Log("[TTS] Playback started");
        Debug.Log("[TTS] Speech started");

        while (audioSource != null && audioSource.isPlaying)
        {
            float amp = GetOutputVolume();
            if (BreathingEmotionalOrb.Instance != null)
            {
                BreathingEmotionalOrb.Instance.UpdateVolumeAmplitude(amp);
            }
            yield return null;
        }

        Debug.Log("[TTS] Speech ended");

        if (conversationState == ConversationState.AISpeaking)
        {
            SetConversationState(ConversationState.WaitingForUser);
        }
    }

    private IEnumerator SpeechToTextAndRespondFlow(byte[] wavBytes, InteractiveRegion3D region)
    {
        aiAskedQuestion = false;
        string userTranscript = null;
        float confidence = 0f;

        yield return StartCoroutine(ProcessSTT(wavBytes, (text, conf) => {
            userTranscript = text;
            confidence = conf;
        }));

        if (string.IsNullOrWhiteSpace(userTranscript))
        {
            isProcessingUserAnswer = false;
            SetConversationState(ConversationState.WaitingForUser);
            yield break;
        }

        AddChatMessage("User", userTranscript, new Color(0.2f, 0.6f, 1f));

        List<MessageData> history;
        RegionMemory mem = null;
        if (reflectionState == ReflectionState.InitialIntro)
        {
            if (!regionMemories.ContainsKey(-1))
            {
                RegionMemory introMem = new RegionMemory();
                introMem.regionId = -1;
                introMem.fullConversation = new List<MessageData> {
                    new MessageData { role = "system", content = GetLocalizedString("intro_system") }
                };
                regionMemories[-1] = introMem;
            }
            mem = regionMemories[-1];
            history = mem.fullConversation;
        }
        else if (region != null && regionMemories.ContainsKey(region.id))
        {
            mem = regionMemories[region.id];
            history = mem.fullConversation;
        }
        else
        {
            isProcessingUserAnswer = false;
            SetConversationState(ConversationState.WaitingForUser);
            yield break;
        }

        history.Add(new MessageData { role = "user", content = userTranscript });

        if (confidence < 0.55f)
        {
            if (userTranscript.Length <= 8)
            {
                string retryMsg = GetLocalizedString("low_confidence_retry");
                history.Add(new MessageData { role = "assistant", content = retryMsg });

                AudioClip retryClip = null;
                yield return StartCoroutine(ProcessTTS(retryMsg, (c) => retryClip = c));
                yield return StartCoroutine(PlayTTSAndReturnToListening(retryClip, retryMsg));
                isProcessingUserAnswer = false;
                yield break;
            }
            else
            {
                List<MessageData> tempHistory = new List<MessageData>(history);
                tempHistory.Insert(1, new MessageData {
                    role = "system",
                    content = GetLocalizedString("low_confidence_fallback_system")
                });

                string rawAiResponse = null;
                yield return StartCoroutine(ProcessGPT(tempHistory, (region != null && reflectionState == ReflectionState.RegionReflection), (resp) => rawAiResponse = resp));

                if (string.IsNullOrEmpty(rawAiResponse))
                {
                    isProcessingUserAnswer = false;
                    SetConversationState(ConversationState.WaitingForUser);
                    yield break;
                }

                yield return StartCoroutine(HandleGPTResponse(rawAiResponse, region, mem, history));
                isProcessingUserAnswer = false;
                yield break;
            }
        }

        {
            string rawAiResponse = null;
            yield return StartCoroutine(ProcessGPT(history, (region != null && reflectionState == ReflectionState.RegionReflection), (resp) => rawAiResponse = resp));

            if (string.IsNullOrEmpty(rawAiResponse))
            {
                isProcessingUserAnswer = false;
                SetConversationState(ConversationState.WaitingForUser);
                yield break;
            }

            yield return StartCoroutine(HandleGPTResponse(rawAiResponse, region, mem, history));
            isProcessingUserAnswer = false;
        }
    }

    private IEnumerator HandleGPTResponse(string rawAiResponse, InteractiveRegion3D region, RegionMemory mem, List<MessageData> history)
    {
        bool useJsonMode = (region != null && reflectionState == ReflectionState.RegionReflection);
        string cleanAiResponse = rawAiResponse;
        string emotionDesc = "";
        string shapeDesc = "";
        string textureDesc = "";
        string movementDesc = "";
        string relationshipDesc = "";
        string finalPrompt = "";
        bool shouldConclude = false;

        if (useJsonMode && mem != null)
        {
            string cleanJson = CleanJsonResponse(rawAiResponse);
            GPTJsonResponse responseObj = null;
            try
            {
                responseObj = JsonUtility.FromJson<GPTJsonResponse>(cleanJson);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[GPT] JSON Parse Error: " + ex.Message + "\nRaw: " + rawAiResponse);
            }

            if (responseObj != null)
            {
                cleanAiResponse = responseObj.assistantResponse;
                emotionDesc = responseObj.emotionDescription;
                shapeDesc = responseObj.shapeDescription;
                textureDesc = responseObj.textureDescription;
                movementDesc = responseObj.movementDescription;
                relationshipDesc = responseObj.relationshipDescription;
                finalPrompt = responseObj.finalPrompt;
                shouldConclude = responseObj.shouldConclude;

                int categoriesGathered = 0;
                if (!string.IsNullOrEmpty(emotionDesc)) categoriesGathered++;
                if (!string.IsNullOrEmpty(shapeDesc)) categoriesGathered++;
                if (!string.IsNullOrEmpty(textureDesc)) categoriesGathered++;
                if (!string.IsNullOrEmpty(movementDesc)) categoriesGathered++;
                if (!string.IsNullOrEmpty(relationshipDesc)) categoriesGathered++;

                mem.infoGatheredCount = categoriesGathered;
            }
        }

        if (mem != null)
        {
            mem.aiQuestionCount++;
            aiAskedQuestion = true;
        }

        if (reflectionState == ReflectionState.InitialIntro)
        {
            if (mem != null && mem.aiQuestionCount < 3)
            {
                history.Add(new MessageData { role = "assistant", content = cleanAiResponse });
                AudioClip normalClip = null;
                yield return StartCoroutine(ProcessTTS(cleanAiResponse, (c) => normalClip = c));
                yield return StartCoroutine(PlayTTSAndReturnToListening(normalClip, cleanAiResponse));
            }
            else
            {
                history.Add(new MessageData { role = "assistant", content = cleanAiResponse });
                AudioClip normalClip = null;
                yield return StartCoroutine(ProcessTTS(cleanAiResponse, (c) => normalClip = c));
                yield return StartCoroutine(PlayTTSAndReturnToListening(normalClip, cleanAiResponse));

                string suggestMsg = GetLocalizedString("suggest_select_region");
                history.Add(new MessageData { role = "assistant", content = suggestMsg });

                AudioClip suggestClip = null;
                yield return StartCoroutine(ProcessTTS(suggestMsg, (c) => suggestClip = c));

                SetConversationState(ConversationState.TransitioningStep);
                reflectionState = ReflectionState.AwaitingSelection;

                yield return StartCoroutine(PlayTTSAndReturnToListening(suggestClip, suggestMsg));
            }
            yield break;
        }

        bool forceConclusion = false;
        if (useJsonMode && mem != null)
        {
            if (shouldConclude || mem.infoGatheredCount >= 3 || mem.aiQuestionCount >= 4)
            {
                forceConclusion = true;
            }
        }

        if (forceConclusion && region != null && mem != null)
        {
            string closingMsg = GetLocalizedString("closing_msg");
            history.Add(new MessageData { role = "assistant", content = closingMsg });

            AudioClip ttsClip = null;
            yield return StartCoroutine(ProcessTTS(closingMsg, (c) => ttsClip = c));
            yield return StartCoroutine(PlayTTSAndReturnToListening(ttsClip, closingMsg));

            if (string.IsNullOrEmpty(finalPrompt))
            {
                finalPrompt = $"{region.colorName} emotional form in {region.bodyLocation}";
                if (!string.IsNullOrEmpty(emotionDesc)) finalPrompt += $", representing {emotionDesc}";
                if (!string.IsNullOrEmpty(shapeDesc)) finalPrompt += $", {shapeDesc}";
                if (!string.IsNullOrEmpty(textureDesc)) finalPrompt += $", with {textureDesc} texture";
                if (!string.IsNullOrEmpty(movementDesc)) finalPrompt += $", showing {movementDesc} movement";
            }

            CaptureEmotionProfile(region, mem, emotionDesc, shapeDesc, textureDesc, movementDesc, relationshipDesc, finalPrompt);
            yield break;
        }

        history.Add(new MessageData { role = "assistant", content = cleanAiResponse });

        AudioClip normalClip2 = null;
        yield return StartCoroutine(ProcessTTS(cleanAiResponse, (c) => normalClip2 = c));
        yield return StartCoroutine(PlayTTSAndReturnToListening(normalClip2, cleanAiResponse));
    }

    private void CaptureEmotionProfile(InteractiveRegion3D region, RegionMemory mem, string emotionDesc, string shapeDesc, string textureDesc, string movementDesc, string relationshipDesc, string finalPrompt)
    {
        mem.generationState = RegionGenerationState.ReadyForGeneration;

        mem.emotionProfile.emotionDescription = emotionDesc;
        mem.emotionProfile.shapeDescription = shapeDesc;
        mem.emotionProfile.textureDescription = textureDesc;
        mem.emotionProfile.movementDescription = movementDesc;
        mem.emotionProfile.relationshipDescription = relationshipDesc;
        mem.emotionProfile.finalPrompt = finalPrompt;

        Debug.Log("[Emotion] Profile completed");
        Debug.Log($"[Emotion] Final prompt: {finalPrompt}");
        Debug.Log("[Emotion] Generation deferred");

        AddChatMessage("System", GetLocalizedString("profile_captured_text"), Color.green);

        if (BreathingEmotionalOrb.Instance != null)
        {
            BreathingEmotionalOrb.Instance.SetState(OrbState.ReadyForGeneration);
        }

        reflectionState = ReflectionState.AwaitingSelection;
        if (lastSelectedRegion == region)
        {
            lastSelectedRegion = null;
        }
        if (mapReceiver != null)
        {
            mapReceiver.DeselectActiveRegion();
        }
    }

    private string CleanJsonResponse(string text)
    {
        if (string.IsNullOrEmpty(text)) return "{}";
        string trimmed = text.Trim();
        if (trimmed.StartsWith("```"))
        {
            int firstLineEnd = trimmed.IndexOf('\n');
            if (firstLineEnd != -1)
            {
                trimmed = trimmed.Substring(firstLineEnd).Trim();
            }
            if (trimmed.EndsWith("```"))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 3).Trim();
            }
        }
        return trimmed;
    }

    private float GetOutputVolume()
    {
        if (audioSource == null || !audioSource.isPlaying) return 0f;
        float[] samples = new float[64];
        audioSource.GetOutputData(samples, 0);
        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i] * samples[i];
        }
        return Mathf.Sqrt(sum / samples.Length);
    }

    private List<MessageData> CreateSystemPromptForRegion(InteractiveRegion3D region)
    {
        var history = new List<MessageData>();
        string sysPrompt =
            "You are a warm, empathetic Art Therapy companion helping the user explore a body sensation. " +
            "Your role is like a gentle counselor — not an interviewer. Never ask checklist questions (like 'What is the texture?'). " +
            "Reflect back what the user shares, then ask ONE natural follow-up question that feels like genuine curiosity.\n\n" +
            "You MUST respond ONLY in JSON format matching the following schema:\n" +
            "{\n" +
            "  \"assistantResponse\": \"AI's warm conversational response in Korean (under 2-3 sentences)\",\n" +
            "  \"hasEnoughInfo\": true/false,\n" +
            "  \"categoryCount\": integer (0 to 6),\n" +
            "  \"generatedPrompt\": \"English description of the emotional object for 3D model generation, including forms, textures, colors, and feelings (ONLY if hasEnoughInfo is true or categoryCount >= 3, otherwise empty)\",\n" +
            "  \"shouldConclude\": true/false\n" +
            "}\n\n" +
            "Track the following 6 information categories:\n" +
            "1. What this emotion feels like (describedEmotion)\n" +
            "2. What kind of shape/presence/lump it is (describedShape)\n" +
            "3. Surface texture/feeling (describedSurface)\n" +
            "4. Movement or stillness (describedMovement)\n" +
            "5. Proximity preference (want to keep it close or far)\n" +
            "6. How the user wants to deal with/handle it\n\n" +
            "When you have identified answers to at least 3 of these categories in the conversation, set hasEnoughInfo=true, shouldConclude=true, " +
            "and generate the English 3D prompt in generatedPrompt. " +
            "If the user wants to stop, or categoryCount >= 3, or turn count is high, generate the prompt.";

        history.Add(new MessageData { role = "system", content = sysPrompt });
        return history;
    }

    private void UpdateChatDisplayForRegion(int id)
    {
        if (chatContentTrans != null)
        {
            foreach (Transform child in chatContentTrans)
            {
                Destroy(child.gameObject);
            }
        }

        if (regionMemories.ContainsKey(id))
        {
            var mem = regionMemories[id];
            var history = mem.fullConversation;
            
            // Limit visible messages strictly to 10 (excluding index 0 system prompt)
            int conversationMessageCount = history.Count - 1;
            int maxVisible = 10;
            
            int startIndex = 1;
            bool collapsed = false;
            
            if (conversationMessageCount > maxVisible)
            {
                startIndex = history.Count - maxVisible;
                collapsed = true;
            }

            if (collapsed)
            {
                AddChatMessage("System", GetLocalizedString("earlier_collapsed"), Color.gray);
            }

            for (int i = startIndex; i < history.Count; i++)
            {
                var msg = history[i];
                Color c = msg.role == "user" ? new Color(0.2f, 0.6f, 1f) : Color.white;
                string cleanText = msg.content;
                int promptStart = cleanText.IndexOf("[[3D Prompt:");
                if (promptStart != -1)
                {
                    cleanText = cleanText.Substring(0, promptStart).Trim();
                }
                AddChatMessage(msg.role == "user" ? "User" : "AI", cleanText, c);
            }

            // Auto-scroll to bottom
            Canvas.ForceUpdateCanvases();
            var scrollRect = chatPanelObj?.GetComponentInChildren<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }

    private void CreateSiriSphere()
    {
        if (BreathingEmotionalOrb.Instance == null)
        {
            Canvas canvas = GetOrCreateHudCanvas();
            GameObject orbGO = new GameObject("BreathingEmotionalOrb", typeof(RectTransform));
            if (canvas != null)
            {
                orbGO.transform.SetParent(canvas.transform, false);
            }
            orbGO.AddComponent<BreathingEmotionalOrb>();
            orbGO.SetActive(isUiActive);
            Debug.Log("[BodyMapAI] Procedural BreathingEmotionalOrb created in scene under Canvas_AI_HUD.");
        }
    }

    public Canvas GetOrCreateHudCanvas()
    {
        if (hudCanvas != null) return hudCanvas;

        GameObject existingCanvas = GameObject.Find("Canvas_AI_HUD");
        if (existingCanvas != null)
        {
            hudCanvas = existingCanvas.GetComponent<Canvas>();
            if (hudCanvas != null) return hudCanvas;
        }

        GameObject canvasGO = new GameObject("Canvas_AI_HUD");
        hudCanvas = canvasGO.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        hudCanvas.worldCamera = targetCamera != null ? targetCamera : FindFirstObjectByType<Camera>();
        hudCanvas.planeDistance = 0.8f;
        hudCanvas.sortingOrder = 99;

        var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        return hudCanvas;
    }

    private void CreateChatUI()
    {
        Canvas canvas = GetOrCreateHudCanvas();
        if (canvas == null) { Debug.LogError("[BodyMapAI] Cannot create ChatUI: HUD canvas is null!"); return; }

        // Ensure Canvas render settings
        canvas.sortingOrder = 100;
        Debug.Log($"[BodyMapAI] HUD Canvas: renderMode={canvas.renderMode} sortingOrder={canvas.sortingOrder}");

        chatPanelObj = new GameObject("AIChatPanel", typeof(RectTransform));
        chatPanelObj.transform.SetParent(canvas.transform, false);

        // Bottom-right anchor, 420x340, 32px margin
        var panelRect = chatPanelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot     = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-32f, 32f);
        panelRect.sizeDelta = new Vector2(420f, 340f);

        // Background: dark panel, alpha 0.65
        var panelImg = chatPanelObj.AddComponent<Image>();
        panelImg.color = new Color(0.05f, 0.05f, 0.10f, 0.65f);

        var outline = chatPanelObj.AddComponent<Outline>();
        outline.effectColor  = new Color(0.3f, 0.6f, 1f, 0.35f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // Title bar
        GameObject titleGO = new GameObject("Chat Title", typeof(RectTransform));
        titleGO.transform.SetParent(chatPanelObj.transform, false);
        var titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.text      = GetLocalizedString("chat_title");
        titleText.fontSize  = 18;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color     = Color.white;
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin        = new Vector2(0f, 1f);
        titleRect.anchorMax        = new Vector2(1f, 1f);
        titleRect.pivot            = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -6f);
        titleRect.sizeDelta        = new Vector2(-16f, 28f);
        ApplyKoreanFont(titleText);

        // Scroll container (below title, 36px top offset)
        GameObject scrollGO = new GameObject("ScrollRect", typeof(RectTransform));
        scrollGO.transform.SetParent(chatPanelObj.transform, false);
        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        var scrollRectTransform = scrollGO.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin  = Vector2.zero;
        scrollRectTransform.anchorMax  = Vector2.one;
        scrollRectTransform.offsetMin  = new Vector2(8f, 8f);
        scrollRectTransform.offsetMax  = new Vector2(-8f, -36f);

        // Viewport with mask
        GameObject viewportGO = new GameObject("Viewport", typeof(RectTransform));
        viewportGO.transform.SetParent(scrollGO.transform, false);
        var mask       = viewportGO.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        viewportGO.AddComponent<Image>(); // required by Mask
        var viewportRect = viewportGO.GetComponent<RectTransform>();
        viewportRect.anchorMin        = Vector2.zero;
        viewportRect.anchorMax        = Vector2.one;
        viewportRect.sizeDelta        = Vector2.zero;
        viewportRect.anchoredPosition = Vector2.zero;

        // Content (grows downward from top)
        GameObject contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(viewportGO.transform, false);
        var contentRect = contentGO.GetComponent<RectTransform>();
        contentRect.anchorMin        = new Vector2(0f, 1f);
        contentRect.anchorMax        = new Vector2(1f, 1f);
        contentRect.pivot            = new Vector2(0.5f, 1f);
        contentRect.sizeDelta        = new Vector2(0f, 0f);
        contentRect.anchoredPosition = Vector2.zero;

        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing              = 5f;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth  = true;
        vlg.childControlHeight     = true;
        vlg.childControlWidth      = true;
        vlg.padding                = new RectOffset(6, 6, 6, 6);

        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport  = viewportRect;
        scrollRect.content   = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical   = true;
        scrollRect.scrollSensitivity = 20f;

        chatContentTrans = contentRect;

        // Start hidden — toggled by E key
        chatPanelObj.SetActive(false);
        Debug.Log("[BodyMapAI] CreateChatUI complete. Panel size: 420x340, alpha: 0.65");
    }

    private void AddChatMessage(string sender, string message, Color color)
    {
        if (chatContentTrans == null)
        {
            Debug.LogWarning("[BodyMapAI] AddChatMessage: chatContentTrans is null! CreateChatUI may not have run.");
            return;
        }

        string senderLabel = sender;
        bool isKorean = true;
        if (GameManager.Instance != null)
        {
            isKorean = GameManager.Instance.currentLanguage == "ko";
        }

        if (isKorean)
        {
            if (sender == "User") senderLabel = "사용자";
            else if (sender == "System") senderLabel = "시스템";
            else if (sender == "AI") senderLabel = "AI";
        }

        string gameObjectName = sender + " Message";
        GameObject msgGO = new GameObject(gameObjectName, typeof(RectTransform));
        msgGO.transform.SetParent(chatContentTrans, false);

        var text = msgGO.AddComponent<TextMeshProUGUI>();
        // sender label: bold, message body: normal
        text.text = $"<b>{senderLabel}:</b> {message}";
        text.fontSize  = 15;
        text.color     = color;
        text.textWrappingMode   = TextWrappingModes.Normal;

        ApplyKoreanFont(text);

        Canvas.ForceUpdateCanvases();
        if (chatContentTrans != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(chatContentTrans.GetComponent<RectTransform>());
        }
        var scrollRect = chatPanelObj?.GetComponentInChildren<ScrollRect>();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }

        Debug.Log($"[BodyMapAI] Chat message rendered [{sender}]: {message.Substring(0, Mathf.Min(40, message.Length))}");
    }

    private void LoadAPIKeys()
    {
        string path = Path.Combine(Application.dataPath, "../api_keys.json");
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            APIKeys keys = JsonUtility.FromJson<APIKeys>(json);
            openAiApiKey = keys.OpenAI_API_Key;
            tripoApiKey = keys.Tripo3D_API_Key;
            Debug.Log("[BodyMapAI] API keys loaded successfully.");
        }
        else
        {
            APIKeys template = new APIKeys();
            template.OpenAI_API_Key = "YOUR_OPENAI_API_KEY_HERE";
            template.Tripo3D_API_Key = "YOUR_TRIPO3D_API_KEY_HERE";
            string json = JsonUtility.ToJson(template, true);
            File.WriteAllText(path, json);
            Debug.LogError($"[BodyMapAI] api_keys.json not found! Template created at {path}.");
        }
    }

    private string EscapeJsonString(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    private byte[] ConvertClipToWav(AudioClip clip, int lengthSamples)
    {
        float[] samples = new float[lengthSamples * clip.channels];
        clip.GetData(samples, 0);

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
                writer.Write((short)clip.channels);
                writer.Write(clip.frequency);
                writer.Write(clip.frequency * clip.channels * 2);
                writer.Write((short)(clip.channels * 2));
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

        int decibelWindow = 128;
        float[] samples = new float[decibelWindow];
        int micPosition = Microphone.GetPosition(null);
        int startPosition = micPosition - decibelWindow;
        if (startPosition < 0) return 0f;

        micRecordClip.GetData(samples, startPosition);

        float sum = 0f;
        for (int i = 0; i < decibelWindow; i++)
        {
            sum += samples[i] * samples[i];
        }
        float rms = Mathf.Sqrt(sum / decibelWindow);
        return rms;
    }

    [System.Serializable]
    public class APIKeys
    {
        public string OpenAI_API_Key;
        public string Tripo3D_API_Key;
    }

    [System.Serializable]
    public class OpenAIWhisperResponse
    {
        public string text;
    }

    [System.Serializable]
    public class OpenAIChatResponse
    {
        public string id;
        public string model;
        public OpenAIChoice[] choices;
    }

    [System.Serializable]
    public class OpenAIChoice
    {
        public int index;
        public OpenAIMessage message;
        public string finish_reason;
    }

    [System.Serializable]
    public class OpenAIMessage
    {
        public string role;
        public string content;
    }

    private static TMP_FontAsset runtimeKoreanFontAsset = null;
    public static TMP_FontAsset GetKoreanFontAsset()
    {
        if (runtimeKoreanFontAsset != null) return runtimeKoreanFontAsset;

        // 1st priority: Inspector-assigned font asset
        if (Instance != null && Instance.koreanFontAsset != null)
        {
            runtimeKoreanFontAsset = Instance.koreanFontAsset;
            Debug.Log("[Font] Using Inspector-assigned koreanFontAsset: " + runtimeKoreanFontAsset.name);
            return runtimeKoreanFontAsset;
        }

        // 2nd priority: Resources.Load TMP_FontAsset (requires .asset file with valid atlas)
        TMP_FontAsset loaded = Resources.Load<TMP_FontAsset>("Fonts/NotoSansKR");
        if (loaded != null)
        {
            // CRITICAL: Validate atlas — Dynamic assets saved incorrectly have null atlasTextures
            bool atlasOk = (loaded.atlasTextures != null);
            bool materialOk = (loaded.material != null);

            if (atlasOk && materialOk)
            {
                runtimeKoreanFontAsset = loaded;
                Debug.Log("[Font] Loaded: NotoSansKR  (atlas: OK, material: OK)");
                return runtimeKoreanFontAsset;
            }
            else
            {
                Debug.LogWarning("[Font] NotoSansKR.asset is broken. " +
                                 "atlas=" + atlasOk + " material=" + materialOk +
                                 " → Falling through to OS fallback.");
                // Do NOT use this broken asset — fall through
            }
        }

        // 3rd priority: OS Dynamic font fallback
        Debug.LogWarning("[Font] TMP_FontAsset not found in Resources. Trying OS dynamic font fallback...");
        string[] osFontNames = { "Malgun Gothic", "맑은 고딕", "NanumGothic", "Dotum", "Gulim", "Arial Unicode MS", "Arial" };
        Font font = null;
        for (int i = 0; i < osFontNames.Length; i++)
        {
            font = Font.CreateDynamicFontFromOSFont(osFontNames[i], 24);
            if (font != null)
            {
                Debug.Log($"[Font] OS Fallback: Using OS Font: {osFontNames[i]}");
                break;
            }
        }
        if (font != null)
        {
            runtimeKoreanFontAsset = TMP_FontAsset.CreateFontAsset(
                font, 86, 4, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic
            );
            if (runtimeKoreanFontAsset != null && runtimeKoreanFontAsset.material != null)
            {
                Shader dfShader = Shader.Find("TextMeshPro/Mobile/Distance Field");
                if (dfShader == null) dfShader = Shader.Find("TextMeshPro/Distance Field");
                if (dfShader != null)
                {
                    runtimeKoreanFontAsset.material.shader = dfShader;
                    Debug.Log("[Font] Applied Distance Field shader to OS fallback font.");
                }
            }
            return runtimeKoreanFontAsset;
        }

        Debug.LogError("[Font] ALL font loading paths failed. Korean text will appear as squares.");
        return null;
    }

    public static void ApplyKoreanFont(TMP_Text text)
    {
        if (text == null) return;
        var koreanFont = GetKoreanFontAsset();
        if (koreanFont != null)
        {
            try
            {
                text.font = koreanFont;
                text.fontSharedMaterial = koreanFont.material;
                Debug.Log($"[Font] {text.gameObject.name} font = {koreanFont.name}");
            }
            catch (System.Exception e)
            {
                // Font failure must NEVER crash other systems (Orb, BodyMapReceiver, E-key)
                Debug.LogError("[Font] Failed to apply Korean font to " + text.gameObject.name + ": " + e.Message);
            }
        }
        else
        {
            Debug.LogError("[Font] FAILED: koreanFontAsset is null. " + text.gameObject.name + " will show squares.");
        }
        try { text.textWrappingMode = TextWrappingModes.Normal; }
        catch { /* ignore wrapping mode errors */ }
    }

#if UNITY_EDITOR
    private void CreateDebugUI()
    {
        Canvas canvas = GetOrCreateHudCanvas();
        if (canvas == null) return;

        debugPanelObj = new GameObject("AIDebugPanel", typeof(RectTransform));
        debugPanelObj.transform.SetParent(canvas.transform, false);

        var rect = debugPanelObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f); // Top-left
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(20f, -20f);
        rect.sizeDelta = new Vector2(280f, 240f);

        var img = debugPanelObj.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.75f);

        var textGO = new GameObject("DebugText", typeof(RectTransform));
        textGO.transform.SetParent(debugPanelObj.transform, false);

        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 10f);
        textRect.offsetMax = new Vector2(-10f, -10f);

        debugText = textGO.AddComponent<TextMeshProUGUI>();
        debugText.fontSize = 11;
        debugText.color = Color.green;
        debugText.alignment = TextAlignmentOptions.TopLeft;

        ApplyKoreanFont(debugText);
        
        debugPanelObj.SetActive(false);
    }

    private void UpdateDebugUI()
    {
        if (debugPanelObj == null || !debugPanelObj.activeSelf) return;

        // Calculate FPS
        fpsAccumulator++;
        if (Time.realtimeSinceStartup > fpsNextPeriod)
        {
            currentFps = (int)(fpsAccumulator / fpsMeasurePeriod);
            fpsAccumulator = 0;
            fpsNextPeriod = Time.realtimeSinceStartup + fpsMeasurePeriod;
        }

        string regionStr = lastSelectedRegion != null ? $"Region #{lastSelectedRegion.id} ({lastSelectedRegion.colorName})" : "None";
        string orbStateStr = BreathingEmotionalOrb.Instance != null ? BreathingEmotionalOrb.Instance.CurrentState.ToString() : "N/A";
        string audioStr = audioSource != null && audioSource.isPlaying ? "Playing" : "Stopped";
        string fontStr = GetKoreanFontAsset() != null ? "Loaded" : "Missing";
        
        int loadedModelsCount = 0;
        foreach (var kv in regionMemories)
        {
            if (kv.Value.generationState == RegionGenerationState.Generated)
                loadedModelsCount++;
        }

        debugText.text = $"<b>[AI Debug Panel] (F3)</b>\n\n" +
                         $"FPS: {currentFps}\n" +
                         $"Current Region: {regionStr}\n" +
                         $"Orb State: {orbStateStr}\n" +
                         $"Audio Playing: {audioStr}\n" +
                         $"Conversation State: {conversationState}\n" +
                         $"Loaded Models: {loadedModelsCount}\n" +
                         $"Font Status: {fontStr}\n" +
                         $"Intro Started: {introStarted}";
    }
#endif
}

public enum RegionGenerationState
{
    NotStarted,
    Reflecting,
    Generating,
    Generated,
    ReadyForGeneration
}

public enum EmotionInteractionState
{
    Observe,
    Touch,
    Hold,
    Place
}
