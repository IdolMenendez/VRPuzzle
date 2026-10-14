using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// V72 Selection authority. Head gaze is resolved from each exhibit's rendered
/// silhouette bounds, not the semantic target's transform or proxy collider.
/// This makes every model in the carousel selectable where it is actually
/// visible; a fresh tracked-hand pinch confirms the currently gazed exhibit.
/// </summary>
[DefaultExecutionOrder(-180)]
[DisallowMultipleComponent]
public sealed class SelectionGazeBoundsPinchDirectInputV72 : MonoBehaviour
{
    public SelectionCarouselController selection;
    public SelectionExhibitTargetV09[] targets;
    public SelectionMechanicalSelectorV66 feedback;
    public Camera sceneCamera;
    [Min(0.02f)] public float releaseArmSeconds = 0.08f;
    [Min(0.02f)] public float actionCooldownSeconds = 0.30f;
    [Min(1f)] public float rayLength = 18f;
    [Min(0f)] public float visibleBoundsPadding = 0.12f;
    [Range(4f, 35f)] public float angularFallback = 24f;
    [Tooltip("V73 uses this component as gaze-preview only and owns confirmation separately.")]
    public bool directEntryEnabled = true;

    readonly Dictionary<Transform, Renderer[]> rendererCache =
        new Dictionary<Transform, Renderer[]>();
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
        ResolveSceneCamera();
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
        ResolveSceneCamera();
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

        SelectionExhibitTargetV09 target = ResolveGazeTargetV72();
        SetPreview(target);
        if (!directEntryEnabled)
        {
            leftWasPressed = leftPressed;
            rightWasPressed = rightPressed;
            return;
        }
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
        Debug.Log("[PPVR V72 Selection] event=gaze-pinch exhibit=" +
            target.exhibitIndex + " accepted=" + accepted);
    }

    void SetPreview(SelectionExhibitTargetV09 target)
    {
        int next = target ? target.exhibitIndex : -1;
        if (next == previewIndex) return;
        previewIndex = next;
        feedback?.SetPreviewIndexV66(next);
        Debug.Log("[PPVR V72 Selection] event=visible-bounds-preview exhibit=" + next);
    }

    public SelectionExhibitTargetV09 ResolveGazeTargetV72()
    {
        if (!sceneCamera) return null;
#if UNITY_EDITOR
        Ray ray = hasAutomationGaze ? automationGaze : new Ray(
            sceneCamera.transform.position, sceneCamera.transform.forward.normalized);
#else
        Ray ray = new Ray(sceneCamera.transform.position,
            sceneCamera.transform.forward.normalized);
#endif
        return ResolveTargetForRayV72(ray);
    }

    void ResolveSceneCamera()
    {
        // Never retain the editor/reference capture camera as the gaze source:
        // on hardware the persistent XR eye is the only authoritative head
        // pose. The Selection camera is the standalone-scene fallback.
        Camera preferred = PersistentPuzzleBootstrap.Instance &&
            PersistentPuzzleBootstrap.Instance.xrCamera
            ? PersistentPuzzleBootstrap.Instance.xrCamera
            : selection && selection.sceneCamera
                ? selection.sceneCamera
                : Camera.main;
        if (preferred) sceneCamera = preferred;
    }

    public SelectionExhibitTargetV09 ResolveTargetForRayV72(Ray ray)
    {
        SelectionExhibitTargetV09 best = null;
        float nearest = float.PositiveInfinity;
        foreach (SelectionExhibitTargetV09 candidate in targets ??
                 Array.Empty<SelectionExhibitTargetV09>())
        {
            if (!Valid(candidate) ||
                !TryGetVisibleBoundsV72(candidate, out Bounds bounds))
                continue;
            bounds.Expand(Vector3.one * Mathf.Max(0f, visibleBoundsPadding) * 2f);
            if (!bounds.IntersectRay(ray, out float distance) ||
                distance < 0f || distance > rayLength || distance >= nearest)
                continue;
            nearest = distance;
            best = candidate;
        }
        if (best) return best;

        // The direct bounds test owns precise gaze. This bounded angular fall-
        // back only bridges natural head jitter near a silhouette edge.
        float bestError = angularFallback;
        foreach (SelectionExhibitTargetV09 candidate in targets ??
                 Array.Empty<SelectionExhibitTargetV09>())
        {
            if (!Valid(candidate) ||
                !TryGetVisibleBoundsV72(candidate, out Bounds bounds))
                continue;
            Vector3 to = bounds.center - ray.origin;
            float distance = to.magnitude;
            if (distance < 0.01f || distance > rayLength) continue;
            float silhouetteHalfAngle = Mathf.Asin(Mathf.Clamp01(
                bounds.extents.magnitude / distance)) * Mathf.Rad2Deg;
            float error = Mathf.Max(0f, Vector3.Angle(ray.direction,
                to / distance) - silhouetteHalfAngle);
            if (error >= bestError) continue;
            bestError = error;
            best = candidate;
        }
        return best;
    }

    public bool TryGetVisibleBoundsV72(
        SelectionExhibitTargetV09 target,
        out Bounds bounds)
    {
        bounds = default;
        if (!Valid(target) || selection.exhibits == null ||
            target.exhibitIndex < 0 || target.exhibitIndex >= selection.exhibits.Length)
            return false;
        Transform exhibit = selection.exhibits[target.exhibitIndex];
        if (!exhibit) return false;
        if (!rendererCache.TryGetValue(exhibit, out Renderer[] renderers) ||
            renderers == null)
        {
            renderers = exhibit.GetComponentsInChildren<Renderer>(true);
            rendererCache[exhibit] = renderers;
        }
        bool initialized = false;
        foreach (Renderer renderer in renderers)
        {
            if (!renderer || !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy ||
                renderer is LineRenderer || renderer is ParticleSystemRenderer)
                continue;
            if (!initialized) { bounds = renderer.bounds; initialized = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return initialized;
    }

    bool Valid(SelectionExhibitTargetV09 target) =>
        target && target.enabled && target.gameObject.activeInHierarchy &&
        target.carousel == selection && Array.IndexOf(targets, target) >= 0;

    public void RefreshTargets()
    {
        rendererCache.Clear();
        targets = FindObjectsByType<SelectionExhibitTargetV09>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        targets = Array.FindAll(targets, value => value &&
            value.gameObject.scene == gameObject.scene && value.enabled);
        Array.Sort(targets, (a, b) => a.exhibitIndex.CompareTo(b.exhibitIndex));
    }

#if UNITY_EDITOR
    public void SetAutomationGazeRayV72(Ray ray)
    {
        hasAutomationGaze = true;
        automationGaze = ray;
    }

    public void ClearAutomationGazeRayV72() => hasAutomationGaze = false;
#endif
}
