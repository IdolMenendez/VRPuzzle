using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Hands;

/// <summary>
/// One head-directed intent ray for V46. Splash targets the complete visible
/// MODEL BUILDER bounds. Selection targets the visible exhibit model bounds.
/// Debug rendering is optional and does not own interaction state.
/// </summary>
[DefaultExecutionOrder(-260)]
[DisallowMultipleComponent]
public sealed class RuntimeIntentRayV46 : MonoBehaviour
{
    [Header("Scene binding")]
    public Camera headCamera;
    public SplashWorkshopController splash;
    public SelectionCarouselController selection;
    public SelectionExhibitTargetV09[] selectionTargets;

    [Header("Targeting")]
    [Min(5f)] public float rayLength = 30f;
    [Min(0f)] public float splashBoundsPadding = 0.12f;
    // V52: the worn test showed Splash targeting felt fine but Selection
    // "went green once then never again". Selection used a much tighter hit
    // pad (0.055) than Splash (0.12) and a very short retention (0.12s), so on
    // the carousel — with a head-aimed ray — a small head drift dropped the
    // bounds hit and the green target cleared almost immediately. Widen the
    // Selection hit band and hold the target longer so green is sticky and
    // forgiving, matching how Splash already feels.
    [Min(0f)] public float selectionBoundsPadding = 0.16f;
    [Min(0f)] public float targetStableSeconds = 0.05f;
    [Min(0f)] public float targetRetentionSeconds = 0.5f;
    [Tooltip(
        "V54: tolerance cone (degrees) for head-aim Selection. A model within " +
        "this look angle is selectable even if the ray does not pierce its " +
        "bounds, so looking toward a side carousel model selects it; the model " +
        "closest to the gaze wins. A direct bounds pierce still takes priority.")]
    [Range(4f, 40f)] public float selectionMaxAngleV54 = 22f;
    [Tooltip(
        "V55: physics layers the head-aim Selection cast is allowed to hit. " +
        "Point this at the exhibits' collider layer (SelectionExhibit) so the " +
        "cast ignores all environment geometry instead of testing everything.")]
    public LayerMask selectionHitLayersV55 = ~0;

    [Header("V46 OpenXR hand pinch")]
    [Tooltip(
        "Thumb/index distance is the guaranteed PICO OpenXR path. Official " +
        "Input System devices are accepted as an additional source, never as " +
        "a reason to disable XR Hands.")]
    public bool xrHandsJointPinchV46 = true;
    [Min(0.034f)] public float pinchStartDistanceV46 = 0.055f;
    [Min(0.045f)] public float pinchReleaseDistanceV46 = 0.075f;

    [Header("V47 shared real-hand provider")]
    [Tooltip(
        "Read the persistent V47 OpenXR hand-interaction/joint provider. " +
        "V46 scenes leave this disabled for rollback fidelity.")]
    public bool sharedRealHandPinchV47;

    [Header("Temporary debug visuals")]
    public bool debugVisualsEnabled = true;
    [Min(0.001f)] public float rayWidth = 0.008f;
    public Color idleColor = new Color(1f, 0.72f, 0.24f, 0.55f);
    public Color targetColor = new Color(0.28f, 1f, 0.68f, 0.96f);
    public Color pinchColor = new Color(0.20f, 0.84f, 1f, 1f);

    readonly Dictionary<Transform, Renderer[]> rendererCache =
        new Dictionary<Transform, Renderer[]>();
    readonly RaycastHit[] selectionHitBufferV55 = new RaycastHit[16];
    readonly List<XRHandSubsystem> handSubsystems =
        new List<XRHandSubsystem>();

    LineRenderer rayLine;
    readonly Transform[] pinchBars =
        new Transform[4];
    Material debugMaterial;
    Material pinchBarMaterial;
    SelectionExhibitTargetV09 pendingSelectionTarget;
    float pendingSelectionSince = -1f;
    float stableSelectionLastSeen = -1f;
    Vector3 hitPoint;
    XRHandSubsystem hands;
    float nextHandScan;
    bool leftJointPinching;
    bool rightJointPinching;
    string lastPinchSource = string.Empty;
#if UNITY_EDITOR
    bool automationInputV46;
    bool automationRayOnlyV47;
    bool automationTrackedV46;
    bool automationPressedV46;
    Ray automationRayV46;
#endif

