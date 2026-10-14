using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Completes the physical Splash touch with a short cyan-to-brass energy arc
/// from the touched logo face into the assembled workshop mark.  It contains
/// no button or screen-space UI and finishes before the shared comfort cover.
/// </summary>
public sealed class RuntimeOpeningWakeCeremonyV05 : MonoBehaviour
{
    public SplashWorkshopController splash;
    public Transform touchCore;
    public Transform logoRoot;
    [Min(0.55f)] public float duration = 0.96f;

    const int ArcSegments = 48;
    LineRenderer arc;
    LineRenderer touchRing;
    Material arcMaterial;
    Material ringMaterial;
    Light brassBloom;
    bool began;
    float beganAt;

    void Awake()
    {
        if (!splash)
            splash = GetComponent<SplashWorkshopController>();
        if (!touchCore && splash)
            touchCore = splash.touchCore;
        if (!logoRoot && splash)
            logoRoot = splash.logoRoot;
        CreateVisuals();
        SetVisible(false);
    }

    void Update()
    {
        if (!began)
        {
            if (!splash || !splash.IsActivated)
                return;
            began = true;
            beganAt = Time.unscaledTime;
            SetVisible(true);
        }

        float t = Mathf.Clamp01(
            (Time.unscaledTime - beganAt) / Mathf.Max(0.55f, duration));
        float eased = t * t * (3f - 2f * t);
        float envelope = Mathf.Sin(t * Mathf.PI);
        Vector3 start = touchCore ? touchCore.position : transform.position;
        Vector3 end = logoRoot
            ? logoRoot.position + Vector3.up * 0.015f
            : transform.position;
        Camera camera = Camera.main;
        Vector3 right = camera ? camera.transform.right : Vector3.right;
        Vector3 up = camera ? camera.transform.up : Vector3.up;
        Vector3 control = Vector3.Lerp(start, end, 0.48f) +
            up * 0.15f + right * 0.035f;
        int visibleSegments = Mathf.Clamp(
            Mathf.CeilToInt(ArcSegments * Mathf.Min(1f, eased * 1.24f)),
            2,
            ArcSegments);
        arc.positionCount = visibleSegments;
        for (int index = 0; index < visibleSegments; index++)
        {
            float u = index / (float)(ArcSegments - 1);
            arc.SetPosition(index, Quadratic(start, control, end, u));
        }
        arc.widthMultiplier = Mathf.Lerp(0.009f, 0.0025f, eased);
        Color arcColor = Color.Lerp(
            new Color(0.10f, 0.92f, 1f, 1f),
            new Color(1f, 0.48f, 0.06f, 1f),
            Mathf.SmoothStep(0.10f, 0.82f, eased));
        arcColor.a = envelope * 0.92f;
        SetMaterialColor(arcMaterial, arcColor);

        float ringRadius = Mathf.Lerp(0.018f, 0.105f, eased);
        touchRing.positionCount = ArcSegments;
        for (int index = 0; index < ArcSegments; index++)
        {
            float angle = index / (float)ArcSegments * Mathf.PI * 2f;
            touchRing.SetPosition(
                index,
                start + right * (Mathf.Cos(angle) * ringRadius) +
                up * (Mathf.Sin(angle) * ringRadius));
        }
        touchRing.widthMultiplier = Mathf.Lerp(0.007f, 0.0015f, eased);
        Color ringColor = new Color(0.12f, 0.88f, 1f, envelope * 0.72f);
        SetMaterialColor(ringMaterial, ringColor);
        if (brassBloom)
        {
            brassBloom.transform.position = Vector3.Lerp(start, end, eased);
            brassBloom.color = Color.Lerp(
                new Color(0.08f, 0.80f, 1f),
                new Color(1f, 0.42f, 0.05f),
                eased);
            brassBloom.intensity = envelope * 1.85f;
            brassBloom.range = Mathf.Lerp(0.11f, 0.28f, eased);
        }
        if (t >= 1f)
        {
            SetVisible(false);
            enabled = false;
        }
    }

    void CreateVisuals()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (!shader)
            return;
        arc = CreateLine("V05 touch-to-logo physical wake arc", shader, out arcMaterial);
        arc.loop = false;
        touchRing = CreateLine("V05 fingertip wake ring", shader, out ringMaterial);
        touchRing.loop = true;
        GameObject lightObject = new GameObject("V05 wake energy travelling bloom");
        lightObject.transform.SetParent(transform, false);
        brassBloom = lightObject.AddComponent<Light>();
        brassBloom.type = LightType.Point;
        brassBloom.shadows = LightShadows.None;
        brassBloom.renderMode = LightRenderMode.ForcePixel;
    }

    LineRenderer CreateLine(
        string objectName,
        Shader shader,
        out Material material)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(transform, false);
        LineRenderer line = child.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 5;
        line.numCornerVertices = 3;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = 42;
        material = new Material(shader)
        {
            name = "Runtime " + objectName,
            renderQueue = 3140,
        };
        ConfigureTransparent(material);
        line.sharedMaterial = material;
        return line;
    }

    void SetVisible(bool visible)
    {
        if (arc)
            arc.enabled = visible;
        if (touchRing)
            touchRing.enabled = visible;
        if (brassBloom)
            brassBloom.enabled = visible;
    }

    static Vector3 Quadratic(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float oneMinus = 1f - t;
        return oneMinus * oneMinus * a +
               2f * oneMinus * t * b +
               t * t * c;
    }

    static void ConfigureTransparent(Material material)
    {
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 1f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", 5f);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", 10f);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    static void SetMaterialColor(Material material, Color color)
    {
        if (!material)
            return;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", color * 2.5f);
    }

    void OnDestroy()
    {
        if (arcMaterial)
            Destroy(arcMaterial);
        if (ringMaterial)
            Destroy(ringMaterial);
    }
}
