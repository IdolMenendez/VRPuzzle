using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// A physical, one-axis pull-chain shared by controller and tracked-hand
/// direct interactors. Selecting the brass bead alone does not spend a hint:
/// the player must pull it down through the authored travel threshold.
/// </summary>
[RequireComponent(
    typeof(Rigidbody),
    typeof(SphereCollider),
    typeof(XRGrabInteractable))]
public sealed class RuntimeHintLampPullV13 : MonoBehaviour
{
    public RuntimeHintLamp hintLamp;
    public Transform anchor;
    public Transform chainVisual;
    [Min(0.02f)] public float pullDistance = 0.035f;
    [Min(0.035f)] public float maxTravel = 0.055f;
    [Min(2f)] public float returnSpeed = 15f;
    [Header("V25 forgiving physical pull")]
    [Tooltip(
        "Measures a tracked-hand pull from the point where the bead/cord was " +
        "captured instead of from the bead's authored world height. This " +
        "keeps the interaction reliable when the player pinches anywhere " +
        "along the visible cord.")]
    public bool relativeExternalPullV25;
    [Min(0.12f)] public float externalGrabRadiusV25 = 0.22f;
    [Tooltip(
        "Allows a tracked index fingertip to tap the brass bead without a " +
        "pinch. This avoids the palm-facing-self PICO system-menu gesture.")]
    public bool fingertipTapHintV25;
    [Min(0.015f)] public float fingertipTapRadiusV25 = 0.045f;
    [Tooltip(
        "V59: a fingertip tap toggles the global assembly guidance (socket " +
        "ghosts / reference model) on and off instead of (or as well as) " +
        "spending a hint. This is the tap-able retro desk bell behaviour: hints " +
        "can be shown, and also switched off, by tapping — no pinch required.")]
    public bool tapTogglesHintsV59 = true;

    XRGrabInteractable grab;
    Vector3 restPosition;
    Quaternion restRotation;
    bool selected;
    bool firedDuringCurrentPull;
    bool desktopDragging;
    bool externalDragging;
    int externalHolderId;
    Pose externalPose;
    float externalStartY;
    float desktopStartY;
    float desktopTravel;
    float smokeTravel = -1f;
    float currentTravel;
    float fingertipTapAnimation;
    float nextFingertipTap;
    bool fingertipTouchLatched;
    bool initialized;

    public float CurrentTravel => currentTravel;

    public bool IsSelected =>
        selected || desktopDragging || externalDragging;

    void Awake()
    {
        Cache();
        CaptureRestPose();
    }

    void OnEnable()
    {
        Cache();
        grab.selectEntered.AddListener(OnSelectEntered);
        grab.selectExited.AddListener(OnSelectExited);
    }

    void OnDisable()
    {
        if (!grab)
            return;
        grab.selectEntered.RemoveListener(OnSelectEntered);
        grab.selectExited.RemoveListener(OnSelectExited);
    }

