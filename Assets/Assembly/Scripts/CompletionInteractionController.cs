using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Completion-stage free play: a gentle unattended turntable becomes a direct
/// two-hand object when either tracked hand pinches it. XR controllers and a
/// desktop keyboard/mouse path provide complete fallbacks.
/// </summary>
public sealed class CompletionInteractionController : MonoBehaviour
{
    public Camera sceneCamera;
    public Transform heroModel;
    public Transform particleOrbit;
    public Transform previewHands;
    public XRGrabInteractable grabInteractable;
    public float autoRotationDegreesPerSecond = 12f;
    public float handGrabRadius = 0.18f;
    public bool freezeReferencePose;
    [Header("V47 shared OpenXR pinch provider")]
    public bool resilientRealHandPinchV47;

    [Header("V79 lift and two-hand inspection")]
    [Tooltip(
        "Allows one hand to lift the completed model in XYZ and two pinching " +
        "hands to resize it by changing their separation.")]
    public bool enableFreeLiftAndTwoHandScaleV79;
    [Range(0.5f, 1f)] public float minimumScaleMultiplierV79 = 0.75f;
    [Range(1f, 3f)] public float maximumScaleMultiplierV79 = 2.20f;
    [Min(1f)] public float oneHandMoveResponseV79 = 18f;
    [Min(1f)] public float twoHandScaleResponseV79 = 16f;
    [Min(0.25f)] public float maximumInspectDistanceV79 = 0.90f;
    [Tooltip(
        "The physical glass shell remains visible at rest and is hidden after " +
        "the first successful pickup so the collectible can be taken out.")]
    public Transform removableShowcaseShellV79;
    public bool hideShowcaseShellOnPickupV79 = true;

    readonly List<XRHandSubsystem> handSubsystems = new List<XRHandSubsystem>();
    readonly List<UnityEngine.XR.InputDevice> devices = new List<UnityEngine.XR.InputDevice>();
    XRHandSubsystem hands;
    Vector3 previousPalm;
    Quaternion previousControllerRotation = Quaternion.identity;
    float nextScan;
    bool hasPalm;
    bool grabbedForSmoke;
    bool runtimeGrabbed;
    bool handGrabActiveV69;
    float handGrabYawOffsetV69;
    bool leftGrabActiveV79;
    bool rightGrabActiveV79;
    bool oneHandInitializedV79;
    bool oneHandIsLeftV79;
    bool twoHandInitializedV79;
    Vector3 oneHandPositionOffsetV79;
    float oneHandYawOffsetV79;
    Vector3 baselinePositionV79;
    Vector3 baselineLocalScaleV79;
    Vector3 twoHandStartScaleV79;
    Vector3 twoHandPositionOffsetV79;
    Vector3 twoHandStartAxisV79;
    float twoHandStartDistanceV79;
    float twoHandStartYawV79;
    bool inspectionBaselineCapturedV79;
    AudioSource music;
    AudioSource cue;
    float uprightY;

    public bool IsGrabbed =>
        grabbedForSmoke ||
        runtimeGrabbed ||
        leftGrabActiveV79 ||
        rightGrabActiveV79 ||
        (grabInteractable && grabInteractable.isSelected);
    public Vector3 InspectionBaselinePositionV79 => baselinePositionV79;
    public Vector3 InspectionBaselineScaleV79 => baselineLocalScaleV79;
    public bool ShowcaseShellExtractedV79 { get; private set; }
    public float CurrentScaleMultiplierV79 =>
        inspectionBaselineCapturedV79 &&
        Mathf.Abs(baselineLocalScaleV79.x) > 0.00001f &&
        heroModel
            ? heroModel.localScale.x / baselineLocalScaleV79.x
            : 1f;

    void Awake()
    {
        if (heroModel)
        {
            uprightY = heroModel.position.y;
            RecacheInspectionBaselineV79();
            Rigidbody heroBody = heroModel.GetComponent<Rigidbody>();
            if (heroBody)
            {
                heroBody.useGravity = false;
                heroBody.isKinematic = true;
                heroBody.linearVelocity = Vector3.zero;
                heroBody.angularVelocity = Vector3.zero;
            }
        }
        CreateOriginalCompletionAudio();
    }

