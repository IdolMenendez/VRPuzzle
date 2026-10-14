using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Persistent, camera-locked comfort transition for additive VR stages.
/// The overlay deliberately belongs to the persistent bootstrap instead of
/// any streamed stage, and every transition ends by explicitly clearing it.
/// This prevents a stage-owned fade from being reparented into
/// DontDestroyOnLoad and obscuring all later scenes.
/// </summary>
public sealed class RuntimeComfortTransitionV01 : MonoBehaviour
{
    public Camera sceneCamera;
    [Min(0.20f)] public float coverSeconds = 0.42f;
    [Min(0.25f)] public float revealSeconds = 0.58f;
    [Min(0.04f)] public float coveredHoldSeconds = 0.14f;
    [Range(0.08f, 0.20f)] public float cameraDistance = 0.12f;
    [Range(0f, 0.30f)] public float goldPulseOpacity = 0.16f;
    public Color comfortColor = new Color(0.012f, 0.007f, 0.004f, 1f);
    public Color handoffGold = new Color(1f, 0.48f, 0.08f, 1f);

    Renderer overlayRenderer;
    Material overlayMaterial;
    Renderer goldRenderer;
    Material goldMaterial;
    Texture2D goldTexture;
    float alpha = 1f;
    float goldAlpha;
    Coroutine activeFade;
    RuntimePuzzleStage outgoingStage;
    RuntimePuzzleStage incomingStage;
    bool initialReveal;
    bool externalCoverLock;
    int fadeGeneration;

    public float Alpha => alpha;
    public bool IsCovered => alpha >= 0.995f;
    public bool IsClear => alpha <= 0.005f && goldAlpha <= 0.005f;
    public bool ExternalCoverLocked => externalCoverLock;

    void Awake()
    {
        if (!sceneCamera)
            sceneCamera = Camera.main;
        CreateOverlay();
        SetImmediate(true);
    }

    void LateUpdate()
    {
        if (!sceneCamera)
            sceneCamera = Camera.main;
        UpdateOverlayPose();
    }

    public void SetImmediate(bool covered)
    {
        fadeGeneration++;
        if (activeFade != null)
        {
            StopCoroutine(activeFade);
            activeFade = null;
        }
        if (externalCoverLock)
            covered = true;
        SetAlpha(covered ? 1f : 0f);
        SetGoldAlpha(0f);
    }

    public void SetExternalCoverLock(bool locked)
    {
        externalCoverLock = locked;
        if (locked)
            SetImmediate(true);
    }

    public void PrepareHandoff(
        RuntimePuzzleStage outgoing,
        RuntimePuzzleStage incoming,
        bool isInitialReveal)
    {
        outgoingStage = outgoing;
        incomingStage = incoming;
        initialReveal = isInitialReveal;
        handoffGold = incoming switch
        {
            RuntimePuzzleStage.Selection => new Color(1f, 0.43f, 0.055f, 1f),
            RuntimePuzzleStage.Cottage => new Color(1f, 0.50f, 0.11f, 1f),
            RuntimePuzzleStage.Ship => new Color(0.95f, 0.34f, 0.055f, 1f),
            RuntimePuzzleStage.Eiffel => new Color(1f, 0.58f, 0.13f, 1f),
            RuntimePuzzleStage.Completion => new Color(1f, 0.67f, 0.20f, 1f),
            RuntimePuzzleStage.Collection => new Color(1f, 0.48f, 0.07f, 1f),
            _ => new Color(1f, 0.44f, 0.08f, 1f),
        };
        Debug.Log(
            "[Personally Puzzled Handoff] prepared " +
            $"{outgoingStage}->{incomingStage} initial={initialReveal}",
            this);
    }

    public IEnumerator Cover()
    {
        yield return FadeTo(1f, coverSeconds, true);
    }

    public IEnumerator Reveal()
    {
        if (externalCoverLock)
        {
            SetImmediate(true);
            yield break;
        }
        yield return FadeTo(0f, revealSeconds, false);
    }

