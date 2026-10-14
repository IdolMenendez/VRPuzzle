using System;
using System.Collections.Generic;
using ByteDance.PICO.XR;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR.Hands;
using UnityEngine.XR.OpenXR.Features.Interactions;

/// <summary>
/// V47's single real-device pinch authority. It consumes fresh XR Hands joint
/// updates without requiring the root-pose success flag, and also consumes the
/// OpenXR XR_EXT_hand_interaction analogue controls when that profile exists.
/// Screen gates, direct puzzle grabbing and completion portals all read this
/// same per-hand state.
/// </summary>
[DefaultExecutionOrder(-10020)]
[DisallowMultipleComponent]
public sealed class RuntimeRealHandPinchV47 : MonoBehaviour
{
    [Min(0.034f)] public float jointPressDistance = 0.055f;
    [Min(0.045f)] public float jointReleaseDistance = 0.075f;
    [Range(0.1f, 1f)] public float officialPressValue = 0.62f;
    [Range(0f, 0.9f)] public float officialReleaseValue = 0.34f;
    [Tooltip(
        "V58: the official value the hand must fall below to re-arm for the next " +
        "pinch. The worn log showed a light release only reaches ~0.42-0.48, so " +
        "a 0.5 re-arm gate forced the user to open the hand very wide before a " +
        "second pinch would register. 0.65 sits between the resting baseline " +
        "(~0.70, still no accidental press because press requires >=0.9) and a " +
        "real pinch (1.0), so a light release re-arms immediately.")]
    [Range(0.3f, 0.85f)] public float officialRearmValueV58 = 0.65f;
    [Tooltip(
        "V50: treat the pinch as a complete, system-like discrete action. The " +
        "XR_EXT_hand_interaction pinch value has a non-zero resting baseline " +
        "(~0.70) that a low level threshold caught, auto-firing on a merely " +
        "tracked, half-relaxed hand. When true, a press requires a genuine " +
        "open->close edge (the value must first drop to release, then rise to " +
        "press) so entering already-pinched or resting cannot trigger. An " +
        "exposed system boolean (indexPressed/pointerActivated) still forces " +
        "the press directly.")]
    public bool requireReleaseBeforePressV50 = true;
    [Tooltip(
        "V57: only the right hand drives pinch. The worn log showed both hands " +
        "tracked together for 212 frames and the combined AnyPressed let either " +
        "hand's jitter disturb the other, making selection unreliable. Locking " +
        "to one hand removes that cross-talk. Left-hand pinches are ignored " +
        "(the hand still tracks/renders; it just does not act).")]
    public bool rightHandOnlyV57 = true;
    [Min(0.03f)] public float freshSampleSeconds = 0.20f;
    [Min(0.2f)] public float telemetryInterval = 0.75f;
    public bool emitTelemetry = true;

    public struct HandState
    {
        public bool available;
        public bool pressed;
        public bool poseValid;
        public Pose localPinchPose;
        public float jointDistance;
        public float officialValue;
        public string source;
        public float sampleAge;
    }

    sealed class MutableHand
    {
        public bool left;
        public bool jointValid;
        public bool jointPressed;
        public Pose jointPose;
        public Vector3 indexTipLocalV60;
        public float jointDistance = float.PositiveInfinity;
        public float jointTime = float.NegativeInfinity;
        public bool palmValid;
        public Pose palmPose;
        public float palmTime = float.NegativeInfinity;
        public bool officialPresent;
        public bool officialTracked;
        public bool officialPressed;
        public bool officialDigital;
        public bool officialPoseValid;
        public Pose officialPose;
        public float officialValue;
        public string officialSource = string.Empty;
        // V50 complete-action edge state (covers both the official pinch value
        // and the joint-distance fallback at the combined level). pressArmed is
        // set once the hand is seen released; a press is only latched into
        // pressLatched on a rising edge while armed, so a hand that enters or
        // rests already-closed cannot auto-fire.
        public bool pressArmed;
        public bool pressLatched;
        public HandState combined;
        public bool previousAvailable;
        public bool previousPressed;
        public string previousSource = string.Empty;
    }

