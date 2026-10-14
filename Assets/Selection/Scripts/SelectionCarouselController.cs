using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

/// <summary>
/// Near-field 1+5 physical exhibit carousel.  Tracked-hand swipes are the
/// primary input; controller sticks and desktop arrows provide full fallback.
/// Every swap follows a real quadratic arc with scale, light and trail handoff.
/// </summary>
public sealed class SelectionCarouselController : MonoBehaviour
{
    [Header("Exhibits")]
    public Transform[] exhibits;
    public Transform centralAnchor;
    public Transform[] candidateAnchors;
    public int currentIndex;
    public float centralScale = 1f;
    public float candidateScale = 0.62f;
    [Tooltip(
        "Optional per-exhibit visual normalization. V38 uses this to keep " +
        "differently authored models equally readable in the same cases.")]
    public float[] exhibitScaleMultipliersV38;
    [Tooltip(
        "V39 opt-in. Disable palm swipe, controller axis, keyboard, and touch " +
        "browsing. Only an explicit fresh pinch on a semantic exhibit target " +
        "may change the carousel.")]
    public bool targetedPinchOnlyV39;

    [Header("Transition")]
    public float transitionSeconds = 0.92f;
    public float arcHeight = 0.32f;
    [Tooltip("Opt-in normalized lead for the outgoing model. Zero preserves the V04 path exactly.")]
    [Min(0f)] public float outgoingReturnLead;
    [Tooltip("Opt-in normalized lead for the incoming model. Zero preserves the V04 path exactly.")]
    [Min(0f)] public float incomingCenterLead;
    [Tooltip("Scale-curve exponent. One preserves the V04 outgoing scale exactly; values below one shrink earlier.")]
    [Min(0.1f)] public float outgoingScaleExponent = 1f;
    [Tooltip("Scale-curve exponent. One preserves the V04 incoming scale exactly; values below one enlarge earlier.")]
    [Min(0.1f)] public float incomingScaleExponent = 1f;
    public LineRenderer outgoingTrail;
    public LineRenderer incomingTrail;
    [Tooltip("Optional additional render layers that follow the exact outgoing runtime curve.")]
    public LineRenderer[] outgoingTrailLayers;
    [Tooltip("Optional additional render layers that follow the exact incoming runtime curve.")]
    public LineRenderer[] incomingTrailLayers;
    public Light centralSpot;
    public Light incomingSpot;
    [Tooltip(
        "V41 opt-in. Keeps the central spotlight aimed at the visible top of " +
        "the current exhibit instead of its mid-body.")]
    public bool topTargetedCentralSpotV41;
    [Tooltip(
        "V41 authored central key lights that must share the same top target.")]
    public Light[] topTargetedSpotlightsV41;
    [Tooltip(
        "V43 dedicated follow spot. It is moved directly above the centred " +
        "exhibit and aimed straight down, so the pool lands on the model " +
        "instead of splashing across the room.")]
    public Light followSpotV43;
    [Min(0.2f)] public float followSpotHeightV43 = 1.6f;
    [Tooltip(
        "V44 optional visible/physical top-light mount. It follows the same " +
        "position as the dedicated spotlight and remains above the model.")]
    public Transform followSpotMountV44;
    public AudioSource transitionAudio;
    [Min(0f)] public float centralRestingIntensity = 4.2f;
    [Min(0f)] public float centralTransitionFloorIntensity = 1.35f;
    [Min(0f)] public float incomingTransitionPeakIntensity = 3.25f;
    [Min(0f)] public float commitHighlightBoost = 2.4f;

    [Header("Selection commit")]
    [Min(0.55f)] public float commitSeconds = 0.82f;
    [Min(0.08f)] public float commitTowardPlayer = 0.18f;
    [Min(1f)] public float commitScale = 1.12f;

    [Header("Versioned authored audio")]
    public AudioClip authoredAmbience;
    public AudioClip authoredTransition;

