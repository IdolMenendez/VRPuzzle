using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

/// <summary>
/// Remote Selection confirmation for a large central exhibit. Any deliberate
/// hand pinch or controller trigger enters the currently centered model; the
/// user never has to physically reach the display case.
/// </summary>
[DefaultExecutionOrder(-115)]
[DisallowMultipleComponent]
public sealed class SelectionRemoteConfirmV33 : MonoBehaviour
{
    public SelectionCarouselController selection;
    public SelectionDirectPuzzleIntentV31 directIntent;
    [Min(0.034f)] public float pinchDistance = 0.055f;
    [Min(0.2f)] public float cooldownSeconds = 0.6f;
    [Header("V36 intentional centered selection")]
    public bool requireCenteredGazeAndFreshPinchV36;
    [Range(3f, 30f)] public float centeredGazeAngleV36 = 11f;
    public Camera gazeCameraV36;

    readonly List<XRHandSubsystem> handSubsystems =
        new List<XRHandSubsystem>();
    readonly List<XRInputDevice> controllers =
        new List<XRInputDevice>();
    XRHandSubsystem hands;
    float nextScan;
    float nextAllowed;
    bool leftWasPinching;
    bool rightWasPinching;
    bool controllerWasPressed;
    bool inputReleasedSinceEnableV36;

    void OnEnable()
    {
        leftWasPinching = rightWasPinching = controllerWasPressed = false;
        inputReleasedSinceEnableV36 = !requireCenteredGazeAndFreshPinchV36;
    }

    void Update()
    {
        if (!PersonallyPuzzledExperienceModeState.IsPlayerManual ||
            !selection ||
            !directIntent ||
            selection.IsTransitioning ||
            selection.IsCommitting ||
            directIntent.EntryPending)
            return;

        bool anyHandPinching;
        bool requested = ReadHands(out anyHandPinching);
        bool anyControllerPressed;
        if (!requested)
            requested = ReadController(out anyControllerPressed);
        else
            ReadController(out anyControllerPressed);
        if (requireCenteredGazeAndFreshPinchV36)
        {
            if (!anyHandPinching && !anyControllerPressed)
                inputReleasedSinceEnableV36 = true;
            if (!inputReleasedSinceEnableV36 || !IsLookingAtCenteredExhibitV36())
                requested = false;
        }
        if (!requested ||
            Time.unscaledTime < nextAllowed)
            return;

        // V61 #2/#6: when the 3D confirm plaque is required, a remote pinch/
        // trigger while looking at the centered exhibit must NOT enter directly
        // (that was the accidental "looked and entered" path). Instead arm the
        // plaque; SelectionEnterConfirmV61 enters only when the plaque is
        // deliberately touched.
        if (SelectionExhibitTargetV09.RequireConfirmPlaqueV61)
        {
            SelectionExhibitTargetV09 target = FindCenteredTargetV61();
            if (target)
            {
                SelectionExhibitTargetV09.ArmPendingConfirmV61(target);
                nextAllowed = Time.unscaledTime + Mathf.Max(0.2f, cooldownSeconds);
            }
            return;
        }

        if (directIntent.RequestEnterExhibit(
                selection.currentIndex))
        {
            nextAllowed =
                Time.unscaledTime +
                Mathf.Max(
                    0.2f,
                    cooldownSeconds);
        }
    }

    SelectionExhibitTargetV09 FindCenteredTargetV61()
    {
        foreach (SelectionExhibitTargetV09 target in
                 FindObjectsByType<SelectionExhibitTargetV09>(
                     FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
            if (target && target.IsCurrent)
                return target;
        return null;
    }

    bool ReadHands(out bool anyPinching)
    {
        anyPinching = false;
        if ((hands == null || !hands.running) &&
            Time.unscaledTime >= nextScan)
        {
            nextScan =
                Time.unscaledTime + 1f;
            handSubsystems.Clear();
            SubsystemManager.GetSubsystems(
                handSubsystems);
            hands = handSubsystems.Find(
                value =>
                    value != null &&
                    value.running);
        }
        if (hands == null ||
            !hands.running)
        {
            leftWasPinching =
                rightWasPinching =
                false;
            return false;
        }

        bool left =
            IsPinching(hands.leftHand);
        bool right =
            IsPinching(hands.rightHand);
        anyPinching = left || right;
        bool pressed =
            (left && !leftWasPinching) ||
            (right && !rightWasPinching);
        leftWasPinching = left;
        rightWasPinching = right;
        return pressed;
    }

    bool IsPinching(XRHand hand)
    {
        return hand.isTracked &&
            hand.GetJoint(
                    XRHandJointID.IndexTip)
                .TryGetPose(
                    out Pose index) &&
            hand.GetJoint(
                    XRHandJointID.ThumbTip)
                .TryGetPose(
                    out Pose thumb) &&
            Vector3.Distance(
                index.position,
                thumb.position) <=
            Mathf.Max(
                0.034f,
                pinchDistance);
    }

    bool ReadController(out bool pressed)
    {
        controllers.Clear();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller |
            InputDeviceCharacteristics.TrackedDevice,
            controllers);
        pressed = false;
        foreach (XRInputDevice device in controllers)
        {
            if ((device.TryGetFeatureValue(
                     XRCommonUsages.triggerButton,
                     out bool trigger) &&
                 trigger) ||
                (device.TryGetFeatureValue(
                     XRCommonUsages.primaryButton,
                     out bool primary) &&
                 primary))
            {
                pressed = true;
                break;
            }
        }
        bool began =
            pressed &&
            !controllerWasPressed;
        controllerWasPressed = pressed;
        return began;
    }

    public bool IsLookingAtCenteredExhibitV36()
    {
        if (!selection || !selection.CurrentExhibit)
            return false;
        Camera camera =
            selection && selection.sceneCamera
                ? selection.sceneCamera
                : gazeCameraV36
                    ? gazeCameraV36
                    : Camera.main;
        if (!camera)
            return false;
        Renderer[] renderers =
            selection.CurrentExhibit.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return false;
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
            if (renderers[index])
                bounds.Encapsulate(renderers[index].bounds);
        Vector3 direction = bounds.center - camera.transform.position;
        return direction.sqrMagnitude > 0.0001f &&
            Vector3.Angle(camera.transform.forward, direction.normalized) <=
            Mathf.Clamp(centeredGazeAngleV36, 3f, 30f);
    }
}
