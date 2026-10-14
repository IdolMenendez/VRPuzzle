using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.SceneManagement;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

public sealed class RuntimePuzzleHandInteractor : MonoBehaviour
{
    public bool leftHand;
    public float grabRadius = 0.11f;
    public Transform visual;
    [Header("V24 PICO/Swan native tracked-hand interaction")]
    public bool swanNativeHandsV24;
    [Min(0.02f)] public float pinchStartDistanceV24 = 0.045f;
    [Min(0.03f)] public float pinchReleaseDistanceV24 = 0.062f;
    [Header("V25 stable, forgiving direct interaction")]
    public bool groundedGuidedInteractionV25;
    [Min(0.05f)] public float trackingLossGraceV25 = 0.18f;
    [Header("V45 touch plus official pinch")]
    [Tooltip(
        "Use PICO's official pinch intent while retaining this hand's real " +
        "XR Hands pinch-point pose and collider proximity. Raw joint distance " +
        "is fallback-only when no official PICO hand device is available.")]
    public bool officialPicoPinchIntentV45;
    [Header("V46 per-hand OpenXR pinch integrity")]
    [Tooltip(
        "Use this hand's XR Hands thumb/index distance directly. This avoids " +
        "the V45 any-hand Input System signal and cannot be disabled merely " +
        "because an unrelated device exists.")]
    public bool xrHandsJointPinchV46;
    [Header("V47 shared OpenXR pinch provider")]
    public bool resilientRealHandPinchV47;
    [Tooltip(
        "V49 diagnostics. Log a level grab attempt and, when nothing is " +
        "grabbed, the nearest Part distance versus grabRadius so a worn run " +
        "shows why an in-level pinch missed. Cleanly removable.")]
    public bool pinchEdgeTelemetryV49 = true;
    [Header("V80 zero-allocation scene interaction")]
    [Tooltip(
        "Caches the active scene's Parts, console, rotator and retired hint " +
        "targets once per stage instead of calling FindObjectsByType from " +
        "both hand Update loops.")]
    public bool cacheSceneObjectsV80;
    readonly List<XRHandSubsystem> handSubsystems = new List<XRHandSubsystem>();
    readonly List<XRInputDevice> devices = new List<XRInputDevice>();
    XRHandSubsystem hands;
    XRInputDevice controller;
    RuntimePuzzlePiece held;
    RuntimePuzzleBaseRotator heldBase;
    RuntimeHintLampPullV13 heldLamp;
    bool wasPressed;
    bool nativeHandPinching;
    Pose lastNativeHandPose;
    float nativeHandLostAt = -1f;
    Vector3 trackedIndexTipWorldV25;
    bool trackedIndexTipValidV25;
    float nextScan;
    bool sceneCacheDirtyV80 = true;
    RuntimePuzzleStage cachedStageV80;
    RuntimePuzzlePiece[] cachedPiecesV80 =
        Array.Empty<RuntimePuzzlePiece>();
    RuntimePuzzleBaseRotator[] cachedRotatorsV80 =
        Array.Empty<RuntimePuzzleBaseRotator>();
    RuntimeAssemblyConsoleV61[] cachedConsolesV80 =
        Array.Empty<RuntimeAssemblyConsoleV61>();
    RuntimeHintCompassV36[] cachedCompassesV80 =
        Array.Empty<RuntimeHintCompassV36>();
    RuntimeHintLampPullV13[] cachedLampsV80 =
        Array.Empty<RuntimeHintLampPullV13>();
#if UNITY_EDITOR
    bool automationContactPinchV45;
#endif
    int HolderId => leftHand ? 101 : 202;

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoadedV80;
        SceneManager.sceneUnloaded += OnSceneUnloadedV80;
        sceneCacheDirtyV80 = true;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedV80;
        SceneManager.sceneUnloaded -= OnSceneUnloadedV80;
    }

    void OnSceneLoadedV80(Scene _, LoadSceneMode __) =>
        sceneCacheDirtyV80 = true;

    void OnSceneUnloadedV80(Scene _) =>
        sceneCacheDirtyV80 = true;

    void RefreshSceneCacheV80()
    {
        if (!cacheSceneObjectsV80)
            return;
        RuntimePuzzleStage stage =
            PersistentPuzzleBootstrap.Instance
                ? PersistentPuzzleBootstrap.Instance.CurrentStage
                : default;
        if (!sceneCacheDirtyV80 && stage == cachedStageV80)
            return;
        cachedStageV80 = stage;
        sceneCacheDirtyV80 = false;
        cachedPiecesV80 =
            FindObjectsByType<RuntimePuzzlePiece>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        cachedRotatorsV80 =
            FindObjectsByType<RuntimePuzzleBaseRotator>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        cachedConsolesV80 =
            FindObjectsByType<RuntimeAssemblyConsoleV61>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        cachedCompassesV80 =
            FindObjectsByType<RuntimeHintCompassV36>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        cachedLampsV80 =
            FindObjectsByType<RuntimeHintLampPullV13>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        Debug.Log(
            "[PPVR V80 Interaction Cache] stage=" + stage +
            " pieces=" + cachedPiecesV80.Length +
            " rotators=" + cachedRotatorsV80.Length +
            " consoles=" + cachedConsolesV80.Length +
            " legacyHints=" +
            (cachedCompassesV80.Length + cachedLampsV80.Length));
    }

    RuntimePuzzlePiece[] PiecesV80()
    {
        if (!cacheSceneObjectsV80)
            return FindObjectsByType<RuntimePuzzlePiece>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        RefreshSceneCacheV80();
        return cachedPiecesV80;
    }

    RuntimePuzzleBaseRotator[] RotatorsV80()
    {
        if (!cacheSceneObjectsV80)
            return FindObjectsByType<RuntimePuzzleBaseRotator>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        RefreshSceneCacheV80();
        return cachedRotatorsV80;
    }

    RuntimeAssemblyConsoleV61[] ConsolesV80()
    {
        if (!cacheSceneObjectsV80)
            return FindObjectsByType<RuntimeAssemblyConsoleV61>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        RefreshSceneCacheV80();
        return cachedConsolesV80;
    }

    RuntimeHintCompassV36[] CompassesV80()
    {
        if (!cacheSceneObjectsV80)
            return FindObjectsByType<RuntimeHintCompassV36>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        RefreshSceneCacheV80();
        return cachedCompassesV80;
    }

    RuntimeHintLampPullV13[] LampsV80()
    {
        if (!cacheSceneObjectsV80)
            return FindObjectsByType<RuntimeHintLampPullV13>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        RefreshSceneCacheV80();
        return cachedLampsV80;
    }

    // V52: this interactor lives on the persistent XR rig (DontDestroyOnLoad),
    // so it kept running its every-frame grab scan (and grab-miss logging)
    // during Splash/Selection, where nothing is grabbable and the head-aim +
    // pinch selection is what matters. On device that per-frame scan + Debug.Log
    // stuttered the Selection carousel and competed with selection. Idle the
    // interactor on those two home stages only; every other stage (the five
    // assembly levels, plus Completion free-play / Collection) is left untouched
    // so nothing that legitimately grabs is affected.
    static bool PuzzleGrabStageActiveV52()
    {
        PersistentPuzzleBootstrap bootstrap = PersistentPuzzleBootstrap.Instance;
        if (!bootstrap)
            return true;
        switch (bootstrap.CurrentStage)
        {
            case RuntimePuzzleStage.Splash:
            case RuntimePuzzleStage.Selection:
                return false;
            default:
                return true;
        }
    }

    void ReleaseAllHeldV52()
    {
        if (held) held.EndExternalGrab(HolderId);
        if (heldBase) heldBase.EndGrab(HolderId);
        if (heldLamp) heldLamp.EndExternalPull(HolderId);
        held = null;
        heldBase = null;
        heldLamp = null;
        wasPressed = false;
        if (visual) visual.gameObject.SetActive(false);
    }

    void Update()
    {
        // V52 stage gate: idle entirely outside the assembly stages so the
        // persistent interactor cannot stutter or interfere with Selection.
        if (!PuzzleGrabStageActiveV52())
        {
            if (held || heldBase || heldLamp || wasPressed)
                ReleaseAllHeldV52();
            return;
        }
#if UNITY_EDITOR
        if (automationContactPinchV45)
            return;
        bool providerAutomationV47 =
            resilientRealHandPinchV47 &&
            RuntimeRealHandPinchV47.Instance &&
            RuntimeRealHandPinchV47.Instance.AutomationActiveV47;
#else
        const bool providerAutomationV47 = false;
#endif
        if (RuntimeDesktopHeadNavigatorV01.DesktopEditorPreviewActive &&
            !providerAutomationV47)
        {
            if (held) held.EndExternalGrab(HolderId);
            if (heldBase) heldBase.EndGrab(HolderId);
            if (heldLamp) heldLamp.EndExternalPull(HolderId);
            held = null;
            heldBase = null;
            heldLamp = null;
            wasPressed = false;
            if (visual) visual.gameObject.SetActive(false);
            return;
        }
        if (PersistentPuzzleBootstrap.Instance && PersistentPuzzleBootstrap.Instance.Save.singleHandMode)
        {
            bool dominantLeft = PersistentPuzzleBootstrap.Instance.Save.handedness == 1;
            if (leftHand != dominantLeft)
            {
                if (held) held.EndExternalGrab(HolderId);
                if (heldBase) heldBase.EndGrab(HolderId);
                if (heldLamp) heldLamp.EndExternalPull(HolderId);
                held = null;
                heldBase = null;
                heldLamp = null;
                return;
            }
        }
        if (swanNativeHandsV24)
        {
            UpdateNativeSwanHandsV24();
            return;
        }
        bool pressed = TryReadHand(out Pose pose) || TryReadController(out pose);
        if (visual)
        {
            visual.position = pose.position;
            visual.rotation = pose.rotation;
            visual.gameObject.SetActive(!XRSettings.isDeviceActive || pressed);
        }
        if (pressed && !wasPressed) TryGrab(pose);
        if (pressed && heldBase) heldBase.MoveGrab(HolderId, pose);
        else if (pressed && held) held.MoveExternalGrab(HolderId, pose);
        if (!pressed && wasPressed)
        {
            if (held) held.EndExternalGrab(HolderId);
            if (heldBase) heldBase.EndGrab(HolderId);
            if (heldLamp) heldLamp.EndExternalPull(HolderId);
            held = null;
            heldBase = null;
            heldLamp = null;
        }
        wasPressed = pressed;
    }

    void UpdateNativeSwanHandsV24()
    {
        bool handTracked =
            TryReadNativeHandV24(out Pose pose, out bool handPressed);
        bool fingertipFed = false;
        // The shared pinch provider intentionally locks grabbing to the right
        // hand, but a console touch is not a pinch. Preserve the provider's
        // independent left index-tip path so either hand can still press V67.
        if (groundedGuidedInteractionV25 &&
            leftHand &&
            resilientRealHandPinchV47 &&
            RuntimeRealHandPinchV47.Instance &&
            RuntimeRealHandPinchV47.Instance.TryGetIndexTipWorldV59(
                true, out Vector3 leftTip))
        {
            bool consolePressed = TryFingertipConsolePressV67(leftTip);
            if (!consolePressed)
                TryFingertipLampTapAtV59(leftTip);
            fingertipFed = true;
        }
        if (handTracked)
        {
            lastNativeHandPose = pose;
            nativeHandLostAt = -1f;
            if (groundedGuidedInteractionV25 &&
                trackedIndexTipValidV25 &&
                !fingertipFed)
            {
                // V67: either hand can operate the recessed console. Legacy
                // compass/lamp fingertip targets remain left-hand-only.
                bool consolePressed =
                    TryFingertipConsolePressV67(trackedIndexTipWorldV25);
                if (leftHand && !consolePressed)
                    TryFingertipLampTapV25();
            }
        }
        else if (groundedGuidedInteractionV25 &&
                 wasPressed &&
                 (held || heldBase || heldLamp))
        {
            if (nativeHandLostAt < 0f)
                nativeHandLostAt = Time.unscaledTime;
            if (Time.unscaledTime - nativeHandLostAt <=
                trackingLossGraceV25)
            {
                handTracked = true;
                handPressed = true;
                pose = lastNativeHandPose;
            }
        }
        bool pressed = handTracked
            ? handPressed
            : TryReadController(out pose);
        if (handTracked)
            transform.SetPositionAndRotation(pose.position, pose.rotation);
        if (visual)
        {
            visual.position = pose.position;
            visual.rotation = pose.rotation;
            visual.gameObject.SetActive(handTracked || pressed);
        }
        if (pressed &&
            (!wasPressed ||
             (groundedGuidedInteractionV25 &&
              !held && !heldBase && !heldLamp)))
            TryGrabNativeV24(pose);
        if (pressed && heldLamp)
            heldLamp.MoveExternalPull(HolderId, pose);
        else if (pressed && heldBase)
            heldBase.MoveGrab(HolderId, pose);
        else if (pressed && held)
            held.MoveExternalGrab(HolderId, pose);
        if (!pressed && wasPressed)
            ReleaseNativeV24();
        wasPressed = pressed;
    }

    // Legacy left-hand compass/lamp fingertip targets. V67 level scenes disable
    // the retired lamp component and every collider below it, but keeping this
    // path supports older rollback scenes.
    void TryFingertipLampTapAtV59(Vector3 worldIndexTip)
    {
        foreach (RuntimeHintCompassV36 compass in CompassesV80())
        {
            if (compass.TryFingertipTouchV36(worldIndexTip))
                return;
        }
        foreach (RuntimeHintLampPullV13 lamp in LampsV80())
        {
            if (lamp.TryFingertipTapV25(worldIndexTip))
                return;
        }
    }

    void TryFingertipLampTapV25()
    {
        if (!trackedIndexTipValidV25)
            return;
        foreach (RuntimeHintCompassV36 compass in CompassesV80())
        {
            if (compass.TryFingertipTouchV36(trackedIndexTipWorldV25))
                return;
        }
        foreach (RuntimeHintLampPullV13 lamp in LampsV80())
        {
            if (lamp.TryFingertipTapV25(trackedIndexTipWorldV25))
                return;
        }
    }

    // V67: the tabletop console is intentionally independent of pinch and can
    // be touched by either tracked index fingertip.
    bool TryFingertipConsolePressV67(Vector3 worldIndexTip)
    {
        foreach (RuntimeAssemblyConsoleV61 console in ConsolesV80())
        {
            if (console.TryFingertipPressV61(worldIndexTip))
                return true;
        }
        return false;
    }

    void TryGrabNativeV24(Pose pose)
    {
        // V67: assembly parts always win a pinch candidate search. The retired
        // hint pull-bead used to be searched first and could invisibly steal a
        // grab from a nearby part even when its visuals were hidden.
        if (groundedGuidedInteractionV25 &&
            TryGrabPieceNative(pose))
            return;

        RuntimeHintLampPullV13 bestLamp = null;
        float lampDistance = float.MaxValue;
        foreach (RuntimeHintLampPullV13 lamp in LampsV80())
        {
            float distance = groundedGuidedInteractionV25
                ? lamp.ExternalGrabDistance(pose.position)
                : DistanceToColliders(lamp.transform, pose.position);
            float lampRadius = groundedGuidedInteractionV25
                ? Mathf.Max(grabRadius, lamp.externalGrabRadiusV25)
                : grabRadius;
            if (distance <= lampRadius &&
                distance < lampDistance)
            {
                bestLamp = lamp;
                lampDistance = distance;
            }
        }
        if (bestLamp &&
            bestLamp.BeginExternalPull(HolderId, pose))
        {
            heldLamp = bestLamp;
            PersistentPuzzleBootstrap.Instance
                ?.GetComponent<RuntimeXrOriginInvariantV25>()
                ?.BeginInteraction();
            return;
        }

        // V61 #8: the assembly turntable can be grabbed by EITHER hand to rotate
        // the in-progress model. Piece grabbing above already has priority
        // (TryGrabPieceNative returns first), so the right hand only grabs the
        // turntable when no piece is within reach — no conflict with picking up
        // parts. The left hand keeps the console; the turntable is shared.
        {
            RuntimePuzzleBaseRotator bestBase = null;
            float baseDistance = float.MaxValue;
            foreach (RuntimePuzzleBaseRotator rotator in RotatorsV80())
            {
                Transform anchor = rotator.gripAnchor
                    ? rotator.gripAnchor
                    : rotator.transform;
                float distance =
                    Vector3.Distance(pose.position, anchor.position);
                if (distance < baseDistance &&
                    rotator.CanGrab(HolderId, pose))
                {
                    bestBase = rotator;
                    baseDistance = distance;
                }
            }
            if (bestBase &&
                bestBase.BeginGrab(HolderId, pose))
            {
                heldBase = bestBase;
                PersistentPuzzleBootstrap.Instance
                    ?.GetComponent<RuntimeXrOriginInvariantV25>()
                    ?.BeginInteraction();
                RuntimeHaptics.Pulse(!leftHand, 0.28f, 0.055f);
                return;
            }
        }

        if (groundedGuidedInteractionV25)
            return;
        TryGrabPieceNative(pose);
    }

    bool TryGrabPieceNative(Pose pose)
    {
        RuntimePuzzlePiece best = null;
        float bestDistance = float.MaxValue;
        float bestCenterDistance = float.MaxValue;
        foreach (RuntimePuzzlePiece piece in PiecesV80())
        {
            // V67: V57 keeps the right hand as the only primary pinch. The
            // left interactor may only join a heavy piece after the right hand
            // already holds it; it cannot independently pick ordinary parts.
            if (leftHand &&
                resilientRealHandPinchV47 &&
                RuntimeRealHandPinchV47.Instance &&
                RuntimeRealHandPinchV47.Instance.rightHandOnlyV57 &&
                (!piece.requiresTwoHands || piece.ExternalHolderCount != 1))
                continue;
            float distance = DistanceToColliders(
                piece.transform,
                pose.position);
            float centerDistance =
                DistanceToColliderCenters(
                    piece.transform,
                    pose.position);
            if (distance <= grabRadius &&
                piece.CanExternalGrab(HolderId) &&
                (distance < bestDistance - 0.0001f ||
                 Mathf.Abs(distance - bestDistance) <= 0.0001f &&
                 centerDistance < bestCenterDistance))
            {
                best = piece;
                bestDistance = distance;
                bestCenterDistance = centerDistance;
            }
        }
        if (!best)
        {
            if (pinchEdgeTelemetryV49)
                Debug.Log(
                    "[PPVR V49 Puzzle] event=grab-miss hand=" +
                    (leftHand ? "left" : "right") +
                    " nearestSurface=" +
                    (bestDistance == float.MaxValue
                        ? "none"
                        : bestDistance.ToString("F3")) +
                    " grabRadius=" + grabRadius.ToString("F3") +
                    " palm=" + pose.position.ToString("F3"));
            return false;
        }
        held = best;
        held.BeginExternalGrab(HolderId, pose);
        RuntimeHaptics.Pulse(leftHand, 0.22f, 0.045f);
        RuntimeXrOriginInvariantV25 guard =
            PersistentPuzzleBootstrap.Instance
                ? PersistentPuzzleBootstrap.Instance
                    .GetComponent<RuntimeXrOriginInvariantV25>()
                : null;
        guard?.BeginInteraction();
        if (resilientRealHandPinchV47)
            Debug.Log(
                "[PPVR V47 Puzzle] event=grab-accepted hand=" +
                (leftHand ? "left" : "right") +
                " target=" + best.name);
        return true;
    }

    void ReleaseNativeV24()
    {
        string releasedName = held
            ? held.name
            : heldBase
                ? heldBase.name
                : heldLamp
                    ? heldLamp.name
                    : "none";
        if (held)
            held.EndExternalGrab(HolderId);
        if (heldBase)
            heldBase.EndGrab(HolderId);
        if (heldLamp)
            heldLamp.EndExternalPull(HolderId);
        held = null;
        heldBase = null;
        heldLamp = null;
        RuntimeXrOriginInvariantV25 guard =
            PersistentPuzzleBootstrap.Instance
                ? PersistentPuzzleBootstrap.Instance
                    .GetComponent<RuntimeXrOriginInvariantV25>()
                : null;
        guard?.EndInteraction();
        if (resilientRealHandPinchV47 && releasedName != "none")
            Debug.Log(
                "[PPVR V47 Puzzle] event=release-accepted hand=" +
                (leftHand ? "left" : "right") +
                " target=" + releasedName);
    }

    public void CancelInteractionV25()
    {
        ReleaseNativeV24();
        wasPressed = false;
        nativeHandPinching = false;
        nativeHandLostAt = -1f;
    }

    bool TryReadNativeHandV24(
        out Pose pose,
        out bool pressed)
    {
        pose = new Pose(transform.position, transform.rotation);
        pressed = false;
        if (resilientRealHandPinchV47 &&
            RuntimeRealHandPinchV47.Instance)
        {
            bool available = RuntimeRealHandPinchV47.Instance.TryGetHand(
                leftHand,
                out RuntimeRealHandPinchV47.HandState state);
            if (!available || !state.poseValid)
            {
                nativeHandPinching = false;
                trackedIndexTipValidV25 = false;
                return false;
            }
            Transform sharedTracking = NativeTrackingSpaceV24();
            trackedIndexTipWorldV25 = sharedTracking
                ? sharedTracking.TransformPoint(state.localPinchPose.position)
                : state.localPinchPose.position;
            trackedIndexTipValidV25 = true;
            pose = new Pose(
                trackedIndexTipWorldV25,
                sharedTracking
                    ? sharedTracking.rotation * state.localPinchPose.rotation
                    : state.localPinchPose.rotation);
            nativeHandPinching = state.pressed;
            pressed = state.pressed;
            return true;
        }
        ScanHands();
        XRHand hand = hands == null
            ? default
            : leftHand ? hands.leftHand : hands.rightHand;
        if (hands == null ||
            !hands.running ||
            !hand.isTracked ||
            !hand.GetJoint(XRHandJointID.Palm)
                .TryGetPose(out Pose palm) ||
            !hand.GetJoint(XRHandJointID.IndexTip)
                .TryGetPose(out Pose index) ||
            !hand.GetJoint(XRHandJointID.ThumbTip)
                .TryGetPose(out Pose thumb))
        {
            nativeHandPinching = false;
            trackedIndexTipValidV25 = false;
            return false;
        }

        Vector3 localPinch =
            (index.position + thumb.position) * 0.5f;
        Transform tracking = NativeTrackingSpaceV24();
        trackedIndexTipWorldV25 = tracking
            ? tracking.TransformPoint(index.position)
            : index.position;
        trackedIndexTipValidV25 = true;
        pose = new Pose(
            tracking
                ? tracking.TransformPoint(localPinch)
                : localPinch,
            tracking
                ? tracking.rotation * palm.rotation
                : palm.rotation);

        float pinchDistance =
            Vector3.Distance(index.position, thumb.position);
        if (xrHandsJointPinchV46)
        {
            nativeHandPinching = nativeHandPinching
                ? pinchDistance <=
                    Mathf.Max(
                        pinchStartDistanceV24,
                        pinchReleaseDistanceV24)
                : pinchDistance <= pinchStartDistanceV24;
        }
        else
        {
            bool officialTracked = false;
            bool officialPressed = false;
            bool officialAvailable =
                officialPicoPinchIntentV45 &&
                PicoSwanPinchSignalV40.TryReadIntentV44(
                    out officialTracked,
                    out officialPressed);
            nativeHandPinching = officialAvailable
                ? officialTracked && officialPressed
                : nativeHandPinching
                    ? pinchDistance <=
                        Mathf.Max(
                            pinchStartDistanceV24,
                            pinchReleaseDistanceV24)
                    : pinchDistance <= pinchStartDistanceV24;
        }
        pressed = nativeHandPinching;
        return true;
    }