    void LateUpdate()
    {
        if (!initialized)
            CaptureRestPose();

        float travel;
        if (fingertipTapAnimation > 0f)
        {
            fingertipTapAnimation = Mathf.Max(
                0f,
                fingertipTapAnimation - Time.unscaledDeltaTime);
            float normalized =
                1f - fingertipTapAnimation / 0.26f;
            travel =
                Mathf.Sin(Mathf.Clamp01(normalized) * Mathf.PI) *
                maxTravel;
        }
        else if (smokeTravel >= 0f)
        {
            travel = smokeTravel;
        }
        else if (desktopDragging)
        {
            travel = desktopTravel;
        }
        else if (externalDragging)
        {
            travel = Mathf.Clamp(
                (relativeExternalPullV25
                    ? externalStartY
                    : restPosition.y) -
                externalPose.position.y,
                0f,
                maxTravel);
        }
        else if (selected)
        {
            travel = Mathf.Clamp(
                Vector3.Dot(
                    restPosition - transform.position,
                    Vector3.up),
                0f,
                maxTravel);
        }
        else
        {
            Vector3 returned = Vector3.Lerp(
                transform.position,
                restPosition,
                1f - Mathf.Exp(-returnSpeed * Time.unscaledDeltaTime));
            travel = Mathf.Clamp(
                Vector3.Dot(restPosition - returned, Vector3.up),
                0f,
                maxTravel);
        }

        transform.SetPositionAndRotation(
            restPosition + Vector3.down * travel,
            restRotation);
        currentTravel = travel;
        UpdateChain();

        if ((selected || desktopDragging || externalDragging) &&
            !firedDuringCurrentPull &&
            travel >= pullDistance)
        {
            firedDuringCurrentPull = true;
            hintLamp?.Trigger();
            RuntimeHaptics.PulseAll(0.34f, 0.060f);
        }
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        selected = true;
        smokeTravel = -1f;
        firedDuringCurrentPull = false;
        RuntimeHaptics.PulseAll(0.08f, 0.025f);
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        selected = false;
        smokeTravel = -1f;
    }

    void OnMouseDown()
    {
        if (Mouse.current == null)
            return;
        desktopDragging = true;
        selected = false;
        firedDuringCurrentPull = false;
        desktopStartY = Mouse.current.position.ReadValue().y;
        desktopTravel = 0f;
    }

    void OnMouseDrag()
    {
        if (!desktopDragging || Mouse.current == null)
            return;
        float pixelsDown =
            desktopStartY - Mouse.current.position.ReadValue().y;
        desktopTravel = Mathf.Clamp(
            pixelsDown * 0.0009f,
            0f,
            maxTravel);
    }

    void OnMouseUp()
    {
        desktopDragging = false;
        desktopTravel = 0f;
    }

    /// <summary>
    /// Exercises the same threshold and trigger path without synthesizing an
    /// XR interactor. Used only by the headless PlayerManual regression.
    /// </summary>
    public void PullForSmoke()
    {
        if (!initialized)
            CaptureRestPose();
        selected = true;
        smokeTravel = maxTravel;
        firedDuringCurrentPull = false;
        LateUpdate();
    }

    public void ReleaseForSmoke()
    {
        selected = false;
        smokeTravel = -1f;
    }

    public void ResetToRest()
    {
        selected = false;
        desktopDragging = false;
        externalDragging = false;
        externalHolderId = 0;
        desktopTravel = 0f;
        smokeTravel = -1f;
        currentTravel = 0f;
        firedDuringCurrentPull = false;
        if (!initialized)
            CaptureRestPose();
        transform.SetPositionAndRotation(restPosition, restRotation);
        UpdateChain();
    }

    public bool BeginExternalPull(int holderId, Pose pose)
    {
        if (externalDragging && externalHolderId != holderId)
            return false;
        if (!initialized)
            CaptureRestPose();
        selected = false;
        desktopDragging = false;
        smokeTravel = -1f;
        externalDragging = true;
        externalHolderId = holderId;
        externalPose = pose;
        externalStartY = pose.position.y;
        firedDuringCurrentPull = false;
        RuntimeHaptics.PulseAll(0.08f, 0.025f);
        return true;
    }

    public void MoveExternalPull(int holderId, Pose pose)
    {
        if (!externalDragging || externalHolderId != holderId)
            return;
        externalPose = pose;
    }

    public void EndExternalPull(int holderId)
    {
        if (!externalDragging || externalHolderId != holderId)
            return;
        externalDragging = false;
        externalHolderId = 0;
    }

