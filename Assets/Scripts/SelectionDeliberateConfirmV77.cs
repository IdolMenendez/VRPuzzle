using UnityEngine;

/// <summary>
/// V77 Selection authority. Gaze remains as fluid as V76, while the first
/// fresh pinch locks a choice and a separate release-and-pinch confirms it.
/// The lock has no timeout, so the player can inspect and reconsider.
/// </summary>
[DefaultExecutionOrder(-175)]
[DisallowMultipleComponent]
public sealed class SelectionDeliberateConfirmV77 : MonoBehaviour
{
    public SelectionGazeBoundsPinchDirectInputV72 gazeResolver;
    public SelectionCarouselController selection;
    public SelectionMechanicalSelectorV66 feedback;
    [Min(0.02f)] public float stableGazeSeconds = 0.07f;
    [Min(0.02f)] public float switchGazeSeconds = 0.11f;
    [Min(0.05f)] public float gazeGraceSeconds = 0.20f;
    [Min(0.02f)] public float releaseArmSeconds = 0.06f;
    [Min(0.02f)] public float confirmHoldSeconds = 0.055f;
    [Min(0.10f)] public float actionCooldownSeconds = 0.16f;

    SelectionExhibitTargetV09 stableTarget;
    SelectionExhibitTargetV09 candidateTarget;
    SelectionExhibitTargetV09 pressedTarget;
    SelectionExhibitTargetV09 lockedTarget;
    float candidateSince = -1f;
    float stableLastSeen = -1f;
    float releasedSince = -1f;
    float pressStartedAt = -1f;
    float nextAction;
    bool leftWasPressed;
    bool rightWasPressed;
    bool armed;
    int lockActionCount;
    int acceptedActionCount;

    public SelectionExhibitTargetV09 StableTargetV77 => stableTarget;
    public SelectionExhibitTargetV09 LockedTargetV77 => lockedTarget;
    public int LockActionCountV77 => lockActionCount;
    public int AcceptedActionCountV77 => acceptedActionCount;

    void Awake()
    {
        if (!gazeResolver)
            gazeResolver =
                FindFirstObjectByType<SelectionGazeBoundsPinchDirectInputV72>();
        if (!selection && gazeResolver)
            selection = gazeResolver.selection;
        if (!feedback && gazeResolver)
            feedback = gazeResolver.feedback;
        gazeResolver?.RefreshTargets();
    }

    void OnEnable()
    {
        stableTarget = null;
        candidateTarget = null;
        pressedTarget = null;
        lockedTarget = null;
        candidateSince = -1f;
        stableLastSeen = -1f;
        releasedSince = -1f;
        pressStartedAt = -1f;
        nextAction = 0f;
        leftWasPressed = false;
        rightWasPressed = false;
        armed = false;
        lockActionCount = 0;
        acceptedActionCount = 0;
        feedback?.SetPreviewIndexV66(-1);
    }

    void OnDisable()
    {
        if (lockedTarget && feedback)
            feedback.SetSecondConfirmArmedV75(
                lockedTarget.exhibitIndex, false);
    }