    void Update()
    {
        if (!heroModel)
            return;
        if (previewHands && XRSettings.isDeviceActive)
            previewHands.gameObject.SetActive(false);
        if (freezeReferencePose)
            return;

        bool providerAutomationV47 = false;
#if UNITY_EDITOR
        providerAutomationV47 =
            resilientRealHandPinchV47 &&
            RuntimeRealHandPinchV47.Instance &&
            RuntimeRealHandPinchV47.Instance.AutomationActiveV47;
#endif
        bool directGrab =
            RuntimeDesktopHeadNavigatorV01.DesktopEditorPreviewActive &&
            !providerAutomationV47
            ? UpdateDesktopGrab()
            : UpdateTrackedHandGrab() || UpdateControllerGrab() || UpdateDesktopGrab();
        runtimeGrabbed = directGrab || (grabInteractable && grabInteractable.isSelected);
        if (!IsGrabbed)
            heroModel.Rotate(Vector3.up, autoRotationDegreesPerSecond * Time.unscaledDeltaTime, Space.World);
        KeepHeroUpright();
        if (particleOrbit)
            particleOrbit.Rotate(Vector3.up, 4.5f * Time.unscaledDeltaTime, Space.World);
    }

    public void SetGrabbedForSmoke(bool value)
    {
        grabbedForSmoke = value;
    }

    public string ApplyFreePlayPoseForSmoke(Vector3 worldDelta, Quaternion rotationDelta)
    {
        if (!heroModel)
            return "missing";
        heroModel.position += new Vector3(worldDelta.x, 0f, worldDelta.z);
        float yaw = (rotationDelta * heroModel.rotation).eulerAngles.y;
        heroModel.rotation = Quaternion.Euler(0f, yaw, 0f);
        KeepHeroUpright();
        return "free-play-applied";
    }

    public float ApplyAutoRotationForSmoke(float seconds)
    {
        if (!heroModel || IsGrabbed)
            return 0f;
        float degrees = autoRotationDegreesPerSecond * Mathf.Max(0f, seconds);
        heroModel.Rotate(Vector3.up, degrees, Space.World);
        return degrees;
    }

    public void RecacheUprightYV78()
    {
        if (heroModel)
            uprightY = heroModel.position.y;
    }

    public void RecacheInspectionBaselineV79()
    {
        if (!heroModel)
            return;
        baselinePositionV79 = heroModel.position;
        baselineLocalScaleV79 = heroModel.localScale;
        inspectionBaselineCapturedV79 = true;
        leftGrabActiveV79 = false;
        rightGrabActiveV79 = false;
        oneHandInitializedV79 = false;
        twoHandInitializedV79 = false;
        ShowcaseShellExtractedV79 = false;
        if (removableShowcaseShellV79)
        {
            foreach (Renderer renderer in
                     removableShowcaseShellV79.GetComponentsInChildren<
                         Renderer>(true))
                if (renderer)
                    renderer.enabled = true;
        }
    }

    bool UpdateTrackedHandGrab()
    {
        if (resilientRealHandPinchV47 &&
            RuntimeRealHandPinchV47.Instance)
        {
            if (enableFreeLiftAndTwoHandScaleV79)
                return UpdateInspectableHandsV79(
                    RuntimeRealHandPinchV47.Instance);
            bool driven =
                RuntimeRealHandPinchV47.Instance.TryGetHand(
                    true,
                    out RuntimeRealHandPinchV47.HandState leftState) &&
                TryDriveFromV47(leftState) ||
                RuntimeRealHandPinchV47.Instance.TryGetHand(
                    false,
                    out RuntimeRealHandPinchV47.HandState rightState) &&
                TryDriveFromV47(rightState);
            if (!driven)
                hasPalm = false;
            return driven;
        }
        ScanHands();
        if (hands == null || !hands.running)
            return false;
        if (TryDriveFromHand(hands.leftHand) || TryDriveFromHand(hands.rightHand))
            return true;
        hasPalm = false;
        return false;
    }