    [Header("Presence")]
    public Camera sceneCamera;
    public Transform previewHands;
    public float swipeVelocityThreshold = 0.52f;
    public float swipeDistanceThreshold = 0.115f;
    public float swipeCooldown = 0.55f;

    [Header("Editor reference keyframe")]
    [Range(-1f, 1f)] public float referenceTransitionT = -1f;
    public int referenceIncomingIndex = 1;

    readonly List<XRHandSubsystem> handSubsystems = new List<XRHandSubsystem>();
    readonly List<UnityEngine.XR.InputDevice> controllers = new List<UnityEngine.XR.InputDevice>();
    int[] slotByExhibit;
    XRHandSubsystem handSubsystem;
    Vector3[] startPositions;
    Quaternion[] startRotations;
    Vector3[] startScales;
    int outgoingIndex = -1;
    int incomingIndex = -1;
    int destinationCandidateSlot = -1;
    float transitionStarted = -1f;
    float nextSubsystemScan;
    float nextControllerScan;
    float nextSwipeAllowed;
    bool controllerAxisArmed;
    bool touchSwipeTracking;
    Vector2 touchSwipeStart;
    bool hasPalmSample;
    Vector3 lastPalmLocal;
    float lastPalmSampleTime;
    AudioSource ambienceAudio;
    float commitStarted = -1f;
    Transform commitExhibit;
    Vector3 commitStartPosition;
    Quaternion commitStartRotation;
    Vector3 commitStartScale;

    public bool IsTransitioning => transitionStarted >= 0f;
    public bool IsCommitting => commitStarted >= 0f;
    public Transform CurrentExhibit =>
        exhibits != null && exhibits.Length > 0
            ? exhibits[Mathf.Clamp(currentIndex, 0, exhibits.Length - 1)]
            : null;

    void Awake()
    {
        EnsureState();
        ApplyRestingLayout();
        CreateOriginalAudio();
        SetTrailSetEnabled(outgoingTrail, outgoingTrailLayers, false);
        SetTrailSetEnabled(incomingTrail, incomingTrailLayers, false);
    }