#if UNITY_EDITOR
    public RuntimePuzzlePiece HeldPieceForAutomationV45 => held;

    public bool TryContactPinchForAutomationV45(Pose pose)
    {
        if (held || heldBase || heldLamp)
            return false;
        automationContactPinchV45 = true;
        if (TryGrabPieceNative(pose))
            return true;
        automationContactPinchV45 = false;
        return false;
    }

    public void MoveContactPinchForAutomationV45(Pose pose)
    {
        if (held)
            held.MoveExternalGrab(HolderId, pose);
    }

    public void ReleaseContactPinchForAutomationV45()
    {
        ReleaseNativeV24();
        wasPressed = false;
        nativeHandPinching = false;
        automationContactPinchV45 = false;
    }
#endif

    static Transform NativeTrackingSpaceV24()
    {
        PersistentPuzzleBootstrap bootstrap =
            PersistentPuzzleBootstrap.Instance;
        if (bootstrap &&
            bootstrap.xrOrigin &&
            bootstrap.xrOrigin.CameraFloorOffsetObject)
            return bootstrap.xrOrigin.CameraFloorOffsetObject.transform;
        return Camera.main && Camera.main.transform.parent
            ? Camera.main.transform.parent
            : null;
    }

    static float DistanceToColliders(
        Transform root,
        Vector3 worldPoint)
    {
        float best = Vector3.Distance(root.position, worldPoint);
        foreach (Collider collider in
                 root.GetComponentsInChildren<Collider>(true))
        {
            if (!collider || !collider.enabled)
                continue;
            Vector3 closest = collider.ClosestPoint(worldPoint);
            best = Mathf.Min(
                best,
                Vector3.Distance(worldPoint, closest));
        }
        return best;
    }

    static float DistanceToColliderCenters(
        Transform root,
        Vector3 worldPoint)
    {
        float best =
            Vector3.Distance(
                root.position,
                worldPoint);
        foreach (Collider collider in
                 root.GetComponentsInChildren<Collider>(true))
        {
            if (!collider || !collider.enabled)
                continue;
            best = Mathf.Min(
                best,
                Vector3.Distance(
                    collider.bounds.center,
                    worldPoint));
        }
        return best;
    }

    void TryGrab(Pose pose)
    {
        if (leftHand)
        {
            RuntimePuzzleBaseRotator bestBase = null;
            float baseDistance = float.MaxValue;
            foreach (RuntimePuzzleBaseRotator rotator in RotatorsV80())
            {
                Transform anchor = rotator.gripAnchor ? rotator.gripAnchor : rotator.transform;
                float d = Vector3.Distance(pose.position, anchor.position);
                if (d < baseDistance && rotator.CanGrab(HolderId, pose))
                {
                    bestBase = rotator;
                    baseDistance = d;
                }
            }
            if (bestBase && bestBase.BeginGrab(HolderId, pose))
            {
                heldBase = bestBase;
                RuntimeHaptics.Pulse(true, 0.28f, 0.055f);
                return;
            }
        }
        RuntimePuzzlePiece best = null;
        float distance = float.MaxValue;
        foreach (RuntimePuzzlePiece piece in PiecesV80())
        {
            float d = Vector3.Distance(pose.position, piece.transform.position);
            if (d <= grabRadius && d < distance && piece.CanExternalGrab(HolderId)) { best = piece; distance = d; }
        }
        if (!best) return;
        held = best;
        held.BeginExternalGrab(HolderId, pose);
        if (resilientRealHandPinchV47)
            Debug.Log(
                "[PPVR V47 Puzzle] event=grab-accepted hand=" +
                (leftHand ? "left" : "right") +
                " target=" + best.name);
        RuntimeHaptics.Pulse(leftHand, 0.22f, 0.045f);
    }

    bool TryReadHand(out Pose pose)
    {
        pose = new Pose(transform.position, transform.rotation);
        ScanHands();
        XRHand hand = hands == null ? default : leftHand ? hands.leftHand : hands.rightHand;
        if (hands == null || !hands.running || !hand.isTracked ||
            !hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palm) ||
            !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose index) ||
            !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumb)) return false;
        Transform tracking = Camera.main && Camera.main.transform.parent ? Camera.main.transform.parent : null;
        pose = new Pose(tracking ? tracking.TransformPoint(palm.position) : palm.position,
            tracking ? tracking.rotation * palm.rotation : palm.rotation);
        return Vector3.Distance(index.position, thumb.position) <= 0.032f;
    }

    bool TryReadController(out Pose pose)
    {
        pose = new Pose(transform.position, transform.rotation);
        if (!controller.isValid) ScanController();
        if (!controller.isValid || !controller.TryGetFeatureValue(XRCommonUsages.devicePosition, out Vector3 local)) return false;
        controller.TryGetFeatureValue(XRCommonUsages.deviceRotation, out Quaternion rotation);
        Transform tracking = Camera.main && Camera.main.transform.parent ? Camera.main.transform.parent : null;
        pose = new Pose(tracking ? tracking.TransformPoint(local) : local, tracking ? tracking.rotation * rotation : rotation);
        return (controller.TryGetFeatureValue(XRCommonUsages.gripButton, out bool grip) && grip) ||
            (controller.TryGetFeatureValue(XRCommonUsages.triggerButton, out bool trigger) && trigger);
    }

    void ScanHands()
    {
        if (Time.unscaledTime < nextScan && hands != null) return;
        nextScan = Time.unscaledTime + 1f;
        handSubsystems.Clear();
        SubsystemManager.GetSubsystems(handSubsystems);
        hands = handSubsystems.Find(value => value != null && value.running);
    }

    void ScanController()
    {
        devices.Clear();
        InputDeviceCharacteristics side = leftHand ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right;
        InputDevices.GetDevicesWithCharacteristics(side | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.TrackedDevice, devices);
        if (devices.Count > 0) controller = devices[0];
    }
}

public static class RuntimeHaptics
{
    public static void PulseAll(float amplitude, float duration) { Pulse(true, amplitude, duration); Pulse(false, amplitude, duration); }
    public static void Pulse(bool left, float amplitude, float duration)
    {
        var devices = new List<XRInputDevice>();
        InputDeviceCharacteristics side = left ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right;
        InputDevices.GetDevicesWithCharacteristics(side | InputDeviceCharacteristics.Controller, devices);
        foreach (XRInputDevice device in devices)
            if (device.TryGetHapticCapabilities(out HapticCapabilities capabilities) && capabilities.supportsImpulse)
                device.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), Mathf.Max(0.01f, duration));
    }
}
