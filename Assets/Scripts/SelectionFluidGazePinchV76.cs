using UnityEngine;

/// <summary>
/// V76 Selection authority: short stabilized head gaze previews one exhibit,
/// and one fresh deliberate pinch enters it. The selected target is locked at
/// pinch start so normal head jitter cannot redirect the action.
/// </summary>
[DefaultExecutionOrder(-175)]
[DisallowMultipleComponent]
public sealed class SelectionFluidGazePinchV76 : MonoBehaviour
{
    public SelectionGazeBoundsPinchDirectInputV72 gazeResolver;
    public SelectionCarouselController selection;
    public SelectionMechanicalSelectorV66 feedback;
    [Min(0.02f)] public float stableGazeSeconds = 0.07f;
    [Min(0.02f)] public float switchGazeSeconds = 0.11f;
    [Min(0.05f)] public float gazeGraceSeconds = 0.20f;
    [Min(0.02f)] public float releaseArmSeconds = 0.06f;
    [Min(0.02f)] public float confirmHoldSeconds = 0.055f;
    [Min(0.10f)] public float actionCooldownSeconds = 0.30f;

    SelectionExhibitTargetV09 stableTarget;
    SelectionExhibitTargetV09 candidateTarget;
    SelectionExhibitTargetV09 pressedTarget;
    float candidateSince = -1f;
    float stableLastSeen = -1f;
    float releasedSince = -1f;
    float pressStartedAt = -1f;
    float nextAction;
    bool leftWasPressed;
    bool rightWasPressed;
    bool armed;
    int acceptedActionCount;

    public SelectionExhibitTargetV09 StableTargetV76 => stableTarget;
    public int AcceptedActionCountV76 => acceptedActionCount;

    void Awake()
    {
        if (!gazeResolver)
            gazeResolver = FindFirstObjectByType<SelectionGazeBoundsPinchDirectInputV72>();
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
        candidateSince = -1f;
        stableLastSeen = -1f;
        releasedSince = -1f;
        pressStartedAt = -1f;
        nextAction = 0f;
        leftWasPressed = false;
        rightWasPressed = false;
        armed = false;
        acceptedActionCount = 0;
        feedback?.SetPreviewIndexV66(-1);
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
        bool accepted = target.directPuzzleIntentV31 &&
            target.directPuzzleIntentV31.RequestEnterExhibit(target.exhibitIndex);
        if (accepted)
            acceptedActionCount++;
        RuntimeAudioRouter.PlayCue(
            accepted ? RuntimeAudioCue.Touch : RuntimeAudioCue.Error,
            target.transform.position);
        RuntimeHaptics.PulseAll(
            accepted ? 0.14f : 0.08f,
            accepted ? 0.020f : 0.015f);
        Debug.Log("[PPVR V76 Selection] event=single-gaze-pinch exhibit=" +
            target.exhibitIndex + " accepted=" + accepted);
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
        feedback?.SetPreviewIndexV66(target ? target.exhibitIndex : -1);
        Debug.Log("[PPVR V76 Selection] event=stable-gaze exhibit=" +
            (target ? target.exhibitIndex : -1));
    }
}