    public bool SplashTargetedV46 { get; private set; }
    public SelectionExhibitTargetV09 StableSelectionTargetV46
    {
        get;
        private set;
    }
    public bool PinchTrackedV46 { get; private set; }
    public bool PinchingV46 { get; private set; }
    public Vector3 HitPointV46 => hitPoint;
    public string PinchSourceV46 { get; private set; } = "none";

    void Awake()
    {
        EnsureVisuals();
        RefreshSelectionTargetsV46();
    }

    void OnEnable()
    {
        pendingSelectionTarget = null;
        StableSelectionTargetV46 = null;
        pendingSelectionSince = -1f;
        stableSelectionLastSeen = -1f;
        leftJointPinching = false;
        rightJointPinching = false;
    }

    void OnDisable()
    {
        SetVisualsEnabled(false);
    }

    void OnDestroy()
    {
        if (debugMaterial)
            Destroy(debugMaterial);
        if (pinchBarMaterial)
            Destroy(pinchBarMaterial);
    }

    void Update()
    {
        ResolveCamera();
        if (!headCamera)
        {
            SplashTargetedV46 = false;
            StableSelectionTargetV46 = null;
            SetVisualsEnabled(false);
            return;
        }

        Ray ray;
#if UNITY_EDITOR
        ray = automationInputV46 || automationRayOnlyV47
            ? automationRayV46
            : new Ray(
                headCamera.transform.position,
                headCamera.transform.forward.normalized);
#else
        ray = new Ray(
            headCamera.transform.position,
            headCamera.transform.forward.normalized);
#endif
        SplashTargetedV46 = ResolveSplashTarget(
            ray,
            out float splashDistance);
        SelectionExhibitTargetV09 rawSelection =
            ResolveSelectionTarget(
                ray,
                out float selectionDistance);
        UpdateStableSelectionTarget(rawSelection);
        ReadPinch();

        float distance = Mathf.Min(
            splashDistance,
            selectionDistance);
        hitPoint = float.IsPositiveInfinity(distance)
            ? ray.GetPoint(Mathf.Max(5f, rayLength))
            : ray.GetPoint(distance);
        UpdateVisuals(ray);
    }

    public void RefreshSelectionTargetsV46()
    {
        if (!selection)
        {
            selectionTargets = Array.Empty<SelectionExhibitTargetV09>();
            return;
        }
        selectionTargets = FindObjectsByType<SelectionExhibitTargetV09>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(value =>
                value &&
                value.gameObject.scene == gameObject.scene &&
                value.carousel == selection)
            .OrderBy(value => value.exhibitIndex)
            .ToArray();
    }

    public bool ResolveSplashRayV46(
        Ray ray,
        out float distance)
    {
        return ResolveSplashTarget(ray, out distance);
    }

    public SelectionExhibitTargetV09 ResolveSelectionRayV46(
        Ray ray,
        out float distance)
    {
        return ResolveSelectionTarget(ray, out distance);
    }

    public bool TryGetSplashBoundsV46(out Bounds bounds)
    {
        return TryVisibleBounds(
            splash ? splash.logoRoot : null,
            out bounds);
    }

    public bool TryGetSelectionBoundsV46(
        SelectionExhibitTargetV09 target,
        out Bounds bounds)
    {
        bounds = default;
        if (!target ||
            !selection ||
            selection.exhibits == null ||
            target.exhibitIndex < 0 ||
            target.exhibitIndex >= selection.exhibits.Length)
            return false;
        return TryVisibleBounds(
            selection.exhibits[target.exhibitIndex],
            out bounds);
    }

    void ResolveCamera()
    {
        Camera preferred =
            PersistentPuzzleBootstrap.Instance &&
            PersistentPuzzleBootstrap.Instance.xrCamera
                ? PersistentPuzzleBootstrap.Instance.xrCamera
                : selection && selection.sceneCamera
                    ? selection.sceneCamera
                    : splash && splash.sceneCamera
                        ? splash.sceneCamera
                        : Camera.main;
        if (preferred && preferred != headCamera)
            headCamera = preferred;
    }

