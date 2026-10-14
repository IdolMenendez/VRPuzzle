using UnityEngine;

/// <summary>
/// V61 #2/#6: a deliberate 3D confirm plaque gates entering a puzzle level.
/// Looking at / centering an exhibit never enters by itself (that was the
/// accidental "I looked and it entered" behaviour). When an exhibit is centered
/// and armed (SelectionExhibitTargetV09.PendingConfirmTargetV61), this shows a
/// world-space "确认进入 / CONFIRM ENTER" plaque floating in front of the
/// centered model. Entering happens ONLY when the plaque is deliberately
/// activated:
///   - right-hand gaze pinch whose head ray hits the plaque collider, or
///   - left-hand index fingertip touching the plaque.
/// Both play RuntimeAudioCue.Touch. The plaque hides whenever the carousel is
/// transitioning/committing or nothing is centered.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class SelectionEnterConfirmV61 : MonoBehaviour
{
    public SelectionCarouselController selection;
    public RuntimeIntentRayV46 ray;
    public Transform plaqueRoot;          // visual + collider root
    public Collider plaqueCollider;
    public TextMesh plaqueLabel;
    public Camera headCamera;
    [Min(0.02f)] public float fingertipTouchRadius = 0.06f;
    [Min(0.2f)] public float cooldownSeconds = 0.7f;
    public Vector3 plaqueForwardOffset = new Vector3(0f, -0.12f, -0.28f);

    float nextAllowed;
    bool pinchWasDown;

    void Awake()
    {
        if (!headCamera)
            headCamera = ray && ray.headCamera ? ray.headCamera : Camera.main;
        // Ensure the confirm gate is active for the whole selection stage.
        SelectionExhibitTargetV09.RequireConfirmPlaqueV61 = true;
        SetPlaqueVisible(false);
    }

    void Update()
    {
        SelectionExhibitTargetV09 pending =
            SelectionExhibitTargetV09.PendingConfirmTargetV61;
        bool showable =
            selection &&
            !selection.IsTransitioning &&
            !selection.IsCommitting &&
            pending != null &&
            pending.IsCurrent;

        if (!showable)
        {
            SetPlaqueVisible(false);
            pinchWasDown = ray && ray.PinchingV46;
            return;
        }

        PositionPlaque();
        SetPlaqueVisible(true);

        // (a) right-hand gaze pinch that hits the plaque collider (fresh edge).
        bool pinching = ray && ray.PinchingV46;
        bool freshPinch = pinching && !pinchWasDown;
        pinchWasDown = pinching;
        if (freshPinch &&
            Time.unscaledTime >= nextAllowed &&
            HeadRayHitsPlaque())
        {
            Confirm(pending);
            return;
        }

        // (b) left-hand index fingertip touch (non-pinch), via the shared
        // provider so it is independent of the right-hand pinch lock.
        if (Time.unscaledTime >= nextAllowed &&
            RuntimeRealHandPinchV47.Instance &&
            RuntimeRealHandPinchV47.Instance.TryGetIndexTipWorldV59(
                true, out Vector3 leftTip) &&
            plaqueCollider &&
            Vector3.Distance(
                plaqueCollider.ClosestPoint(leftTip), leftTip) <=
                fingertipTouchRadius)
            Confirm(pending);
    }

    void Confirm(SelectionExhibitTargetV09 pending)
    {
        nextAllowed = Time.unscaledTime + Mathf.Max(0.2f, cooldownSeconds);
        RuntimeAudioRouter.PlayCue(
            RuntimeAudioCue.Touch,
            plaqueRoot ? plaqueRoot.position : transform.position);
        RuntimeHaptics.PulseAll(0.3f, 0.05f);
        Debug.Log(
            "[PPVR V61 EnterConfirm] event=confirm exhibit=" +
            pending.exhibitIndex);
        SelectionExhibitTargetV09.ClearPendingConfirmV61();
        SetPlaqueVisible(false);
        pending.ConfirmEnterV61();
    }

    bool HeadRayHitsPlaque()
    {
        if (!headCamera || !plaqueCollider)
            return false;
        Ray headRay = new Ray(
            headCamera.transform.position,
            headCamera.transform.forward);
        return plaqueCollider.Raycast(headRay, out _, 12f);
    }

    void PositionPlaque()
    {
        if (!plaqueRoot || !selection || !selection.CurrentExhibit)
            return;
        Transform exhibit = selection.CurrentExhibit;
        Camera cam = headCamera ? headCamera : Camera.main;
        Vector3 basePos = exhibit.position;
        if (cam)
        {
            Vector3 toCam = Vector3.ProjectOnPlane(
                cam.transform.position - exhibit.position, Vector3.up);
            if (toCam.sqrMagnitude > 1e-4f)
                toCam.Normalize();
            else
                toCam = Vector3.back;
            basePos = exhibit.position +
                toCam * -plaqueForwardOffset.z +
                Vector3.up * plaqueForwardOffset.y;
            plaqueRoot.position = basePos;
            plaqueRoot.rotation = Quaternion.LookRotation(
                (plaqueRoot.position - cam.transform.position).normalized,
                Vector3.up);
        }
        else
        {
            plaqueRoot.position = basePos + plaqueForwardOffset;
        }
    }

    void SetPlaqueVisible(bool visible)
    {
        if (plaqueRoot && plaqueRoot.gameObject.activeSelf != visible)
            plaqueRoot.gameObject.SetActive(visible);
        if (visible && plaqueLabel)
            plaqueLabel.text = "确认进入\nCONFIRM";
    }
}