    public float ExternalGrabDistance(Vector3 worldPoint)
    {
        float distance = Vector3.Distance(transform.position, worldPoint);
        if (anchor)
        {
            Vector3 segment = transform.position - anchor.position;
            float denominator = segment.sqrMagnitude;
            float t = denominator <= 0.000001f
                ? 0f
                : Mathf.Clamp01(Vector3.Dot(
                    worldPoint - anchor.position,
                    segment) / denominator);
            Vector3 closest = anchor.position + segment * t;
            distance = Mathf.Min(
                distance,
                Vector3.Distance(worldPoint, closest));
        }
        foreach (Collider collider in
                 GetComponentsInChildren<Collider>(true))
        {
            if (!collider || !collider.enabled)
                continue;
            distance = Mathf.Min(
                distance,
                Vector3.Distance(
                    worldPoint,
                    collider.ClosestPoint(worldPoint)));
        }
        return distance;
    }

    public bool TryFingertipTapV25(Vector3 worldIndexTip)
    {
        float distance = ExternalGrabDistance(worldIndexTip);
        // V60: the V59 worn log showed zero taps. The bead is tiny, so the 4.5cm
        // radius plus a wrong-tracking-space fingertip never reached it. With the
        // provider now feeding the real index tip in the correct space, widen the
        // effective tap radius so a deliberate poke at the bell registers.
        float effectiveRadius = Mathf.Max(fingertipTapRadiusV25, 0.075f);
        if (distance > effectiveRadius * 1.40f)
            fingertipTouchLatched = false;
        if (!fingertipTapHintV25 ||
            fingertipTouchLatched ||
            Time.unscaledTime < nextFingertipTap ||
            distance > effectiveRadius)
            return false;
        if (!initialized)
            CaptureRestPose();
        nextFingertipTap = Time.unscaledTime + 0.65f;
        fingertipTouchLatched = true;
        fingertipTapAnimation = 0.26f;
        firedDuringCurrentPull = true;
        // V59: a tap flips the global guidance on/off (the desk-bell toggle) so
        // the player can hide the ghosts to build unaided, then tap again to
        // bring them back. It still lights the lamp/plays the cue as feedback.
        if (tapTogglesHintsV59)
            RuntimePuzzlePiece.HintsEnabledV59 =
                !RuntimePuzzlePiece.HintsEnabledV59;
        // V60: verifiable telemetry so the worn log proves the tap fired and
        // which way guidance flipped.
        Debug.Log(
            "[PPVR V60 BellTap] event=fingertip-tap object=" + name +
            " distance=" + distance.ToString("F3") +
            " radius=" + effectiveRadius.ToString("F3") +
            " hintsEnabled=" + RuntimePuzzlePiece.HintsEnabledV59);
        hintLamp?.Trigger();
        RuntimeHaptics.PulseAll(0.22f, 0.045f);
        return true;
    }

    void Cache()
    {
        if (!grab)
            grab = GetComponent<XRGrabInteractable>();
    }

    void CaptureRestPose()
    {
        restPosition = transform.position;
        restRotation = transform.rotation;
        initialized = true;
        UpdateChain();
    }

    void UpdateChain()
    {
        if (!anchor || !chainVisual)
            return;
        Vector3 axis = transform.position - anchor.position;
        float length = Mathf.Max(0.002f, axis.magnitude);
        chainVisual.position =
            (anchor.position + transform.position) * 0.5f;
        chainVisual.up = axis / length;
        // The physical lamp was historically staged beneath a non-uniformly
        // scaled presentation root.  Writing the desired world length directly
        // into localScale made the chain detach or stretch between levels.
        // Compensate for the parent's lossy scale on every axis so the brass
        // cord remains a constant-radius world-space object.
        Vector3 parentScale = chainVisual.parent
            ? chainVisual.parent.lossyScale
            : Vector3.one;
        const float worldRadius = 0.00155f;
        chainVisual.localScale = new Vector3(
            Divide(worldRadius, parentScale.x),
            Divide(length * 0.5f, parentScale.y),
            Divide(worldRadius, parentScale.z));
    }

    static float Divide(float value, float divisor) =>
        Mathf.Abs(divisor) <= 0.00001f ? value : value / divisor;
}