    bool ResolveSplashTarget(
        Ray ray,
        out float distance)
    {
        distance = float.PositiveInfinity;
        if (!splash ||
            !TryVisibleBounds(splash.logoRoot, out Bounds bounds))
            return false;
        bounds.Expand(Vector3.one * Mathf.Max(0f, splashBoundsPadding) * 2f);
        return bounds.IntersectRay(ray, out distance) &&
            distance >= 0f &&
            distance <= Mathf.Max(5f, rayLength);
    }

    SelectionExhibitTargetV09 ResolveSelectionTarget(
        Ray ray,
        out float distance)
    {
        distance = float.PositiveInfinity;
        if (!selection)
            return null;

        // V55: use Unity physics against the exhibits' box colliders instead of
        // manually unioning every child renderer's bounds each frame. The old
        // path walked ~296 renderers per frame (heavy on device and made worse
        // when V54 removed the early ray-miss reject), while every exhibit
        // already carries a BoxCollider. A sphere cast gives a forgiving hit so
        // looking near / just in front of a model still selects it; a straight
        // raycast is the exact-aim fallback. The nearest exhibit hit wins.
        float maxDistance = Mathf.Max(5f, rayLength);
        int count = Physics.SphereCastNonAlloc(
            ray,
            Mathf.Max(0.01f, selectionBoundsPadding),
            selectionHitBufferV55,
            maxDistance,
            selectionHitLayersV55,
            QueryTriggerInteraction.Collide);
        if (count == 0)
            count = Physics.RaycastNonAlloc(
                ray,
                selectionHitBufferV55,
                maxDistance,
                selectionHitLayersV55,
                QueryTriggerInteraction.Collide);

        SelectionExhibitTargetV09 best = null;
        for (int index = 0; index < count; index++)
        {
            RaycastHit hit = selectionHitBufferV55[index];
            if (!hit.collider)
                continue;
            SelectionExhibitTargetV09 target =
                hit.collider.GetComponentInParent<SelectionExhibitTargetV09>();
            if (!target ||
                !target.enabled ||
                target.carousel != selection)
                continue;
            // SphereCast reports distance 0 when the cast starts already
            // overlapping the collider; treat that as an immediate hit.
            float hitDistance = hit.distance <= 0f ? 0.001f : hit.distance;
            if (hitDistance >= distance)
                continue;
            best = target;
            distance = hitDistance;
        }
        return best;
    }