    public static RuntimeRealHandPinchV47 Instance { get; private set; }

    readonly List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();
    readonly MutableHand left = new MutableHand { left = true };
    readonly MutableHand right = new MutableHand { left = false };
    XRHandSubsystem hands;
    float nextSubsystemScan;
    float nextTelemetry;
    float nextPicoModePoll;
    int picoActiveInputMode = -1;
#if UNITY_EDITOR
    bool automationActiveV47;
    HandState automationLeftV47;
    HandState automationRightV47;
#endif

    public HandState Left => left.combined;
    public HandState Right => right.combined;
    public bool AnyAvailable =>
        rightHandOnlyV57
            ? right.combined.available
            : left.combined.available || right.combined.available;
    public bool AnyPressed =>
        rightHandOnlyV57
            ? right.combined.available && right.combined.pressed
            : left.combined.available && left.combined.pressed ||
              right.combined.available && right.combined.pressed;

    void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnEnable()
    {
        ScanSubsystem(true);
        ReadOfficialDevices();
        RefreshCombined(left);
        RefreshCombined(right);
    }

    void OnDisable()
    {
        Subscribe(null);
        if (Instance == this)
            Instance = null;
    }

    void OnDestroy()
    {
        Subscribe(null);
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
#if UNITY_EDITOR
        if (automationActiveV47)
        {
            left.combined = automationLeftV47;
            right.combined = automationRightV47;
            LogChanges(left);
            LogChanges(right);
            return;
        }
#endif
        ScanSubsystem(false);
        PollPicoActiveInputMode();
        ReadOfficialDevices();
        RefreshCombined(left);
        RefreshCombined(right);
        LogChanges(left);
        LogChanges(right);
        if (emitTelemetry && Time.unscaledTime >= nextTelemetry)
        {
            nextTelemetry = Time.unscaledTime + Mathf.Max(0.2f, telemetryInterval);
            Debug.Log(
                "[PPVR V47 Pinch] event=sample subsystem=" +
                (hands != null ? hands.subsystemDescriptor.id : "none") +
                " running=" + (hands != null && hands.running) +
                " picoMode=" + picoActiveInputMode +
                " left=" + Describe(left.combined) +
                " right=" + Describe(right.combined));
        }
    }

    public bool TryReadAny(out bool available, out bool pressed)
    {
        available = AnyAvailable;
        pressed = AnyPressed;
        return available;
    }

    public bool TryGetHand(
        bool leftHand,
        out HandState state)
    {
        // V67: expose the tracked left state to the per-hand puzzle interactor
        // even when global discrete actions remain right-hand-only. The puzzle
        // interactor itself restricts this left state to joining an already
        // right-held two-hand part; without it every heavy module is impossible.
        state = leftHand ? left.combined : right.combined;
        return state.available;
    }

    // V59/V60: index-tip world position for either hand, independent of the
    // right-hand-only pinch lock. The bell tap is not a pinch, so the LEFT hand
    // must be able to tap it even while the right hand owns pinch. V60 fixes:
    // use the real IndexTip joint (not the thumb/index midpoint) and convert it
    // through the SAME tracking space the grab interactor uses (the XR Origin
    // camera-floor-offset object, not Camera.main's parent). On this rig those
    // two transforms differ, which is why the V59 tap landed off the bell.
    public bool TryGetIndexTipWorldV59(bool leftHand, out Vector3 world)
    {
        world = default;
        MutableHand target = leftHand ? left : right;
        if (!target.jointValid ||
            Time.unscaledTime - target.jointTime >
                Mathf.Max(0.03f, freshSampleSeconds))
            return false;
        Transform tracking = NativeTrackingSpaceV60();
        world = tracking
            ? tracking.TransformPoint(target.indexTipLocalV60)
            : target.indexTipLocalV60;
        return true;
    }

