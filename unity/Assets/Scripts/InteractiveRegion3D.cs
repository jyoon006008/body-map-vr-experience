using UnityEngine;
using System.Collections;

public class InteractiveRegion3D : MonoBehaviour
{
    [Header("Metadata")]
    public int id;
    public string colorName;
    public string colorHex;
    public string bodyLocation;
    public string emotionLabel;
    [TextArea(3, 5)]
    public string description;
    public int pixelCount;

    [Header("Visual Effects")]
    public float floatSpeed = 0.8f;
    public float floatHeight = 0.015f;
    public float hoverScaleMultiplier = 1.05f;

    private Vector3 startLocalPos;
    private Vector3 originalScale;
    private bool isHovered = false;
    private bool isSelected = false;

    private GameObject outlineObj;
    private GameObject selectionOutlineObj;
    private GameObject hoverTextObj;
    private BodyMapReceiver receiver;

    void Start()
    {
        startLocalPos = transform.localPosition;
        originalScale = transform.localScale;
        receiver = FindObjectOfType<BodyMapReceiver>();

        // Create Hover Text GameObject dynamically
        CreateHoverText();
    }

    private void CreateHoverText()
    {
        if (hoverTextObj != null) return;

        hoverTextObj = new GameObject("Region Hover");
        hoverTextObj.transform.SetParent(transform, false);

        float textX = 1.0f;
        float textY = 0f;
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            float extentsX = sr.sprite.bounds.extents.x;
            // Determine left vs right side based on position relative to body midline (x = 0)
            bool isLeftSide = transform.localPosition.x < 0f;
            
            // Tooltip card is 14f * 0.08f = 1.12f wide, so half-width is 0.56f
            float halfCardWidth = 0.56f;
            float buffer = 0.15f;
            
            if (isLeftSide)
            {
                textX = -(extentsX + halfCardWidth + buffer);
            }
            else
            {
                textX = (extentsX + halfCardWidth + buffer);
            }
        }
        else
        {
            bool isLeftSide = transform.localPosition.x < 0f;
            textX = isLeftSide ? -1.0f : 1.0f;
        }
        
