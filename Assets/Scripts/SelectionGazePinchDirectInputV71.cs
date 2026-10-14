using System;
using UnityEngine;

/// <summary>
/// V71 Selection authority: the player's head gaze chooses the exhibit and a
/// fresh pinch confirms it. There is deliberately no hand-direction ray and
/// no grab affordance: looking at a model moves the physical selector there;
/// pinching with either tracked hand enters that model's puzzle directly.
/// </summary>
[DefaultExecutionOrder(-180)]
[DisallowMultipleComponent]
public sealed class SelectionGazePinchDirectInputV71 : MonoBehaviour
{
    public SelectionCarouselController selection;
    public SelectionExhibitTargetV09[] targets;
    public SelectionMechanicalSelectorV66 feedback;
    public Camera sceneCamera;
    [Min(0.02f)] public float releaseArmSeconds = 0.08f;
    [Min(0.02f)] public float actionCooldownSeconds = 0.30f;
    [Min(0.01f)] public float rayRadius = 0.14f;
    [Min(1f)] public float rayLength = 18f;
    [Range(4f, 30f)] public float angularFallback = 18f;

    readonly RaycastHit[] hits = new RaycastHit[24];
    bool leftWasPressed;
    bool rightWasPressed;
    float releasedSince = -1f;
    float nextAction;
    bool armed;
    int previewIndex = -1;
#if UNITY_EDITOR
    bool hasAutomationGaze;
    Ray automationGaze;
#endif

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
        previewIndex = -1;
    }

    void Update()
    {
        if (!selection || selection.IsTransitioning || selection.IsCommitting ||
            !RuntimeRealHandPinchV47.Instance)
            return;
        if (!sceneCamera)
            sceneCamera = selection.sceneCamera ? selection.sceneCamera : Camera.main;
        if (!sceneCamera) return;

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
        else releasedSince = -1f;

        SelectionExhibitTargetV09 target = ResolveGazeTargetV71();
        SetPreview(target);
        bool fresh = leftPressed && !leftWasPressed ||
                     rightPressed && !rightWasPressed;
        leftWasPressed = leftPressed;
        rightWasPressed = rightPressed;
        if (!fresh || !armed || Time.unscaledTime < nextAction || !target)
            return;

        armed = false;
        nextAction = Time.unscaledTime + actionCooldownSeconds;
        bool accepted = target.directPuzzleIntentV31 &&
            target.directPuzzleIntentV31.RequestEnterExhibit(target.exhibitIndex);
        RuntimeAudioRouter.PlayCue(accepted ? RuntimeAudioCue.Touch : RuntimeAudioCue.Error,
            target.transform.position);
        RuntimeHaptics.PulseAll(accepted ? 0.16f : 0.08f,
            accepted ? 0.022f : 0.015f);
        Debug.Log("[PPVR V71 Selection] event=gaze-pinch exhibit=" +
            target.exhibitIndex + " accepted=" + accepted);
    }

    void SetPreview(SelectionExhibitTargetV09 target)
    {
        int next = target ? target.exhibitIndex : -1;
        if (next == previewIndex) return;
        previewIndex = next;
        feedback?.SetPreviewIndexV66(next);
        Debug.Log("[PPVR V71 Selection] event=gaze-preview exhibit=" + next);
    }

    public SelectionExhibitTargetV09 ResolveGazeTargetV71()
    {
        if (!sceneCamera) return null;
#if UNITY_EDITOR
        Ray ray = hasAutomationGaze ? automationGaze : new Ray(
            sceneCamera.transform.position, sceneCamera.transform.forward.normalized);
#else
        Ray ray = new Ray(sceneCamera.transform.position,
            sceneCamera.transform.forward.normalized);
#endif
        return ResolveTargetForRayV71(ray);
    }

    public SelectionExhibitTargetV09 ResolveTargetForRayV71(Ray ray)
    {
        int count = Physics.SphereCastNonAlloc(ray, rayRadius, hits, rayLength,
            ~0, QueryTriggerInteraction.Collide);
        SelectionExhibitTargetV09 best = null;
        float distance = float.MaxValue;
        for (int index = 0; index < count; index++)
        {
            if (!hits[index].collider) continue;
            SelectionExhibitTargetV09 candidate = hits[index].collider
                .GetComponentInParent<SelectionExhibitTargetV09>();
            if (!Valid(candidate) || hits[index].distance >= distance) continue;
            best = candidate;
            distance = hits[index].distance;
        }
        if (best) return best;

        float angle = angularFallback;
        foreach (SelectionExhibitTargetV09 candidate in targets ??
                 Array.Empty<SelectionExhibitTargetV09>())
        {
            if (!Valid(candidate)) continue;
            Vector3 to = candidate.transform.position - ray.origin;
            if (to.sqrMagnitude < 0.01f) continue;
            float candidateAngle = Vector3.Angle(ray.direction, to.normalized);
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
        targets = Array.FindAll(targets, value => value &&
            value.gameObject.scene == gameObject.scene && value.enabled);
        Array.Sort(targets, (a, b) => a.exhibitIndex.CompareTo(b.exhibitIndex));
    }

#if UNITY_EDITOR
    public void SetAutomationGazeRayV71(Ray ray)
    {
        hasAutomationGaze = true;
        automationGaze = ray;
    }

    public void ClearAutomationGazeRayV71() => hasAutomationGaze = false;
#endif
}
