using System;
using UnityEngine;

/// <summary>
/// V70 Selection hand authority. Uses the native index-tip world position
/// supplied by RuntimeRealHandPinchV47, which is already converted through the
/// XR Origin CameraFloorOffsetObject. This avoids the camera-parent transform
/// mismatch that made V69 point at empty space on the worn PICO rig.
/// </summary>
[DefaultExecutionOrder(-180)]
[DisallowMultipleComponent]
public sealed class SelectionHandDirectInputV70 : MonoBehaviour
{
    public SelectionCarouselController selection;
    public SelectionExhibitTargetV09[] targets;
    public SelectionMechanicalSelectorV66 feedback;
    public Camera sceneCamera;
    [Min(0.02f)] public float releaseArmSeconds = 0.08f;
    [Min(0.02f)] public float actionCooldownSeconds = 0.30f;
    [Min(0.01f)] public float rayRadius = 0.085f;
    [Min(1f)] public float rayLength = 18f;
    [Range(4f, 30f)] public float angularFallback = 18f;

    readonly RaycastHit[] hits = new RaycastHit[24];
    bool leftWasPressed;
    bool rightWasPressed;
    float releasedSince = -1f;
    float nextAction;
    float nextPreviewFeedback;
    bool armed;
    int previewIndex = -1;

    void Awake()
    {
        if (!selection) selection = GetComponent<SelectionCarouselController>();
        if (!sceneCamera)
            sceneCamera = selection && selection.sceneCamera
                ? selection.sceneCamera : Camera.main;
        RefreshTargets();
    }

    void OnEnable()
    {
        leftWasPressed = rightWasPressed = false;
        releasedSince = -1f;
        nextAction = 0f;
        nextPreviewFeedback = 0f;
        armed = false;
    }

    void Update()
    {
        if (!selection || selection.IsTransitioning || selection.IsCommitting ||
            !RuntimeRealHandPinchV47.Instance)
            return;
        if (!sceneCamera)
            sceneCamera = selection.sceneCamera ? selection.sceneCamera : Camera.main;

        bool leftAvailable = RuntimeRealHandPinchV47.Instance.TryGetHand(
            true, out RuntimeRealHandPinchV47.HandState left) && left.poseValid;
        bool rightAvailable = RuntimeRealHandPinchV47.Instance.TryGetHand(
            false, out RuntimeRealHandPinchV47.HandState right) && right.poseValid;
        bool leftPressed = leftAvailable && left.pressed;
        bool rightPressed = rightAvailable && right.pressed;
        bool anyPressed = leftPressed || rightPressed;

        if (!anyPressed)
        {
            if (releasedSince < 0f) releasedSince = Time.unscaledTime;
            if (Time.unscaledTime - releasedSince >= releaseArmSeconds)
                armed = true;
        }
        else
            releasedSince = -1f;

        // The hand that is actively pinching owns the action. When neither is
        // pinching, prefer a hand that actually resolves a target instead of
        // letting an idle right hand suppress a correctly aimed left hand.
        SelectionExhibitTargetV09 rightTarget = rightAvailable
            ? TargetForHand(false) : null;
        SelectionExhibitTargetV09 leftTarget = leftAvailable
            ? TargetForHand(true) : null;
        SelectionExhibitTargetV09 target = rightPressed ? rightTarget :
            leftPressed ? leftTarget : rightTarget ? rightTarget : leftTarget;
        SetPreview(target);

        bool fresh = leftPressed && !leftWasPressed ||
                     rightPressed && !rightWasPressed;
        leftWasPressed = leftPressed;
        rightWasPressed = rightPressed;
        if (!fresh || !armed || Time.unscaledTime < nextAction || !target)
            return;

        armed = false;
        nextAction = Time.unscaledTime + actionCooldownSeconds;
        bool accepted = target.RequestFocus();
        RuntimeAudioRouter.PlayCue(
            accepted ? RuntimeAudioCue.Touch : RuntimeAudioCue.Error,
            target.transform.position);
        RuntimeHaptics.PulseAll(accepted ? 0.16f : 0.08f,
            accepted ? 0.022f : 0.015f);
        Debug.Log("[PPVR V70 Selection] event=hand-direct exhibit=" +
            target.exhibitIndex + " current=" + target.IsCurrent +
            " accepted=" + accepted);
    }

    void SetPreview(SelectionExhibitTargetV09 target)
    {
        int next = target ? target.exhibitIndex : -1;
        if (next == previewIndex) return;
        previewIndex = next;
        feedback?.SetPreviewIndexV66(next);
        if (target && Time.unscaledTime >= nextPreviewFeedback)
        {
            nextPreviewFeedback = Time.unscaledTime + 0.12f;
            RuntimeAudioRouter.PlayCue(RuntimeAudioCue.Hint,
                target.transform.position);
            RuntimeHaptics.PulseAll(0.09f, 0.012f);
        }
        Debug.Log("[PPVR V70 Selection] event=preview exhibit=" + next);
    }

    SelectionExhibitTargetV09 TargetForHand(bool leftHand)
    {
        if (!sceneCamera || !RuntimeRealHandPinchV47.Instance ||
            !RuntimeRealHandPinchV47.Instance.TryGetIndexTipWorldV59(
                leftHand, out Vector3 tip))
            return null;
        return ResolveTargetForTipWorldV70(tip);
    }

    // Kept explicit for the PlayMode smoke: it verifies the same native-tip
    // conversion used in Update, rather than only testing geometry with a
    // pre-converted world point.
    public SelectionExhibitTargetV09 ResolveTrackedHandForSmokeV70(bool leftHand)
    {
        return TargetForHand(leftHand);
    }

    public SelectionExhibitTargetV09 ResolveTargetForTipWorldV70(Vector3 tip)
    {
        if (!sceneCamera) return null;
        Vector3 direction = tip - sceneCamera.transform.position;
        if (direction.sqrMagnitude < 0.01f) return null;
        direction.Normalize();
        Ray ray = new Ray(sceneCamera.transform.position, direction);
        int count = Physics.SphereCastNonAlloc(
            ray, rayRadius, hits, rayLength, ~0,
            QueryTriggerInteraction.Collide);
        SelectionExhibitTargetV09 best = null;
        float distance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (!hits[i].collider) continue;
            SelectionExhibitTargetV09 candidate =
                hits[i].collider.GetComponentInParent<SelectionExhibitTargetV09>();
            if (!Valid(candidate) || hits[i].distance >= distance) continue;
            best = candidate;
            distance = hits[i].distance;
        }
        if (best) return best;

        float angle = angularFallback;
        foreach (SelectionExhibitTargetV09 candidate in targets ??
                 Array.Empty<SelectionExhibitTargetV09>())
        {
            if (!Valid(candidate)) continue;
            Vector3 to = candidate.transform.position - sceneCamera.transform.position;
            if (to.sqrMagnitude < 0.01f) continue;
            float candidateAngle = Vector3.Angle(direction, to.normalized);
            if (candidateAngle >= angle) continue;
            angle = candidateAngle;
            best = candidate;
        }
        return best;
    }

    bool Valid(SelectionExhibitTargetV09 target) =>
        target && target.enabled && target.gameObject.activeInHierarchy &&
        target.carousel == selection && Array.IndexOf(targets, target) >= 0;

    public void RefreshTargets()
    {
        targets = FindObjectsByType<SelectionExhibitTargetV09>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        targets = Array.FindAll(targets, value =>
            value && value.gameObject.scene == gameObject.scene && value.enabled);
        Array.Sort(targets, (a, b) => a.exhibitIndex.CompareTo(b.exhibitIndex));
    }
}
