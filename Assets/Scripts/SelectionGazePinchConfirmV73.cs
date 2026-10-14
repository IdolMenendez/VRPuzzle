using UnityEngine;

/// <summary>
/// Deliberate V73 entry gate. Gaze can only preview; a target must be watched
/// with an open hand first, then confirmed with a continuous new pinch.
/// </summary>
[DefaultExecutionOrder(-175)]
[DisallowMultipleComponent]
public sealed class SelectionGazePinchConfirmV73 : MonoBehaviour
{
    public SelectionGazeBoundsPinchDirectInputV72 gazePreview;
    [Min(0.05f)] public float openHandArmSeconds = 0.18f;
    [Min(0.05f)] public float confirmHoldSeconds = 0.14f;
    [Min(0.25f)] public float secondConfirmWindowSeconds = 2.2f;
    [Min(0.10f)] public float cooldownSeconds = 0.45f;

    SelectionExhibitTargetV09 armedTarget;
    float targetSeenOpenAt = -1f;
    float pressStartedAt = -1f;
    float nextAction;
    bool leftWasPressed;
    bool rightWasPressed;
    bool firstConfirmAccepted;
    float firstConfirmAt = -1f;

    void SetSecondConfirmFeedback(bool armed)
    {
        if (armedTarget && gazePreview && gazePreview.feedback)
            gazePreview.feedback.SetSecondConfirmArmedV75(
                armedTarget.exhibitIndex, armed);
    }

    void OnEnable()
    {
        armedTarget = null;
        targetSeenOpenAt = -1f;
        pressStartedAt = -1f;
        nextAction = 0f;
        leftWasPressed = rightWasPressed = false;
        firstConfirmAccepted = false;
        firstConfirmAt = -1f;
        SetSecondConfirmFeedback(false);
    }

    void Update()
    {
        if (!gazePreview || !RuntimeRealHandPinchV47.Instance ||
            !gazePreview.selection || gazePreview.selection.IsTransitioning ||
            gazePreview.selection.IsCommitting)
            return;
        bool leftAvailable = RuntimeRealHandPinchV47.Instance.TryGetHand(
            true, out RuntimeRealHandPinchV47.HandState left) && left.poseValid;
        bool rightAvailable = RuntimeRealHandPinchV47.Instance.TryGetHand(
            false, out RuntimeRealHandPinchV47.HandState right) && right.poseValid;
        bool leftPressed = leftAvailable && left.pressed;
        bool rightPressed = rightAvailable && right.pressed;
        bool pressed = leftPressed || rightPressed;
        SelectionExhibitTargetV09 target = gazePreview.ResolveGazeTargetV72();
        float now = Time.unscaledTime;

        if (!target || target != armedTarget)
        {
            armedTarget = target;
            targetSeenOpenAt = pressed ? -1f : now;
            pressStartedAt = -1f;
            firstConfirmAccepted = false;
            firstConfirmAt = -1f;
            SetSecondConfirmFeedback(false);
        }
        else if (!pressed && targetSeenOpenAt < 0f)
        {
            targetSeenOpenAt = now;
            pressStartedAt = -1f;
        }

        bool freshPress = (leftPressed && !leftWasPressed) ||
            (rightPressed && !rightWasPressed);
        leftWasPressed = leftPressed;
        rightWasPressed = rightPressed;
        if (!pressed)
            return;

        if (firstConfirmAccepted && now - firstConfirmAt > secondConfirmWindowSeconds)
        {
            firstConfirmAccepted = false;
            firstConfirmAt = -1f;
            SetSecondConfirmFeedback(false);
        }

        bool targetWasOpenArmed = targetSeenOpenAt >= 0f &&
            now - targetSeenOpenAt >= openHandArmSeconds;
        if (!targetWasOpenArmed)
            return;
        if (freshPress)
            pressStartedAt = now;
        if (pressStartedAt < 0f || now - pressStartedAt < confirmHoldSeconds ||
            now < nextAction)
            return;

        // First deliberate pinch only arms the visible selection. A distinct
        // second release-and-pinch is required to commit the scene route.
        if (!firstConfirmAccepted)
        {
            firstConfirmAccepted = true;
            firstConfirmAt = now;
            pressStartedAt = -1f;
            SetSecondConfirmFeedback(true);
            RuntimeAudioRouter.PlayCue(RuntimeAudioCue.Touch, target.transform.position);
            RuntimeHaptics.PulseAll(0.10f, 0.018f);
            Debug.Log("[PPVR V75 Selection] event=first-confirm exhibit=" + target.exhibitIndex);
            return;
        }

        // Consume the second press before requesting a transition, so a noisy
        // input frame cannot queue a second stage entry.
        nextAction = now + cooldownSeconds;
        pressStartedAt = -1f;
        targetSeenOpenAt = -1f;
        firstConfirmAccepted = false;
        firstConfirmAt = -1f;
        SetSecondConfirmFeedback(false);
        bool accepted = target.directPuzzleIntentV31 &&
            target.directPuzzleIntentV31.RequestEnterExhibit(target.exhibitIndex);
        RuntimeAudioRouter.PlayCue(accepted ? RuntimeAudioCue.Touch : RuntimeAudioCue.Error,
            target.transform.position);
        RuntimeHaptics.PulseAll(accepted ? 0.16f : 0.08f,
            accepted ? 0.022f : 0.015f);
        Debug.Log("[PPVR V73 Selection] event=deliberate-gaze-pinch exhibit=" +
            target.exhibitIndex + " accepted=" + accepted);
    }
}