    bool UpdateInspectableHandsV79(RuntimeRealHandPinchV47 provider)
    {
        if (!inspectionBaselineCapturedV79)
            RecacheInspectionBaselineV79();

        bool leftPressed = TryWorldHandV79(
            provider, true, out Vector3 leftPosition,
            out Quaternion leftRotation);
        bool rightPressed = TryWorldHandV79(
            provider, false, out Vector3 rightPosition,
            out Quaternion rightRotation);

        if (!leftPressed)
            leftGrabActiveV79 = false;
        if (!rightPressed)
            rightGrabActiveV79 = false;

        if (leftPressed && !leftGrabActiveV79 &&
            CanJoinInspectionV79(leftPosition))
        {
            leftGrabActiveV79 = true;
            ExtractShowcaseShellV79();
        }
        if (rightPressed && !rightGrabActiveV79 &&
            CanJoinInspectionV79(rightPosition))
        {
            rightGrabActiveV79 = true;
            ExtractShowcaseShellV79();
        }

        if (leftGrabActiveV79 && rightGrabActiveV79)
        {
            oneHandInitializedV79 = false;
            DriveTwoHandInspectionV79(leftPosition, rightPosition);
            return true;
        }

        twoHandInitializedV79 = false;
        bool oneActive = leftGrabActiveV79 || rightGrabActiveV79;
        if (!oneActive)
        {
            oneHandInitializedV79 = false;
            return false;
        }

        bool useLeft = leftGrabActiveV79;
        DriveOneHandInspectionV79(
            useLeft,
            useLeft ? leftPosition : rightPosition,
            useLeft ? leftRotation : rightRotation);
        return true;
    }

    bool TryWorldHandV79(
        RuntimeRealHandPinchV47 provider,
        bool leftHand,
        out Vector3 worldPosition,
        out Quaternion worldRotation)
    {
        worldPosition = default;
        worldRotation = Quaternion.identity;
        if (!provider.TryGetHand(
                leftHand,
                out RuntimeRealHandPinchV47.HandState state) ||
            !state.available ||
            !state.pressed ||
            !state.poseValid)
            return false;
        Transform tracking = TrackingSpaceV79();
        worldPosition = tracking
            ? tracking.TransformPoint(state.localPinchPose.position)
            : state.localPinchPose.position;
        worldRotation = tracking
            ? tracking.rotation * state.localPinchPose.rotation
            : state.localPinchPose.rotation;
        return true;
    }

    bool CanJoinInspectionV79(Vector3 handPosition)
    {
        if (leftGrabActiveV79 || rightGrabActiveV79)
            return DistanceToHeroSurfaceV79(handPosition) <=
                handGrabRadius * 1.45f;
        return DistanceToHeroSurfaceV79(handPosition) <= handGrabRadius;
    }

    public void ExtractShowcaseShellV79()
    {
        if (!hideShowcaseShellOnPickupV79 ||
            !removableShowcaseShellV79 ||
            ShowcaseShellExtractedV79)
            return;
        foreach (Renderer renderer in
                 removableShowcaseShellV79.GetComponentsInChildren<Renderer>(
                     true))
            if (renderer)
                renderer.enabled = false;
        ShowcaseShellExtractedV79 = true;
    }

