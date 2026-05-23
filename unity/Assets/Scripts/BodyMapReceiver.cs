using UnityEngine;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

[ExecuteAlways]
public class BodyMapReceiver : MonoBehaviour
{
    [Header("Server Settings")]
    public int port = 8200;

    [Header("Visuals Placement")]
    public SpriteRenderer avatarSpriteRenderer;
    public float pixelsPerUnit = 100f;

    [Header("UI References")]
    public TMPro.TextMeshProUGUI detailText; // Bottom-left region details
    public TMPro.TextMeshProUGUI statusText; // Bottom-right status text
    public GameObject instructionPanel;      // Center instruction card
    public GameObject noticePanel;           // Bottom-right notice panel
    public TMPro.TextMeshProUGUI noticeText; // Bottom-right notice text
    public GameObject detailPanel;           // Bottom-left details panel

    private StarterAssets.StarterAssetsInputs playerInputs;
    private Coroutine noticeCoroutine;

    // HTTP server thread variables
    private HttpListener listener;
    private Thread serverThread;
    private bool isRunning = false;

    // Thread-safe queue for incoming requests
    private struct UploadRequest
    {
        public string base64Data;
        public string jsonRegions;
    }
    private Queue<UploadRequest> requestQueue = new Queue<UploadRequest>();
    private object queueLock = new object();

    // Model classes for parsing the JSON body
    [System.Serializable]
    public class BoundingBoxData
    {
        public int x;
        public int y;
        public int width;
        public int height;
    }

    [System.Serializable]
    public class CenterData
    {
        public float x;
        public float y;
    }

    [System.Serializable]
    public class RegionData
    {
        public int id;
        public string color_name;
        public string color_hex;
        public string body_region;
        public string body_side;
        public string body_location;
        public string emotion_label;
        public string visual_shape;
        public string description;
        public BoundingBoxData bounding_box;
        public CenterData center;
        public CenterData center_normalized;
        public int pixel_count;
        public float area_ratio;
        public string image_base64; // base64 sprite data from web client
    }

    [System.Serializable]
    public class BodyMapPayload
    {
        public string imageBase64;
        public RegionData[] regions;
    }

    private List<GameObject> instantiatedShapes = new List<GameObject>();
    private InteractiveRegion3D currentlyHoveredRegion = null;

    // ── Edit Mode State ──
    private bool editMode = false;
    private List<InteractiveRegion3D> selectedRegions = new List<InteractiveRegion3D>();
    private GameObject editModePanel;
    private TMPro.TextMeshProUGUI editModeText;
    private GameObject aimDotObj;

    void Start()
    {
        FixFloorMaterial();

        if (!Application.isPlaying) return;

        // Find player inputs and unlock cursor initially for uploader button interaction
        playerInputs = FindObjectOfType<StarterAssets.StarterAssetsInputs>();
        if (playerInputs != null)
        {
            playerInputs.cursorLocked = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (statusText != null)
        {
            statusText.text = "body mapping analysis: waiting for scan";
        }

        if (instructionPanel != null)
        {
            instructionPanel.SetActive(true);
        }

        if (noticePanel != null)
        {
            noticePanel.SetActive(false);
        }

        if (detailPanel != null)
        {
            detailPanel.SetActive(false);
        }

        if (avatarSpriteRenderer == null)
        {
            GameObject g = GameObject.Find("BodyMap_Transparent");
            if (g != null)
            {
                avatarSpriteRenderer = g.GetComponent<SpriteRenderer>();
            }
            else
            {
                g = new GameObject("BodyMap_Transparent");
                g.transform.position = new Vector3(0f, 0.2f, 0f);
                avatarSpriteRenderer = g.AddComponent<SpriteRenderer>();
            }
        }

        CreateEditModeUI();
        CreateAimDotUI();
        gameObject.AddComponent<BodyMapAIController>();
        StartServer();
    }

    void OnDestroy()
    {
        StopServer();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Delayed call ensures scene object manipulation doesn't cause warning in OnValidate
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) FixFloorMaterial();
        };
    }