    void Update()
    {
        if (previewHands && XRSettings.isDeviceActive)
            previewHands.gameObject.SetActive(false);

        if (referenceTransitionT >= 0f)
        {
            ApplyReferenceTransition(currentIndex, Mathf.Clamp(referenceIncomingIndex, 0, exhibits.Length - 1), referenceTransitionT);
            return;
        }

        if (IsCommitting)
        {
            ApplyCommitPose();
            return;
        }

        if (IsTransitioning)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - transitionStarted) / Mathf.Max(0.1f, transitionSeconds));
            ApplySwapPose(t);
            if (t >= 1f)
                CompleteSwap();
            return;
        }

        if (targetedPinchOnlyV39)
            return;

        int direction = ReadDesktopDirection();
        if (direction == 0)
            direction = ReadTouchDirection();
        if (direction == 0 &&
            !RuntimeDesktopHeadNavigatorV01.DesktopEditorPreviewActive)
            direction = ReadControllerDirection();
        if (direction == 0 &&
            !RuntimeDesktopHeadNavigatorV01.DesktopEditorPreviewActive)
            direction = ReadTrackedHandDirection();
        if (direction != 0)
            BeginSwap(direction);
    }

    void LateUpdate()
    {
        if (topTargetedCentralSpotV41)
            ApplyTopTargetedCentralSpotV41();
    }

    public bool ApplyTopTargetedCentralSpotV41()
    {
        Transform exhibit = CurrentExhibit;
        if (!centralSpot || !exhibit)
            return false;

        // V43: the renderer list is cached per exhibit. The V41 version called
        // GetComponentsInChildren every LateUpdate, which allocated on every
        // frame and was a real cost on device.
        Renderer[] renderers =
            CachedRenderersV43(exhibit);
        bool hasBounds = false;
        Bounds visibleBounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (!renderer || !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy)
                continue;
            if (!hasBounds)
            {
                visibleBounds = renderer.bounds;
                hasBounds = true;
            }
            else
                visibleBounds.Encapsulate(renderer.bounds);
        }
        if (!hasBounds)
            return false;

        Vector3 target = new Vector3(
            visibleBounds.center.x,
            visibleBounds.max.y,
            visibleBounds.center.z);
        bool aimed = AimSpotAtTopV41(
            centralSpot,
            target);
        if (topTargetedSpotlightsV41 != null)
        {
            foreach (Light spotlight in
                     topTargetedSpotlightsV41)
                aimed |= AimSpotAtTopV41(
                    spotlight,
                    target);
        }

        // V43: the follow spot sits directly above the model and points
        // straight down, so the visible pool is on the exhibit itself.
        if (followSpotV43)
        {
            Vector3 followPosition =
                target +
                Vector3.up *
                Mathf.Max(0.2f, followSpotHeightV43);
            followSpotV43.transform.position =
                followPosition;
            followSpotV43.transform.rotation =
                Quaternion.LookRotation(
                    Vector3.down,
                    Vector3.forward);
            if (followSpotMountV44)
            {
                followSpotMountV44.position =
                    followPosition;
                followSpotMountV44.rotation =
                    Quaternion.identity;
            }
            aimed = true;
        }
        return aimed;
    }

    readonly Dictionary<Transform, Renderer[]> rendererCacheV43 =
        new Dictionary<Transform, Renderer[]>();

    Renderer[] CachedRenderersV43(Transform exhibit)
    {
        if (rendererCacheV43.TryGetValue(
                exhibit,
                out Renderer[] cached) &&
            cached != null)
            return cached;
        Renderer[] found =
            exhibit.GetComponentsInChildren<Renderer>(true);
        rendererCacheV43[exhibit] = found;
        return found;
    }

    static bool AimSpotAtTopV41(
        Light spotlight,
        Vector3 target)
    {
        if (!spotlight ||
            spotlight.type != LightType.Spot)
            return false;
        Vector3 direction =
            target - spotlight.transform.position;
        if (direction.sqrMagnitude < 0.0001f)
            return false;
        spotlight.transform.rotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);
        return true;
    }

    public void BeginSwap(int direction)
    {
        EnsureState();
        if (exhibits == null || exhibits.Length < 2)
            return;
        int next = (currentIndex + (direction >= 0 ? 1 : -1) + exhibits.Length) % exhibits.Length;
        BeginSwapToIndex(next);
    }

    public bool BeginSwapToIndex(int targetIndex)
    {
        EnsureState();
        if (IsTransitioning || IsCommitting ||
            exhibits == null || exhibits.Length < 2 ||
            targetIndex < 0 || targetIndex >= exhibits.Length ||
            targetIndex == currentIndex || !exhibits[targetIndex])
            return false;
        outgoingIndex = currentIndex;
        incomingIndex = targetIndex;
        destinationCandidateSlot = slotByExhibit[incomingIndex];
        if (destinationCandidateSlot <= 0)
        {
            outgoingIndex = incomingIndex = destinationCandidateSlot = -1;
            return false;
        }
        CacheStartPose();
        transitionStarted = Time.unscaledTime;
        SetTrailSetEnabled(outgoingTrail, outgoingTrailLayers, true);
        SetTrailSetEnabled(incomingTrail, incomingTrailLayers, true);
        PlayCarouselCue();
        return true;
    }

    public bool BeginCommit()
    {
        EnsureState();
        if (IsTransitioning || IsCommitting ||
            exhibits == null || exhibits.Length == 0)
            return false;
        int index = Mathf.Clamp(currentIndex, 0, exhibits.Length - 1);
        commitExhibit = exhibits[index];
        if (!commitExhibit)
            return false;
        commitStartPosition = commitExhibit.position;
        commitStartRotation = commitExhibit.rotation;
        commitStartScale = commitExhibit.localScale;
        commitStarted = Time.unscaledTime;
        return true;
    }

    public void ApplyEditorRestingLayout()
    {
        commitStarted = -1f;
        commitExhibit = null;
        EnsureState(true);
        ApplyRestingLayout();
    }

    public bool SelectIndexImmediately(int index)
    {
        EnsureState();
        if (exhibits == null || index < 0 || index >= exhibits.Length ||
            !exhibits[index] || IsTransitioning || IsCommitting)
            return false;
        int selectedSlot = slotByExhibit[index];
        int currentCenter = System.Array.FindIndex(
            slotByExhibit,
            value => value == 0);
        if (selectedSlot != 0)
        {
            if (currentCenter < 0)
                return false;
            slotByExhibit[currentCenter] = selectedSlot;
            slotByExhibit[index] = 0;
        }
        currentIndex = index;
        transitionStarted = -1f;
        outgoingIndex = incomingIndex = destinationCandidateSlot = -1;
        commitStarted = -1f;
        commitExhibit = null;
        ApplyRestingLayout();
        SetTrailSetEnabled(outgoingTrail, outgoingTrailLayers, false);
        SetTrailSetEnabled(incomingTrail, incomingTrailLayers, false);
        if (centralSpot) centralSpot.intensity = centralRestingIntensity;
        if (incomingSpot) incomingSpot.intensity = 0f;
        return true;
    }

    void ApplyCommitPose()
    {
        if (!commitExhibit)
        {
            commitStarted = -1f;
            return;
        }
        float t = Mathf.Clamp01(
            (Time.unscaledTime - commitStarted) /
            Mathf.Max(0.1f, commitSeconds));
        float eased = t * t * (3f - 2f * t);
        Vector3 towardPlayer = sceneCamera
            ? -sceneCamera.transform.forward
            : Vector3.back;
        float lift = Mathf.Sin(eased * Mathf.PI) * arcHeight * 0.32f;
        commitExhibit.position =
            commitStartPosition +
            towardPlayer * commitTowardPlayer * eased +
            Vector3.up * lift;
        commitExhibit.rotation =
            commitStartRotation *
            Quaternion.Euler(0f, 8f * eased, 0f);
        commitExhibit.localScale = Vector3.Lerp(
            commitStartScale,
            commitStartScale * commitScale,
            eased);
        if (centralSpot)
            centralSpot.intensity =
                centralRestingIntensity +
                Mathf.Sin(eased * Mathf.PI) * commitHighlightBoost;
    }

    public void ApplyReferenceTransition(int outgoing, int incoming, float t)
    {
        EnsureState(true);
        outgoing = Mathf.Clamp(outgoing, 0, exhibits.Length - 1);
        incoming = Mathf.Clamp(incoming, 0, exhibits.Length - 1);
        if (outgoing == incoming)
        {
            ApplyRestingLayout();
            return;
        }
        currentIndex = outgoing;
        for (int i = 0; i < slotByExhibit.Length; i++)
            slotByExhibit[i] = i;
        destinationCandidateSlot = slotByExhibit[incoming];
        outgoingIndex = outgoing;
        incomingIndex = incoming;
        ApplyRestingLayout();
        CacheStartPose();
        SetTrailSetEnabled(outgoingTrail, outgoingTrailLayers, true);
        SetTrailSetEnabled(incomingTrail, incomingTrailLayers, true);
        ApplySwapPose(Mathf.Clamp01(t));
    }

    void EnsureState(bool reset = false)
    {
        if (exhibits == null)
            exhibits = new Transform[0];
        if (slotByExhibit == null || slotByExhibit.Length != exhibits.Length || reset)
        {
            slotByExhibit = new int[exhibits.Length];
            for (int i = 0; i < slotByExhibit.Length; i++)
                slotByExhibit[i] = i;
            currentIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, exhibits.Length - 1));
            // The slot map is runtime-only. Reconstruct it from the
            // serialized current exhibit instead of silently putting exhibit
            // zero in the center while currentIndex points somewhere else.
            // That mismatch was the source of Selection models jumping and
            // the wrong model receiving the central scale after scene load.
            if (currentIndex > 0 &&
                currentIndex < slotByExhibit.Length)
            {
                slotByExhibit[0] = currentIndex;
                slotByExhibit[currentIndex] = 0;
            }
        }
        if (startPositions == null || startPositions.Length != exhibits.Length)
        {
            startPositions = new Vector3[exhibits.Length];
            startRotations = new Quaternion[exhibits.Length];
            startScales = new Vector3[exhibits.Length];
        }
    }

    void ApplyRestingLayout()
    {
        if (!centralAnchor || candidateAnchors == null)
            return;
        for (int i = 0; i < exhibits.Length; i++)
        {
            Transform exhibit = exhibits[i];
            if (!exhibit)
                continue;
            Transform anchor = AnchorForSlot(slotByExhibit[i]);
            if (!anchor)
                continue;
            exhibit.SetPositionAndRotation(anchor.position, anchor.rotation);
            exhibit.localScale =
                Vector3.one *
                (slotByExhibit[i] == 0
                    ? centralScale
                    : candidateScale) *
                ExhibitScaleMultiplierV38(i);
        }
    }

    Transform AnchorForSlot(int slot)
    {
        if (slot == 0)
            return centralAnchor;
        int candidate = slot - 1;
        return candidateAnchors != null && candidate >= 0 && candidate < candidateAnchors.Length
            ? candidateAnchors[candidate]
            : null;
    }

    void CacheStartPose()
    {
        for (int i = 0; i < exhibits.Length; i++)
        {
            startPositions[i] = exhibits[i].position;
            startRotations[i] = exhibits[i].rotation;
            startScales[i] = exhibits[i].localScale;
        }
    }

    void ApplySwapPose(float t)
    {
        if (outgoingIndex < 0 || incomingIndex < 0)
            return;
        float eased = t * t * (3f - 2f * t);
        bool useOptInShaping =
            outgoingReturnLead > 0.0001f ||
            incomingCenterLead > 0.0001f ||
            Mathf.Abs(outgoingScaleExponent - 1f) > 0.0001f ||
            Mathf.Abs(incomingScaleExponent - 1f) > 0.0001f;
        float outgoingT = useOptInShaping
            ? Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(
                    (t + Mathf.Max(0f, outgoingReturnLead)) / 0.82f))
            : eased;
        float incomingT = useOptInShaping
            ? Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(
                    (t - 0.12f + Mathf.Max(0f, incomingCenterLead)) / 0.88f))
            : eased;
        float outgoingScaleT = useOptInShaping
            ? Mathf.Pow(
                outgoingT,
                Mathf.Max(0.1f, outgoingScaleExponent))
            : eased;
        float incomingScaleT = useOptInShaping
            ? Mathf.Pow(
                incomingT,
                Mathf.Max(0.1f, incomingScaleExponent))
            : eased;
        Transform outgoing = exhibits[outgoingIndex];
        Transform incoming = exhibits[incomingIndex];
        Transform candidateAnchor = AnchorForSlot(destinationCandidateSlot);
        Vector3 lateral = Vector3.Cross(Vector3.up, candidateAnchor.position - centralAnchor.position).normalized;
        Vector3 outgoingControl = (startPositions[outgoingIndex] + candidateAnchor.position) * 0.5f +
                                  Vector3.up * (arcHeight * (useOptInShaping ? 0.78f : 1f)) +
                                  lateral * (useOptInShaping ? 0.30f : 0f);
        Vector3 incomingControl = (startPositions[incomingIndex] + centralAnchor.position) * 0.5f +
                                  Vector3.up * (arcHeight * (useOptInShaping ? 1.34f : 1.16f)) -
                                  lateral * (useOptInShaping ? 0.38f : 0f);
        outgoing.position = Quadratic(startPositions[outgoingIndex], outgoingControl, candidateAnchor.position, outgoingT);
        incoming.position = Quadratic(startPositions[incomingIndex], incomingControl, centralAnchor.position, incomingT);
        outgoing.rotation = Quaternion.Slerp(startRotations[outgoingIndex], candidateAnchor.rotation, outgoingT);
        incoming.rotation = Quaternion.Slerp(startRotations[incomingIndex], centralAnchor.rotation, incomingT);
        outgoing.localScale = Vector3.Lerp(
            startScales[outgoingIndex],
            Vector3.one *
                candidateScale *
                ExhibitScaleMultiplierV38(
                    outgoingIndex),
            outgoingScaleT);
        incoming.localScale = Vector3.Lerp(
            startScales[incomingIndex] * (useOptInShaping ? 0.86f : 1f),
            Vector3.one *
                centralScale *
                ExhibitScaleMultiplierV38(
                    incomingIndex),
            incomingScaleT);
        UpdateTrailSet(outgoingTrail, outgoingTrailLayers, startPositions[outgoingIndex], outgoingControl, outgoing.position, outgoingT);
        UpdateTrailSet(incomingTrail, incomingTrailLayers, startPositions[incomingIndex], incomingControl, incoming.position, incomingT);
        if (centralSpot)
            centralSpot.intensity = Mathf.Lerp(
                centralRestingIntensity,
                centralTransitionFloorIntensity,
                Mathf.Sin(eased * Mathf.PI));
        if (incomingSpot)
            incomingSpot.intensity = Mathf.Lerp(
                0f,
                incomingTransitionPeakIntensity,
                Mathf.SmoothStep(0.25f, 1f, eased));
    }

    void CompleteSwap()
    {
        int oldSlot = slotByExhibit[outgoingIndex];
        slotByExhibit[outgoingIndex] = destinationCandidateSlot;
        slotByExhibit[incomingIndex] = oldSlot;
        currentIndex = incomingIndex;
        transitionStarted = -1f;
        outgoingIndex = incomingIndex = destinationCandidateSlot = -1;
        ApplyRestingLayout();
        SetTrailSetEnabled(outgoingTrail, outgoingTrailLayers, false);
        SetTrailSetEnabled(incomingTrail, incomingTrailLayers, false);
        if (centralSpot) centralSpot.intensity = centralRestingIntensity;
        if (incomingSpot) incomingSpot.intensity = 0f;
    }

    float ExhibitScaleMultiplierV38(int exhibitIndex)
    {
        if (exhibitScaleMultipliersV38 == null ||
            exhibitIndex < 0 ||
            exhibitIndex >=
                exhibitScaleMultipliersV38.Length)
            return 1f;
        return Mathf.Clamp(
            exhibitScaleMultipliersV38[exhibitIndex],
            0.35f,
            4f);
    }

    int ReadDesktopDirection()
    {
        if (Keyboard.current == null || Time.unscaledTime < nextSwipeAllowed)
            return 0;
        if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
        {
            nextSwipeAllowed = Time.unscaledTime + swipeCooldown;
            return 1;
        }
        if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
        {
            nextSwipeAllowed = Time.unscaledTime + swipeCooldown;
            return -1;
        }
        return 0;
    }

    int ReadControllerDirection()
    {
        if (Time.unscaledTime < nextControllerScan)
            return 0;
        nextControllerScan = Time.unscaledTime + 0.08f;
        controllers.Clear();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.TrackedDevice, controllers);
        float strongestX = 0f;
        foreach (UnityEngine.XR.InputDevice device in controllers)
        {
            if ((device.TryGetFeatureValue(
                    UnityEngine.XR.CommonUsages.isTracked,
                    out bool tracked) && !tracked) ||
                !device.TryGetFeatureValue(
                    UnityEngine.XR.CommonUsages.primary2DAxis,
                    out Vector2 axis))
                continue;
            if (Mathf.Abs(axis.x) > Mathf.Abs(strongestX))
                strongestX = axis.x;
        }

        // A controller axis must return to neutral before it can request the
        // next carousel swap.  This prevents an emulator-retained/stuck axis
        // from continuously rotating the exhibits and making confirmation
        // appear broken.
        if (Mathf.Abs(strongestX) <= 0.35f)
        {
            controllerAxisArmed = true;
            return 0;
        }
        if (!controllerAxisArmed ||
            Time.unscaledTime < nextSwipeAllowed ||
            Mathf.Abs(strongestX) < 0.72f)
            return 0;
        controllerAxisArmed = false;
        nextSwipeAllowed = Time.unscaledTime + swipeCooldown;
        return strongestX > 0f ? 1 : -1;
    }

    int ReadTouchDirection()
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen == null)
            return 0;
        var touch = touchscreen.primaryTouch;
        if (touch.press.wasPressedThisFrame)
        {
            touchSwipeTracking = true;
            touchSwipeStart = touch.position.ReadValue();
            return 0;
        }
        if (!touchSwipeTracking)
            return 0;
        if (touch.press.wasReleasedThisFrame)
        {
            touchSwipeTracking = false;
            return 0;
        }
        if (!touch.press.isPressed || Time.unscaledTime < nextSwipeAllowed)
            return 0;

        Vector2 current = touch.position.ReadValue();
        float threshold = Mathf.Max(54f, Screen.width * 0.09f);
        float deltaX = current.x - touchSwipeStart.x;
        if (Mathf.Abs(deltaX) < threshold)
            return 0;
        touchSwipeTracking = false;
        nextSwipeAllowed = Time.unscaledTime + swipeCooldown;
        return deltaX > 0f ? 1 : -1;
    }

    int ReadTrackedHandDirection()
    {
        if ((handSubsystem == null || !handSubsystem.running) && Time.unscaledTime >= nextSubsystemScan)
        {
            nextSubsystemScan = Time.unscaledTime + 1f;
            handSubsystems.Clear();
            SubsystemManager.GetSubsystems(handSubsystems);
            handSubsystem = null;
            foreach (XRHandSubsystem candidate in handSubsystems)
            {
                if (candidate != null && candidate.running)
                {
                    handSubsystem = candidate;
                    break;
                }
            }
        }
        if (handSubsystem == null || !handSubsystem.running || !handSubsystem.leftHand.isTracked || !sceneCamera)
        {
            hasPalmSample = false;
            return 0;
        }
        if (!handSubsystem.leftHand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palmPose))
            return 0;
        Transform trackingSpace = sceneCamera.transform.parent ? sceneCamera.transform.parent : null;
        Vector3 world = trackingSpace ? trackingSpace.TransformPoint(palmPose.position) : palmPose.position;
        Vector3 local = sceneCamera.transform.InverseTransformPoint(world);
        float now = Time.unscaledTime;
        if (!hasPalmSample)
        {
            hasPalmSample = true;
            lastPalmLocal = local;
            lastPalmSampleTime = now;
            return 0;
        }
        float dt = Mathf.Max(0.001f, now - lastPalmSampleTime);
        float dx = local.x - lastPalmLocal.x;
        float velocity = dx / dt;
        lastPalmLocal = local;
        lastPalmSampleTime = now;
        if (now < nextSwipeAllowed || Mathf.Abs(dx) < swipeDistanceThreshold * 0.18f || Mathf.Abs(velocity) < swipeVelocityThreshold)
            return 0;
        nextSwipeAllowed = now + swipeCooldown;
        return velocity > 0f ? 1 : -1;
    }

    static Vector3 Quadratic(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    static void UpdateTrail(LineRenderer line, Vector3 start, Vector3 control, Vector3 end, float t)
    {
        if (!line)
            return;
        const int count = 24;
        line.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            float u = t * i / (count - 1f);
            line.SetPosition(i, Quadratic(start, control, end, u));
        }
    }

    static void UpdateTrailSet(
        LineRenderer primary,
        LineRenderer[] layers,
        Vector3 start,
        Vector3 control,
        Vector3 end,
        float t)
    {
        UpdateTrail(primary, start, control, end, t);
        if (layers == null)
            return;
        foreach (LineRenderer layer in layers)
            UpdateTrail(layer, start, control, end, t);
    }

    static void SetTrailSetEnabled(
        LineRenderer primary,
        LineRenderer[] layers,
        bool enabled)
    {
        if (primary)
            primary.enabled = enabled;
        if (layers == null)
            return;
        foreach (LineRenderer layer in layers)
            if (layer)
                layer.enabled = enabled;
    }

    void CreateOriginalAudio()
    {
        // The persistent conductor is the only full-game music/SFX authority.
        // Older stage-local procedural beds produced phase beating and the
        // siren-like impression reported during real play.  Keep the local
        // generator strictly as a standalone-scene fallback.
        if (RuntimeAudioRouter.Conductor)
            return;
        if (authoredAmbience && authoredTransition)
        {
            ambienceAudio = gameObject.AddComponent<AudioSource>();
            ambienceAudio.clip = authoredAmbience;
            ambienceAudio.loop = true;
            ambienceAudio.volume = 0.42f;
            ambienceAudio.spatialBlend = 0f;
            ambienceAudio.Play();
            if (!transitionAudio)
                transitionAudio = gameObject.AddComponent<AudioSource>();
            transitionAudio.clip = authoredTransition;
            transitionAudio.playOnAwake = false;
            transitionAudio.spatialBlend = 0.64f;
            transitionAudio.volume = 0.72f;
            return;
        }
        const int rate = 22050;
        const int ambienceSeconds = 12;
        float[] ambience = new float[rate * ambienceSeconds];
        float[] tones = { 146.83f, 220.00f, 277.18f, 329.63f, 440.00f };
        uint seed = 0x5A17u;
        float filteredNoise = 0f;
        for (int i = 0; i < ambience.Length; i++)
        {
            float t = i / (float)rate;
            float value = 0f;
            for (int note = 0; note < tones.Length; note++)
            {
                float breathing = 0.38f + 0.62f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 0.31f + note * 1.37f), 2f);
                value += Mathf.Sin(t * Mathf.PI * 2f * tones[note] + note * 0.73f) * breathing * (0.0065f / (1f + note * 0.34f));
            }
            seed = seed * 1664525u + 1013904223u;
            float noise = ((seed >> 8) / 16777216f) * 2f - 1f;
            filteredNoise = Mathf.Lerp(filteredNoise, noise, 0.0025f);
            ambience[i] = value + filteredNoise * 0.0028f;
        }
        AudioClip ambienceClip = AudioClip.Create("Original wonder rotunda nocturne", ambience.Length, 1, rate, false);
        ambienceClip.SetData(ambience, 0);
        ambienceAudio = gameObject.AddComponent<AudioSource>();
        ambienceAudio.clip = ambienceClip;
        ambienceAudio.loop = true;
        ambienceAudio.volume = 0.52f;
        ambienceAudio.spatialBlend = 0f;
        ambienceAudio.Play();

        if (!transitionAudio)
            transitionAudio = gameObject.AddComponent<AudioSource>();
        const float transitionLength = 0.92f;
        int count = Mathf.CeilToInt(rate * transitionLength);
        float[] transition = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            float envelope = Mathf.Sin(Mathf.Clamp01(t / transitionLength) * Mathf.PI);
            float glide = 392f + 196f * Mathf.SmoothStep(0f, 1f, t / transitionLength);
            transition[i] = (Mathf.Sin(t * Mathf.PI * 2f * glide) * 0.10f +
                             Mathf.Sin(t * Mathf.PI * 2f * glide * 1.5f) * 0.045f) * envelope;
        }
        AudioClip transitionClip = AudioClip.Create("Original golden carousel handoff", count, 1, rate, false);
        transitionClip.SetData(transition, 0);
        transitionAudio.clip = transitionClip;
        transitionAudio.playOnAwake = false;
        transitionAudio.spatialBlend = 0.65f;
        transitionAudio.volume = 0.62f;
    }

    void PlayCarouselCue()
    {
        if (RuntimeAudioRouter.Conductor)
        {
            RuntimeAudioRouter.PlayCue(
                RuntimeAudioCue.CarouselSwipe,
                CurrentExhibit ? CurrentExhibit.position : transform.position);
            return;
        }
        if (transitionAudio)
            transitionAudio.Play();
    }
}
