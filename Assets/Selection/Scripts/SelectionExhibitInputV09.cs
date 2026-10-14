using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

/// <summary>
/// Routes direct Selection exhibit targeting for desktop, tracked controllers,
/// and tracked-hand pinches. Entering the central exhibit remains the stage
/// portal's responsibility.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class SelectionExhibitInputV09 : MonoBehaviour
{
    public SelectionCarouselController selection;
    public Camera sceneCamera;
    public SelectionExhibitTargetV09[] targets;
    [Min(0.05f)] public float directTouchRadius = 0.11f;
    [Min(1f)] public float maximumRayDistance = 20f;
    [Tooltip("V20 opt-in: overlapping generous hit volumes resolve to the semantic target whose center is closest to the pointer ray.")]
    public bool preferNearestTargetCenter;
    [Tooltip(
        "V33 opt-in. Use the native OpenXR hand pinch envelope instead of " +
        "the older desktop-sized threshold.")]
    public bool nativeOpenXrPinchEnvelopeV33;
    [Min(0.034f)]
    public float nativeOpenXrPinchDistanceV33 = 0.05f;
    [Tooltip(
        "V33 opt-in. If a tracked pinch ray is noisy, a deliberate pinch " +
        "while looking at the centered exhibit still means 'build this'.")]
    public bool centeredGazePinchFallbackV33;
    [Range(4f, 30f)]
    public float centeredGazeMaximumAngleV33 = 16f;
    [Tooltip(
        "V38 opt-in. Ignore a pinch/press that is already held when tracking " +
        "appears. A tracked released state must remain stable before a later " +
        "press edge can target an exhibit.")]
    public bool requireTrackedReleaseBeforeTargetV38;
    [Min(0.08f)] public float trackedReleaseSecondsV38 = 0.18f;
    [Tooltip(
        "V39 opt-in. On a fresh pinch, choose the exhibit closest to the " +
        "current eye gaze. Gaze alone never changes selection.")]
    public bool gazeTargetAnyExhibitV39;
    [Range(5f, 28f)] public float gazeTargetAngleV39 = 18f;
    [Tooltip(
        "V40 opt-in. On PICO hardware, consume only the official combined " +
        "remote pinch/select signal. Desktop, touch, controller and per-hand " +
        "joint-distance paths cannot compete with it.")]
    public bool officialPicoRemotePinchOnlyV40;
    [Min(0.10f)] public float officialReleaseSecondsV40 = 0.20f;
    [Tooltip(
        "V42 opt-in. Require the strict official pinch envelope, a minimum " +
        "hold, and a cooldown after each accepted action. Without this a " +
        "single hand pose could fire repeated carousel swaps.")]
    public bool strictDeliberatePinchV42;
    [Min(0.05f)] public float pinchHoldSecondsV42 = 0.12f;
    [Min(0.15f)] public float actionCooldownSecondsV42 = 0.55f;
    [Tooltip(
        "V42 opt-in. If the centred exhibit is inside this cone, a pinch " +
        "always enters it. Side exhibits are only focused when the centre is " +
        "clearly not being looked at, so the pick cannot flip per frame.")]
    public bool centrePriorityGazeV42;
    [Range(4f, 20f)] public float centreGazeAngleV42 = 9f;
    [Tooltip(
        "V42 opt-in. Keep the controller trigger live alongside the official " +
        "pinch so Selection can always be confirmed.")]
    public bool controllerFallbackV42;
    [Tooltip(
        "V43 opt-in. Target with a real ray cast straight out of the head " +
        "instead of an angular gaze cone. A pinch only counts when the ray " +
        "actually hits an exhibit collider.")]
    public bool headRaycastTargetingV43;
    [Min(1f)] public float headRayDistanceV43 = 18f;
    [Tooltip(
        "V44 opt-in. Use the shared long head ray and lock its stable target " +
        "when the pinch begins. The locked target cannot change while the " +
        "pinch is being held.")]
    public RuntimeHeadRayDebugV44 headRayTargetingV44;
    public bool lockHeadRayTargetOnPinchV44;
    public bool responsiveOfficialPicoPinchV44;

    readonly List<XRInputDevice> devices = new List<XRInputDevice>();
    readonly List<XRHandSubsystem> handSubsystems = new List<XRHandSubsystem>();
    XRHandSubsystem hands;
    float nextHandScan;
    bool controllerWasPressed;
    bool leftPinchWasPressed;
    bool rightPinchWasPressed;
    float controllerReleasedSinceV38 = -1f;
    float leftReleasedSinceV38 = -1f;
    float rightReleasedSinceV38 = -1f;
    bool touchTracking;
    Vector2 touchStart;
    bool officialWasPressedV40;
    float officialReleasedSinceV40 = -1f;
    bool officialArmedV40;
    float officialPressedSinceV42 = -1f;
    bool officialConsumedV42;
    float nextActionAllowedV42 = -1f;
    SelectionExhibitTargetV09 lockedHeadRayTargetV44;

    void Awake()
    {
        if (!selection)
            selection = GetComponent<SelectionCarouselController>();
        if (selection.sceneCamera &&
            selection.sceneCamera != sceneCamera)
            sceneCamera = selection.sceneCamera;
        else if (!sceneCamera)
            sceneCamera = Camera.main;
        RefreshTargets();
    }

    void OnEnable()
    {
        controllerWasPressed = false;
        leftPinchWasPressed = false;
        rightPinchWasPressed = false;
        controllerReleasedSinceV38 = -1f;
        leftReleasedSinceV38 = -1f;
        rightReleasedSinceV38 = -1f;
        officialWasPressedV40 = false;
        officialReleasedSinceV40 = -1f;
        officialArmedV40 = false;
        officialPressedSinceV42 = -1f;
        officialConsumedV42 = false;
        lockedHeadRayTargetV44 = null;
    }

    void Update()
    {
        if (!PersonallyPuzzledExperienceModeState.IsPlayerManual ||
            !selection || selection.IsTransitioning || selection.IsCommitting)
            return;
        if (selection.sceneCamera &&
            selection.sceneCamera != sceneCamera)
            sceneCamera = selection.sceneCamera;
        else if (!sceneCamera)
            sceneCamera = Camera.main;

        if (officialPicoRemotePinchOnlyV40 &&
            XRSettings.isDeviceActive)
        {
            if (!ReadOfficialPicoRemotePinchV40())
                ReadHands();
            // V42 keeps the controller trigger live so Selection can always
            // be confirmed even if the official hand device misreports.
            if (controllerFallbackV42)
                ReadControllers();
            return;
        }

        ReadDesktopAndTouch();
        ReadControllers();
        ReadHands();
    }

    bool ReadOfficialPicoRemotePinchV40()
    {
        bool tracked;
        bool pressed;
        bool available =
            responsiveOfficialPicoPinchV44
                ? PicoSwanPinchSignalV40.TryReadIntentV44(
                    out tracked,
                    out pressed)
                : strictDeliberatePinchV42
                ? PicoSwanPinchSignalV40.TryReadStrictV42(
                    out tracked,
                    out pressed)
                : PicoSwanPinchSignalV40.TryRead(
                    out tracked,
                    out pressed);
        if (!available)
            return false;
        if (!tracked)
        {
            officialWasPressedV40 = false;
            officialReleasedSinceV40 = -1f;
            officialArmedV40 = false;
            officialPressedSinceV42 = -1f;
            officialConsumedV42 = false;
            lockedHeadRayTargetV44 = null;
            return true;
        }

        if (!pressed)
        {
            if (officialReleasedSinceV40 < 0f)
                officialReleasedSinceV40 =
                    Time.unscaledTime;
            if (Time.unscaledTime -
                    officialReleasedSinceV40 >=
                Mathf.Max(
                    0.10f,
                    officialReleaseSecondsV40))
                officialArmedV40 = true;
            officialWasPressedV40 = false;
            officialPressedSinceV42 = -1f;
            officialConsumedV42 = false;
            lockedHeadRayTargetV44 = null;
            return true;
        }

        bool pressEdge =
            !officialWasPressedV40;
        officialWasPressedV40 = true;
        officialReleasedSinceV40 = -1f;

        if (strictDeliberatePinchV42)
        {
            if (pressEdge)
            {
                officialPressedSinceV42 = Time.unscaledTime;
                officialConsumedV42 = false;
                lockedHeadRayTargetV44 =
                    lockHeadRayTargetOnPinchV44 &&
                    headRayTargetingV44
                        ? headRayTargetingV44
                            .TargetForPinchV44()
                        : null;
            }
            if (!officialArmedV40 ||
                officialConsumedV42 ||
                officialPressedSinceV42 < 0f ||
                Time.unscaledTime - officialPressedSinceV42 <
                    Mathf.Max(0.05f, pinchHoldSecondsV42) ||
                Time.unscaledTime < nextActionAllowedV42)
                return true;
            officialConsumedV42 = true;
            officialArmedV40 = false;
            if (PerformDeliberateActionV42())
                nextActionAllowedV42 =
                    Time.unscaledTime +
                    Mathf.Max(0.15f, actionCooldownSecondsV42);
            return true;
        }

        if (!pressEdge ||
            !officialArmedV40)
            return true;

        // A single combined edge is consumed before changing any state. This
        // prevents left/right duplicate devices or tracking reacquisition from
        // performing multiple carousel actions for one physical pinch.
        officialArmedV40 = false;
        if (TryFocusGazeTargetV39())
            return true;

        // Always preserve a way forward: if the deliberate pinch did not land
        // inside a side target's angular envelope, confirm the current center.
        TryFocusCurrentCenterV40();
        return true;
    }

    /// <summary>
    /// V42: one accepted pinch performs exactly one action. Looking at the
    /// centred exhibit always enters it; otherwise the gazed side exhibit is
    /// brought to the centre.
    /// </summary>
    bool PerformDeliberateActionV42()
    {
        if (lockHeadRayTargetOnPinchV44 &&
            headRayTargetingV44)
            return lockedHeadRayTargetV44 &&
                lockedHeadRayTargetV44.RequestFocus();
        // V43: a real ray out of the head decides the target. Nothing is
        // selected unless the ray actually hits an exhibit collider.
        if (headRaycastTargetingV43)
            return TryFocusHeadRaycastV43();
        if (centrePriorityGazeV42 &&
            IsCentreGazedV42())
            return TryFocusCurrentCenterV40();
        if (TryFocusGazeTargetV39())
            return true;
        return TryFocusCurrentCenterV40();
    }

    /// <summary>
    /// V43: cast straight out of the head. The pinch is only consumed when the
    /// ray hits a semantic exhibit target.
    /// </summary>
    bool TryFocusHeadRaycastV43()
    {
        if (!sceneCamera)
            return false;
        Ray ray =
            new Ray(
                sceneCamera.transform.position,
                sceneCamera.transform.forward);
        RaycastHit[] hits =
            Physics.RaycastAll(
                ray,
                Mathf.Max(1f, headRayDistanceV43),
                ~0,
                QueryTriggerInteraction.Collide);
        if (hits.Length == 0)
            return false;
        Array.Sort(
            hits,
            (first, second) =>
                first.distance.CompareTo(second.distance));
        foreach (RaycastHit hit in hits)
        {
            if (!hit.collider)
                continue;
            SelectionExhibitTargetV09 target =
                hit.collider.GetComponentInParent<
                    SelectionExhibitTargetV09>();
            if (!target ||
                !target.enabled ||
                (targets != null &&
                 Array.IndexOf(targets, target) < 0))
                continue;
            return target.RequestFocus();
        }
        return false;
    }

    bool IsCentreGazedV42()
    {
        if (!sceneCamera || !selection)
            return false;
        Transform current = selection.CurrentExhibit;
        if (!current)
            return false;
        Renderer[] renderers =
            current.GetComponentsInChildren<Renderer>(true);
        bool initialized = false;
        Bounds bounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (!renderer || !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy)
                continue;
            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
                bounds.Encapsulate(renderer.bounds);
        }
        if (!initialized)
            return false;
        Vector3 direction =
            bounds.center - sceneCamera.transform.position;
        if (direction.sqrMagnitude <= 0.0001f)
            return false;
        return Vector3.Angle(
            sceneCamera.transform.forward,
            direction.normalized) <=
            Mathf.Clamp(centreGazeAngleV42, 4f, 20f);
    }

    bool TryFocusCurrentCenterV40()
    {
        if (!selection ||
            targets == null)
            return false;
        SelectionExhibitTargetV09 current =
            Array.Find(
                targets,
                value =>
                    value &&
                    value.enabled &&
                    value.IsCurrent &&
                    value.directPuzzleIntentV31);
        return current &&
            current.RequestFocus();
    }

    public void RefreshTargets()
    {
        SelectionExhibitTargetV09[] found =
            FindObjectsByType<SelectionExhibitTargetV09>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        targets = Array.FindAll(
            found,
            value => value && value.gameObject.scene == gameObject.scene);
    }

    void ReadDesktopAndTouch()
    {
        if (sceneCamera && Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
            TryFocusScreenPoint(Mouse.current.position.ReadValue());

        Touchscreen touchscreen = Touchscreen.current;
        if (!sceneCamera || touchscreen == null)
            return;
        var touch = touchscreen.primaryTouch;
        if (touch.press.wasPressedThisFrame)
        {
            touchTracking = true;
            touchStart = touch.position.ReadValue();
        }
        if (touchTracking && touch.press.wasReleasedThisFrame)
        {
            touchTracking = false;
            Vector2 position = touch.position.ReadValue();
            float maximumTravel = Mathf.Max(32f, Screen.width * 0.035f);
            if (Vector2.Distance(touchStart, position) <= maximumTravel)
                TryFocusScreenPoint(position);
        }
        else if (touchTracking && !touch.press.isPressed)
        {
            touchTracking = false;
        }
    }

    void ReadControllers()
    {
        devices.Clear();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.TrackedDevice,
            devices);
        bool pressed = false;
        XRInputDevice pressedDevice = default;
        foreach (XRInputDevice device in devices)
        {
            bool devicePressed =
                (device.TryGetFeatureValue(
                    XRCommonUsages.triggerButton,
                    out bool trigger) && trigger) ||
                (device.TryGetFeatureValue(
                    XRCommonUsages.primaryButton,
                    out bool primary) && primary);
            if (!devicePressed)
                continue;
            pressed = true;
            pressedDevice = device;
            break;
        }
        bool controllerEdge =
            pressed &&
            !controllerWasPressed;
        if (requireTrackedReleaseBeforeTargetV38)
        {
            if (!pressed)
            {
                if (controllerReleasedSinceV38 < 0f)
                    controllerReleasedSinceV38 =
                        Time.unscaledTime;
            }
            else
            {
                controllerEdge &=
                    controllerReleasedSinceV38 >= 0f &&
                    Time.unscaledTime -
                        controllerReleasedSinceV38 >=
                    Mathf.Max(
                        0.08f,
                        trackedReleaseSecondsV38);
                controllerReleasedSinceV38 = -1f;
            }
        }
        if (controllerEdge &&
            Time.unscaledTime >= nextActionAllowedV42)
        {
            if (controllerFallbackV42)
            {
                // V42: the controller shares the pinch cooldown and the same
                // centre-priority rule, so both paths behave identically.
                lockedHeadRayTargetV44 =
                    lockHeadRayTargetOnPinchV44 &&
                    headRayTargetingV44
                        ? headRayTargetingV44
                            .TargetForPinchV44()
                        : null;
                if (PerformDeliberateActionV42())
                    nextActionAllowedV42 =
                        Time.unscaledTime +
                        Mathf.Max(0.15f, actionCooldownSecondsV42);
            }
            else
                TryFocusController(pressedDevice);
        }
        controllerWasPressed = pressed;
    }

    void TryFocusController(XRInputDevice device)
    {
        if (!device.isValid ||
            !device.TryGetFeatureValue(
                XRCommonUsages.devicePosition,
                out Vector3 localPosition))
            return;
        Transform tracking = TrackingSpace();
        Vector3 origin = tracking
            ? tracking.TransformPoint(localPosition)
            : localPosition;
        if (TryFocusNear(origin))
            return;
        if (!device.TryGetFeatureValue(
                XRCommonUsages.deviceRotation,
                out Quaternion localRotation))
            return;
        Vector3 direction = tracking
            ? tracking.TransformDirection(localRotation * Vector3.forward)
            : localRotation * Vector3.forward;
        TryFocusRay(origin, direction);
    }

    void ReadHands()
    {
        if ((hands == null || !hands.running) &&
            Time.unscaledTime >= nextHandScan)
        {
            nextHandScan = Time.unscaledTime + 1f;
            handSubsystems.Clear();
            SubsystemManager.GetSubsystems(handSubsystems);
            hands = handSubsystems.Find(value => value != null && value.running);
        }
        if (hands == null || !hands.running)
        {
            leftPinchWasPressed = rightPinchWasPressed = false;
            leftReleasedSinceV38 =
                rightReleasedSinceV38 = -1f;
            return;
        }
        leftPinchWasPressed = ReadHand(
            hands.leftHand,
            leftPinchWasPressed,
            ref leftReleasedSinceV38);
        rightPinchWasPressed = ReadHand(
            hands.rightHand,
            rightPinchWasPressed,
            ref rightReleasedSinceV38);
    }

    bool ReadHand(
        XRHand hand,
        bool wasPressed,
        ref float releasedSinceV38)
    {
        if (!hand.isTracked ||
            !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(
                out Pose indexPose) ||
            !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(
                out Pose thumbPose))
        {
            releasedSinceV38 = -1f;
            return false;
        }
        float pinchDistance =
            nativeOpenXrPinchEnvelopeV33
                ? Mathf.Max(
                    0.034f,
                    nativeOpenXrPinchDistanceV33)
                : 0.034f;
        bool pressed =
            Vector3.Distance(
                indexPose.position,
                thumbPose.position) <= pinchDistance;
        if (!pressed)
        {
            if (requireTrackedReleaseBeforeTargetV38 &&
                releasedSinceV38 < 0f)
                releasedSinceV38 =
                    Time.unscaledTime;
            return false;
        }
        if (wasPressed)
            return pressed;
        if (requireTrackedReleaseBeforeTargetV38)
        {
            bool armed =
                releasedSinceV38 >= 0f &&
                Time.unscaledTime -
                    releasedSinceV38 >=
                Mathf.Max(
                    0.08f,
                    trackedReleaseSecondsV38);
            releasedSinceV38 = -1f;
            if (!armed)
                return pressed;
        }

        if (gazeTargetAnyExhibitV39 &&
            TryFocusGazeTargetV39())
            return pressed;

        Transform tracking = TrackingSpace();
        Vector3 indexWorld = tracking
            ? tracking.TransformPoint(indexPose.position)
            : indexPose.position;
        if (TryFocusNear(indexWorld))
            return pressed;

        Vector3 direction = tracking
            ? tracking.TransformDirection(indexPose.rotation * Vector3.forward)
            : indexPose.rotation * Vector3.forward;
        if (hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out Pose wristPose))
        {
            Vector3 wristWorld = tracking
                ? tracking.TransformPoint(wristPose.position)
                : wristPose.position;
            Vector3 handDirection = indexWorld - wristWorld;
            if (handDirection.sqrMagnitude > 0.0001f)
                direction = handDirection.normalized;
        }
        if (!TryFocusRay(indexWorld, direction) &&
            centeredGazePinchFallbackV33)
            TryFocusCenteredGaze();
        return pressed;
    }

    bool TryFocusGazeTargetV39()
    {
        if (!sceneCamera ||
            !selection ||
            targets == null)
            return false;

        SelectionExhibitTargetV09 best = null;
        float bestAngle =
            Mathf.Clamp(
                gazeTargetAngleV39,
                5f,
                28f);
        foreach (SelectionExhibitTargetV09 target in
                 targets)
        {
            if (!target ||
                !target.enabled)
                continue;
            Collider collider =
                target.GetComponent<Collider>();
            Vector3 center;
            if (collider)
            {
                center = collider.bounds.center;
            }
            else
            {
                Renderer[] renderers =
                    target.GetComponentsInChildren<
                        Renderer>(true);
                if (renderers.Length == 0)
                    continue;
                Bounds bounds =
                    renderers[0].bounds;
                for (int index = 1;
                     index < renderers.Length;
                     index++)
                    if (renderers[index])
                        bounds.Encapsulate(
                            renderers[index].bounds);
                center = bounds.center;
            }
            Vector3 direction =
                center -
                sceneCamera.transform.position;
            if (direction.sqrMagnitude <=
                0.0001f)
                continue;
            float angle =
                Vector3.Angle(
                    sceneCamera.transform.forward,
                    direction.normalized);
            if (angle > bestAngle)
                continue;
            bestAngle = angle;
            best = target;
        }
        return best &&
            best.RequestFocus();
    }

    bool TryFocusCenteredGaze()
    {
        if (!sceneCamera ||
            !selection ||
            targets == null)
            return false;
        SelectionExhibitTargetV09 centered =
            Array.Find(
                targets,
                value =>
                    value &&
                    value.IsCurrent &&
                    value.directPuzzleIntentV31);
        if (!centered)
            return false;
        Collider collider =
            centered.GetComponent<Collider>();
        Vector3 center =
            collider
                ? collider.bounds.center
                : centered.transform.position;
        Vector3 toCenter =
            center -
            sceneCamera.transform.position;
        if (toCenter.sqrMagnitude <= 0.0001f ||
            Vector3.Angle(
                sceneCamera.transform.forward,
                toCenter) >
            Mathf.Clamp(
                centeredGazeMaximumAngleV33,
                4f,
                30f))
            return false;
        return centered.RequestFocus();
    }

    bool TryFocusScreenPoint(Vector2 screenPosition)
    {
        return sceneCamera &&
               TryFocusRay(
                   sceneCamera.ScreenPointToRay(screenPosition).origin,
                   sceneCamera.ScreenPointToRay(screenPosition).direction);
    }

