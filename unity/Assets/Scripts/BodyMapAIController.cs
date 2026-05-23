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

    [Header("Siri Sphere Settings")]
    [SerializeField] private float siriIdlePulseSpeed = 2f;
    [SerializeField] private float siriIdlePulseHeight = 0.08f;
    [SerializeField] private float volumeSensitivity = 3f;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    private AudioClip micRecordClip;
    private int maxRecordTime = 20; // max 20 seconds recording

    // UI elements dynamically built
    private GameObject siriSphereObj;
    private GameObject siriLayerCyan;
    private GameObject siriLayerPurple;
    private GameObject siriLayerPink;
    private GameObject siriLayerCore;
    private TMPro.TextMeshProUGUI siriStatusText;
    private GameObject chatPanelObj;
    private Transform chatContentTrans;

    // State Tracking
    private bool isRecording = false;
    private float recordStartTime = 0f;
    private InteractiveRegion3D lastSelectedRegion = null;
    private BodyMapReceiver mapReceiver;

    // Hybrid V-Key and Cost Saving Variables
    private float keyPressTime = 0f;
    private bool isToggleMode = false;
    private float maxVolumeRecorded = 0f;

    // Conversation History per Region
    [System.Serializable]
    public struct MessageData
    {
        public string role;
        public string content;
    }
    private Dictionary<int, List<MessageData>> regionConversations = new Dictionary<int, List<MessageData>>();

    // Background GLB generation tasks
    private HashSet<int> generationTasksInProgress = new HashSet<int>();

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        mapReceiver = GetComponent<BodyMapReceiver>();
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Disable standard 3D tooltip text scaling/billboards if we want or just keep them
        LoadAPIKeys();
        CreateSiriSphere();
        CreateChatUI();
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        // Check if region selection changed to update UI panels
        UpdateActiveSelectionState();

        // ── Siri Idle Animation ──
        AnimateSiriSphere();

        // ── Track max volume while recording for silence/cost-saving threshold ──
        if (isRecording)
        {
            float currentVol = GetMicVolume();
            if (currentVol > maxVolumeRecorded)
            {
                maxVolumeRecorded = currentVol;
            }
        }

        // ── Push-To-Talk Keyboard Input (V Key) ──
        HandlePushToTalkInput();
    }

    private void UpdateActiveSelectionState()
    {
        if (mapReceiver == null) return;

        InteractiveRegion3D activeRegion = mapReceiver.selectedRegionNormal;
        if (activeRegion != lastSelectedRegion)
        {
            lastSelectedRegion = activeRegion;
            if (activeRegion != null)
            {
                // Show panels
                if (chatPanelObj != null) chatPanelObj.SetActive(true);
                UpdateChatDisplayForRegion(activeRegion.id);
                SetSiriStatus($"Press [V] to Toggle / Hold [V] to Talk about Region #{activeRegion.id} ({activeRegion.colorName})");
            }
            else
            {
                // Hide panels
                if (chatPanelObj != null) chatPanelObj.SetActive(false);
                SetSiriStatus("Select an Emotion Region to begin");
            }
        }
    }

    private void HandlePushToTalkInput()
    {
        if (lastSelectedRegion == null) return;
        if (generationTasksInProgress.Contains(lastSelectedRegion.id))
        {
            SetSiriStatus("3D generation in progress for this region...");
            return;
        }

        // V key pressed
        if (Input.GetKeyDown(KeyCode.V))
        {
            if (!isRecording)
            {
                // Start recording
                isToggleMode = false;
                keyPressTime = Time.time;
                StartVoiceRecording();
            }
            else
            {
                // We are recording, and user pressed V again.
                // This means they are in Toggle Mode and want to stop.
                if (isToggleMode)
                {
                    StopVoiceRecording();
                }
            }
        }

        // V key released
        if (Input.GetKeyUp(KeyCode.V) && isRecording)
        {
            float holdDuration = Time.time - keyPressTime;
            if (holdDuration > 0.35f)
            {
                // The key was held down and now released -> Push-To-Talk. Stop and send.
                isToggleMode = false;
                StopVoiceRecording();
            }
            else
            {
                // The key was just tapped -> enter Toggle Mode. Keep recording.
                isToggleMode = true;
                SetSiriStatus("Listening... (Press [V] again to send)");
            }
        }
    }

    private void StartVoiceRecording()
    {
        isRecording = true;
        recordStartTime = Time.time;
        maxVolumeRecorded = 0f; // reset volume tracker
        SetSiriStatus("Listening... (Hold [V] & release, or press [V] to stop)");

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();

        // Start Unity Microphone recording
        micRecordClip = Microphone.Start(null, false, maxRecordTime, 16000);
    }

    private void StopVoiceRecording()
    {
        isRecording = false;
        isToggleMode = false;
        int micPos = Microphone.GetPosition(null);
        Microphone.End(null);

        float recordDuration = Time.time - recordStartTime;
        if (recordDuration < 0.5f)
        {
            SetSiriStatus("Recording too short. Press [V] to talk.");
            return;
        }

        // Cost-saving: Silence check
        // If max volume is below 0.008 (basically background noise or silent), we abort!
        if (maxVolumeRecorded < 0.008f)
        {
            SetSiriStatus("No speech detected. Cancelled to save API cost.");
            AddChatMessage("System", "Speech cancelled (no voice input detected).", Color.yellow);
            return;
        }

        // Trim AudioClip to actual duration
        int totalSamples = Mathf.Min(micPos, micRecordClip.samples);
        if (totalSamples <= 0) return;

        SetSiriStatus("Thinking...");
        byte[] wavBytes = ConvertClipToWav(micRecordClip, totalSamples);

        // Run STT and Chat pipeline in Background
        StartCoroutine(SpeechToTextAndRespond(wavBytes, lastSelectedRegion));
    }

    private IEnumerator SpeechToTextAndRespond(byte[] wavBytes, InteractiveRegion3D region)
    {
        if (string.IsNullOrEmpty(openAiApiKey) || openAiApiKey.Contains("YOUR_"))
        {
            AddChatMessage("System", "Error: OpenAI API Key is missing or invalid. Check api_keys.json.", Color.red);
            SetSiriStatus("Error: Missing API Key");
            yield break;
        }

        // ── 1. Whisper Speech-To-Text API ──
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", wavBytes, "audio.wav", "audio/wav");
        form.AddField("model", "whisper-1");
        form.AddField("language", "ko");

        using (UnityWebRequest whisperReq = UnityWebRequest.Post("https://api.openai.com/v1/audio/transcriptions", form))
        {
            whisperReq.SetRequestHeader("Authorization", "Bearer " + openAiApiKey);
            yield return whisperReq.SendWebRequest();

            if (whisperReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Whisper API Error] {whisperReq.error}\nResponse: {whisperReq.downloadHandler.text}");
                AddChatMessage("System", "Speech transcription failed. Please try again.", Color.red);
                SetSiriStatus("Hold [V] to Talk");
                yield break;
            }

            OpenAIWhisperResponse whisperRes = JsonUtility.FromJson<OpenAIWhisperResponse>(whisperReq.downloadHandler.text);
            string userTranscript = whisperRes.text;

            if (string.IsNullOrWhiteSpace(userTranscript))
            {
                SetSiriStatus("No voice detected. Hold [V] to talk.");
                yield break;
            }

            // Display user text in chat log
            AddChatMessage("User", userTranscript, new Color(0.2f, 0.6f, 1f));

            // ── 2. OpenAI Chat Completions (GPT-4o-mini) ──
            if (!regionConversations.ContainsKey(region.id))
            {
                regionConversations[region.id] = CreateSystemPromptForRegion(region);
            }

            // Add user message to history
            var history = regionConversations[region.id];
            MessageData userMsg = new MessageData { role = "user", content = userTranscript };
            history.Add(userMsg);

            // Limit history size to save tokens (context window of last 8 messages)
            // history[0] is the system prompt. So if count > 9, we remove the oldest Q&A turn (indices 1 and 2)
            while (history.Count > 9)
            {
                history.RemoveAt(1);
                history.RemoveAt(1);
            }

            // Build request JSON manually
            string messagesJson = "";
            for (int i = 0; i < history.Count; i++)
            {
                messagesJson += $"{{\"role\":\"{history[i].role}\",\"content\":\"{EscapeJsonString(history[i].content)}\"}}";
                if (i < history.Count - 1) messagesJson += ",";
            }
            string jsonPayload = $"{{\"model\":\"gpt-4o-mini\",\"messages\":[{messagesJson}],\"temperature\":0.7}}";

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
                    Debug.LogError($"[GPT-4 Error] {chatReq.error}");
                    AddChatMessage("System", "AI failed to respond. Please try again.", Color.red);
                    SetSiriStatus("Hold [V] to Talk");
                    yield break;
                }

                OpenAIChatResponse chatRes = JsonUtility.FromJson<OpenAIChatResponse>(chatReq.downloadHandler.text);
                if (chatRes.choices == null || chatRes.choices.Length == 0)
                {
                    AddChatMessage("System", "Empty AI response.", Color.red);
                    SetSiriStatus("Hold [V] to Talk");
                    yield break;
                }

                string rawAiResponse = chatRes.choices[0].message.content;

                // Add AI message to history
                MessageData aiMsg = new MessageData { role = "assistant", content = rawAiResponse };
                history.Add(aiMsg);

                // Check if a 3D prompt is generated in the response [[3D Prompt: ...]]
                string cleanAiResponse = rawAiResponse;
                string tripoPrompt = "";
                int promptStart = rawAiResponse.IndexOf("[[3D Prompt:");
                if (promptStart != -1)
                {
                    int promptEnd = rawAiResponse.IndexOf("]]", promptStart);
                    if (promptEnd != -1)
                    {
                        tripoPrompt = rawAiResponse.Substring(promptStart + 12, promptEnd - (promptStart + 12)).Trim();
                        // Clean the response text by removing the tag for displaying in the chat log
                        cleanAiResponse = rawAiResponse.Remove(promptStart, (promptEnd + 2) - promptStart).Trim();
                    }
                }

                // Display AI text in chat log
                AddChatMessage("AI", cleanAiResponse, Color.white);

                // Run Text-To-Speech
                StartCoroutine(TextToSpeech(cleanAiResponse));

                // If GPT generated a 3D prompt, trigger the Tripo3D API
                if (!string.IsNullOrEmpty(tripoPrompt))
                {
                    StartCoroutine(Generate3DModelTask(tripoPrompt, region));
                }
            }
        }
    }

    private IEnumerator TextToSpeech(string text)
    {
        SetSiriStatus("AI Speaking...");

        // Limit length slightly to prevent massive downloads
        if (text.Length > 200) text = text.Substring(0, 200);

        string escapedText = EscapeJsonString(text);
        string jsonPayload = $"{{\"model\":\"tts-1\",\"input\":\"{escapedText}\",\"voice\":\"nova\",\"response_format\":\"wav\"}}";

        using (UnityWebRequest request = new UnityWebRequest("https://api.openai.com/v1/audio/speech", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerAudioClip("https://api.openai.com/v1/audio/speech", AudioType.WAV);
            request.SetRequestHeader("Authorization", "Bearer " + openAiApiKey);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                if (audioSource != null && clip != null)
                {
                    audioSource.clip = clip;
                    audioSource.Play();
                }
            }
            else
            {
                Debug.LogError("[TTS Error] Failed to generate TTS voice.");
            }
        }

        // Return to standard idle status once speaking completes
        if (lastSelectedRegion != null)
        {
            SetSiriStatus($"Hold [V] to Talk about Region #{lastSelectedRegion.id}");
        }
    }

    private IEnumerator Generate3DModelTask(string prompt, InteractiveRegion3D region)
    {
        if (string.IsNullOrEmpty(tripoApiKey) || tripoApiKey.Contains("YOUR_"))
        {
            AddChatMessage("System", "Error: Tripo3D API Key is missing. Check api_keys.json.", Color.red);
            yield break;
        }

        int regionId = region.id;
        generationTasksInProgress.Add(regionId);
        AddChatMessage("System", $"Generating 3D model in background for Prompt: \"{prompt}\"...", Color.yellow);
        mapReceiver.ShowNotice("3D 소환을 시작합니다...");

        // ── 1. Create Tripo3D Task ──
        string jsonPayload = $"{{\"type\":\"text_to_model\",\"prompt\":\"{EscapeJsonString(prompt)}\"}}";
        string taskId = "";

        using (UnityWebRequest request = new UnityWebRequest("https://api.tripo3d.ai/v2/openapi/task", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", "Bearer " + tripoApiKey);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Tripo Create Task Error] {request.error}\nResponse: {request.downloadHandler.text}");
                AddChatMessage("System", "Failed to initialize 3D model task with Tripo3D.", Color.red);
                generationTasksInProgress.Remove(regionId);
                yield break;
            }

            // Manually parse task_id from JSON (to avoid importing full JSON parsing packages)
            string resText = request.downloadHandler.text;
            int taskStart = resText.IndexOf("\"task_id\":\"");
            if (taskStart != -1)
            {
                int taskEnd = resText.IndexOf("\"", taskStart + 11);
                if (taskEnd != -1)
                {
                    taskId = resText.Substring(taskStart + 11, taskEnd - (taskStart + 11));
                }
            }
        }

        if (string.IsNullOrEmpty(taskId))
        {
            AddChatMessage("System", "Failed to retrieve Tripo3D Task ID from response.", Color.red);
            generationTasksInProgress.Remove(regionId);
            yield break;
        }

        // ── 2. Poll Status Endpoint ──
        bool isDone = false;
        string glbUrl = "";
        float timeout = 90f; // 90 seconds timeout
        float startTime = Time.time;

        while (!isDone && (Time.time - startTime) < timeout)
        {
            yield return new WaitForSeconds(3.5f); // check every 3.5 seconds

            using (UnityWebRequest pollReq = UnityWebRequest.Get($"https://api.tripo3d.ai/v2/openapi/task/{taskId}"))
            {
                pollReq.SetRequestHeader("Authorization", "Bearer " + tripoApiKey);
                yield return pollReq.SendWebRequest();

                if (pollReq.result == UnityWebRequest.Result.Success)
                {
                    string pollRes = pollReq.downloadHandler.text;
                    // Check status in JSON
                    if (pollRes.Contains("\"status\":\"success\""))
                    {
                        // Extract GLB model URL
                        int modelStart = pollRes.IndexOf("\"model\":\"");
                        if (modelStart != -1)
                        {
                            int modelEnd = pollRes.IndexOf("\"", modelStart + 9);
                            if (modelEnd != -1)
                            {
                                glbUrl = pollRes.Substring(modelStart + 9, modelEnd - (modelStart + 9));
                                glbUrl = glbUrl.Replace("\\/", "/"); // Unescape JSON slashes
                                isDone = true;
                            }
                        }
                    }
                    else if (pollRes.Contains("\"status\":\"failed\""))
                    {
                        Debug.LogError($"[Tripo Status] Task failed: {pollRes}");
                        AddChatMessage("System", "Tripo3D failed to generate the model.", Color.red);
                        isDone = true;
                    }
                    else
                    {
                        // Still running or queued
                        int progressStart = pollRes.IndexOf("\"progress\":");
                        int progress = 0;
                        if (progressStart != -1)
                        {
                            int progressEnd = pollRes.IndexOf(",", progressStart);
                            if (progressEnd == -1) progressEnd = pollRes.IndexOf("}", progressStart);
                            if (progressEnd != -1)
                            {
                                int.TryParse(pollRes.Substring(progressStart + 11, progressEnd - (progressStart + 11)).Trim(), out progress);
                            }
                        }
                        SetSiriStatus($"3D 소환 중... ({progress}%)");
                    }
                }
                else
                {
                    Debug.LogError($"[Tripo Poll Error] {pollReq.error}");
                }
            }
        }

        generationTasksInProgress.Remove(regionId);
        if (lastSelectedRegion != null && lastSelectedRegion.id == regionId)
        {
            SetSiriStatus($"Hold [V] to Talk about Region #{regionId}");
        }

        if (string.IsNullOrEmpty(glbUrl))
        {
            AddChatMessage("System", "3D model generation timed out or failed.", Color.red);
            yield break;
        }

        // ── 3. Load GLB file using GLTFast ──
        AddChatMessage("System", "Downloading and instantiating 3D model...", Color.yellow);
        
        GameObject glbHolder = new GameObject($"3D_Emotion_{regionId}");
        glbHolder.transform.SetParent(region.transform, false);
        glbHolder.transform.localPosition = Vector3.zero;
        glbHolder.transform.localScale = Vector3.one * 0.4f; // Scale down 3D model inside the scene

        var gltfAsset = glbHolder.AddComponent<GLTFast.GltfAsset>();
        gltfAsset.Url = glbUrl;

        // Hide the original 2D sprite
        var sr = region.GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;

        // Disable outline highlights
        region.SetTooltipActive(false);

        AddChatMessage("System", $"3D Model successfully loaded for Region #{regionId}!", Color.green);
        mapReceiver.ShowNotice($"Region #{regionId} 3D 소환 완료!");
    }

    private List<MessageData> CreateSystemPromptForRegion(InteractiveRegion3D region)
    {
        var history = new List<MessageData>();
        string sysPrompt = "You are an empathetic and professional Art Therapy conversation assistant. " +
            "The user is viewing their analyzed body map drawing as floating interactive 2D segments in a 3D environment. " +
            "They have clicked on a segment, and you are discussing it. " +
            $"This segment has ID: {region.id}, Color: {region.colorName}, Location: {region.bodyLocation}. " +
            "Guide them in discussing what emotion or feeling this segment represents. Ask warm questions like: " +
            "\"어떤 모양인가요?\", \"그릴 때 어떤 감정이었나요?\", \"이 색상은 몸의 그 부위에서 어떤 느낌으로 기억되나요?\". " +
            "Discuss in Korean naturally. Keep each response concise (2-3 sentences max). " +
            "CRITICAL INSTRUCTION: Once the user has described the shape and details of this emotion (usually after 3 or 4 dialogue turns), " +
            "you must synthesize their description into a detailed English prompt for a 3D AI generator. " +
            "Attach this prompt at the very end of your response, wrapped exactly like: [[3D Prompt: <English description of 3D object>]] " +
            "For example: '[[3D Prompt: a spiky black sphere with sharp red horns, abstract representation of rage, clean geometry, 3d asset]]'. " +
            "Keep the prompt description in English, as the 3D model generator works best with English.";

        history.Add(new MessageData { role = "system", content = sysPrompt });
        return history;
    }

    private void UpdateChatDisplayForRegion(int id)
    {
        // Clear old chat message GameObjects
        if (chatContentTrans != null)
        {
            foreach (Transform child in chatContentTrans)
            {
                Destroy(child.gameObject);
            }
        }

        if (regionConversations.ContainsKey(id))
        {
            var history = regionConversations[id];
            // Skip system prompt at index 0
            for (int i = 1; i < history.Count; i++)
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
        }
    }

    private void SetSiriStatus(string status)
    {
        if (siriStatusText != null)
        {
            siriStatusText.text = status;
        }
    }

    private void AnimateSiriSphere()
    {
        if (siriSphereObj == null || siriLayerCyan == null || siriLayerPurple == null || siriLayerPink == null || siriLayerCore == null) 
            return;

        // 1. Slow rotation for layers in opposite directions
        siriLayerCyan.transform.Rotate(0f, 0f, 25f * Time.deltaTime);
        siriLayerPurple.transform.Rotate(0f, 0f, -35f * Time.deltaTime);
        siriLayerPink.transform.Rotate(0f, 0f, 15f * Time.deltaTime);
        siriLayerCore.transform.Rotate(0f, 0f, -10f * Time.deltaTime);

        // 2. Slow liquid-like drifting (local offset) using trigonometry
        float driftSpeed = 1.0f;
        siriLayerCyan.GetComponent<RectTransform>().anchoredPosition = new Vector2(Mathf.Sin(Time.time * driftSpeed) * 3f, Mathf.Cos(Time.time * driftSpeed * 0.8f) * 3f);
        siriLayerPurple.GetComponent<RectTransform>().anchoredPosition = new Vector2(Mathf.Cos(Time.time * driftSpeed * 1.2f) * 4f, Mathf.Sin(Time.time * driftSpeed * 0.9f) * 4f);
        siriLayerPink.GetComponent<RectTransform>().anchoredPosition = new Vector2(Mathf.Sin(Time.time * driftSpeed * 0.7f) * -3f, Mathf.Cos(Time.time * driftSpeed * 1.1f) * -3f);
        siriLayerCore.GetComponent<RectTransform>().anchoredPosition = new Vector2(Mathf.Sin(Time.time * driftSpeed * 1.5f) * 1.5f, Mathf.Cos(Time.time * driftSpeed * 1.3f) * 1.5f);

        // Get images to control transparency/glow color
        Image imgCyan = siriLayerCyan.GetComponent<Image>();
        Image imgPurple = siriLayerPurple.GetComponent<Image>();
        Image imgPink = siriLayerPink.GetComponent<Image>();
        Image imgCore = siriLayerCore.GetComponent<Image>();

        if (isRecording)
        {
            // ── Listening State (glowing green/cyan responding to voice) ──
            float rms = GetMicVolume();
            float baseScale = 1.0f + rms * volumeSensitivity;
            baseScale = Mathf.Clamp(baseScale, 0.8f, 2.2f);

            // Layers react with different frequencies and scales
            siriLayerCyan.transform.localScale = Vector3.one * baseScale * (1.0f + Mathf.Sin(Time.time * 15f) * 0.08f);
            siriLayerPurple.transform.localScale = Vector3.one * baseScale * (0.9f + Mathf.Cos(Time.time * 18f) * 0.12f);
            siriLayerPink.transform.localScale = Vector3.one * baseScale * (0.8f + Mathf.Sin(Time.time * 12f) * 0.1f);
            siriLayerCore.transform.localScale = Vector3.one * baseScale * (0.6f + Mathf.Cos(Time.time * 25f) * 0.05f);

            // Glow shifts to active cyan/green/yellow spectrum
            imgCyan.color = new Color(0f, 0.9f, 0.7f, 0.7f);
            imgPurple.color = new Color(0.1f, 0.8f, 0.4f, 0.6f);
            imgPink.color = new Color(0.7f, 0.9f, 0.1f, 0.5f);
            imgCore.color = new Color(0.9f, 1f, 0.95f, 0.9f);
        }
        else if (generationTasksInProgress.Contains(lastSelectedRegion != null ? lastSelectedRegion.id : -1))
        {
            // ── Generating 3D state (swirling gold/orange) ──
            float baseScale = 1.0f + Mathf.Sin(Time.time * 6f) * 0.12f;

            siriLayerCyan.transform.localScale = Vector3.one * baseScale * 1.0f;
            siriLayerPurple.transform.localScale = Vector3.one * baseScale * 0.9f;
            siriLayerPink.transform.localScale = Vector3.one * baseScale * 0.8f;
            siriLayerCore.transform.localScale = Vector3.one * baseScale * 0.5f;

            imgCyan.color = new Color(1f, 0.8f, 0f, 0.6f);
            imgPurple.color = new Color(1f, 0.5f, 0f, 0.5f);
            imgPink.color = new Color(1f, 0.3f, 0.1f, 0.4f);
            imgCore.color = new Color(1f, 0.95f, 0.7f, 0.85f);
        }
        else if (audioSource != null && audioSource.isPlaying && siriStatusText.text.Contains("Speaking"))
        {
            // ── AI Speaking state (pulses to output speaker volume) ──
            float[] samples = new float[64];
            audioSource.GetOutputData(samples, 0);
            float sum = 0f;
            for (int i = 0; i < samples.Length; i++) sum += samples[i] * samples[i];
            float rms = Mathf.Sqrt(sum / samples.Length);

            float baseScale = 1.0f + rms * 3.5f;
            baseScale = Mathf.Clamp(baseScale, 0.9f, 1.8f);

            siriLayerCyan.transform.localScale = Vector3.one * baseScale * (1.0f + Mathf.Sin(Time.time * 8f) * 0.05f);
            siriLayerPurple.transform.localScale = Vector3.one * baseScale * (0.9f + Mathf.Cos(Time.time * 10f) * 0.08f);
            siriLayerPink.transform.localScale = Vector3.one * baseScale * (0.8f + Mathf.Sin(Time.time * 7f) * 0.06f);
            siriLayerCore.transform.localScale = Vector3.one * baseScale * (0.5f + Mathf.Cos(Time.time * 12f) * 0.04f);

            // Rich royal colors (blue, magenta, deep purple)
            imgCyan.color = new Color(0.2f, 0.3f, 0.9f, 0.6f);
            imgPurple.color = new Color(0.8f, 0.1f, 0.8f, 0.5f);
            imgPink.color = new Color(0.9f, 0.1f, 0.4f, 0.4f);
            imgCore.color = new Color(0.95f, 0.85f, 1f, 0.85f);
        }
        else if (lastSelectedRegion != null)
        {
            // ── Idle breathing (region selected, ready to talk) ──
            siriLayerCyan.transform.localScale = Vector3.one * (1.0f + Mathf.Sin(Time.time * siriIdlePulseSpeed) * siriIdlePulseHeight);
            siriLayerPurple.transform.localScale = Vector3.one * (0.9f + Mathf.Sin(Time.time * siriIdlePulseSpeed * 1.2f + 1f) * siriIdlePulseHeight * 1.2f);
            siriLayerPink.transform.localScale = Vector3.one * (0.8f + Mathf.Sin(Time.time * siriIdlePulseSpeed * 0.8f + 2f) * siriIdlePulseHeight * 0.9f);
            siriLayerCore.transform.localScale = Vector3.one * (0.5f + Mathf.Sin(Time.time * siriIdlePulseSpeed * 1.5f + 0.5f) * siriIdlePulseHeight * 0.5f);

            imgCyan.color = new Color(0f, 0.5f, 1f, 0.45f);
            imgPurple.color = new Color(0.5f, 0f, 0.9f, 0.35f);
            imgPink.color = new Color(0.9f, 0f, 0.5f, 0.3f);
            imgCore.color = new Color(0.9f, 0.95f, 1f, 0.7f);
        }
        else
        {
            // ── Completely Idle (no active region selection) ──
            siriLayerCyan.transform.localScale = Vector3.one * (0.6f + Mathf.Sin(Time.time * 0.8f) * 0.02f);
            siriLayerPurple.transform.localScale = Vector3.one * (0.5f + Mathf.Sin(Time.time * 1.0f) * 0.01f);
            siriLayerPink.transform.localScale = Vector3.one * (0.4f + Mathf.Sin(Time.time * 0.6f) * 0.01f);
            siriLayerCore.transform.localScale = Vector3.one * (0.25f + Mathf.Sin(Time.time * 1.2f) * 0.005f);

            imgCyan.color = new Color(1f, 1f, 1f, 0.15f);
            imgPurple.color = new Color(1f, 1f, 1f, 0.12f);
            imgPink.color = new Color(1f, 1f, 1f, 0.1f);
            imgCore.color = new Color(1f, 1f, 1f, 0.3f);
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

    private void CreateSiriSphere()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        siriSphereObj = new GameObject("SiriSphere");
        siriSphereObj.transform.SetParent(canvas.transform, false);

        var rect = siriSphereObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 40f);
        rect.sizeDelta = new Vector2(90f, 90f);

        Sprite knob = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");

        siriLayerCyan = CreateSphereLayer("SiriLayer_Cyan", siriSphereObj.transform, knob, new Color(0f, 0.7f, 1f, 0.5f), 1.0f);
        siriLayerPurple = CreateSphereLayer("SiriLayer_Purple", siriSphereObj.transform, knob, new Color(0.6f, 0.1f, 0.9f, 0.4f), 0.9f);
        siriLayerPink = CreateSphereLayer("SiriLayer_Pink", siriSphereObj.transform, knob, new Color(1f, 0.1f, 0.5f, 0.35f), 0.8f);
        siriLayerCore = CreateSphereLayer("SiriLayer_Core", siriSphereObj.transform, knob, new Color(0.9f, 0.95f, 1f, 0.8f), 0.5f);

        // Status text
        GameObject statusGO = new GameObject("SiriStatusText");
        statusGO.transform.SetParent(siriSphereObj.transform, false);
        siriStatusText = statusGO.AddComponent<TextMeshProUGUI>();
        siriStatusText.text = "Select an Emotion Region to begin";
        siriStatusText.fontSize = 13;
        siriStatusText.color = Color.white;
        siriStatusText.alignment = TextAlignmentOptions.Center;

        var textOutline = statusGO.AddComponent<Outline>();
        textOutline.effectColor = Color.black;
        textOutline.effectDistance = new Vector2(1f, -1f);

        var statusRect = statusGO.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 1f);
        statusRect.anchorMax = new Vector2(0.5f, 1f);
        statusRect.pivot = new Vector2(0.5f, 0f);
        statusRect.anchoredPosition = new Vector2(0f, 15f);
        statusRect.sizeDelta = new Vector2(400f, 30f);

        siriSphereObj.SetActive(true);
    }

    private GameObject CreateSphereLayer(string name, Transform parent, Sprite sprite, Color color, float initialScale)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(90f, 90f);
        go.transform.localScale = Vector3.one * initialScale;

        var img = go.AddComponent<Image>();
        img.color = color;
        if (sprite != null) img.sprite = sprite;

        return go;
    }

    private void CreateChatUI()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        chatPanelObj = new GameObject("AIChatPanel");
        chatPanelObj.transform.SetParent(canvas.transform, false);

        var panelRect = chatPanelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0.5f);
        panelRect.anchorMax = new Vector2(1f, 0.5f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.anchoredPosition = new Vector2(-20f, 0f);
        panelRect.sizeDelta = new Vector2(300f, 420f);

        var panelImg = chatPanelObj.AddComponent<Image>();
        panelImg.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);

        var outline = chatPanelObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.15f, 0.15f, 0.25f, 0.5f);
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject scrollGO = new GameObject("ScrollRect");
        scrollGO.transform.SetParent(chatPanelObj.transform, false);
        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        var scrollRectTransform = scrollGO.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.sizeDelta = new Vector2(-16f, -50f);
        scrollRectTransform.anchoredPosition = new Vector2(-4f, -10f);

        GameObject viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollGO.transform, false);
        var mask = viewportGO.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        var viewportImg = viewportGO.AddComponent<Image>();
        var viewportRect = viewportGO.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportRect.anchoredPosition = Vector2.zero;

        GameObject contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform, false);
        var contentRect = contentGO.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 0f);
        contentRect.anchoredPosition = Vector2.zero;

        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.padding = new RectOffset(8, 8, 8, 8);

        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        chatContentTrans = contentRect;

        GameObject titleGO = new GameObject("ChatTitle");
        titleGO.transform.SetParent(chatPanelObj.transform, false);
        var titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.text = "💬 AI Emotion Chat Log";
        titleText.fontSize = 14;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;
        
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(10f, -8f);
        titleRect.sizeDelta = new Vector2(-20f, 25f);

        chatPanelObj.SetActive(false);
    }

    private void AddChatMessage(string sender, string message, Color color)
    {
        if (chatContentTrans == null) return;

        GameObject msgGO = new GameObject("Message");
        msgGO.transform.SetParent(chatContentTrans, false);

        var text = msgGO.AddComponent<TextMeshProUGUI>();
        text.text = $"<b>{sender}:</b> {message}";
        text.fontSize = 12;
        text.color = color;
        text.enableWordWrapping = true;

        Canvas.ForceUpdateCanvases();
        var scrollRect = chatPanelObj.GetComponentInChildren<ScrollRect>();
        if (scrollRect != null)
        {
            scrollRect.normalizedPosition = new Vector2(0f, 0f);
        }
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
            Debug.LogError($"[BodyMapAI] api_keys.json not found! Template created at {path}. Please fill API keys.");
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
}