#endif

    // ── Fix Floor Material (prevent pink shader error) ──
    private void FixFloorMaterial()
    {
        // Disabled per user request: keep the inspector-assigned material
    }

    private void CreateAimDotUI()
    {
        // Find Canvas
        UnityEngine.Canvas canvas = FindObjectOfType<UnityEngine.Canvas>();
        if (canvas == null) return;

        // Check if already exists
        aimDotObj = GameObject.Find("AimDot");
        if (aimDotObj != null)
        {
            if (Application.isPlaying)
                Destroy(aimDotObj);
            else
                DestroyImmediate(aimDotObj);
        }

        aimDotObj = new GameObject("AimDot");
        aimDotObj.transform.SetParent(canvas.transform, false);

        var rect = aimDotObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(6f, 6f); // Small dot

        var img = aimDotObj.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(1f, 1f, 1f, 0.9f); // Semi-transparent white dot

        // Add a very small black outline/shadow effect to make it visible on white backgrounds
        var outline = aimDotObj.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        outline.effectDistance = new Vector2(1f, -1f);

        // Try to load default Knob sprite for circular shape
        Sprite knob = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
        if (knob != null)
        {
            img.sprite = knob;
        }

        aimDotObj.SetActive(true);
    }

    // ── Edit Mode UI ──
    private void CreateEditModeUI()
    {
        // Find or create the Canvas
        UnityEngine.Canvas canvas = FindObjectOfType<UnityEngine.Canvas>();
        if (canvas == null) return;

        editModePanel = new GameObject("EditModePanel");
        editModePanel.transform.SetParent(canvas.transform, false);
        var panelImg = editModePanel.AddComponent<UnityEngine.UI.Image>();
        panelImg.color = new Color(0.1f, 0.1f, 0.3f, 0.9f);

        var panelRect = editModePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -10f);
        panelRect.sizeDelta = new Vector2(700f, 45f);

        var textGO = new GameObject("EditModeText");
        textGO.transform.SetParent(editModePanel.transform, false);
        editModeText = textGO.AddComponent<TMPro.TextMeshProUGUI>();
        editModeText.fontSize = 14;
        editModeText.color = Color.white;
        editModeText.alignment = TMPro.TextAlignmentOptions.Center;
        editModeText.text = "✏️ EDIT MODE  |  Click: Select  |  M: Merge  |  Del: Delete  |  Tab: Exit";

        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 5f);
        textRect.offsetMax = new Vector2(-10f, -5f);

        editModePanel.SetActive(false);
    }

    void StartServer()
    {
        listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        
        try
        {
            listener.Prefixes.Add($"http://*:{port}/");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BodyMapReceiver] Could not add wildcard prefix (requires admin rights): {ex.Message}");
        }

        try
        {
            isRunning = true;
            listener.Start();

            serverThread = new Thread(ListenLoop);
            serverThread.IsBackground = true;
            serverThread.Start();

            Debug.Log($"[BodyMapReceiver] Local HTTP server started successfully on http://localhost:{port}/");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BodyMapReceiver] Failed to start HTTP listener: {ex.Message}");
            if (listener.Prefixes.Count > 2)
            {
                Debug.Log("[BodyMapReceiver] Attempting fallback to localhost-only binding...");
                try
                {
                    listener.Close();
                    listener = new HttpListener();
                    listener.Prefixes.Add($"http://localhost:{port}/");
                    listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                    
                    listener.Start();
                    
                    serverThread = new Thread(ListenLoop);
                    serverThread.IsBackground = true;
                    serverThread.Start();
                    
                    Debug.Log($"[BodyMapReceiver] Local HTTP server started on http://localhost:{port}/ (localhost fallback)");
                }
                catch (Exception fallbackEx)
                {
                    Debug.LogError($"[BodyMapReceiver] Fallback listener failed: {fallbackEx.Message}");
                }
            }
        }
    }

    void StopServer()
    {
        isRunning = false;
        if (listener != null)
        {
            listener.Close();
            listener = null;
        }

        if (serverThread != null)
        {
            serverThread.Join();
            serverThread = null;
        }
        Debug.Log("[BodyMapReceiver] Local HTTP server stopped.");
    }

    private void ListenLoop()
    {
        while (isRunning)
        {
            HttpListenerContext context = null;
            try
            {
                context = listener.GetContext();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
                response.Headers.Add("Access-Control-Max-Age", "1728000");

                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    continue;
                }

                if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/ping")
                {
                    byte[] buffer = Encoding.UTF8.GetBytes("pong");
                    response.StatusCode = 200;
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                    response.Close();
                    continue;
                }

                if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/api_keys")
                {
                    string keysJson = "{}";
                    try
                    {
                        string path = Path.Combine(Application.dataPath, "../api_keys.json");
                        if (File.Exists(path))
                        {
                            keysJson = File.ReadAllText(path);
                        }
                        else
                        {
                            Debug.LogWarning($"[BodyMapReceiver] api_keys.json not found at {path}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[BodyMapReceiver] Error reading api_keys.json: {ex.Message}");
                    }

                    byte[] buffer = Encoding.UTF8.GetBytes(keysJson);
                    response.StatusCode = 200;
                    response.ContentType = "application/json";
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                    response.Close();
                    continue;
                }

                if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/upload")
                {
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        string body = reader.ReadToEnd();
                        
                        lock (queueLock)
                        {
                            requestQueue.Enqueue(new UploadRequest
                            {
                                base64Data = body,
                                jsonRegions = body
                            });
                        }
                    }

                    byte[] buffer = Encoding.UTF8.GetBytes("OK");
                    response.StatusCode = 200;
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                    response.Close();
                    continue;
                }

                response.StatusCode = 404;
                response.Close();
            }
            catch (Exception ex)
            {
                if (!isRunning) break;
                Debug.LogError($"[BodyMapReceiver] Server exception: {ex.ToString()}");
                if (context != null && context.Response != null)
                {
                    try
                    {
                        context.Response.Close();
                    }
                    catch {}
                }
            }
        }
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        UploadRequest reqToProcess = new UploadRequest();
        bool hasRequest = false;

        lock (queueLock)
        {
            if (requestQueue.Count > 0)
            {
                reqToProcess = requestQueue.Dequeue();
                hasRequest = true;
            }
        }

        if (hasRequest)
        {
            ProcessData(reqToProcess.jsonRegions);
        }

        // ── Aim Dot Visibility Control ──
        if (aimDotObj != null)
        {
            aimDotObj.SetActive(!editMode && Cursor.lockState == CursorLockMode.Locked);
        }

        // ── Edit Mode Toggle ──
        if (Input.GetKeyDown(KeyCode.Tab) && instantiatedShapes.Count > 0)
        {
            ToggleEditMode();
        }

        if (editMode)
        {
            HandleEditModeInput();
        }
        else
        {
            HandleRaycasting();
        }
    }

    // ══════════════════════════════════════════════
    //  EDIT MODE
    // ══════════════════════════════════════════════

    private void ToggleEditMode()
    {
        editMode = !editMode;

        if (editMode)
        {
            // Enter edit mode: unlock cursor, show edit UI
            if (playerInputs != null)
            {
                playerInputs.cursorLocked = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (editModePanel != null) editModePanel.SetActive(true);
            if (statusText != null) statusText.text = "✏️ edit mode active";
            if (aimDotObj != null) aimDotObj.SetActive(false);
            ShowNotice("Edit mode: click regions to select, M to merge, Del to delete");
        }
        else
        {
            // Exit edit mode: lock cursor, hide edit UI, deselect all
            DeselectAll();

            if (playerInputs != null)
            {
                playerInputs.cursorLocked = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (editModePanel != null) editModePanel.SetActive(false);
            if (statusText != null) statusText.text = "body mapping analysis: scan active";
            if (aimDotObj != null) aimDotObj.SetActive(true);
        }
    }

    private void HandleEditModeInput()
    {
        if (Camera.main == null) return;

        // Mouse click to select/deselect regions
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f))
            {
                InteractiveRegion3D region = hit.collider.GetComponent<InteractiveRegion3D>();
                if (region != null)
                {
                    region.ToggleSelected();
                    if (region.IsSelected)
                    {
                        if (!selectedRegions.Contains(region))
                            selectedRegions.Add(region);
                    }
                    else
                    {
                        selectedRegions.Remove(region);
                    }
                    UpdateEditModeStatus();
                }
            }
        }

        // M = Merge selected regions
        if (Input.GetKeyDown(KeyCode.M) && selectedRegions.Count >= 2)
        {
            MergeSelectedRegions();
        }

        // Delete / Backspace = Delete selected regions
        if ((Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)) && selectedRegions.Count > 0)
        {
            DeleteSelectedRegions();
        }

        // Hover preview in edit mode
        HandleEditModeHover();
    }

    private void HandleEditModeHover()
    {
        if (Camera.main == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        InteractiveRegion3D hitRegion = null;

        if (Physics.Raycast(ray, out hit, 100f))
        {
            hitRegion = hit.collider.GetComponent<InteractiveRegion3D>();
        }

        if (hitRegion != currentlyHoveredRegion)
        {
            if (currentlyHoveredRegion != null)
                currentlyHoveredRegion.OnHoverExit();

            currentlyHoveredRegion = hitRegion;

            if (currentlyHoveredRegion != null)
                currentlyHoveredRegion.OnHoverEnter();
        }
    }

    private void UpdateEditModeStatus()
    {
        if (editModeText != null)
        {
            int count = selectedRegions.Count;
            string names = string.Join(", ", selectedRegions.Select(r => $"#{r.id}({r.colorName})"));
            editModeText.text = count > 0
                ? $"✏️ Selected: {names}  |  M: Merge  |  Del: Delete  |  Tab: Exit"
                : "✏️ EDIT MODE  |  Click: Select  |  M: Merge  |  Del: Delete  |  Tab: Exit";
        }
    }

    private void DeselectAll()
    {
        foreach (var region in selectedRegions)
        {
            if (region != null) region.SetSelected(false);
        }
        selectedRegions.Clear();
    }

    private void MergeSelectedRegions()
    {
        if (selectedRegions.Count < 2) return;

        // Keep the first selected region as the merge target
        InteractiveRegion3D primary = selectedRegions[0];
        SpriteRenderer primarySR = primary.GetComponent<SpriteRenderer>();
        if (primarySR == null || primarySR.sprite == null) return;

        // Gather all sprites to merge
        List<SpriteRenderer> toMerge = new List<SpriteRenderer>();
        for (int i = 1; i < selectedRegions.Count; i++)
        {
            SpriteRenderer sr = selectedRegions[i].GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
                toMerge.Add(sr);
        }

        // Calculate combined bounding box in parent-local space
        Bounds combinedBounds = primarySR.bounds;
        foreach (var sr in toMerge)
        {
            combinedBounds.Encapsulate(sr.bounds);
        }

        // Create a merged texture by compositing all region sprites
        int mergedWidth = Mathf.CeilToInt(combinedBounds.size.x * pixelsPerUnit);
        int mergedHeight = Mathf.CeilToInt(combinedBounds.size.y * pixelsPerUnit);
        mergedWidth = Mathf.Max(mergedWidth, 4);
        mergedHeight = Mathf.Max(mergedHeight, 4);

        Texture2D mergedTex = new Texture2D(mergedWidth, mergedHeight, TextureFormat.RGBA32, false);
        Color[] clearPixels = new Color[mergedWidth * mergedHeight];
        mergedTex.SetPixels(clearPixels); // transparent

        // Composite primary
        CompositeSprite(mergedTex, primarySR, combinedBounds);

        // Composite others
        foreach (var sr in toMerge)
        {
            CompositeSprite(mergedTex, sr, combinedBounds);
        }

        mergedTex.Apply();
        mergedTex.filterMode = FilterMode.Bilinear;

        // Create new sprite
        Sprite mergedSprite = Sprite.Create(mergedTex,
            new Rect(0, 0, mergedWidth, mergedHeight),
            new Vector2(0.5f, 0.5f), pixelsPerUnit);

        // Update primary region
        primarySR.sprite = mergedSprite;
        primary.transform.position = combinedBounds.center;

        // Update collider
        BoxCollider bc = primary.GetComponent<BoxCollider>();
        if (bc != null)
        {
            bc.size = new Vector3(mergedSprite.bounds.size.x, mergedSprite.bounds.size.y, 0.1f);
        }

        // Aggregate pixel count
        int totalPixels = primary.pixelCount;
        string mergedColors = primary.colorName;

        // Destroy merged regions
        for (int i = 1; i < selectedRegions.Count; i++)
        {
            InteractiveRegion3D region = selectedRegions[i];
            if (selectedRegionNormal == region)
            {
                selectedRegionNormal = null;
            }
            totalPixels += region.pixelCount;
            if (!mergedColors.Contains(region.colorName))
                mergedColors += "+" + region.colorName;

            instantiatedShapes.Remove(region.gameObject);
            Destroy(region.gameObject);
        }

        primary.pixelCount = totalPixels;
        primary.colorName = mergedColors;
        primary.SetSelected(false);

        selectedRegions.Clear();
        UpdateEditModeStatus();

        ShowNotice($"Merged {toMerge.Count + 1} regions into Region #{primary.id}");
        Debug.Log($"[EditMode] Merged into Region #{primary.id}: {mergedColors}");
    }

    private void CompositeSprite(Texture2D target, SpriteRenderer sr, Bounds combinedBounds)
    {
        Texture2D srcTex = sr.sprite.texture;
        Rect srcRect = sr.sprite.textureRect;
        Vector3 srcCenter = sr.bounds.center;
        Vector3 srcMin = sr.bounds.min;

        int targetW = target.width;
        int targetH = target.height;

        for (int sy = 0; sy < (int)srcRect.height; sy++)
        {
            for (int sx = 0; sx < (int)srcRect.width; sx++)
            {
                Color srcPixel = srcTex.GetPixel((int)srcRect.x + sx, (int)srcRect.y + sy);
                if (srcPixel.a < 0.01f) continue;

                // Map source pixel to world position
                float worldX = srcMin.x + (sx / pixelsPerUnit);
                float worldY = srcMin.y + (sy / pixelsPerUnit);

                // Map to target texture coordinates
                int tx = Mathf.RoundToInt((worldX - combinedBounds.min.x) * pixelsPerUnit);
                int ty = Mathf.RoundToInt((worldY - combinedBounds.min.y) * pixelsPerUnit);

                if (tx >= 0 && tx < targetW && ty >= 0 && ty < targetH)
                {
                    Color existing = target.GetPixel(tx, ty);
                    // Alpha-blend
                    float outA = srcPixel.a + existing.a * (1f - srcPixel.a);
                    if (outA > 0)
                    {
                        Color blended = new Color(
                            (srcPixel.r * srcPixel.a + existing.r * existing.a * (1f - srcPixel.a)) / outA,
                            (srcPixel.g * srcPixel.a + existing.g * existing.a * (1f - srcPixel.a)) / outA,
                            (srcPixel.b * srcPixel.a + existing.b * existing.a * (1f - srcPixel.a)) / outA,
                            outA
                        );
                        target.SetPixel(tx, ty, blended);
                    }
                }
            }
        }
    }

    private void DeleteSelectedRegions()
    {
        int count = selectedRegions.Count;
        foreach (var region in selectedRegions)
        {
            if (region != null)
            {
                if (selectedRegionNormal == region)
                {
                    selectedRegionNormal = null;
                }
                instantiatedShapes.Remove(region.gameObject);
                Destroy(region.gameObject);
            }
        }
        selectedRegions.Clear();
        UpdateEditModeStatus();
        ShowNotice($"Deleted {count} region(s)");
        Debug.Log($"[EditMode] Deleted {count} region(s)");
    }

    // ══════════════════════════════════════════════
    //  RAYCASTING (Normal Mode)
    // ══════════════════════════════════════════════

    private void HandleRaycasting()
    {
        if (Camera.main == null) return;

        // Perform raycast from center of screen if cursor is locked (FPS gameplay style)
        // Otherwise raycast from mouse position (for editor convenience)
        Ray ray;
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }
        else
        {
            ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        }

        RaycastHit hit;
        InteractiveRegion3D hitRegion = null;

        if (Physics.Raycast(ray, out hit, 100f))
        {
            hitRegion = hit.collider.GetComponent<InteractiveRegion3D>();
        }

        // Hover enter/exit states
        if (hitRegion != currentlyHoveredRegion)
        {
            if (currentlyHoveredRegion != null)
            {
                currentlyHoveredRegion.OnHoverExit();
            }

            currentlyHoveredRegion = hitRegion;

            if (currentlyHoveredRegion != null)
            {
                currentlyHoveredRegion.OnHoverEnter();
            }
        }

        // Select on left click or press 'E'
        if (currentlyHoveredRegion != null && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E)))
        {
            currentlyHoveredRegion.OnSelect();
        }
        else if (currentlyHoveredRegion == null && Input.GetMouseButtonDown(0))
        {
            // Clicked background, clear normal selection
            DeselectActiveRegion();
        }
    }

    // ══════════════════════════════════════════════
    //  DATA PROCESSING
    // ══════════════════════════════════════════════

    private void ProcessData(string json)
    {
        try
        {
            BodyMapPayload payload = JsonUtility.FromJson<BodyMapPayload>(json);
            if (payload == null || string.IsNullOrEmpty(payload.imageBase64))
            {
                Debug.LogWarning("[BodyMapReceiver] Received payload is empty or invalid.");
                return;
            }

            Debug.Log($"[BodyMapReceiver] Successfully received scan! Regions: {payload.regions?.Length ?? 0}");

            // Exit edit mode if active
            if (editMode) ToggleEditMode();

            // 1. Convert Base64 back into Texture2D for main avatar silhouette
            byte[] imageBytes = Convert.FromBase64String(payload.imageBase64);
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(imageBytes);
            texture.filterMode = FilterMode.Bilinear;

            // 2. Calculate pixelsPerUnit dynamically to ensure the height is exactly 2.7 meters (1.5 times player height)
            pixelsPerUnit = texture.height / 2.7f;

            // 3. Create sprite and apply to renderer
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            avatarSpriteRenderer.sprite = sprite;

            // Clear previous shapes
            ClearInstantiatedShapes();

            // Calculate physical dimensions in Unity units
            float spriteWidth = texture.width / pixelsPerUnit;
            float spriteHeight = texture.height / pixelsPerUnit;

            // Position the avatar sprite so its bottom sits exactly on the floor plane (y = -1.8f)
            avatarSpriteRenderer.transform.position = new Vector3(0f, -1.8f + (spriteHeight / 2f), 0f);

            // 4. Instantiate floating sprite regions
            if (payload.regions != null)
            {
                foreach (var region in payload.regions)
                {
                    // Map normalized coordinates [0, 1] to local position
                    // Web: (0,0) is top-left, (1,1) is bottom-right
                    // Unity: local (0,0) is center of sprite
                    float localX = (region.center_normalized.x - 0.5f) * spriteWidth;
                    float localY = ((1.0f - region.center_normalized.y) - 0.5f) * spriteHeight;

                    // Place the region sprite slightly in front of the avatar (z = -0.08f) for subtle float
                    Vector3 localPos = new Vector3(localX, localY, -0.08f);

                    GameObject marker = new GameObject($"Region_{region.id}");
                    marker.transform.SetParent(avatarSpriteRenderer.transform, false);
                    marker.transform.localPosition = localPos;

                    if (!string.IsNullOrEmpty(region.image_base64))
                    {
                        try
                        {
                            byte[] imgBytes = Convert.FromBase64String(region.image_base64);
                            Texture2D tex = new Texture2D(2, 2);
                            tex.LoadImage(imgBytes);
                            tex.filterMode = FilterMode.Bilinear;

                            // Per-region PPU: region sprites are at analysis resolution,
                            // but should appear at the same size as on the full-res silhouette.
                            // regionPPU = spriteTexW * mainPPU / bboxW_fullRes
                            float regionPPU = pixelsPerUnit;
                            if (region.bounding_box != null && region.bounding_box.width > 0 && region.bounding_box.height > 0)
                            {
                                regionPPU = (float)tex.width * pixelsPerUnit / (float)region.bounding_box.width;
                                Debug.Log($"[BodyMapReceiver] Region #{region.id}: tex={tex.width}x{tex.height}, bbox={region.bounding_box.width}x{region.bounding_box.height}, mainPPU={pixelsPerUnit:F1}, regionPPU={regionPPU:F1}");
                            }
                            else
                            {
                                Debug.LogWarning($"[BodyMapReceiver] Region #{region.id}: bounding_box missing or zero, using mainPPU={pixelsPerUnit:F1}");
                            }

                            Sprite regionSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), regionPPU);
                            
                            SpriteRenderer sr = marker.AddComponent<SpriteRenderer>();
                            sr.sprite = regionSprite;
                            sr.sortingOrder = 10 + region.id; // Render in front of avatar

                            // Add BoxCollider for 3D Raycasting hits
                            BoxCollider bc = marker.AddComponent<BoxCollider>();
                            bc.size = new Vector3(regionSprite.bounds.size.x, regionSprite.bounds.size.y, 0.1f);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[BodyMapReceiver] Error decoding sprite for Region #{region.id}: {e.Message}");
                            // Fallback
                            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            Destroy(marker);
                            marker = fallback;
                            marker.name = $"Region_{region.id}_Fallback";
                            marker.transform.SetParent(avatarSpriteRenderer.transform, false);
                            marker.transform.localPosition = localPos;
                            marker.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
                        }
                    }
                    else
                    {
                        GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        Destroy(marker);
                        marker = fallback;
                        marker.name = $"Region_{region.id}_Fallback";
                        marker.transform.SetParent(avatarSpriteRenderer.transform, false);
                        marker.transform.localPosition = localPos;
                        marker.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
                    }

                    // Add metadata and hover/float logic component
                    InteractiveRegion3D ir3d = marker.AddComponent<InteractiveRegion3D>();
                    ir3d.id = region.id;
                    ir3d.colorName = region.color_name;
                    ir3d.colorHex = region.color_hex;
                    ir3d.bodyLocation = region.body_location;
                    ir3d.emotionLabel = region.emotion_label;
                    ir3d.description = region.description;
                    ir3d.pixelCount = region.pixel_count;

                    instantiatedShapes.Add(marker);
                }
            }

            // Hide startup instruction card and lock cursor for player look/move
            if (instructionPanel != null)
            {
                instructionPanel.SetActive(false);
            }

            if (playerInputs != null)
            {
                playerInputs.cursorLocked = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (statusText != null)
            {
                statusText.text = "body mapping analysis: scan active";
            }

            ShowNotice("imported new body map scan successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BodyMapReceiver] Failed to process received JSON: {ex}");
        }
    }

    public void ShowNotice(string message)
    {
        if (noticePanel != null && noticeText != null)
        {
            if (noticeCoroutine != null)
            {
                StopCoroutine(noticeCoroutine);
            }
            noticeCoroutine = StartCoroutine(NoticeSequence(message));
        }
    }

    private System.Collections.IEnumerator NoticeSequence(string message)
    {
        noticeText.text = message;
        noticePanel.SetActive(true);
        yield return new WaitForSeconds(5f);
        noticePanel.SetActive(false);
    }

    private void ClearInstantiatedShapes()
    {
        currentlyHoveredRegion = null;
        selectedRegions.Clear();
        selectedRegionNormal = null;
        foreach (var shape in instantiatedShapes)
        {
            if (shape != null)
            {
                Destroy(shape);
            }
        }
        instantiatedShapes.Clear();
    }

    public InteractiveRegion3D selectedRegionNormal = null;

    public void DeselectActiveRegion()
    {
        if (selectedRegionNormal != null)
        {
            selectedRegionNormal.SetTooltipActive(false);
            selectedRegionNormal = null;
        }
        if (detailPanel != null)
        {
            detailPanel.SetActive(false);
        }
    }

    public void ShowRegionDetails(InteractiveRegion3D region)
    {
        if (selectedRegionNormal == region)
        {
            // Toggle off if clicking the already selected region
            DeselectActiveRegion();
            return;
        }

        // Hide previous tooltip
        if (selectedRegionNormal != null)
        {
            selectedRegionNormal.SetTooltipActive(false);
        }

        selectedRegionNormal = region;
        
        if (selectedRegionNormal != null)
        {
            selectedRegionNormal.SetTooltipActive(true);
        }

        // Left-bottom detail panel is disabled per user request
        if (detailPanel != null)
        {
            detailPanel.SetActive(false);
        }
        Debug.Log($"[InteractiveRegion3D] Selected Region {region.id}: {region.colorName} ({region.bodyLocation})");
    }
}
