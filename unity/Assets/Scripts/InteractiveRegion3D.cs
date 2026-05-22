using UnityEngine;

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
        hoverTextObj = new GameObject("HoverText");
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
        
        string hex = string.IsNullOrEmpty(colorHex) ? "#FFFF00" : colorHex;
        if (!hex.StartsWith("#")) hex = "#" + hex;
        
        tmp.text = $"Region #{id}\n<color={hex}><b>{colorName.ToUpper()}</b></color>";
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
        bgObj.name = "TooltipBackground";
        bgObj.transform.SetParent(hoverTextObj.transform, false);
        bgObj.transform.localPosition = new Vector3(0f, 0f, 0.02f); // slightly behind the text
        bgObj.transform.localScale = new Vector3(14f, 6f, 1f); // fits 2 lines of text nicely
        
        // Remove collider from background so it doesn't intercept raycasts
        Destroy(bgObj.GetComponent<Collider>());

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

        hoverTextObj.SetActive(false);
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
        if (hoverTextObj != null && hoverTextObj.activeSelf && Camera.main != null)
        {
            hoverTextObj.transform.LookAt(Camera.main.transform);
            hoverTextObj.transform.Rotate(0f, 180f, 0f);
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
}