    static Transform NativeTrackingSpaceV60()
    {
        PersistentPuzzleBootstrap bootstrap = PersistentPuzzleBootstrap.Instance;
        if (bootstrap &&
            bootstrap.xrOrigin &&
            bootstrap.xrOrigin.CameraFloorOffsetObject)
            return bootstrap.xrOrigin.CameraFloorOffsetObject.transform;
        Camera camera = Camera.main;
        return camera && camera.transform.parent
            ? camera.transform.parent
            : null;
    }

    public bool TryGetPalm(bool leftHand, out Pose localPalmPose)
    {
        MutableHand target = leftHand ? left : right;
        bool fresh =
            target.palmValid &&
            Time.unscaledTime - target.palmTime <=
                Mathf.Max(0.03f, freshSampleSeconds);
        localPalmPose = fresh ? target.palmPose : default;
        return fresh;
    }

    void ScanSubsystem(bool immediate)
    {
        if (!immediate &&
            hands != null &&
            hands.running &&
            Time.unscaledTime < nextSubsystemScan)
            return;
        if (!immediate && Time.unscaledTime < nextSubsystemScan)
            return;
        nextSubsystemScan = Time.unscaledTime + 0.50f;
        subsystems.Clear();
        SubsystemManager.GetSubsystems(subsystems);
        XRHandSubsystem running = subsystems.Find(value =>
            value != null && value.running);
        if (running != hands)
            Subscribe(running);
    }

    void Subscribe(XRHandSubsystem next)
    {
        if (hands != null)
            hands.updatedHands -= OnUpdatedHands;
        hands = next;
        if (hands != null)
        {
            hands.updatedHands += OnUpdatedHands;
            Debug.Log(
                "[PPVR V47 Pinch] event=subsystem-bound id=" +
                hands.subsystemDescriptor.id +
                " running=" + hands.running);
        }
    }

    void OnUpdatedHands(
        XRHandSubsystem subsystem,
        XRHandSubsystem.UpdateSuccessFlags flags,
        XRHandSubsystem.UpdateType updateType)
    {
#if UNITY_EDITOR
        if (automationActiveV47)
            return;
#endif
        if (updateType != XRHandSubsystem.UpdateType.Dynamic)
            return;
        if ((flags & XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints) != 0)
        {
            ReadPalmSample(subsystem.leftHand, left);
            ReadJointSample(subsystem.leftHand, left);
        }
        if ((flags & XRHandSubsystem.UpdateSuccessFlags.RightHandJoints) != 0)
        {
            ReadPalmSample(subsystem.rightHand, right);
            ReadJointSample(subsystem.rightHand, right);
        }
        RefreshCombined(left);
        RefreshCombined(right);
    }