    float DistanceToHeroSurfaceV79(Vector3 point)
    {
        if (!heroModel)
            return float.PositiveInfinity;
        bool found = false;
        float best = float.PositiveInfinity;
        foreach (Renderer renderer in
                 heroModel.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer ||
                !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy ||
                renderer is ParticleSystemRenderer ||
                renderer.GetComponent<TextMesh>())
                continue;
            found = true;
            best = Mathf.Min(
                best,
                Vector3.Distance(
                    renderer.bounds.ClosestPoint(point),
                    point));
        }
        return found
            ? best
            : Vector3.Distance(point, heroModel.position);
    }

    void DriveOneHandInspectionV79(
        bool leftHand,
        Vector3 handPosition,
        Quaternion handRotation)
    {
        if (!oneHandInitializedV79 ||
            oneHandIsLeftV79 != leftHand)
        {
            oneHandInitializedV79 = true;
            oneHandIsLeftV79 = leftHand;
            oneHandPositionOffsetV79 =
                heroModel.position - handPosition;
            oneHandYawOffsetV79 = Mathf.DeltaAngle(
                handRotation.eulerAngles.y,
                heroModel.eulerAngles.y);
        }
        Vector3 target =
            handPosition + oneHandPositionOffsetV79;
        target = baselinePositionV79 +
            Vector3.ClampMagnitude(
                target - baselinePositionV79,
                Mathf.Max(0.25f, maximumInspectDistanceV79));
        float move = 1f - Mathf.Exp(
            -Mathf.Max(1f, oneHandMoveResponseV79) *
            Time.unscaledDeltaTime);
        heroModel.position = Vector3.Lerp(
            heroModel.position, target, move);
        float yaw =
            handRotation.eulerAngles.y + oneHandYawOffsetV79;
        heroModel.rotation = Quaternion.Slerp(
            heroModel.rotation,
            Quaternion.Euler(0f, yaw, 0f),
            move);
    }

    void DriveTwoHandInspectionV79(
        Vector3 leftPosition,
        Vector3 rightPosition)
    {
        Vector3 midpoint = (leftPosition + rightPosition) * 0.5f;
        Vector3 axis = rightPosition - leftPosition;
        float distance = Mathf.Max(0.035f, axis.magnitude);
        if (!twoHandInitializedV79)
        {
            twoHandInitializedV79 = true;
            twoHandStartDistanceV79 = distance;
            twoHandStartScaleV79 = heroModel.localScale;
            twoHandPositionOffsetV79 =
                heroModel.position - midpoint;
            twoHandStartAxisV79 =
                Vector3.ProjectOnPlane(axis, Vector3.up).normalized;
            twoHandStartYawV79 = heroModel.eulerAngles.y;
        }

        float baselineX = Mathf.Max(
            0.00001f,
            Mathf.Abs(baselineLocalScaleV79.x));
        float startMultiplier =
            Mathf.Abs(twoHandStartScaleV79.x) / baselineX;
        float targetMultiplier = Mathf.Clamp(
            startMultiplier *
            distance / Mathf.Max(0.035f, twoHandStartDistanceV79),
            Mathf.Min(
                minimumScaleMultiplierV79,
                maximumScaleMultiplierV79),
            Mathf.Max(
                minimumScaleMultiplierV79,
                maximumScaleMultiplierV79));
        Vector3 targetScale =
            baselineLocalScaleV79 * targetMultiplier;
        float response = 1f - Mathf.Exp(
            -Mathf.Max(1f, twoHandScaleResponseV79) *
            Time.unscaledDeltaTime);
        heroModel.localScale = Vector3.Lerp(
            heroModel.localScale,
            targetScale,
            response);

        Vector3 targetPosition =
            midpoint + twoHandPositionOffsetV79;
        targetPosition = baselinePositionV79 +
            Vector3.ClampMagnitude(
                targetPosition - baselinePositionV79,
                Mathf.Max(0.25f, maximumInspectDistanceV79));
        heroModel.position = Vector3.Lerp(
            heroModel.position,
            targetPosition,
            response);

        Vector3 currentAxis =
            Vector3.ProjectOnPlane(axis, Vector3.up).normalized;
        if (twoHandStartAxisV79.sqrMagnitude > 0.0001f &&
            currentAxis.sqrMagnitude > 0.0001f)
        {
            float deltaYaw = Vector3.SignedAngle(
                twoHandStartAxisV79,
                currentAxis,
                Vector3.up);
            heroModel.rotation = Quaternion.Slerp(
                heroModel.rotation,
                Quaternion.Euler(
                    0f,
                    twoHandStartYawV79 + deltaYaw,
                    0f),
                response);
        }
    }

    Transform TrackingSpaceV79()
    {
        PersistentPuzzleBootstrap bootstrap =
            PersistentPuzzleBootstrap.Instance;
        if (bootstrap &&
            bootstrap.xrOrigin &&
            bootstrap.xrOrigin.CameraFloorOffsetObject)
            return bootstrap.xrOrigin.CameraFloorOffsetObject.transform;
        return sceneCamera && sceneCamera.transform.parent
            ? sceneCamera.transform.parent
            : null;
    }

    bool TryDriveFromV47(RuntimeRealHandPinchV47.HandState hand)
    {
        if (!hand.available || !hand.pressed || !hand.poseValid)
        {
            handGrabActiveV69 = false;
            return false;
        }
        Transform tracking =
            sceneCamera && sceneCamera.transform.parent
                ? sceneCamera.transform.parent
                : null;
        Vector3 worldPalm = tracking
            ? tracking.TransformPoint(hand.localPinchPose.position)
            : hand.localPinchPose.position;
        if (Vector3.Distance(worldPalm, heroModel.position) > handGrabRadius)
        {
            handGrabActiveV69 = false;
            return false;
        }
        if (hasPalm)
            heroModel.position += Vector3.ClampMagnitude(
                worldPalm - previousPalm,
                0.045f);
        Quaternion localRotation = hand.localPinchPose.rotation;
        Quaternion palmRotation = tracking
            ? tracking.rotation * localRotation
            : localRotation;
        if (!handGrabActiveV69)
        {
            handGrabYawOffsetV69 = Mathf.DeltaAngle(
                palmRotation.eulerAngles.y,
                heroModel.eulerAngles.y);
            handGrabActiveV69 = true;
        }
        float desiredYaw = palmRotation.eulerAngles.y + handGrabYawOffsetV69;
        heroModel.rotation = Quaternion.Slerp(
            heroModel.rotation,
            Quaternion.Euler(0f, desiredYaw, 0f),
            0.32f);
        previousPalm = worldPalm;
        hasPalm = true;
        return true;
    }

    bool TryDriveFromHand(XRHand hand)
    {
        if (!hand.isTracked || !hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palm) ||
            !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose index) ||
            !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumb))
            return false;
        bool pinching = Vector3.Distance(index.position, thumb.position) <= 0.032f;
        Transform tracking = sceneCamera && sceneCamera.transform.parent ? sceneCamera.transform.parent : null;
        Vector3 worldPalm = tracking ? tracking.TransformPoint(palm.position) : palm.position;
        if (!pinching || Vector3.Distance(worldPalm, heroModel.position) > handGrabRadius)
        {
            handGrabActiveV69 = false;
            return false;
        }
        if (hasPalm)
            heroModel.position += Vector3.ClampMagnitude(worldPalm - previousPalm, 0.045f);
        Quaternion palmRotation =
            tracking ? tracking.rotation * palm.rotation : palm.rotation;
        if (!handGrabActiveV69)
        {
            handGrabYawOffsetV69 = Mathf.DeltaAngle(
                palmRotation.eulerAngles.y,
                heroModel.eulerAngles.y);
            handGrabActiveV69 = true;
        }
        float desiredYaw = palmRotation.eulerAngles.y + handGrabYawOffsetV69;
        heroModel.rotation = Quaternion.Slerp(
            heroModel.rotation,
            Quaternion.Euler(0f, desiredYaw, 0f),
            0.32f);
        previousPalm = worldPalm;
        hasPalm = true;
        return true;
    }

    bool UpdateControllerGrab()
    {
        ScanDevices();
        foreach (UnityEngine.XR.InputDevice device in devices)
        {
            bool held = (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool grip) && grip) ||
                (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool trigger) && trigger);
            if (!held || !device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 localPosition))
                continue;
            Transform tracking = sceneCamera && sceneCamera.transform.parent ? sceneCamera.transform.parent : null;
            Vector3 world = tracking ? tracking.TransformPoint(localPosition) : localPosition;
            if (Vector3.Distance(world, heroModel.position) > handGrabRadius)
                continue;
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion rotation))
            {
                Quaternion worldRotation = tracking ? tracking.rotation * rotation : rotation;
                if (previousControllerRotation != Quaternion.identity)
                {
                    Quaternion delta =
                        worldRotation * Quaternion.Inverse(previousControllerRotation);
                    float yaw = (delta * heroModel.rotation).eulerAngles.y;
                    heroModel.rotation = Quaternion.Euler(0f, yaw, 0f);
                }
                previousControllerRotation = worldRotation;
            }
            Vector3 target = new Vector3(world.x, uprightY, world.z);
            heroModel.position = Vector3.Lerp(heroModel.position, target, 0.24f);
            return true;
        }
        previousControllerRotation = Quaternion.identity;
        return false;
    }

    bool UpdateDesktopGrab()
    {
        if (Keyboard.current == null)
            return false;
        float yaw = 0f;
        if (Keyboard.current.leftArrowKey.isPressed) yaw -= 1f;
        if (Keyboard.current.rightArrowKey.isPressed) yaw += 1f;
        if (Mathf.Abs(yaw) <= 0.01f)
            return false;
        heroModel.Rotate(Vector3.up, yaw * 55f * Time.unscaledDeltaTime, Space.World);
        return true;
    }

    void KeepHeroUpright()
    {
        if (!heroModel)
            return;
        if (!enableFreeLiftAndTwoHandScaleV79)
        {
            Vector3 position = heroModel.position;
            position.y = uprightY;
            heroModel.position = position;
        }
        heroModel.rotation = Quaternion.Euler(
            0f,
            heroModel.eulerAngles.y,
            0f);
    }

    void ScanHands()
    {
        if (Time.unscaledTime < nextScan && hands != null)
            return;
        nextScan = Time.unscaledTime + 1f;
        handSubsystems.Clear();
        SubsystemManager.GetSubsystems(handSubsystems);
        hands = handSubsystems.Find(value => value != null && value.running);
    }

    void ScanDevices()
    {
        devices.Clear();
        UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.TrackedDevice, devices);
    }

    void CreateOriginalCompletionAudio()
    {
        // In the complete game the persistent conductor owns the stage score
        // and completion bloom.  The historical oscillator fallback sounded
        // alarm-like when layered with that authored score, so it is now only
        // available when this scene is deliberately run without a conductor.
        if (RuntimeAudioRouter.Conductor)
            return;
        music = gameObject.AddComponent<AudioSource>();
        music.loop = true;
        music.spatialBlend = 0f;
        music.volume = 0.18f;
        music.clip = BuildClip("Original floating brass memory nocturne", 16f, false);
        music.Play();
        cue = gameObject.AddComponent<AudioSource>();
        cue.spatialBlend = 0.4f;
        cue.volume = 0.35f;
        cue.clip = BuildClip("Original completion starlight chime", 2.2f, true);
        cue.Play();
    }

    static AudioClip BuildClip(string name, float seconds, bool chime)
    {
        const int rate = 22050;
        int count = Mathf.CeilToInt(seconds * rate);
        float[] data = new float[count];
        float[] notes = chime ? new[] { 523.25f, 659.25f, 783.99f } : new[] { 196f, 246.94f, 293.66f, 369.99f };
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            float phase = chime ? Mathf.Min(notes.Length - 1, Mathf.FloorToInt(t / 0.55f)) : Mathf.Floor(t / 4f) % notes.Length;
            float f = notes[(int)phase];
            float envelope = chime ? Mathf.Exp(-2.4f * Mathf.Repeat(t, 0.55f)) : 0.55f + 0.45f * Mathf.Sin(Mathf.PI * Mathf.Repeat(t, 4f) / 4f);
            data[i] = (Mathf.Sin(Mathf.PI * 2f * f * t) + 0.32f * Mathf.Sin(Mathf.PI * 4f * f * t)) * 0.055f * envelope;
        }
        AudioClip clip = AudioClip.Create(name, count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
