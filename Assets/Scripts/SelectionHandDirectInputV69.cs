using System;
using UnityEngine;

/// <summary>
/// V69 selection authority: the ray runs from the eye through the user's
/// fingertip, so the hand points at an exhibit without requiring the head to
/// stare at a world-space UI panel. A released hand arms one later pinch and
/// each pinch performs exactly one action.
/// </summary>
[DefaultExecutionOrder(-180)]
[DisallowMultipleComponent]
public sealed class SelectionHandDirectInputV69 : MonoBehaviour
{
    public SelectionCarouselController selection;
    public SelectionExhibitTargetV09[] targets;
    public SelectionMechanicalSelectorV66 feedback;
    public Camera sceneCamera;
    [Min(0.02f)] public float releaseArmSeconds = 0.08f;
    [Min(0.02f)] public float actionCooldownSeconds = 0.28f;
    [Min(0.01f)] public float rayRadius = 0.07f;
    [Min(1f)] public float rayLength = 18f;
    [Range(4f, 30f)] public float angularFallback = 17f;

    readonly RaycastHit[] hits = new RaycastHit[24];
    bool leftWasPressed;
    bool rightWasPressed;
    float releasedSince = -1f;
    float nextAction;
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

        RuntimeRealHandPinchV47.HandState pointing =
            rightAvailable ? right : left;
        SelectionExhibitTargetV09 target =
            rightAvailable || leftAvailable ? TargetForHand(pointing) : null;
        int nextPreview = target ? target.exhibitIndex : -1;
        if (nextPreview != previewIndex)
        {
            previewIndex = nextPreview;
            feedback?.SetPreviewIndexV66(previewIndex);
        }

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
        Debug.Log("[PPVR V69 Selection] event=hand-direct exhibit=" +
            target.exhibitIndex + " current=" + target.IsCurrent +
            " accepted=" + accepted);
    }

    SelectionExhibitTargetV09 TargetForHand(
        RuntimeRealHandPinchV47.HandState hand)
    {
        if (!sceneCamera || !hand.poseValid) return null;
        Transform tracking = sceneCamera.transform.parent;
        Vector3 tip = tracking
            ? tracking.TransformPoint(hand.localPinchPose.position)
            : hand.localPinchPose.position;
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