    void ReadPalmSample(XRHand hand, MutableHand target)
    {
        if (!hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palm))
            return;
        target.palmValid = true;
        target.palmPose = palm;
        target.palmTime = Time.unscaledTime;
    }

    void ReadJointSample(XRHand hand, MutableHand target)
    {
        bool indexValid = hand.GetJoint(XRHandJointID.IndexTip)
            .TryGetPose(out Pose index);
        bool thumbValid = hand.GetJoint(XRHandJointID.ThumbTip)
            .TryGetPose(out Pose thumb);
        bool palmValid = hand.GetJoint(XRHandJointID.Palm)
            .TryGetPose(out Pose palm);
        if (!indexValid || !thumbValid)
            return;

        bool hadJointPose = target.jointValid;
        target.jointValid = true;
        target.jointTime = Time.unscaledTime;
        target.indexTipLocalV60 = index.position;
        target.jointDistance = Vector3.Distance(
            index.position,
            thumb.position);
        float threshold = target.jointPressed
            ? Mathf.Max(jointPressDistance, jointReleaseDistance)
            : Mathf.Max(0.034f, jointPressDistance);
        target.jointPressed = target.jointDistance <= threshold;
        Vector3 position = (index.position + thumb.position) * 0.5f;
        Quaternion rotation = palmValid
            ? palm.rotation
            : hadJointPose
                ? target.jointPose.rotation
                : Quaternion.identity;
        target.jointPose = new Pose(position, rotation);
    }

    void ReadOfficialDevices()
    {
        ResetOfficial(left);
        ResetOfficial(right);
        ReadCommonGestures(left);
        ReadCommonGestures(right);
        foreach (InputDevice device in InputSystem.devices)
        {
            if (device == null || !device.added || !device.enabled)
                continue;
            if (device is HandInteractionProfile.HandInteraction typedHand)
            {
                bool typedLeft = HasUsage(typedHand, "LeftHand");
                bool typedRight = HasUsage(typedHand, "RightHand");
                if (typedLeft)
                    ReadOfficialDevice(typedHand, left);
                if (typedRight)
                    ReadOfficialDevice(typedHand, right);
                continue;
            }
            string product = device.description.product ?? string.Empty;
            bool handInteraction =
                device.layout.IndexOf(
                    "HandInteraction",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                product.IndexOf(
                    "Hand Interaction",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                product.IndexOf(
                    "Pico Hand Interaction",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                product.IndexOf(
                    "Pico Aim Hand",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            if (!handInteraction)
                continue;

            bool leftUsage = HasUsage(device, "LeftHand");
            bool rightUsage = HasUsage(device, "RightHand");
            if (!leftUsage && !rightUsage)
                continue;
            if (leftUsage)
                ReadOfficialDevice(device, left);
            if (rightUsage)
                ReadOfficialDevice(device, right);
        }
        FinalizeOfficial(left);
        FinalizeOfficial(right);
    }

    void ReadCommonGestures(MutableHand target)
    {
        if (hands == null || !hands.running)
            return;

        XRCommonHandGestures gestures = target.left
            ? hands.leftHandCommonGestures
            : hands.rightHandCommonGestures;
        if (gestures == null)
            return;

        bool aimReady = gestures.TryGetAimActivateValue(out _);
        bool pinchReady = gestures.TryGetPinchValue(out float pinchValue);
        bool aimPoseReady = gestures.TryGetAimPose(out Pose aimPose);
        bool pinchPoseReady = gestures.TryGetPinchPose(out Pose pinchPose);
        if (!aimReady && !pinchReady && !aimPoseReady && !pinchPoseReady)
            return;

        target.officialPresent = true;
        target.officialTracked = aimReady || pinchReady;
        // V50: use ONLY the pinch value. The previous code Max-blended the aim
        // (ray-activate) strength with the pinch strength; because AimActivate
        // sits high (~0.7) on a merely tracked hand and the two gestures are out
        // of phase during a real pinch, the blended value both floored the
        // baseline and jittered (1.0/0.27) — the "erratic official value" seen
        // in the worn log. Aim is a different gesture and must not drive pinch.
        if (pinchReady)
            target.officialValue = Mathf.Max(target.officialValue, pinchValue);
        target.officialSource = "xr-hands-common-gesture";

        Pose pose = aimPoseReady ? aimPose : pinchPose;
        if ((aimPoseReady || pinchPoseReady) &&
            IsFinite(pose.position) &&
            IsFinite(pose.rotation) &&
            Quaternion.Dot(pose.rotation, pose.rotation) > 0.25f)
        {
            target.officialPoseValid = true;
            target.officialPose = pose;
        }
    }

    void ReadOfficialDevice(InputDevice device, MutableHand target)
    {
        target.officialPresent = true;
        if (string.IsNullOrEmpty(target.officialSource))
            target.officialSource = "inputsystem-hand-interaction";
        ButtonControl tracked = Control<ButtonControl>(device, "isTracked");
        ButtonControl ready = Control<ButtonControl>(device, "pinchReady");
        ButtonControl pointerReady =
            Control<ButtonControl>(device, "pointerActivateReady");
        AxisControl pinch = Control<AxisControl>(device, "pinchValue");
        if (pinch == null)
            pinch = Control<AxisControl>(device, "pinchStrengthIndex");
        AxisControl pointer = Control<AxisControl>(device, "pointerActivateValue");
        ButtonControl touched = Control<ButtonControl>(device, "pinchTouched");
        if (touched == null)
            touched = Control<ButtonControl>(device, "indexPressed");
        ButtonControl pointerPressed = Control<ButtonControl>(device, "pointerActivated");
        bool pinchIsReady = ready != null && ready.isPressed;
        bool pointerIsReady =
            pointerReady != null && pointerReady.isPressed;
        float raw = Mathf.Max(
            pinchIsReady && pinch != null ? pinch.ReadValue() : 0f,
            pointerIsReady && pointer != null ? pointer.ReadValue() : 0f);
        target.officialValue = Mathf.Max(target.officialValue, raw);
        bool trackedNow =
            tracked != null && tracked.isPressed ||
            pinchIsReady ||
            pointerIsReady;
        target.officialTracked |= trackedNow;
        bool digital =
            pinchIsReady && touched != null && touched.isPressed ||
            pointerIsReady && pointerPressed != null &&
                pointerPressed.isPressed;
        target.officialDigital |= digital;

        Vector3Control position = Control<Vector3Control>(device, "pinchPosition");
        if (position == null)
            position = Control<Vector3Control>(device, "pinchPosePosition");
        QuaternionControl rotation = Control<QuaternionControl>(device, "pinchRotation");
        if (rotation == null)
            rotation = Control<QuaternionControl>(device, "pinchPoseRotation");
        if (trackedNow && position != null && rotation != null)
        {
            Vector3 value = position.ReadValue();
            Quaternion orientation = rotation.ReadValue();
            if (IsFinite(value) &&
                value.sqrMagnitude > 0.000001f &&
                IsFinite(orientation) &&
                Quaternion.Dot(orientation, orientation) > 0.25f)
            {
                target.officialPoseValid = true;
                target.officialPose = new Pose(value, orientation);
            }
        }
    }

    void PollPicoActiveInputMode()
    {
        if (Time.unscaledTime < nextPicoModePoll)
            return;
        nextPicoModePoll = Time.unscaledTime + 0.25f;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            int next = (int)PXR_Plugin.HandTracking
                .UPxr_GetHandTrackerActiveInputType();
            if (next != picoActiveInputMode)
            {
                picoActiveInputMode = next;
                Debug.Log(
                    "[PPVR V48 Official Hand Input] event=pico-input-mode " +
                    "mode=" + picoActiveInputMode +
                    " expectedHands=2");
            }
        }
        catch (Exception exception)
        {
            if (picoActiveInputMode != -2)
                Debug.LogWarning(
                    "[PPVR V48 Official Hand Input] event=pico-input-mode " +
                    "unavailable=" + exception.GetType().Name);
            picoActiveInputMode = -2;
        }
#endif
    }

    void RefreshCombined(MutableHand target)
    {
        float age = Time.unscaledTime - target.jointTime;
        bool jointFresh =
            target.jointValid &&
            age <= Mathf.Max(0.03f, freshSampleSeconds);
        bool officialAvailable =
            target.officialPresent && target.officialTracked;
        bool available = jointFresh || officialAvailable;
        bool rawPressed =
            jointFresh && target.jointPressed ||
            officialAvailable && target.officialPressed;
        bool officialPose =
            officialAvailable && target.officialPoseValid;

        // V56 — reversed from V50/V51. The worn logs are unambiguous: when the
        // hand genuinely pinches, the official common-gesture value reads a
        // stable 1.000 (143 samples), while the thumb-index joint distance on
        // this device is the NOISY signal, scattering 0.004-0.069 within a
        // single held pinch. V51 wrongly made the joint distance primary, which
        // made pinch feel broken ("open very wide then pinch"), starved
        // Selection, and corrupted the level grab/release edges. So the official
        // value is now the primary arbiter with hysteresis (press >= 0.9, open
        // <= 0.5); the joint distance is used ONLY when the official signal is
        // unavailable. A genuine system boolean still wins outright.
        bool jointClose = jointFresh && target.jointPressed;
        bool jointOpen =
            jointFresh &&
            target.jointDistance >=
                Mathf.Max(jointReleaseDistance, jointPressDistance);
        bool officialClose =
            officialAvailable && target.officialPressed;
        bool officialOpen =
            officialAvailable &&
            target.officialValue <=
                Mathf.Clamp(
                    officialRearmValueV58,
                    officialReleaseValue,
                    officialPressValue);
        bool closeNow;
        bool openNow;
        if (officialAvailable)
        {
            closeNow = officialClose;
            openNow = officialOpen;
        }
        else
        {
            closeNow = jointClose;
            openNow = jointOpen;
        }

        bool pressed;
        if (!available)
        {
            target.pressArmed = false;
            target.pressLatched = false;
            pressed = false;
        }
        else if (!requireReleaseBeforePressV50)
        {
            pressed = rawPressed;
        }
        else if (officialAvailable && target.officialDigital)
        {
            // Authoritative system boolean: follow it directly.
            pressed = true;
            target.pressLatched = true;
            target.pressArmed = false;
        }
        else if (target.pressLatched)
        {
            // Hold until the hand clearly opens (both sources), then re-arm.
            if (openNow)
            {
                target.pressLatched = false;
                target.pressArmed = true;
                pressed = false;
            }
            else
                pressed = true;
        }
        else
        {
            // Require a clear open to arm, then latch on the next clear close.
            if (openNow)
                target.pressArmed = true;
            if (target.pressArmed && closeNow)
            {
                target.pressLatched = true;
                target.pressArmed = false;
                pressed = true;
            }
            else
                pressed = false;
        }

        target.combined = new HandState
        {
            available = available,
            pressed = pressed,
            poseValid = jointFresh || officialPose,
            localPinchPose = jointFresh
                ? target.jointPose
                : target.officialPose,
            jointDistance = jointFresh
                ? target.jointDistance
                : float.PositiveInfinity,
            officialValue = target.officialValue,
            source = pressed
                ? officialAvailable && target.officialPressed
                    ? target.officialSource + "-press"
                    : "xr-hands-joints"
                : officialAvailable
                    ? target.officialSource + "-release"
                    : jointFresh
                        ? "xr-hands-joints-release"
                        : "none",
            sampleAge = jointFresh ? Mathf.Max(0f, age) : float.PositiveInfinity,
        };
    }

    void LogChanges(MutableHand target)
    {
        HandState state = target.combined;
        if (!emitTelemetry ||
            state.available == target.previousAvailable &&
            state.pressed == target.previousPressed &&
            string.Equals(
                state.source,
                target.previousSource,
                StringComparison.Ordinal))
            return;
        target.previousAvailable = state.available;
        target.previousPressed = state.pressed;
        target.previousSource = state.source;
        Debug.Log(
            "[PPVR V47 Pinch] event=edge hand=" +
            (target.left ? "left" : "right") +
            " state=" + Describe(state));
    }

#if UNITY_EDITOR
    public bool AutomationActiveV47 => automationActiveV47;

    public void SetAutomationHandStateV47(
        bool leftHand,
        bool available,
        bool pressed,
        bool poseValid,
        Pose localPinchPose)
    {
        automationActiveV47 = true;
        HandState state = new HandState
        {
            available = available,
            pressed = available && pressed,
            poseValid = available && poseValid,
            localPinchPose = localPinchPose,
            jointDistance = available
                ? pressed ? 0.030f : 0.090f
                : float.PositiveInfinity,
            officialValue = available && pressed ? 1f : 0f,
            source = available
                ? pressed
                    ? "editor-v47-provider-press"
                    : "editor-v47-provider-release"
                : "editor-v47-provider-unavailable",
            sampleAge = available ? 0f : float.PositiveInfinity,
        };
        if (leftHand)
            automationLeftV47 = state;
        else
            automationRightV47 = state;
        // Editor automation represents a complete tracked hand sample. Keep
        // the V59 index-tip channel in sync with the automated pinch pose so
        // direct-touch controls (hint bell / V69 completion exit) exercise the
        // same Update path as a real PICO hand instead of timing out with an
        // otherwise valid automated hand.
        MutableHand target = leftHand ? left : right;
        target.jointValid = available && poseValid;
        target.indexTipLocalV60 = localPinchPose.position;
        target.jointTime = target.jointValid
            ? Time.unscaledTime
            : float.NegativeInfinity;
        left.combined = automationLeftV47;
        right.combined = automationRightV47;
    }

    public void SetAutomationPalmV47(
        bool leftHand,
        bool available,
        Pose localPalmPose)
    {
        automationActiveV47 = true;
        MutableHand target = leftHand ? left : right;
        target.palmValid = available;
        target.palmPose = localPalmPose;
        target.palmTime = available
            ? Time.unscaledTime
            : float.NegativeInfinity;
    }

    public void ClearAutomationV47()
    {
        automationActiveV47 = false;
        automationLeftV47 = default;
        automationRightV47 = default;
        left.combined = default;
        right.combined = default;
        left.palmValid = false;
        right.palmValid = false;
        left.palmTime = float.NegativeInfinity;
        right.palmTime = float.NegativeInfinity;
    }
#endif

    static void ResetOfficial(MutableHand target)
    {
        target.officialPresent = false;
        target.officialTracked = false;
        target.officialDigital = false;
        target.officialPoseValid = false;
        target.officialValue = 0f;
        target.officialSource = string.Empty;
    }

    void FinalizeOfficial(MutableHand target)
    {
        if (!target.officialPresent || !target.officialTracked)
        {
            target.officialPressed = false;
            return;
        }

        // A genuine system-provided discrete pinch (indexPressed /
        // pointerActivated) is authoritative and forces the raw press.
        if (target.officialDigital)
        {
            target.officialPressed = true;
            return;
        }

        // Otherwise report the raw level crossing. The complete open->close
        // action gate that rejects the ~0.70 resting baseline lives in
        // RefreshCombined so it covers the joint fallback with one edge too.
        target.officialPressed = target.officialPressed
            ? target.officialValue >
                Mathf.Min(officialPressValue, officialReleaseValue)
            : target.officialValue >=
                Mathf.Max(officialReleaseValue, officialPressValue);
    }

    static bool HasUsage(InputDevice device, string usage)
    {
        foreach (var value in device.usages)
            if (string.Equals(
                    value.ToString(),
                    usage,
                    StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    static T Control<T>(InputDevice device, string name)
        where T : InputControl
    {
        foreach (InputControl value in device.allControls)
            if (value is T typed &&
                string.Equals(
                    value.name,
                    name,
                    StringComparison.Ordinal))
                return typed;
        return null;
    }

    static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.x) &&
        float.IsFinite(value.y) &&
        float.IsFinite(value.z);

    static bool IsFinite(Quaternion value) =>
        float.IsFinite(value.x) &&
        float.IsFinite(value.y) &&
        float.IsFinite(value.z) &&
        float.IsFinite(value.w);

    static string Describe(HandState state) =>
        "available=" + state.available +
        ",pressed=" + state.pressed +
        ",pose=" + state.poseValid +
        ",distance=" +
        (float.IsFinite(state.jointDistance)
            ? state.jointDistance.ToString("F4")
            : "inf") +
        ",official=" + state.officialValue.ToString("F3") +
        ",source=" + state.source;
}
