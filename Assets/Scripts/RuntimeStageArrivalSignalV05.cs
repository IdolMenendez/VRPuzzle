using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A small, world-anchored arrival flourish that begins while the persistent
/// comfort cover reveals a newly streamed stage.  The scene never moves around
/// the player's head; only these thin brass rings and motes animate at the
/// physical focus object, which keeps the handoff comfortable in VR.
/// </summary>
public sealed class RuntimeStageArrivalSignalV05 : MonoBehaviour
{
    public RuntimePuzzleStage stage;
    public Transform anchor;
    [Min(0.08f)] public float radius = 0.23f;
    [Min(0.35f)] public float duration = 0.92f;
    [Min(0f)] public float verticalOffset = 0.015f;
    public Color brassColor = new Color(1f, 0.48f, 0.075f, 1f);

    const int RingCount = 2;
    const int Segments = 96;
    readonly LineRenderer[] rings = new LineRenderer[RingCount];
    readonly Material[] materials = new Material[RingCount];
    Light bloom;
    ParticleSystem motes;
    bool began;
    float beganAt;
    float standaloneReadyAt;

    void Awake()
    {
        if (!anchor)
            anchor = transform;
        standaloneReadyAt = Time.unscaledTime + 0.18f;
        CreateRings();
        CreateBloom();
        SetVisible(false);
    }

    void Update()
    {
        if (!began)
        {
            PersistentPuzzleBootstrap bootstrap =
                PersistentPuzzleBootstrap.Instance;
            bool revealReachedFocus = bootstrap
                ? bootstrap.CurrentStage == stage &&
                  bootstrap.comfortTransition &&
                  bootstrap.comfortTransition.Alpha <= 0.78f
                : Time.unscaledTime >= standaloneReadyAt;
            if (!revealReachedFocus)
                return;
            began = true;
            beganAt = Time.unscaledTime;
            SetVisible(true);
            if (motes)
                motes.Play(true);
        }

        float t = Mathf.Clamp01(
            (Time.unscaledTime - beganAt) / Mathf.Max(0.35f, duration));
        float eased = t * t * (3f - 2f * t);
        float envelope = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 0.72f);
        Vector3 center = anchor
            ? anchor.position + Vector3.up * verticalOffset
            : transform.position;
        Camera camera = Camera.main;
        Vector3 planeRight = camera ? camera.transform.right : Vector3.right;
        Vector3 planeUp = camera ? camera.transform.up : Vector3.up;
        for (int ringIndex = 0; ringIndex < rings.Length; ringIndex++)
        {
            LineRenderer ring = rings[ringIndex];
            if (!ring)
                continue;
            float phase = ringIndex / (float)RingCount;
            float ringRadius = radius *
                Mathf.Lerp(0.42f + phase * 0.13f,
                    1.18f + phase * 0.22f,
                    eased);
            float y = center.y +
                Mathf.Sin((t + phase) * Mathf.PI) * 0.018f * (1f - t);
            for (int segment = 0; segment < Segments; segment++)
            {
                float angle = segment / (float)Segments * Mathf.PI * 2f +
                    phase * 0.38f;
                ring.SetPosition(
                    segment,
                    center +
                    planeRight * (Mathf.Cos(angle) * ringRadius) +
                    planeUp * (Mathf.Sin(angle) * ringRadius * 0.58f) +
                    planeUp * (y - center.y));
            }
            float alpha = envelope * (0.24f - ringIndex * 0.08f);
            Color color = brassColor;
            color.a = alpha;
            SetMaterialColor(materials[ringIndex], color);
            ring.widthMultiplier =
                Mathf.Lerp(0.0018f, 0.00055f, eased) *
                (1f - ringIndex * 0.14f);
        }
        if (bloom)
        {
            bloom.transform.position = center + Vector3.up * 0.04f;
            bloom.intensity = envelope * 0.34f;
            bloom.range = radius * Mathf.Lerp(1.35f, 1.95f, eased);
        }
        if (t >= 1f)
        {
            SetVisible(false);
            enabled = false;
        }
    }

    void CreateRings()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (!shader)
            return;
        for (int index = 0; index < RingCount; index++)
        {
            GameObject child = new GameObject(
                "V05 physical brass arrival orbit " + (index + 1));
            child.transform.SetParent(transform, false);
            LineRenderer ring = child.AddComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = Segments;
            ring.textureMode = LineTextureMode.Stretch;
            ring.alignment = LineAlignment.View;
            ring.numCapVertices = 4;
            ring.numCornerVertices = 2;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.sortingOrder = 34 + index;
            Material material = new Material(shader)
            {
                name = "Runtime V05 arrival brass orbit " + (index + 1),
                renderQueue = 3120 + index,
            };
            ConfigureTransparent(material);
            ring.sharedMaterial = material;
            rings[index] = ring;
            materials[index] = material;
        }
    }

    void CreateBloom()
    {
        GameObject child = new GameObject(
            "V05 physical arrival glow · focus only");
        child.transform.SetParent(transform, false);
        bloom = child.AddComponent<Light>();
        bloom.type = LightType.Point;
        bloom.color = brassColor;
        bloom.intensity = 0f;
        bloom.range = radius * 2f;
        bloom.shadows = LightShadows.None;
        bloom.renderMode = LightRenderMode.ForcePixel;
    }

    void CreateMotes()
    {
        GameObject child = new GameObject(
            "V05 restrained brass arrival motes");
        child.transform.SetParent(transform, false);
        child.transform.position = anchor
            ? anchor.position + Vector3.up * verticalOffset
            : transform.position;
        motes = child.AddComponent<ParticleSystem>();
        var main = motes.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.35f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.95f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.025f, 0.11f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.0035f, 0.009f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.35f, 0.025f, 0.82f),
            new Color(1f, 0.76f, 0.24f, 0.92f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 38;
        var emission = motes.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 24, 34),
        });
        var shape = motes.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * 0.72f;
        shape.radiusThickness = 0.34f;
        shape.rotation = new Vector3(90f, 0f, 0f);
        ParticleSystemRenderer renderer =
            motes.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = 38;
        if (materials[0])
            renderer.sharedMaterial = materials[0];
    }

    void SetVisible(bool value)
    {
        foreach (LineRenderer ring in rings)
            if (ring)
                ring.enabled = value;
        if (bloom)
            bloom.enabled = value;
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
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", 0f);
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
            material.SetColor("_EmissionColor", color * 2.2f);
    }

    void OnDestroy()
    {
        foreach (Material material in materials)
            if (material)
                Destroy(material);
    }
}