        // Z-offset is pulled forward (-0.3f) towards the camera so it stays physically in front of sprites (which are at -0.08f)
        hoverTextObj.transform.localPosition = new Vector3(textX, textY, -0.3f);
        // Scale it down since we want it to be readable and compact
        hoverTextObj.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);

        // Add 3D TextMeshPro component
        var tmp = hoverTextObj.AddComponent<TMPro.TextMeshPro>();
        if (tmp == null)
        {
            Debug.LogError("[InteractiveRegion3D] Failed to add TextMeshPro component.");
            return;
        }

        BodyMapAIController.ApplyKoreanFont(tmp);
        
        string hex = string.IsNullOrEmpty(colorHex) ? "#FFFF00" : colorHex;
        if (!hex.StartsWith("#")) hex = "#" + hex;
        
        string nameUpper = string.IsNullOrEmpty(colorName) ? "UNKNOWN" : colorName.ToUpper();
        
        tmp.text = $"Region #{id}\n<color={hex}><b>{nameUpper}</b></color>";
        tmp.fontSize = 8; // Larger font size for visibility
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.outlineWidth = 0.2f; // Thicker outline
        tmp.outlineColor = Color.black;

        // Force extremely high sortingOrder for TextMeshPro MeshRenderer so it renders in front of all 2D sprites
        var tmpRenderer = hoverTextObj.GetComponent<Renderer>();
        if (tmpRenderer != null)
        {
            tmpRenderer.sortingOrder = 1000;
        }

        // Add a small dark semi-transparent background card behind the text for visibility
        GameObject bgObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        if (bgObj != null)
        {
            bgObj.name = "TooltipBackground";
            bgObj.transform.SetParent(hoverTextObj.transform, false);
            bgObj.transform.localPosition = new Vector3(0f, 0f, 0.02f); // slightly behind the text
            bgObj.transform.localScale = new Vector3(14f, 6f, 1f); // fits 2 lines of text nicely
            
            // Remove collider from background so it doesn't intercept raycasts
            var col = bgObj.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var bgRenderer = bgObj.GetComponent<Renderer>();
            if (bgRenderer != null)
            {
                Shader unlitShader = Shader.Find("Sprites/Default");
                if (unlitShader == null) unlitShader = Shader.Find("Unlit/Transparent");
                Material bgMat = new Material(unlitShader);
                bgMat.color = new Color(0f, 0f, 0f, 0.9f); // 90% opaque black background card for maximum readability
                bgRenderer.material = bgMat;
                
                // Force high sortingOrder (just behind text) so background also renders in front of 2D sprites
                bgRenderer.sortingOrder = 999;
            }
        }

        hoverTextObj.SetActive(false);
    }

    public void UpdateHoverText()
    {
        if (hoverTextObj == null)
        {
            CreateHoverText();
        }
        else
        {
            var tmp = hoverTextObj.GetComponent<TMPro.TextMeshPro>();
            if (tmp != null)
            {
                string hex = string.IsNullOrEmpty(colorHex) ? "#FFFF00" : colorHex;
                if (!hex.StartsWith("#")) hex = "#" + hex;
                string nameUpper = string.IsNullOrEmpty(colorName) ? "UNKNOWN" : colorName.ToUpper();
                tmp.text = $"Region #{id}\n<color={hex}><b>{nameUpper}</b></color>";
            }
        }
    }

    void Update()
    {
        // Gentle vertical floating motion (reduced amplitude for subtlety)
        float offset = Mathf.Sin(Time.time * floatSpeed + id) * floatHeight;
        transform.localPosition = startLocalPos + new Vector3(0f, offset, 0f);

        // Self-rotation is disabled to keep 2D sprite visible from front
    }

    void LateUpdate()
    {
        // Billboard effect: Make hover text face the camera
        if (hoverTextObj != null && hoverTextObj.activeSelf)
        {
            Camera cam = Camera.main;
            if (cam == null) cam = FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                Vector3 screenPos = cam.WorldToScreenPoint(transform.position);
                // Hide if behind camera frustum to prevent visual errors
                if (screenPos.z < 0)
                {
                    hoverTextObj.SetActive(false);
                }
                else
                {
                    hoverTextObj.transform.LookAt(cam.transform);
                    hoverTextObj.transform.Rotate(0f, 180f, 0f);
                }
            }
        }
    }

    public void OnHoverEnter()
    {
        if (isHovered) return;
        isHovered = true;

        // Scale up slightly on hover
        transform.localScale = originalScale * hoverScaleMultiplier;

        // Create outline sprite behind the current sprite
        if (outlineObj == null)
        {
            outlineObj = new GameObject("OutlineHighlight");
            outlineObj.transform.SetParent(transform, false);
            outlineObj.transform.localPosition = new Vector3(0f, 0f, 0.01f); // slightly behind
            outlineObj.transform.localScale = new Vector3(1.08f, 1.08f, 1.0f); // 8% larger for outline effect

            SpriteRenderer parentSr = GetComponent<SpriteRenderer>();
            SpriteRenderer outlineSr = outlineObj.AddComponent<SpriteRenderer>();
            if (parentSr != null)
            {
                outlineSr.sprite = parentSr.sprite;
                outlineSr.sortingOrder = parentSr.sortingOrder - 1; // render behind
            }

            Shader solidColorShader = Shader.Find("Sprites/SolidColor");
            if (solidColorShader != null)
            {
                Material outlineMat = new Material(solidColorShader);
                outlineMat.SetColor("_Color", Color.yellow);
                outlineSr.material = outlineMat;
            }
            else
            {
                Material outlineMat = new Material(Shader.Find("Sprites/Default"));
                outlineMat.color = Color.yellow;
                outlineSr.material = outlineMat;
            }
        }
    }

    public void OnHoverExit()
    {
        if (!isHovered) return;
        isHovered = false;

        // Reset scale
        transform.localScale = originalScale;

        // Destroy outline sprite
        if (outlineObj != null)
        {
            Destroy(outlineObj);
            outlineObj = null;
        }
    }

    public void SetTooltipActive(bool active)
    {
        if (hoverTextObj != null)
        {
            hoverTextObj.SetActive(active);
        }
    }

    public void OnSelect()
    {
        // Inform parent manager script to update UI
        if (receiver == null)
        {
            receiver = FindObjectOfType<BodyMapReceiver>();
        }

        if (receiver != null)
        {
            receiver.ShowRegionDetails(this);
        }
        else
        {
            Debug.LogWarning("[InteractiveRegion3D] BodyMapReceiver not found in scene. Cannot show details.");
        }
    }

    // ── Edit Mode: Selection ──

    public bool IsSelected => isSelected;

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        UpdateSelectionVisual();
    }

    public void ToggleSelected()
    {
        isSelected = !isSelected;
        UpdateSelectionVisual();
    }

    private void UpdateSelectionVisual()
    {
        if (isSelected)
        {
            // Show cyan/blue selection outline
            if (selectionOutlineObj == null)
            {
                selectionOutlineObj = new GameObject("SelectionOutline");
                selectionOutlineObj.transform.SetParent(transform, false);
                selectionOutlineObj.transform.localPosition = new Vector3(0f, 0f, 0.015f);
                selectionOutlineObj.transform.localScale = new Vector3(1.12f, 1.12f, 1.0f);

                SpriteRenderer parentSr = GetComponent<SpriteRenderer>();
                SpriteRenderer outlineSr = selectionOutlineObj.AddComponent<SpriteRenderer>();
                if (parentSr != null)
                {
                    outlineSr.sprite = parentSr.sprite;
                    outlineSr.sortingOrder = parentSr.sortingOrder - 2;
                }

                Shader solidColorShader = Shader.Find("Sprites/SolidColor");
                if (solidColorShader != null)
                {
                    Material mat = new Material(solidColorShader);
                    mat.SetColor("_Color", new Color(0.2f, 0.6f, 1f, 0.9f)); // bright blue
                    outlineSr.material = mat;
                }
                else
                {
                    Material mat = new Material(Shader.Find("Sprites/Default"));
                    mat.color = new Color(0.2f, 0.6f, 1f, 0.9f);
                    outlineSr.material = mat;
                }
            }
            selectionOutlineObj.SetActive(true);
        }
        else
        {
            if (selectionOutlineObj != null)
            {
                Destroy(selectionOutlineObj);
                selectionOutlineObj = null;
            }
        }
    }

    private void SpawnClickParticles()
    {
        // Dynamically instantiate a temporary particle system to celebrate selection
        GameObject particleObj = new GameObject("ClickParticles");
        particleObj.transform.position = transform.position;

        ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        
        // Grab color from SpriteRenderer or fallback to yellow
        Color pColor = Color.yellow;
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            pColor = Color.white;
        }
        
        main.startColor = pColor;
        main.startSize = 0.15f;
        main.startSpeed = 2f;
        main.duration = 0.5f;
        main.loop = false;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.burstCount = 1;
        emission.SetBurst(0, new ParticleSystem.Burst(0.0f, 30));

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        ps.Play();
    }

    // ── Tripo3D Progress Visuals & 3D Swap ──
    private Coroutine generatingPulseCoroutine;
    private GameObject formingTextObj;

    public void StartGeneratingVisuals()
    {
        if (generatingPulseCoroutine != null) StopCoroutine(generatingPulseCoroutine);
        generatingPulseCoroutine = StartCoroutine(GeneratingPulseSequence());

        // Create forming billboard status
        if (formingTextObj == null)
        {
            formingTextObj = new GameObject("Forming Text");
            formingTextObj.transform.SetParent(transform, false);
            formingTextObj.transform.localPosition = new Vector3(0f, -0.4f, -0.2f);
            formingTextObj.transform.localScale = new Vector3(0.06f, 0.06f, 0.06f);

            var tmp = formingTextObj.AddComponent<TMPro.TextMeshPro>();
            BodyMapAIController.ApplyKoreanFont(tmp);
            tmp.text = "forming...";
            tmp.fontSize = 6;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.6f, 0.1f, 0.9f); // gold/orange
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = Color.black;

            var renderer = formingTextObj.GetComponent<Renderer>();
            if (renderer != null) renderer.sortingOrder = 1001;
        }
        formingTextObj.SetActive(true);

        // Highlight/Glow effect using orange/gold outline
        if (outlineObj == null)
        {
            outlineObj = new GameObject("GeneratingHighlight");
            outlineObj.transform.SetParent(transform, false);
            outlineObj.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            outlineObj.transform.localScale = new Vector3(1.05f, 1.05f, 1.0f);

            SpriteRenderer parentSr = GetComponent<SpriteRenderer>();
            SpriteRenderer outlineSr = outlineObj.AddComponent<SpriteRenderer>();
            if (parentSr != null)
            {
                outlineSr.sprite = parentSr.sprite;
                outlineSr.sortingOrder = parentSr.sortingOrder - 1;
            }

            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = new Color(1f, 0.6f, 0.1f, 0.7f); // orange/gold glow
            outlineSr.material = mat;
        }
    }

    public void StopGeneratingVisuals()
    {
        if (generatingPulseCoroutine != null)
        {
            StopCoroutine(generatingPulseCoroutine);
            generatingPulseCoroutine = null;
        }
        if (formingTextObj != null)
        {
            formingTextObj.SetActive(false);
        }
        if (outlineObj != null)
        {
            Destroy(outlineObj);
            outlineObj = null;
        }
        transform.localScale = originalScale;
    }

    private IEnumerator GeneratingPulseSequence()
    {
        float speed = 3f;
        while (true)
        {
            float scaleFactor = 1.0f + Mathf.Sin(Time.time * speed) * 0.06f; // slow pulse
            transform.localScale = originalScale * scaleFactor;
            yield return null;
        }
    }

    public void SwapSpriteWith3DModel(GameObject glbObject)
    {
        StopGeneratingVisuals();
        StartCoroutine(TransitionSpriteTo3DSequence(glbObject));
    }

    private IEnumerator TransitionSpriteTo3DSequence(GameObject glbObject)
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        Vector3 final3DScale = glbObject.transform.localScale;
        glbObject.transform.localScale = Vector3.zero;
        glbObject.SetActive(true);

        // Fetch all renderers and their original colors
        Renderer[] renderers = glbObject.GetComponentsInChildren<Renderer>(true);
        var originalColors = new System.Collections.Generic.Dictionary<Material, Color>();
        var colorPropertyNames = new System.Collections.Generic.Dictionary<Material, string>();

        foreach (var rend in renderers)
        {
            foreach (var mat in rend.materials)
            {
                if (originalColors.ContainsKey(mat)) continue;

                string propName = null;
                if (mat.HasProperty("_BaseColor")) propName = "_BaseColor";
                else if (mat.HasProperty("_Color")) propName = "_Color";

                if (propName != null)
                {
                    Color origCol = mat.GetColor(propName);
                    originalColors[mat] = origCol;
                    colorPropertyNames[mat] = propName;

                    // Set rendering mode to transparent if possible
                    ConfigureMaterialForTransparency(mat);
                }
            }
        }

        Color startColor = sr != null ? sr.color : Color.white;
        float elapsed = 0f;
        float duration = 0.8f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            if (sr != null)
            {
                Color c = startColor;
                c.a = Mathf.Lerp(startColor.a, 0f, t);
                sr.color = c;
            }

            glbObject.transform.localScale = Vector3.Lerp(Vector3.zero, final3DScale, t);

            // Fade in 3D materials
            foreach (var kvp in originalColors)
            {
                Material mat = kvp.Key;
                Color origCol = kvp.Value;
                string propName = colorPropertyNames[mat];

                Color curCol = origCol;
                curCol.a = Mathf.Lerp(0f, origCol.a, t);
                mat.SetColor(propName, curCol);
            }

            yield return null;
        }

        if (sr != null) sr.enabled = false;

        // Restore opaque rendering mode for performance and correct shadow rendering
        foreach (var kvp in originalColors)
        {
            Material mat = kvp.Key;
            Color origCol = kvp.Value;
            string propName = colorPropertyNames[mat];
            mat.SetColor(propName, origCol);
            ConfigureMaterialForOpaque(mat);
        }
    }

    private void ConfigureMaterialForTransparency(Material mat)
    {
        if (mat.shader.name.Contains("Universal Render Pipeline") || mat.shader.name.Contains("Lit"))
        {
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0); // Alpha
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else
        {
            mat.SetFloat("_Mode", 2); // Fade
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }

    private void ConfigureMaterialForOpaque(Material mat)
    {
        if (mat.shader.name.Contains("Universal Render Pipeline") || mat.shader.name.Contains("Lit"))
        {
            mat.SetFloat("_Surface", 0); // Opaque
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = -1;
        }
        else
        {
            mat.SetFloat("_Mode", 0); // Opaque
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = -1;
        }
    }

    [Header("AI Sphere Indicator")]
    private GameObject aiSphereIndicatorObj;
    private Material aiSphereIndicatorMat;
    private Coroutine aiSpherePulseCoroutine;

    public void SetAiSphereIndicatorActive(bool active)
    {
        if (active)
        {
            if (aiSphereIndicatorObj != null) return;

            // 1. Create primitive sphere
            aiSphereIndicatorObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            aiSphereIndicatorObj.name = "AI_Sphere_Indicator";
            aiSphereIndicatorObj.transform.SetParent(transform, false);
            
            // Position slightly in front of the sprite along Z axis
            aiSphereIndicatorObj.transform.localPosition = new Vector3(0f, 0f, -0.15f);
            aiSphereIndicatorObj.transform.localScale = Vector3.one * 0.35f;

            // Remove Collider so it doesn't block raycasts or physics
            var col = aiSphereIndicatorObj.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // 2. Set Material with URP Lit and Emission
            var renderer = aiSphereIndicatorObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                if (urpShader == null) urpShader = Shader.Find("Standard");

                aiSphereIndicatorMat = new Material(urpShader);
                
                // Configure transparent rendering mode
                aiSphereIndicatorMat.SetFloat("_Surface", 1f);
                aiSphereIndicatorMat.SetFloat("_Blend", 0f);
                aiSphereIndicatorMat.SetOverrideTag("RenderType", "Transparent");
                aiSphereIndicatorMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                aiSphereIndicatorMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                aiSphereIndicatorMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                aiSphereIndicatorMat.SetInt("_ZWrite", 0);
                aiSphereIndicatorMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

                // Glowing cyan color to match the companion orb style
                Color baseColor = new Color(0.18f, 0.85f, 1f, 0.65f);
                aiSphereIndicatorMat.SetColor("_BaseColor", baseColor);
                aiSphereIndicatorMat.SetColor("_Color", baseColor);
                aiSphereIndicatorMat.SetColor("_EmissionColor", new Color(0.18f, 0.85f, 1f, 1.5f));
                aiSphereIndicatorMat.EnableKeyword("_EMISSION");

                renderer.material = aiSphereIndicatorMat;
            }

            // 3. Start pulsing animation
            if (aiSpherePulseCoroutine != null) StopCoroutine(aiSpherePulseCoroutine);
            aiSpherePulseCoroutine = StartCoroutine(AiSpherePulseSequence());
        }
        else
        {
            if (aiSpherePulseCoroutine != null)
            {
                StopCoroutine(aiSpherePulseCoroutine);
                aiSpherePulseCoroutine = null;
            }
            if (aiSphereIndicatorObj != null)
            {
                Destroy(aiSphereIndicatorObj);
                aiSphereIndicatorObj = null;
            }
            if (aiSphereIndicatorMat != null)
            {
                Destroy(aiSphereIndicatorMat);
                aiSphereIndicatorMat = null;
            }
        }
    }

    private IEnumerator AiSpherePulseSequence()
    {
        float speed = 2.2f;
        Vector3 baseScale = Vector3.one * 0.35f;
        while (aiSphereIndicatorObj != null)
        {
            float pulse = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
            float scaleFactor = Mathf.Lerp(0.85f, 1.15f, pulse);
            aiSphereIndicatorObj.transform.localScale = baseScale * scaleFactor;
            yield return null;
        }
    }
}