    public IEnumerator HoldCovered()
    {
        float began = Time.unscaledTime;
        float duration = Mathf.Max(0.04f, coveredHoldSeconds);
        while (Time.unscaledTime - began < duration)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - began) / duration);
            SetGoldAlpha(
                goldPulseOpacity *
                Mathf.Lerp(0.10f, 0.17f, Mathf.Sin(t * Mathf.PI)));
            yield return null;
        }
        SetGoldAlpha(goldPulseOpacity * 0.10f);
    }

    IEnumerator FadeTo(float target, float seconds, bool covering)
    {
        int generation = ++fadeGeneration;
        if (!overlayRenderer || !overlayMaterial)
            CreateOverlay();
        float start = alpha;
        float began = Time.unscaledTime;
        float duration = Mathf.Max(0.05f, seconds);
        while (Time.unscaledTime - began < duration)
        {
            if (generation != fadeGeneration)
                yield break;
            if (!covering && externalCoverLock)
            {
                SetImmediate(true);
                yield break;
            }
            float t = Mathf.Clamp01((Time.unscaledTime - began) / duration);
            float eased = t * t * (3f - 2f * t);
            SetAlpha(Mathf.Lerp(start, target, eased));
            float pulse = covering
                ? Mathf.Sin(t * Mathf.PI) * (1f - 0.25f * t)
                : Mathf.Sin(Mathf.Clamp01(t * 1.18f) * Mathf.PI) * (1f - t);
            if (!covering && initialReveal)
                pulse = Mathf.Max(pulse, (1f - eased) * 0.32f);
            SetGoldAlpha(goldPulseOpacity * Mathf.Max(0f, pulse));
            yield return null;
        }
        if (generation != fadeGeneration ||
            (!covering && externalCoverLock))
            yield break;
        SetAlpha(target);
        SetGoldAlpha(target >= 0.995f ? goldPulseOpacity * 0.10f : 0f);
        activeFade = null;
        if (target <= 0.005f)
            Debug.Log(
                "[Personally Puzzled Handoff] revealed " + incomingStage,
                this);
    }

    void CreateOverlay()
    {
        if (overlayRenderer)
            return;
        if (!sceneCamera)
            sceneCamera = Camera.main;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (!shader)
        {
            Debug.LogError(
                "[Personally Puzzled Transition] URP Unlit shader unavailable; " +
                "comfort transition cannot be created.",
                this);
            return;
        }

        GameObject overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
        overlay.name =
            "Persistent warm comfort transition · cleared after every stage";
        overlay.transform.SetParent(transform, false);
        Collider collider = overlay.GetComponent<Collider>();
        if (collider)
            Destroy(collider);
        overlayRenderer = overlay.GetComponent<Renderer>();
        overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
        overlayRenderer.receiveShadows = false;
        overlayRenderer.allowOcclusionWhenDynamic = false;

        overlayMaterial = new Material(shader)
        {
            name = "Runtime persistent warm comfort transition",
            renderQueue = 3995,
        };
        if (overlayMaterial.HasProperty("_Surface"))
            overlayMaterial.SetFloat("_Surface", 1f);
        if (overlayMaterial.HasProperty("_Blend"))
            overlayMaterial.SetFloat("_Blend", 0f);
        if (overlayMaterial.HasProperty("_SrcBlend"))
            overlayMaterial.SetFloat("_SrcBlend", 5f);
        if (overlayMaterial.HasProperty("_DstBlend"))
            overlayMaterial.SetFloat("_DstBlend", 10f);
        if (overlayMaterial.HasProperty("_ZWrite"))
            overlayMaterial.SetFloat("_ZWrite", 0f);
        if (overlayMaterial.HasProperty("_Cull"))
            overlayMaterial.SetFloat("_Cull", 0f);
        overlayMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        overlayRenderer.sharedMaterial = overlayMaterial;
        CreateGoldOverlay(overlay.transform.parent);
        UpdateOverlayPose();
    }

    void CreateGoldOverlay(Transform parent)
    {
        if (goldRenderer)
            return;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (!shader)
            return;
        GameObject gold = GameObject.CreatePrimitive(PrimitiveType.Quad);
        gold.name =
            "Persistent handoff memory flare · transition-only physical effect";
        gold.transform.SetParent(parent, false);
        Collider collider = gold.GetComponent<Collider>();
        if (collider)
            Destroy(collider);
        goldRenderer = gold.GetComponent<Renderer>();
        goldRenderer.shadowCastingMode = ShadowCastingMode.Off;
        goldRenderer.receiveShadows = false;
        goldRenderer.allowOcclusionWhenDynamic = false;
        goldMaterial = new Material(shader)
        {
            name = "Runtime persistent radial handoff flare",
            renderQueue = 3996,
        };
        if (goldMaterial.HasProperty("_Surface"))
            goldMaterial.SetFloat("_Surface", 1f);
        if (goldMaterial.HasProperty("_Blend"))
            goldMaterial.SetFloat("_Blend", 0f);
        if (goldMaterial.HasProperty("_SrcBlend"))
            goldMaterial.SetFloat("_SrcBlend", 5f);
        if (goldMaterial.HasProperty("_DstBlend"))
            goldMaterial.SetFloat("_DstBlend", 10f);
        if (goldMaterial.HasProperty("_ZWrite"))
            goldMaterial.SetFloat("_ZWrite", 0f);
        if (goldMaterial.HasProperty("_Cull"))
            goldMaterial.SetFloat("_Cull", 0f);
        goldMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        goldTexture = BuildRadialTexture();
        if (goldMaterial.HasProperty("_BaseMap"))
            goldMaterial.SetTexture("_BaseMap", goldTexture);
        if (goldMaterial.HasProperty("_MainTex"))
            goldMaterial.SetTexture("_MainTex", goldTexture);
        goldRenderer.sharedMaterial = goldMaterial;
        SetGoldAlpha(0f);
    }

    static Texture2D BuildRadialTexture()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(
            size,
            size,
            TextureFormat.RGBA32,
            false,
            true)
        {
            name = "Runtime handoff soft radial memory flare",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float core = 1f - Mathf.SmoothStep(0.04f, 0.92f, radius);
                float ring = 1f - Mathf.Abs(radius - 0.52f) / 0.28f;
                float value = Mathf.Clamp01(core * 0.82f + Mathf.Clamp01(ring) * 0.24f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, value * value);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    void UpdateOverlayPose()
    {
        if (!overlayRenderer || !sceneCamera)
            return;
        Transform overlay = overlayRenderer.transform;
        float distance = Mathf.Max(
            sceneCamera.nearClipPlane + 0.035f,
            cameraDistance);
        float height =
            2f * distance * Mathf.Tan(sceneCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        // OpenXR can report a per-eye camera aspect that is narrower than the
        // emulator's monoscopic mirror. Overscan both axes so translations,
        // lens asymmetry and the two submitted eyes never expose a hard edge.
        float width = height * Mathf.Max(2.35f, sceneCamera.aspect * 1.30f);
        overlay.SetPositionAndRotation(
            sceneCamera.transform.position +
            sceneCamera.transform.forward * distance,
            sceneCamera.transform.rotation);
        overlay.localScale = new Vector3(width, height * 1.22f, 1f);
        if (goldRenderer)
        {
            Transform gold = goldRenderer.transform;
            float goldDistance = Mathf.Max(
                sceneCamera.nearClipPlane + 0.030f,
                distance - 0.006f);
            gold.SetPositionAndRotation(
                sceneCamera.transform.position +
                sceneCamera.transform.forward * goldDistance,
                sceneCamera.transform.rotation);
            gold.localScale = new Vector3(width * 0.92f, height * 1.08f, 1f);
        }
    }

    void SetAlpha(float value)
    {
        alpha = Mathf.Clamp01(value);
        if (!overlayRenderer || !overlayMaterial)
            return;
        overlayRenderer.enabled = alpha > 0.001f;
        Color valueColor = comfortColor;
        valueColor.a = alpha;
        if (overlayMaterial.HasProperty("_BaseColor"))
            overlayMaterial.SetColor("_BaseColor", valueColor);
        if (overlayMaterial.HasProperty("_Color"))
            overlayMaterial.SetColor("_Color", valueColor);
    }

    void SetGoldAlpha(float value)
    {
        goldAlpha = Mathf.Clamp01(value);
        if (!goldRenderer || !goldMaterial)
            return;
        goldRenderer.enabled = goldAlpha > 0.001f;
        Color valueColor = handoffGold;
        valueColor.a = goldAlpha;
        if (goldMaterial.HasProperty("_BaseColor"))
            goldMaterial.SetColor("_BaseColor", valueColor);
        if (goldMaterial.HasProperty("_Color"))
            goldMaterial.SetColor("_Color", valueColor);
    }

    void OnDestroy()
    {
        if (overlayMaterial)
            Destroy(overlayMaterial);
        if (goldMaterial)
            Destroy(goldMaterial);
        if (goldTexture)
            Destroy(goldTexture);
    }
}
