using System;
using UnityEngine;

/// <summary>
/// V44 head-ray targeting shared by Splash and Selection. Target resolution
/// remains active when debug visuals are disabled, so the temporary line and
/// pinch pulse can be removed without changing interaction behavior.
/// </summary>
[DefaultExecutionOrder(-250)]
[DisallowMultipleComponent]
public sealed class RuntimeHeadRayDebugV44 : MonoBehaviour
{
    [Header("Targeting")]
    public Camera headCamera;
    public SplashWorkshopController splash;
    public SelectionExhibitInputV09 selectionInput;
    [Min(5f)] public float rayLength = 30f;
    [Min(0.01f)] public float interactionRadius = 0.10f;
    [Min(0f)] public float targetStableSeconds = 0.06f;
    [Min(0f)] public float targetRetentionSeconds = 0.14f;

    [Header("Temporary debug visuals")]
    public bool debugVisualsEnabled = true;
    [Min(0.001f)] public float rayWidth = 0.008f;
    public Color idleColor = new Color(1f, 0.78f, 0.32f, 0.58f);
    public Color targetColor = new Color(0.32f, 1f, 0.72f, 0.95f);
    public Color pinchColor = new Color(0.24f, 0.88f, 1f, 1f);

    LineRenderer rayLine;
    LineRenderer pinchRing;
    Material debugMaterial;
    SelectionExhibitTargetV09 pendingSelectionTarget;
    float pendingSelectionSince = -1f;
    float stableSelectionLastSeen = -1f;
    Vector3 hitPoint;

    public SelectionExhibitTargetV09 StableSelectionTargetV44
    {
        get;
        private set;
    }

    public bool SplashTargetedV44
    {
        get;
        private set;
    }

    public bool PinchingV44
    {
        get;
        private set;
    }

    public Vector3 HitPointV44 => hitPoint;

    void Awake()
    {
        EnsureVisuals();
    }

    void OnEnable()
    {
        pendingSelectionTarget = null;
        StableSelectionTargetV44 = null;
        pendingSelectionSince = -1f;
        stableSelectionLastSeen = -1f;
    }

    void OnDisable()
    {
        if (rayLine)
            rayLine.enabled = false;
        if (pinchRing)
            pinchRing.enabled = false;
    }

    void OnDestroy()
    {
        if (debugMaterial)
            Destroy(debugMaterial);
    }

    void Update()
    {
        ResolveCamera();
        if (!headCamera)
        {
            SetVisualsEnabled(false);
            return;
        }

        Vector3 origin = headCamera.transform.position;
        Vector3 direction = headCamera.transform.forward.normalized;
        SelectionExhibitTargetV09 rawSelectionTarget =
            ResolveTarget(origin, direction, out bool splashTarget, out hitPoint);
        SplashTargetedV44 = splashTarget;
        UpdateStableSelectionTarget(rawSelectionTarget);
        ReadPinch();
        UpdateVisuals(origin, direction);
    }

    public SelectionExhibitTargetV09 TargetForPinchV44()
    {
        return StableSelectionTargetV44;
    }

    void ResolveCamera()
    {
        Camera preferred = null;
        if (selectionInput && selectionInput.sceneCamera)
            preferred = selectionInput.sceneCamera;
        else if (splash && splash.sceneCamera)
            preferred = splash.sceneCamera;
        else
            preferred = Camera.main;
        if (preferred && preferred != headCamera)
            headCamera = preferred;
    }

    SelectionExhibitTargetV09 ResolveTarget(
        Vector3 origin,
        Vector3 direction,
        out bool splashTarget,
        out Vector3 resolvedHitPoint)
    {
        splashTarget = false;
        resolvedHitPoint = origin + direction * rayLength;
        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            Mathf.Max(0.01f, interactionRadius),
            direction,
            Mathf.Max(5f, rayLength),
            ~0,
            QueryTriggerInteraction.Collide);
        Array.Sort(
            hits,
            (first, second) =>
                first.distance.CompareTo(second.distance));

        SelectionExhibitTargetV09 best = null;
        float bestScore = float.PositiveInfinity;
        float bestDistance = float.PositiveInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (!hit.collider)
                continue;