    void Update()
    {
        if (!gazeResolver || !selection || !RuntimeRealHandPinchV47.Instance ||
            selection.IsTransitioning || selection.IsCommitting)
            return;

        RefreshLiveCamera();
        float now = Time.unscaledTime;
        UpdateStableTarget(gazeResolver.ResolveGazeTargetV72(), now);

        RuntimeRealHandPinchV47 provider = RuntimeRealHandPinchV47.Instance;
        bool leftAvailable = provider.TryGetHand(
            true, out RuntimeRealHandPinchV47.HandState left) && left.poseValid;
        bool rightAvailable = provider.TryGetHand(
            false, out RuntimeRealHandPinchV47.HandState right) && right.poseValid;
        bool leftPressed = leftAvailable && left.pressed;
        bool rightPressed = rightAvailable && right.pressed;
        bool anyAvailable = leftAvailable || rightAvailable;
        bool anyPressed = leftPressed || rightPressed;
        bool freshPress = leftPressed && !leftWasPressed ||
                          rightPressed && !rightWasPressed;
        leftWasPressed = leftPressed;
        rightWasPressed = rightPressed;

        if (!anyPressed)
        {
            if (anyAvailable)
            {
                if (releasedSince < 0f)
                    releasedSince = now;
                if (now - releasedSince >= releaseArmSeconds)
                    armed = true;
            }
            else
            {
                releasedSince = -1f;
            }
            pressedTarget = null;
            pressStartedAt = -1f;
            return;
        }

        releasedSince = -1f;
        if (freshPress && armed && stableTarget && now >= nextAction)
        {
            armed = false;
            pressedTarget = stableTarget;
            pressStartedAt = now;
        }

        if (!pressedTarget || pressStartedAt < 0f ||
            now - pressStartedAt < confirmHoldSeconds)
            return;

        SelectionExhibitTargetV09 target = pressedTarget;
        pressedTarget = null;
        pressStartedAt = -1f;
        nextAction = now + actionCooldownSeconds;

        if (lockedTarget != target)
        {
            SetLockedTarget(target);
            lockActionCount++;
            RuntimeAudioRouter.PlayCue(
                RuntimeAudioCue.Touch, target.transform.position);
            RuntimeHaptics.PulseAll(0.10f, 0.018f);
            Debug.Log("[PPVR V77 Selection] event=choice-locked exhibit=" +
                target.exhibitIndex);
            return;
        }

        bool accepted = target.directPuzzleIntentV31 &&
            target.directPuzzleIntentV31.RequestEnterExhibit(target.exhibitIndex);
        if (accepted)
            acceptedActionCount++;
        RuntimeAudioRouter.PlayCue(
            accepted ? RuntimeAudioCue.Touch : RuntimeAudioCue.Error,
            target.transform.position);
        RuntimeHaptics.PulseAll(
            accepted ? 0.16f : 0.08f,
            accepted ? 0.022f : 0.015f);
        Debug.Log("[PPVR V77 Selection] event=choice-confirmed exhibit=" +
            target.exhibitIndex + " accepted=" + accepted);
        if (accepted)
            ClearLockedTarget();
    }

    void RefreshLiveCamera()
    {
        Camera preferred = PersistentPuzzleBootstrap.Instance &&
            PersistentPuzzleBootstrap.Instance.xrCamera
            ? PersistentPuzzleBootstrap.Instance.xrCamera
            : selection && selection.sceneCamera
                ? selection.sceneCamera
                : Camera.main;
        if (preferred)
            gazeResolver.sceneCamera = preferred;
    }

    void UpdateStableTarget(SelectionExhibitTargetV09 observed, float now)
    {
        if (observed == stableTarget)
        {
            if (stableTarget)
                stableLastSeen = now;
            candidateTarget = null;
            candidateSince = -1f;
            return;
        }

        if (observed)
        {
            if (observed != candidateTarget)
            {
                candidateTarget = observed;
                candidateSince = now;
            }
            float required = stableTarget ? switchGazeSeconds : stableGazeSeconds;
            if (now - candidateSince >= required)
                SetStableTarget(observed, now);
            return;
        }

        candidateTarget = null;
        candidateSince = -1f;
        if (stableTarget && now - stableLastSeen > gazeGraceSeconds)
            SetStableTarget(null, now);
    }

    void SetStableTarget(SelectionExhibitTargetV09 target, float now)
    {
        stableTarget = target;
        stableLastSeen = target ? now : -1f;
        candidateTarget = null;
        candidateSince = -1f;
        if (!lockedTarget)
            feedback?.SetPreviewIndexV66(target ? target.exhibitIndex : -1);
        Debug.Log("[PPVR V77 Selection] event=stable-gaze exhibit=" +
            (target ? target.exhibitIndex : -1));
    }

    void SetLockedTarget(SelectionExhibitTargetV09 target)
    {
        if (lockedTarget && feedback)
            feedback.SetSecondConfirmArmedV75(
                lockedTarget.exhibitIndex, false);
        lockedTarget = target;
        if (!feedback)
            return;
        feedback.SetPreviewIndexV66(target.exhibitIndex);
        feedback.SetSecondConfirmArmedV75(target.exhibitIndex, true);
    }

    void ClearLockedTarget()
    {
        if (lockedTarget && feedback)
            feedback.SetSecondConfirmArmedV75(
                lockedTarget.exhibitIndex, false);
        lockedTarget = null;
    }
}
