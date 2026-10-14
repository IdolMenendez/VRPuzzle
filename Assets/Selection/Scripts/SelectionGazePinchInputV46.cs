using System;
using UnityEngine;

/// <summary>
/// V46 Selection authority. Gaze only identifies a stable visible exhibit.
/// A fresh pinch locks that target and performs one action: a side exhibit
/// moves to center, while the already-centered exhibit enters its puzzle.
/// </summary>
[DefaultExecutionOrder(-170)]
[DisallowMultipleComponent]
public sealed class SelectionGazePinchInputV46 : MonoBehaviour
{
    public SelectionCarouselController selection;
    public RuntimeIntentRayV46 intentRay;
    public SelectionExhibitTargetV09[] targets;
    [Min(0.05f)] public float releaseArmSeconds = 0.10f;
    [Min(0.02f)] public float pinchHoldSeconds = 0.05f;
    [Min(0.10f)] public float actionCooldownSeconds = 0.30f;
    [Header("V47 tracking-gap recovery")]
    public bool resilientSignalGapV47;
    [Min(0.05f)] public float signalGapArmSecondsV47 = 0.12f;
    [Header("V49 pinch-edge diagnostics")]
    [Tooltip(
        "Log every press edge with the accept/reject reason so a worn run " +
        "pinpoints why a real pinch did or did not act. Cleanly removable.")]
    public bool pinchEdgeTelemetryV49 = true;

    float releasedSince = -1f;
    float unavailableSince = -1f;
    float pressedSince = -1f;
    float nextActionAllowed;
    bool armed;
    bool wasPressed;
    bool consumed;
    SelectionExhibitTargetV09 lockedTarget;

    public bool ArmedV46 => armed;
    public SelectionExhibitTargetV09 LockedTargetV46 => lockedTarget;
    public int AcceptedActionCountV46 { get; private set; }

    void Awake()
    {
        if (!selection)
            selection = GetComponent<SelectionCarouselController>();
        if (!intentRay)
            intentRay = GetComponent<RuntimeIntentRayV46>();
        RefreshTargetsV46();
    }

    void OnEnable()
    {
        ResetGate();
    }

    void Update()
    {
        if (!PersonallyPuzzledExperienceModeState.IsPlayerManual ||
            !selection ||
            !intentRay)
            return;
        if (selection.IsTransitioning ||
            selection.IsCommitting)
        {
            wasPressed = intentRay.PinchingV46;
            return;
        }

        ProcessInput(
            intentRay.PinchTrackedV46,
            intentRay.PinchingV46,
            intentRay.StableSelectionTargetV46,
            Time.unscaledTime);
    }

    public void RefreshTargetsV46()
    {
        if (!selection)
        {
            targets = Array.Empty<SelectionExhibitTargetV09>();
            return;
        }
        intentRay?.RefreshSelectionTargetsV46();
        targets = intentRay && intentRay.selectionTargets != null
            ? intentRay.selectionTargets
            : FindObjectsByType<SelectionExhibitTargetV09>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
    }

    void ProcessInput(
        bool tracked,
        bool pressed,
        SelectionExhibitTargetV09 stableTarget,
        float now)
    {
        if (!tracked)
        {
            if (!resilientSignalGapV47)
            {
                ResetGate();
                return;
            }
            if (unavailableSince < 0f)
                unavailableSince = now;
            if (now - unavailableSince >=
                Mathf.Max(0.05f, signalGapArmSecondsV47))
            {
                if (!armed)
                    Debug.Log(
                        "[PPVR V47 Gate] stage=Selection event=armed " +
                        "reason=tracking-gap gap=" +
                        (now - unavailableSince).ToString("F3"));
                armed = true;
                wasPressed = false;
                consumed = false;
                pressedSince = -1f;
                lockedTarget = null;
            }
            return;
        }
        unavailableSince = -1f;

        if (!pressed)
        {
            if (releasedSince < 0f)
                releasedSince = now;
            if (now - releasedSince >=
                Mathf.Max(0.05f, releaseArmSeconds))
                armed = true;
            wasPressed = false;
            consumed = false;
            pressedSince = -1f;
            lockedTarget = null;
            return;
        }

        bool pressEdge = !wasPressed;
        wasPressed = true;
        releasedSince = -1f;
        if (pressEdge)
        {
            lockedTarget =
                armed &&
                now >= nextActionAllowed
                    ? stableTarget
                    : null;
            pressedSince =
                lockedTarget ? now : -1f;
            consumed = lockedTarget == null;
            if (resilientSignalGapV47 || pinchEdgeTelemetryV49)
                Debug.Log(
                    "[PPVR V49 Gate] stage=Selection event=press-edge " +
                    "armed=" + armed +
                    " target=" +
                    (stableTarget ? stableTarget.name : "none") +
                    " locked=" +
                    (lockedTarget ? lockedTarget.name : "none") +
                    " cooldown=" + (now < nextActionAllowed));
        }
        if (consumed ||
            !lockedTarget ||
            pressedSince < 0f ||
            now - pressedSince <
                Mathf.Max(0.02f, pinchHoldSeconds))
            return;

        consumed = true;
        armed = false;
        if (!lockedTarget.RequestFocus())
        {
            if (resilientSignalGapV47 || pinchEdgeTelemetryV49)
                Debug.Log(
                    "[PPVR V49 Gate] stage=Selection event=rejected " +
                    "reason=RequestFocus-false target=" +
                    lockedTarget.name);
            return;
        }
        AcceptedActionCountV46++;
        if (resilientSignalGapV47 || pinchEdgeTelemetryV49)
            Debug.Log(
                "[PPVR V49 Gate] stage=Selection event=accepted " +
                "target=" + lockedTarget.name +
                " count=" + AcceptedActionCountV46);
        nextActionAllowed =
            now +
            Mathf.Max(0.10f, actionCooldownSeconds);
    }

    void ResetGate()
    {
        releasedSince = -1f;
        unavailableSince = -1f;
        pressedSince = -1f;
        nextActionAllowed = 0f;
        armed = false;
        wasPressed = false;
        consumed = false;
        lockedTarget = null;
    }

#if UNITY_EDITOR
    public void ProcessForAutomationV47(
        bool tracked,
        bool pressed,
        SelectionExhibitTargetV09 stableTarget,
        float now)
    {
        ProcessInput(tracked, pressed, stableTarget, now);
    }

    public void ArmForAutomationV46()
    {
        releasedSince = -1f;
        pressedSince = -1f;
        nextActionAllowed = 0f;
        armed = true;
        wasPressed = false;
        consumed = false;
        lockedTarget = null;
    }

    public bool TryPinchTargetForAutomationV46(
        SelectionExhibitTargetV09 target)
    {
        if (!armed ||
            !target ||
            !target.enabled ||
            selection.IsTransitioning ||
            selection.IsCommitting)
            return false;
        armed = false;
        consumed = true;
        lockedTarget = target;
        bool accepted = target.RequestFocus();
        if (accepted)
            AcceptedActionCountV46++;
        return accepted;
    }

    public void ReleaseForAutomationV46()
    {
        armed = true;
        wasPressed = false;
        consumed = false;
        lockedTarget = null;
    }
#endif
}
