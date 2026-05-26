using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public enum OrbState { Idle, AIThinking, AISpeaking, WaitingForUser, UserSpeaking, ProcessingUserAnswer, TransitioningStep, ReadyForGeneration, Ready }

/// <summary>
/// BreathingEmotionalOrb — Screen-space HUD orb anchored bottom-center.
/// Uses dynamically generated anti-aliased circle texture to avoid built-in Knob.psd dependency.
/// </summary>
public class BreathingEmotionalOrb : MonoBehaviour
{
    public static BreathingEmotionalOrb Instance { get; private set; }

    public OrbState CurrentState { get; private set; } = OrbState.Idle;

    // Procedural UI components
    private Image coreImage;
    private Image auraImage;
    private Image rippleImage;
    private TextMeshProUGUI statusText;

    // Breathing animation settings
    private float breathTimer = 0f;
    private float currentScale = 1.0f;
    private float targetScale = 1.0f;

    // Dynamic color targets
    private Color targetCoreColor = new Color(0f, 1f, 1f, 0.9f);   // Default cyan
    private Color targetAuraColor = new Color(0f, 1f, 1f, 0.25f);  // Default cyan

    private Coroutine rippleCoroutine;
    private Coroutine pulseCoroutine;

    private static Sprite circleSprite = null;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[Orb] Instance assigned: " + gameObject.name);
        }
        else
        {
            Debug.LogWarning("[Orb] Duplicate BreathingEmotionalOrb detected. Destroying: " + gameObject.name);
            Destroy(gameObject);
            return;
        }
    }

    void OnEnable()
    {
        Debug.Log("[Orb] active=true");
    }

    void OnDisable()
    {
        Debug.Log("[Orb] active=false");
    }

    void Start()
    {
        // Adjust the container RectTransform properties as requested
        RectTransform rect = GetComponent<RectTransform>();
        if (rect == null) rect = gameObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot     = new Vector2(0.5f, 0.5f); // Centered pivot for clean animations
        rect.anchoredPosition = new Vector2(0f, 120f);
        rect.sizeDelta = new Vector2(160f, 160f); // Size 160x160

        Debug.Log("[Orb] position=(0,120)");

        BuildOrbUI();
        SetState(OrbState.Idle);

        // Start looping ripple coroutine
        if (rippleCoroutine != null) StopCoroutine(rippleCoroutine);
        rippleCoroutine = StartCoroutine(RippleLoopSequence());
    }

    // ── Create smooth anti-aliased circle sprite programmatically ──────────
    private Sprite CreateCircleSprite()
    {
        if (circleSprite != null) return circleSprite;

        int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] colors = new Color[size * size];
        float radius = size * 0.5f;
        float center = size * 0.5f;
        float smoothWidth = 1.5f; // Anti-aliasing boundary width

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist <= radius)
                {
                    // Smooth edge anti-aliasing
                    float alpha = Mathf.Clamp01((radius - dist) / smoothWidth);
                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    colors[y * size + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(colors);
        texture.Apply();
        circleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return circleSprite;
    }

    // ── Build UI elements dynamically ─────────────────────────────────────
    private void BuildOrbUI()
    {
        Sprite circle = CreateCircleSprite();

        // 1. Aura (large, semi-transparent outer ring)
        {
            var go = new GameObject("OrbAura", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot      = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = Vector2.zero;
            r.sizeDelta  = new Vector2(140f, 140f); // Size 140x140

            auraImage = go.AddComponent<Image>();
            auraImage.sprite = circle;
            auraImage.color  = targetAuraColor;
            auraImage.type   = Image.Type.Simple;
            auraImage.preserveAspect = true;
        }

        // 2. Ripple (expanding pulse ring)
        {
            var go = new GameObject("OrbRipple", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot      = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = Vector2.zero;
            r.sizeDelta  = new Vector2(140f, 140f);

            rippleImage = go.AddComponent<Image>();
            rippleImage.sprite = circle;
            rippleImage.color  = Color.clear;
            rippleImage.type   = Image.Type.Simple;
            rippleImage.preserveAspect = true;
            go.SetActive(false);
        }

        // 3. Core (solid inner circle)
        {
            var go = new GameObject("OrbCore", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot      = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = Vector2.zero;
            r.sizeDelta  = new Vector2(70f, 70f); // Size 70x70

            coreImage = go.AddComponent<Image>();
            coreImage.sprite = circle;
            coreImage.color  = targetCoreColor;
            coreImage.type   = Image.Type.Simple;
            coreImage.preserveAspect = true;
        }

        // 4. Status Text (positioned below the orb)
        {
            var go = new GameObject("Orb Status", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f, 0f);
            r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot     = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -4f);
            r.sizeDelta = new Vector2(260f, 28f);

            statusText = go.AddComponent<TextMeshProUGUI>();
            statusText.fontSize  = 14;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color     = new Color(0.8f, 0.9f, 1f, 1f);

            BodyMapAIController.ApplyKoreanFont(statusText);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────
    public void SetState(OrbState newState)
    {
        CurrentState = newState;
        Debug.Log("[Orb] State=" + newState.ToString());

        UpdateVisuals();

        // Trigger short white/orange pulse sequence on entering Ready/ReadyForGeneration state
        if (newState == OrbState.Ready || newState == OrbState.ReadyForGeneration)
        {
            if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
            pulseCoroutine = StartCoroutine(PulseCoroutine());
        }
    }

    public void UpdateVolumeAmplitude(float amp)
    {
        // Core reacts to volume during UserSpeaking/AISpeaking states
        if (CurrentState == OrbState.UserSpeaking || CurrentState == OrbState.AISpeaking)
        {
            targetScale = 1.0f + Mathf.Clamp(amp * 2f, 0f, 0.2f);
        }
    }

    public void TriggerVisualAcknowledgment()
    {
        Debug.Log("[Feedback] Visual acknowledgment played");
        SetState(OrbState.AIThinking);
        if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
        pulseCoroutine = StartCoroutine(VisualAcknowledgmentCoroutine());
    }

    private IEnumerator VisualAcknowledgmentCoroutine()
    {
        float elapsed = 0f;
        float duration = 0.5f;
        Vector3 startScale = Vector3.one;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // EaseInOutSine pulse logic: sine shape from 0 to pi back to 0
            float factor = (1f - Mathf.Cos(t * Mathf.PI * 2f)) * 0.5f;
            float scaleVal = Mathf.Lerp(1.0f, 1.08f, factor);
            transform.localScale = new Vector3(scaleVal, scaleVal, 1f);
            yield return null;
        }
        transform.localScale = startScale;
    }

    public void TriggerInterruptionPulse()
    {
        Debug.Log("[Orb] Interruption pulse triggered");
        // Temporary green pulse representation
        targetCoreColor = new Color(0.1f, 0.9f, 0.2f, 0.9f); // Green
        targetAuraColor = new Color(0.1f, 0.9f, 0.2f, 0.25f);
        
        if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
        pulseCoroutine = StartCoroutine(InterruptionPulseCoroutine());
    }

    private IEnumerator InterruptionPulseCoroutine()
    {
        float elapsed = 0f;
        float duration = 0.4f;
        Vector3 startScale = Vector3.one;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float factor = (1f - Mathf.Cos(t * Mathf.PI * 2f)) * 0.5f;
            float scaleVal = Mathf.Lerp(1.0f, 1.15f, factor);
            transform.localScale = new Vector3(scaleVal, scaleVal, 1f);
            yield return null;
        }
        transform.localScale = startScale;
    }

    // ── Update and Animation ──────────────────────────────────────────────
    void Update()
    {
        AnimateOrb();
    }

    private void AnimateOrb()
    {
        if (!Application.isPlaying) return;

        // Animate breathing speeds and amplitudes based on state
        float breathPeriod = 3.0f;
        float breathAmp = 0.02f;

        if (CurrentState == OrbState.AIThinking || CurrentState == OrbState.ProcessingUserAnswer)
        {
            breathPeriod = 3.5f; // Slow breathing
            breathAmp = 0.04f;
        }
        else if (CurrentState == OrbState.ReadyForGeneration)
        {
            breathPeriod = 2.5f; // Orange pulse style breathing
            breathAmp = 0.05f;
        }
        else if (CurrentState == OrbState.Ready)
        {
            breathAmp = 0f; // Static after pulse
        }
        else if (CurrentState == OrbState.WaitingForUser || CurrentState == OrbState.TransitioningStep)
        {
            breathPeriod = 4.0f; // Slow wave/breathing
            breathAmp = 0.02f;
        }
        else if (CurrentState == OrbState.UserSpeaking || CurrentState == OrbState.AISpeaking)
        {
            breathAmp = 0.01f; // Very subtle breathing during active ripple
        }

        // Breathing scale (sine wave)
        float breathScale = 1.0f;
        if (breathAmp > 0f)
        {
            breathTimer += Time.deltaTime;
            breathScale = Mathf.Lerp(1f - breathAmp, 1f + breathAmp,
                (Mathf.Sin(breathTimer / breathPeriod * Mathf.PI * 2f) + 1f) * 0.5f);
        }

        // Smooth toward targetScale (volume reaction decay) and apply breathing scale
        currentScale = Mathf.Lerp(currentScale, targetScale, Time.deltaTime * 5f);
        float finalScale = currentScale * breathScale;

        // Apply scale to Core
        if (coreImage != null)
        {
            coreImage.transform.localScale = Vector3.one * finalScale;
        }

        // Smoothly lerp core/aura colors
        if (coreImage != null)
            coreImage.color = Color.Lerp(coreImage.color, targetCoreColor, Time.deltaTime * 4f);
        if (auraImage != null)
            auraImage.color = Color.Lerp(auraImage.color, targetAuraColor, Time.deltaTime * 4f);

        // Decay volume-reactive targetScale back to 1.0
        targetScale = Mathf.Lerp(targetScale, 1.0f, Time.deltaTime * 6f);
    }

    void LateUpdate()
    {
        SafeAreaClamp();
    }

    // ── Color and Status Text Mapping ─────────────────────────────────────
    private void UpdateVisuals()
    {
        switch (CurrentState)
        {
            case OrbState.Idle:
                targetCoreColor = new Color(0f, 1f, 1f, 0.9f);    // Cyan
                targetAuraColor = new Color(0f, 1f, 1f, 0.25f);
                if (statusText != null) statusText.text = "";
                break;

            case OrbState.WaitingForUser:
            case OrbState.UserSpeaking:
                targetCoreColor = new Color(0f, 1f, 1f, 0.9f);    // Cyan (파란색 계열)
                targetAuraColor = new Color(0f, 1f, 1f, 0.25f);
                if (statusText != null) statusText.text = "듣는 중...";
                break;

            case OrbState.AIThinking:
                targetCoreColor = new Color(0.6f, 0.1f, 0.9f, 0.9f); // Purple
                targetAuraColor = new Color(0.6f, 0.1f, 0.9f, 0.25f);
                if (statusText != null) statusText.text = "생각 중...";
                break;

            case OrbState.AISpeaking:
                targetCoreColor = new Color(0.1f, 0.9f, 0.2f, 0.9f);   // Green (초록색)
                targetAuraColor = new Color(0.1f, 0.9f, 0.2f, 0.25f);
                if (statusText != null) statusText.text = "말하는 중...";
                break;

            case OrbState.ProcessingUserAnswer:
                targetCoreColor = new Color(0.6f, 0.1f, 0.9f, 0.9f); // Purple
                targetAuraColor = new Color(0.6f, 0.1f, 0.9f, 0.25f);
                if (statusText != null) statusText.text = "분석 중...";
                break;

            case OrbState.TransitioningStep:
                targetCoreColor = new Color(0f, 1f, 1f, 0.9f);    // Cyan
                targetAuraColor = new Color(0f, 1f, 1f, 0.25f);
                if (statusText != null) statusText.text = "선택 대기 중...";
                break;

            case OrbState.ReadyForGeneration:
                targetCoreColor = new Color(1f, 0.5f, 0f, 0.9f);   // Orange
                targetAuraColor = new Color(1f, 0.5f, 0f, 0.25f);
                if (statusText != null) statusText.text = "생성 준비 완료";
                break;

            case OrbState.Ready:
                targetCoreColor = new Color(1f, 1f, 1f, 0.9f);     // White
                targetAuraColor = new Color(1f, 1f, 1f, 0.25f);
                if (statusText != null) statusText.text = "완료";
                break;
        }
    }

    // ── Looping Ripple Sequence ───────────────────────────────────────────
    private IEnumerator RippleLoopSequence()
    {
        while (true)
        {
            if (CurrentState == OrbState.WaitingForUser || CurrentState == OrbState.UserSpeaking || CurrentState == OrbState.AISpeaking)
            {
                float duration = 1.5f;
                if (CurrentState == OrbState.WaitingForUser) duration = 2.2f;      // slow wave
                else if (CurrentState == OrbState.UserSpeaking) duration = 1.5f; // wave
                else if (CurrentState == OrbState.AISpeaking) duration = 0.8f;   // fast ripple

                float elapsed = 0f;

                if (rippleImage != null)
                {
                    rippleImage.gameObject.SetActive(true);
                }

                while (elapsed < duration)
                {
                    // Check if state changed mid-animation
                    if (CurrentState != OrbState.WaitingForUser && CurrentState != OrbState.UserSpeaking && CurrentState != OrbState.AISpeaking)
                        break;

                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;

                    if (rippleImage != null)
                    {
                        // Ripple scales out and fades alpha from 0.5 to 0
                        rippleImage.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 1.6f, t);
                        Color c = targetCoreColor;
                        c.a = Mathf.Lerp(0.5f, 0f, t);
                        rippleImage.color = c;
                    }
                    yield return null;
                }
            }
            else
            {
                if (rippleImage != null)
                {
                    rippleImage.gameObject.SetActive(false);
                }
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    // ── Ready/Generating state one-shot pulse ─────────────────────────────
    private IEnumerator PulseCoroutine()
    {
        float elapsed = 0f;
        float duration = 0.6f;
        Vector3 startScale = Vector3.one;
        Vector3 peakScale = Vector3.one * 1.3f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float factor = Mathf.Sin(t * Mathf.PI); // Pulse scale expands and retracts
            transform.localScale = Vector3.Lerp(startScale, peakScale, factor);
            yield return null;
        }
        transform.localScale = startScale;
    }

    // ── Screen safe area clamp ────────────────────────────────────────────
    private void SafeAreaClamp()
    {
        var rect = GetComponent<RectTransform>();
        if (rect == null) return;
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        Rect safeArea = Screen.safeArea;
        
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        
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
        
        // Clamping margin
        float margin = 10f;
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
                RectTransformUtility.ScreenPointToWorldPointInRectangle(rect, clampedCenterScreen, cam, out worldPoint);
            }
            rect.position = new Vector3(worldPoint.x, worldPoint.y, rect.position.z);
        }
    }
}