    bool TryVisibleBounds(
        Transform root,
        out Bounds bounds)
    {
        bounds = default;
        if (!root)
            return false;
        if (!rendererCache.TryGetValue(root, out Renderer[] renderers) ||
            renderers == null)
        {
            renderers = root.GetComponentsInChildren<Renderer>(true);
            rendererCache[root] = renderers;
        }
        bool initialized = false;
        foreach (Renderer renderer in renderers)
        {
            if (!renderer ||
                !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy ||
                renderer is LineRenderer ||
                renderer is ParticleSystemRenderer)
                continue;
            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return initialized;
    }

    void UpdateStableSelectionTarget(
        SelectionExhibitTargetV09 rawTarget)
    {
        float now = Time.unscaledTime;
        if (rawTarget == StableSelectionTargetV46)
        {
            if (rawTarget)
                stableSelectionLastSeen = now;
            pendingSelectionTarget = null;
            pendingSelectionSince = -1f;
            return;
        }

        if (!rawTarget)
        {
            pendingSelectionTarget = null;
            pendingSelectionSince = -1f;
            if (StableSelectionTargetV46 &&
                stableSelectionLastSeen >= 0f &&
                now - stableSelectionLastSeen <= targetRetentionSeconds)
                return;
            StableSelectionTargetV46 = null;
            return;
        }

        if (rawTarget != pendingSelectionTarget)
        {
            pendingSelectionTarget = rawTarget;
            pendingSelectionSince = now;
            return;
        }

        if (pendingSelectionSince >= 0f &&
            now - pendingSelectionSince >= targetStableSeconds)
        {
            StableSelectionTargetV46 = pendingSelectionTarget;
            stableSelectionLastSeen = now;
            pendingSelectionTarget = null;
            pendingSelectionSince = -1f;
        }
    }

    void ReadPinch()
    {
#if UNITY_EDITOR
        if (automationInputV46)
        {
            PinchTrackedV46 = automationTrackedV46;
            PinchingV46 = automationTrackedV46 && automationPressedV46;
            SetPinchSourceV46(PinchingV46 ? "automation-press" : "automation-release");
            return;
        }
#endif
        if (sharedRealHandPinchV47 &&
            RuntimeRealHandPinchV47.Instance)
        {
            RuntimeRealHandPinchV47.Instance.TryReadAny(
                out bool sharedAvailable,
                out bool sharedPressed);
            PinchTrackedV46 = sharedAvailable;
            PinchingV46 = sharedPressed;
            SetPinchSourceV46(
                sharedPressed
                    ? "v47-shared-press"
                    : "v47-shared-release");
            return;
        }
        bool officialAvailable =
            PicoSwanPinchSignalV40.TryReadIntentV44(
                out bool officialTracked,
                out bool officialPressed);
        if (!officialAvailable)
            officialAvailable = PicoSwanPinchSignalV40.TryRead(
                out officialTracked,
                out officialPressed);

        bool leftTracked = false;
        bool rightTracked = false;
        float leftDistance = float.PositiveInfinity;
        float rightDistance = float.PositiveInfinity;
        if (xrHandsJointPinchV46)
        {
            ScanHandsV46();
            if (hands != null && hands.running)
            {
                ReadJointPinchV46(
                    hands.leftHand,
                    ref leftJointPinching,
                    out leftTracked,
                    out leftDistance);
                ReadJointPinchV46(
                    hands.rightHand,
                    ref rightJointPinching,
                    out rightTracked,
                    out rightDistance);
            }
            else
            {
                leftJointPinching = false;
                rightJointPinching = false;
            }
        }

        bool jointTracked = leftTracked || rightTracked;
        bool jointPressed =
            leftTracked && leftJointPinching ||
            rightTracked && rightJointPinching;
        bool officialIntent =
            officialAvailable && officialTracked && officialPressed;
        PinchTrackedV46 =
            jointTracked ||
            officialAvailable && officialTracked;
        PinchingV46 =
            PinchTrackedV46 &&
            (jointPressed || officialIntent);
        SetPinchSourceV46(
            jointPressed
                ? "xr-hands-joints"
                : officialIntent
                    ? "pico-input-system"
                    : jointTracked
                        ? "xr-hands-release"
                        : officialAvailable && officialTracked
                            ? "pico-input-release"
                            : "none");
    }

    void ScanHandsV46()
    {
        if (hands != null && hands.running)
            return;
        if (Time.unscaledTime < nextHandScan)
            return;
        nextHandScan = Time.unscaledTime + 0.5f;
        handSubsystems.Clear();
        SubsystemManager.GetSubsystems(handSubsystems);
        hands = handSubsystems.Find(value =>
            value != null && value.running);
    }

    void ReadJointPinchV46(
        XRHand hand,
        ref bool pinching,
        out bool tracked,
        out float distance)
    {
        Pose index = default;
        Pose thumb = default;
        tracked =
            hand.isTracked &&
            hand.GetJoint(XRHandJointID.IndexTip)
                .TryGetPose(out index) &&
            hand.GetJoint(XRHandJointID.ThumbTip)
                .TryGetPose(out thumb);
        if (!tracked)
        {
            pinching = false;
            distance = float.PositiveInfinity;
            return;
        }
        distance = Vector3.Distance(index.position, thumb.position);
        float threshold = pinching
            ? Mathf.Max(pinchStartDistanceV46, pinchReleaseDistanceV46)
            : Mathf.Max(0.034f, pinchStartDistanceV46);
        pinching = distance <= threshold;
    }

    void SetPinchSourceV46(string source)
    {
        PinchSourceV46 = source;
        if (string.Equals(lastPinchSource, source, StringComparison.Ordinal))
            return;
        lastPinchSource = source;
        Debug.Log(
            "[Pinch Integrity V46] input source=" + source +
            " tracked=" + PinchTrackedV46 +
            " pressed=" + PinchingV46);
    }

    void EnsureVisuals()
    {
        if (!rayLine)
        {
            rayLine = GetComponent<LineRenderer>();
            if (!rayLine)
                rayLine = gameObject.AddComponent<LineRenderer>();
        }
        ConfigureLine(rayLine, false);

        if (!pinchBarMaterial)
        {
            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color");
            if (shader)
            {
                pinchBarMaterial =
                    new Material(shader)
                    {
                        name =
                            "V46 temporary pinch bracket material",
                        renderQueue = 4000,
                    };
                if (pinchBarMaterial.HasProperty("_ZWrite"))
                    pinchBarMaterial.SetInt("_ZWrite", 0);
                if (pinchBarMaterial.HasProperty("_ZTest"))
                    pinchBarMaterial.SetInt(
                        "_ZTest",
                        (int)UnityEngine.Rendering
                            .CompareFunction.Always);
                if (pinchBarMaterial.HasProperty("_Cull"))
                    pinchBarMaterial.SetInt(
                        "_Cull",
                        (int)UnityEngine.Rendering
                            .CullMode.Off);
            }
        }
        for (int index = 0;
             index < pinchBars.Length;
             index++)
        {
            string name =
                "V46 pinch debug solid bracket " +
                index.ToString("00");
            Transform bar =
                transform.Find(name);
            if (!bar)
            {
                GameObject barObject =
                    GameObject.CreatePrimitive(
                        PrimitiveType.Cube);
                barObject.name = name;
                barObject.transform.SetParent(
                    transform,
                    false);
                Collider collider =
                    barObject.GetComponent<Collider>();
                if (collider)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(collider);
                    else
                        Destroy(collider);
#else
                    Destroy(collider);
#endif
                }
                bar = barObject.transform;
            }
            MeshRenderer renderer =
                bar.GetComponent<MeshRenderer>();
            if (renderer)
            {
                renderer.sharedMaterial =
                    pinchBarMaterial;
                renderer.shadowCastingMode =
                    UnityEngine.Rendering
                        .ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;
            }
            pinchBars[index] = bar;
        }
    }