#if UNITY_EDITOR
    public bool TryFocusScreenPointForAutomation(Vector2 screenPosition)
    {
        return TryFocusScreenPoint(screenPosition);
    }
#endif

    bool TryFocusRay(Vector3 origin, Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return false;
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction.normalized,
            maximumRayDistance,
            ~0,
            QueryTriggerInteraction.Collide);
        Array.Sort(hits, (left, right) =>
            left.distance.CompareTo(right.distance));
        if (preferNearestTargetCenter)
        {
            SelectionExhibitTargetV09 best = null;
            float bestAngularDistance = float.PositiveInfinity;
            Vector3 rayDirection = direction.normalized;
            foreach (RaycastHit hit in hits)
            {
                SelectionExhibitTargetV09 target = hit.collider
                    ? hit.collider.GetComponentInParent<
                        SelectionExhibitTargetV09>()
                    : null;
                if (!target ||
                    (target.IsCurrent &&
                     !target.directPuzzleIntentV31))
                    continue;
                Vector3 toCenter =
                    hit.collider.bounds.center - origin;
                float forward =
                    Vector3.Dot(toCenter, rayDirection);
                if (forward <= 0f)
                    continue;
                Vector3 perpendicular =
                    toCenter - rayDirection * forward;
                float angularDistance =
                    perpendicular.sqrMagnitude /
                    Mathf.Max(0.0001f, forward * forward);
                if (angularDistance >= bestAngularDistance)
                    continue;
                best = target;
                bestAngularDistance = angularDistance;
            }
            return best && best.RequestFocus();
        }
        foreach (RaycastHit hit in hits)
        {
            SelectionExhibitTargetV09 target = hit.collider
                ? hit.collider.GetComponentInParent<SelectionExhibitTargetV09>()
                : null;
            if (!target ||
                (target.IsCurrent &&
                 !target.directPuzzleIntentV31))
                continue;
            return target.RequestFocus();
        }
        return false;
    }

    bool TryFocusNear(Vector3 worldPosition)
    {
        if (targets == null || targets.Length == 0)
            RefreshTargets();
        SelectionExhibitTargetV09 closest = null;
        float closestDistance = directTouchRadius;
        foreach (SelectionExhibitTargetV09 target in targets)
        {
            if (!target ||
                (target.IsCurrent &&
                 !target.directPuzzleIntentV31))
                continue;
            Collider collider = target.GetComponent<Collider>();
            Vector3 nearest = collider
                ? collider.ClosestPoint(worldPosition)
                : target.transform.position;
            float distance = Vector3.Distance(worldPosition, nearest);
            if (distance >= closestDistance)
                continue;
            closest = target;
            closestDistance = distance;
        }
        return closest && closest.RequestFocus();
    }

    Transform TrackingSpace()
    {
        return sceneCamera && sceneCamera.transform.parent
            ? sceneCamera.transform.parent
            : null;
    }
}