            if (splash &&
                splash.logoRoot &&
                hit.collider.transform.IsChildOf(splash.logoRoot))
            {
                splashTarget = true;
                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    resolvedHitPoint = hit.point;
                }
            }

            SelectionExhibitTargetV09 target =
                hit.collider.GetComponentInParent<SelectionExhibitTargetV09>();
            if (!target || !target.enabled)
                continue;
            if (selectionInput &&
                selectionInput.targets != null &&
                Array.IndexOf(selectionInput.targets, target) < 0)
                continue;

            Vector3 targetCenter = hit.collider.bounds.center;
            Vector3 toCenter = targetCenter - origin;
            float forward = Vector3.Dot(toCenter, direction);
            if (forward <= 0f)
                continue;
            Vector3 perpendicular =
                toCenter - direction * forward;
            float score =
                perpendicular.sqrMagnitude /
                Mathf.Max(0.0001f, forward * forward);
            if (target.IsCurrent)
                score *= 0.82f;
            if (score >= bestScore)
                continue;
            best = target;
            bestScore = score;
            bestDistance = hit.distance;
            resolvedHitPoint = hit.point;
        }
        return best;
    }

    void UpdateStableSelectionTarget(
        SelectionExhibitTargetV09 rawTarget)
    {
        float now = Time.unscaledTime;
        if (rawTarget == StableSelectionTargetV44)
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
            if (StableSelectionTargetV44 &&
                stableSelectionLastSeen >= 0f &&
                now - stableSelectionLastSeen <= targetRetentionSeconds)
                return;
            StableSelectionTargetV44 = null;
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
            StableSelectionTargetV44 = pendingSelectionTarget;
            stableSelectionLastSeen = now;
            pendingSelectionTarget = null;
            pendingSelectionSince = -1f;
        }
    }

    void ReadPinch()
    {
        bool tracked;
        bool pressed;
        if (PicoSwanPinchSignalV40.TryReadIntentV44(
                out tracked,
                out pressed))
        {
            PinchingV44 = tracked && pressed;
            return;
        }
        PinchingV44 =
            PicoSwanPinchSignalV40.TryRead(
                out tracked,
                out pressed) &&
            tracked &&
            pressed;
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

        Transform ringTransform = transform.Find("V44 pinch debug ring");
        if (!ringTransform)
        {
            GameObject ringObject =
                new GameObject("V44 pinch debug ring");
            ringObject.transform.SetParent(transform, false);
            ringTransform = ringObject.transform;
        }
        pinchRing = ringTransform.GetComponent<LineRenderer>();
        if (!pinchRing)
            pinchRing = ringTransform.gameObject.AddComponent<LineRenderer>();
        ConfigureLine(pinchRing, true);
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
        renderer.textureMode = LineTextureMode.Stretch;
        renderer.widthMultiplier =
            loop ? rayWidth * 0.70f : rayWidth;
        if (!debugMaterial)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Sprites/Default");
            if (shader)
                debugMaterial = new Material(shader);
        }
        if (debugMaterial)
            renderer.sharedMaterial = debugMaterial;
    }

    void UpdateVisuals(
        Vector3 origin,
        Vector3 direction)
    {
        EnsureVisuals();
        SetVisualsEnabled(debugVisualsEnabled);
        if (!debugVisualsEnabled)
            return;

        bool hasTarget =
            SplashTargetedV44 ||
            StableSelectionTargetV44;
        Color color =
            PinchingV44
                ? pinchColor
                : hasTarget
                    ? targetColor
                    : idleColor;
        rayLine.positionCount = 2;
        rayLine.SetPosition(
            0,
            origin + direction * 0.22f);
        rayLine.SetPosition(1, hitPoint);
        rayLine.startColor = color;
        rayLine.endColor = color;
        rayLine.widthMultiplier =
            rayWidth *
            (PinchingV44 ? 2.4f : hasTarget ? 1.45f : 1f);

        pinchRing.enabled = PinchingV44;
        if (!PinchingV44)
            return;
        const int segments = 32;
        pinchRing.positionCount = segments;
        float pulse =
            0.085f +
            0.022f *
            (0.5f + 0.5f *
                Mathf.Sin(Time.unscaledTime * 18f));
        Vector3 right = headCamera.transform.right;
        Vector3 up = headCamera.transform.up;
        for (int index = 0; index < segments; index++)
        {
            float angle =
                index * Mathf.PI * 2f / segments;
            pinchRing.SetPosition(
                index,
                hitPoint +
                right * Mathf.Cos(angle) * pulse +
                up * Mathf.Sin(angle) * pulse);
        }
        pinchRing.startColor = pinchColor;
        pinchRing.endColor = pinchColor;
        pinchRing.widthMultiplier = rayWidth * 1.4f;
    }

    void SetVisualsEnabled(bool enabled)
    {
        if (rayLine)
            rayLine.enabled = enabled;
        if (pinchRing)
            pinchRing.enabled =
                enabled && PinchingV44;
    }
}