    void ConfigureLine(
        LineRenderer renderer,
        bool loop)
    {
        renderer.useWorldSpace = true;
        renderer.loop = loop;
        renderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.numCapVertices = 4;
        renderer.numCornerVertices = 2;
        renderer.widthMultiplier =
            loop ? rayWidth * 0.7f : rayWidth;
        if (!debugMaterial)
        {
            Shader shader =
                Shader.Find("Sprites/Default") ??
                Shader.Find("Universal Render Pipeline/Unlit");
            if (shader)
                debugMaterial = new Material(shader)
                {
                    name = "V46 temporary intent-ray material",
                };
        }
        if (debugMaterial)
        {
            debugMaterial.renderQueue = 4000;
            if (debugMaterial.HasProperty("_ZWrite"))
                debugMaterial.SetInt("_ZWrite", 0);
            if (debugMaterial.HasProperty("_ZTest"))
                debugMaterial.SetInt(
                    "_ZTest",
                    (int)UnityEngine.Rendering
                        .CompareFunction.Always);
            if (debugMaterial.HasProperty("_Color"))
                debugMaterial.SetColor(
                    "_Color",
                    Color.white);
            renderer.sharedMaterial = debugMaterial;
        }
    }

    void UpdateVisuals(Ray ray)
    {
        EnsureVisuals();
        SetVisualsEnabled(debugVisualsEnabled);
        if (!debugVisualsEnabled)
            return;

        bool hasTarget =
            SplashTargetedV46 ||
            StableSelectionTargetV46;
        Color color = PinchingV46
            ? pinchColor
            : hasTarget
                ? targetColor
                : idleColor;
        Vector3 visualHitPoint =
            hitPoint -
            ray.direction * 0.055f;
        float visualDistance =
            Vector3.Distance(
                ray.origin,
                visualHitPoint);
        float visualStartDistance =
            Mathf.Clamp(
                visualDistance * 0.35f,
                0.45f,
                1.20f);
        rayLine.positionCount = 2;
        rayLine.SetPosition(
            0,
            ray.GetPoint(visualStartDistance) +
            headCamera.transform.right * 0.018f -
            headCamera.transform.up * 0.014f);
        rayLine.SetPosition(1, visualHitPoint);
        rayLine.startColor = color;
        rayLine.endColor = color;
        rayLine.widthMultiplier =
            rayWidth * 0.34f *
            (PinchingV46 ? 1.40f : hasTarget ? 1.15f : 1f);

        foreach (Transform bar in
                 pinchBars)
        {
            MeshRenderer renderer =
                bar
                    ? bar.GetComponent<MeshRenderer>()
                    : null;
            if (renderer)
                renderer.enabled =
                    PinchingV46;
        }
        if (!PinchingV46)
            return;
        float radius =
            0.082f +
            0.020f *
            (0.5f + 0.5f *
                Mathf.Sin(Time.unscaledTime * 18f));
        Quaternion rotation =
            headCamera.transform.rotation;
        Vector3 right =
            rotation * Vector3.right;
        Vector3 up =
            rotation * Vector3.up;
        float barLength =
            radius * 0.84f;
        float thickness =
            Mathf.Max(0.004f, radius * 0.075f);
        SetSolidBracket(
            pinchBars[0],
            visualHitPoint + up * radius,
            rotation,
            new Vector3(
                barLength,
                thickness,
                thickness));
        SetSolidBracket(
            pinchBars[1],
            visualHitPoint - up * radius,
            rotation,
            new Vector3(
                barLength,
                thickness,
                thickness));
        SetSolidBracket(
            pinchBars[2],
            visualHitPoint - right * radius,
            rotation,
            new Vector3(
                thickness,
                barLength,
                thickness));
        SetSolidBracket(
            pinchBars[3],
            visualHitPoint + right * radius,
            rotation,
            new Vector3(
                thickness,
                barLength,
                thickness));
        if (pinchBarMaterial)
        {
            if (pinchBarMaterial.HasProperty("_BaseColor"))
                pinchBarMaterial.SetColor(
                    "_BaseColor",
                    pinchColor);
            if (pinchBarMaterial.HasProperty("_Color"))
                pinchBarMaterial.SetColor(
                    "_Color",
                    pinchColor);
        }
    }

    void SetSolidBracket(
        Transform bar,
        Vector3 position,
        Quaternion rotation,
        Vector3 worldScale)
    {
        if (!bar)
            return;
        bar.SetPositionAndRotation(
            position,
            rotation);
        ApplyWorldScale(
            bar,
            worldScale);
    }

    static void ApplyWorldScale(
        Transform target,
        Vector3 worldScale)
    {
        Transform parent = target.parent;
        if (!parent)
        {
            target.localScale = worldScale;
            return;
        }
        Vector3 parentScale =
            parent.lossyScale;
        target.localScale =
            new Vector3(
                DivideScale(
                    worldScale.x,
                    parentScale.x),
                DivideScale(
                    worldScale.y,
                    parentScale.y),
                DivideScale(
                    worldScale.z,
                    parentScale.z));
    }

    static float DivideScale(
        float value,
        float parentValue)
    {
        return Mathf.Abs(parentValue) <=
            0.00001f
                ? value
                : value / parentValue;
    }

    void SetVisualsEnabled(bool value)
    {
        if (rayLine)
            rayLine.enabled = value;
        foreach (Transform bar in
                 pinchBars)
        {
            MeshRenderer renderer =
                bar
                    ? bar.GetComponent<MeshRenderer>()
                    : null;
            if (renderer)
                renderer.enabled =
                    value && PinchingV46;
        }
    }

#if UNITY_EDITOR
    public void SetAutomationInputV46(
        Ray ray,
        bool tracked,
        bool pressed)
    {
        automationInputV46 = true;
        automationRayOnlyV47 = false;
        automationRayV46 = ray;
        automationTrackedV46 = tracked;
        automationPressedV46 = pressed;
    }

    public void ClearAutomationInputV46()
    {
        automationInputV46 = false;
        automationTrackedV46 = false;
        automationPressedV46 = false;
    }

    public void SetAutomationRayV47(Ray ray)
    {
        automationInputV46 = false;
        automationRayOnlyV47 = true;
        automationRayV46 = ray;
        automationTrackedV46 = false;
        automationPressedV46 = false;
    }

    public void ClearAutomationRayV47()
    {
        automationRayOnlyV47 = false;
    }

    public void ApplyDebugVisualForCaptureV46(
        Camera captureCamera,
        Vector3 targetPoint,
        bool pinching)
    {
        headCamera = captureCamera;
        hitPoint = targetPoint;
        SplashTargetedV46 = splash;
        PinchTrackedV46 = true;
        PinchingV46 = pinching;
        EnsureVisuals();
        UpdateVisuals(
            new Ray(
                captureCamera.transform.position,
                (targetPoint -
                 captureCamera.transform.position).normalized));
    }
#endif
}
