using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Rigidbody), typeof(Collider), typeof(XRGrabInteractable))]
public sealed class RuntimePuzzlePiece : MonoBehaviour
{
    public RuntimePuzzleLevelController level;
    public Transform targetSocket;
    public Renderer ghostRenderer;
    public Renderer[] ghostRenderers;
    public Renderer[] solidRenderers;
    public bool requiresTwoHands;
    public int sequence;
    public int[] prerequisiteSequences;
    public bool requiresApproachCone;
    public bool hideWhileLocked;
    public Vector3 localApproachAxis = Vector3.up;
    public float maxApproachAngle = 55f;
    public bool requiresPlayerFacing;
    public float maxPlayerFacingAngle = 72f;
    public string semanticChallenge;
    public float hintOuterDistance = 0.15f;
    public float snapDistance = 0.05f;
    public float maxAssistAngle = 30f;

    [Header("V07 stable complete-module presentation")]
    [Tooltip("Keeps a complete module in its authored semantic orientation while carried, then eases it into the exact socket orientation inside the hint band.")]
    public bool stabilizeHeldOrientation;
    [Min(1f)] public float stableOrientationSpeed = 15f;

    [Header("V09 inspectable dependency guidance")]
    [Tooltip("Allows a locked Part to be picked up for inspection while keeping its socket dependency enforced.")]
    public bool allowInspectionWhileLocked;
    [Tooltip("Shows this Part's exact socket ghost whenever it is held, not only inside the outer hint distance.")]
    public bool showTargetGhostWhileHeld;

    [Header("V36 pinch-only 1:1 assembly")]
    [Tooltip("Disables XRGrabInteractable selection so only the explicit native-hand pinch interactor can hold this Part.")]
    public bool externalPinchOnlyV36;
    [Tooltip("A failed release restores the exact authored parent, world scale and pose, with a bounded hard finish.")]
    public bool strictReturnToAuthoredPoseV36;
    [Tooltip("Keeps every unsnapped target mesh visible as the complete reference model.")]
    public bool showCompleteReferenceV36;

    [Header("V59 reference ghost tuning")]
    [Tooltip(
        "Multiplies the socket ghost's shown scale. The worn test found the " +
        "placement ghost looked a touch large; keep 1.0 to match the exact " +
        "socket, lower it slightly (e.g. 0.97) if the ghost reads oversized.")]
    [Range(0.8f, 1.1f)] public float ghostScaleFactorV59 = 1f;
    [Tooltip(
        "V59: render the reference ghost as a warmer, more solid translucent " +
        "body instead of the faint cyan clip outline that gave no real guidance.")]
    public bool warmSolidGhostV59 = true;
    // V59: global hint toggle, flipped by tapping the workshop desk bell. When
    // false, all socket ghosts / reference models hide so the player can build
    // unaided; tapping again brings the guidance back. Static so one tap affects
    // every Part at once.
    public static bool HintsEnabledV59 = true;
    [Min(0.12f)] public float strictReturnSecondsV36 = 0.32f;

    [SerializeField] Vector3 authoredPosition;
    [SerializeField] Quaternion authoredRotation = Quaternion.identity;
    [SerializeField] Vector3 authoredWorldScale = Vector3.one;
    [SerializeField] Transform authoredParent;
    [SerializeField] bool authoredPoseCaptured;
    [SerializeField] Vector3 assembledWorldScale = Vector3.one;
    [SerializeField] bool assembledScaleCaptured;
    [Tooltip(
        "V58: pick the piece up at its true assembled (1:1) scale instead of the " +
        "small parts-bank 'miniature'. The user needs the held piece to match " +
        "the socket exactly so it seats flush and can be compared by eye; the " +
        "old miniature-on-tray then grow-on-snap made held and socket sizes " +
        "disagree. The tray pose is unchanged; only the carried scale is 1:1.")]
    public bool grabAtAssembledScaleV58 = true;
    [Tooltip(
        "V61: the piece sits on the parts table already at its true assembled " +
        "(1:1) scale, not as a miniature that only grows when picked up. The user " +
        "asked for the table parts to be full size from the start. When true, " +
        "ResetForPlay / ApplyAuthoredStartPose apply assembledWorldScale on the " +
        "tray instead of the small authoredWorldScale.")]
    public bool tableAtAssembledScaleV61 = true;
    [Header("V80 zero-latency idle path")]
    [Tooltip(
        "Skips distance, approach and hidden-ghost writes while the Part is " +
        "idle. Held, returning, hinted and highlighted Parts still run the " +
        "complete interaction path.")]
    public bool idleFastPathV80;

    Vector3 TableScaleV61 =>
        tableAtAssembledScaleV61 && assembledScaleCaptured
            ? assembledWorldScale
            : authoredWorldScale;
    readonly Dictionary<int, Pose> externalHolders = new Dictionary<int, Pose>();
    Rigidbody body;
    XRGrabInteractable grab;
    MaterialPropertyBlock block;
    float hintRemaining;
    float missFlash;
    float heavyPulseCooldown;
    float pickupHighlightRemaining;
    bool xriHeld;
    bool returning;
    bool wasInHintBand;
    bool recommended;
    float returnStartedV36;
    // V60: the hand->piece rotation offset captured at grab. MoveExternalGrab
    // keeps the piece at this offset from the hand instead of driving it to the
    // hand's absolute orientation, which is what made the piece snap-rotate the
    // instant it was picked up. Applies to every piece, independent of
    // stabilizeHeldOrientation (which is off on the shipped level scenes).
    Quaternion grabRotationOffsetV60 = Quaternion.identity;
    bool grabRotationOffsetCapturedV60;
    // V69: preserve the exact surface point the user pinched. The previous
    // external-hand path drove a Part's pivot to the fingertip, so large first
    // modules (Pagoda/Windmill plinths) jumped away from the fingers and then
    // appeared not to follow. Store the piece centre in hand-local space and
    // apply it alongside the already preserved rotation offset.
    Vector3 grabPositionOffsetV69;
    bool grabPositionOffsetCapturedV69;
    int grabPositionOffsetHolderCountV69;
    bool solidColorOverrideActiveV80;

    public bool IsSnapped { get; private set; }
    public bool IsRecommended => recommended;
    public int ExternalHolderCount => externalHolders.Count;
    public bool IsHeld => xriHeld || externalHolders.Count > 0;
    public bool IsReturningHomeV45 => returning;
    public Vector3 AuthoredPositionV45 => authoredPosition;
    public Quaternion AuthoredRotationV45 => authoredRotation;
    public Vector3 AuthoredWorldScaleV45 => authoredWorldScale;
    public Vector3 AssembledWorldScaleV45 => assembledWorldScale;
    public bool HasRequiredHeavyGrip =>
        !requiresTwoHands || externalHolders.Count >= 2;
    public bool IsAvailable =>
        prerequisiteSequences == null || prerequisiteSequences.Length == 0 || level == null ||
        prerequisiteSequences.All(required =>
            level.pieces != null && level.pieces.Any(piece => piece && piece.sequence == required && piece.IsSnapped));
    public Vector3 TargetPosition => targetSocket ? targetSocket.position : transform.position;

    void Awake()
    {
        Cache();
        if (!authoredPoseCaptured) CaptureAuthoredPose();
        grab.selectEntered.AddListener(OnSelectEntered);
        grab.selectExited.AddListener(OnSelectExited);
    }

    void Update()
    {
        if (requiresTwoHands && externalHolders.Count >= 2)
        {
            heavyPulseCooldown -= Time.unscaledDeltaTime;
            if (heavyPulseCooldown <= 0f)
            {
                heavyPulseCooldown = 0.32f;
                RuntimeHaptics.PulseAll(0.075f, 0.024f);
            }
        }
        else
        {
            heavyPulseCooldown = 0f;
        }
        if (IsSnapped || !targetSocket) return;
        bool held = IsHeld;
        if (idleFastPathV80 &&
            !held &&
            !returning &&
            hintRemaining <= 0f &&
            missFlash <= 0f &&
            pickupHighlightRemaining <= 0f &&
            !recommended &&
            !showCompleteReferenceV36 &&
            !solidColorOverrideActiveV80)
        {
            wasInHintBand = false;
            return;
        }
        float distance = Vector3.Distance(transform.position, targetSocket.position);
        bool inHintBand = held && distance > snapDistance && distance <= hintOuterDistance && IsApproachValid();
        if (inHintBand && !wasInHintBand)
            RuntimeAudioRouter.PlayCue(RuntimeAudioCue.SocketBreath, targetSocket.position);
        wasInHintBand = inHintBand;
        if (hintRemaining > 0f) hintRemaining = Mathf.Max(0f, hintRemaining - Time.unscaledDeltaTime);
        bool heldGhost =
            held &&
            ((showTargetGhostWhileHeld &&
              (!requiresPlayerFacing || IsSocketFacingPlayer())) ||
             (distance <= hintOuterDistance && IsApproachValid()));
        SetGhost(
            (showCompleteReferenceV36 && !IsSnapped) ||
            heldGhost || hintRemaining > 0f || recommended,
            distance);
        if (held && distance <= snapDistance && IsApproachValid())
        {
            float assist = 1f - Mathf.Exp(-13f * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, targetSocket.position, assist);
            if (Quaternion.Angle(transform.rotation, targetSocket.rotation) <= maxAssistAngle * 1.6f)
                transform.rotation = Quaternion.Slerp(transform.rotation, targetSocket.rotation, assist);
        }
        if (returning)
        {
            RestoreAuthoredParent();
            if (strictReturnToAuthoredPoseV36)
                ApplyWorldScale(authoredWorldScale);
            transform.position = Vector3.Lerp(transform.position, authoredPosition, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, authoredRotation, 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime));
            SyncKinematicBodyPose();
            if (Vector3.Distance(transform.position, authoredPosition) < 0.004f ||
                (strictReturnToAuthoredPoseV36 &&
                 Time.unscaledTime - returnStartedV36 >=
                    Mathf.Max(0.12f, strictReturnSecondsV36)))
            {
                returning = false;
                transform.SetPositionAndRotation(authoredPosition, authoredRotation);
                ApplyWorldScale(authoredWorldScale);
                body.isKinematic = true;
                SyncKinematicBodyPose();
            }
        }
        if (missFlash > 0f)
        {
            missFlash -= Time.unscaledDeltaTime;
            SetSolidColor(Color.Lerp(new Color(0.55f, 0.02f, 0.01f), Color.white, Mathf.Clamp01(missFlash * 4f)));
        }
        else if (pickupHighlightRemaining > 0f)
        {
            pickupHighlightRemaining = Mathf.Max(
                0f,
                pickupHighlightRemaining - Time.unscaledDeltaTime);
            float pulse =
                0.72f +
                0.28f * (0.5f +
                    0.5f * Mathf.Sin(Time.unscaledTime * 7.5f));
            SetSolidColor(new Color(
                1f,
                0.48f + 0.28f * pulse,
                0.08f,
                1f));
        }
        else ClearSolidColor();
    }

    void LateUpdate()
    {
        if (!stabilizeHeldOrientation || IsSnapped || !targetSocket)
            return;
        bool held = IsHeld;
        if (!held)
            return;
        float distance = Vector3.Distance(transform.position, targetSocket.position);
        Quaternion desired = distance <= hintOuterDistance && IsApproachValid()
            ? targetSocket.rotation
            : authoredRotation;
        float align = 1f - Mathf.Exp(
            -Mathf.Max(1f, stableOrientationSpeed) * Time.unscaledDeltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desired, align);
    }

    public void CaptureAuthoredPose()
    {
        authoredPosition = transform.position;
        authoredRotation = transform.rotation;
        authoredWorldScale = transform.lossyScale;
        authoredParent = transform.parent;
        authoredPoseCaptured = true;
    }

    public void ApplyCoveredRegistrationV45(
        Vector3 pivot,
        Quaternion rotation,
        Vector3 translation)
    {
        if (!authoredPoseCaptured)
            CaptureAuthoredPose();
        // V67: RuntimeCoveredStageRegistration has already moved every scene
        // root and then explicitly restored each Rigidbody to its registered
        // world pose before calling here. Some inherited scene data carried a
        // stale authored world pose that did not match the Rigidbody's actual
        // visible tabletop pose. Re-capture the authoritative registered pose
        // instead of transforming the stale value, otherwise a failed release
        // can jump to an unrelated old coordinate.
        authoredPosition = transform.position;
        authoredRotation = transform.rotation;
        authoredWorldScale = transform.lossyScale;
        authoredParent = transform.parent;
        authoredPoseCaptured = true;
    }

    /// <summary>
    /// Records the complete-module scale before a runtime builder creates a
    /// smaller, hand-reachable tray pose.  The piece is deliberately allowed
    /// to be a miniature on the parts bank, but it must recover the exact
    /// authored assembly scale when it enters its socket.
    /// </summary>
    public void CaptureAssembledScaleForRuntime()
    {
        assembledWorldScale = transform.lossyScale;
        assembledScaleCaptured = true;
    }

    public void NormalizeLooseScaleToAssembledV36()
    {
        Cache();
        if (!assembledScaleCaptured)
            CaptureAssembledScaleForRuntime();
        RestoreAuthoredParent();
        ApplyWorldScale(assembledWorldScale);
        CaptureAuthoredPose();
    }

    public bool HasOneToOneScaleV36 =>
        Vector3.Distance(authoredWorldScale, assembledWorldScale) <= 0.0005f;

    public void ScaleAssembledPresentationForRuntime(float factor)
    {
        if (!assembledScaleCaptured)
            CaptureAssembledScaleForRuntime();
        assembledWorldScale *= Mathf.Max(0.01f, factor);
    }

    public void ResetForPlay()
    {
        Cache();
        externalHolders.Clear();
        IsSnapped = xriHeld = returning = false;
        hintRemaining = 0f;
        heavyPulseCooldown = 0f;
        pickupHighlightRemaining = 0f;
        wasInHintBand = false;
        RestoreAuthoredParent();
        transform.SetPositionAndRotation(authoredPosition, authoredRotation);
        ApplyWorldScale(TableScaleV61);
        body.isKinematic = true;
        SyncKinematicBodyPose();
        body.isKinematic = true;
        body.useGravity = false;
        body.linearDamping = requiresTwoHands ? 7.5f : 3.8f;
        body.angularDamping = requiresTwoHands ? 9f : 4.8f;
        RefreshAvailability();
        SetGhost(showCompleteReferenceV36, 1f);
        ClearSolidColor();
    }

    public bool CanExternalGrab(int holderId)
    {
        if (IsSnapped || (!IsAvailable && !allowInspectionWhileLocked) ||
            returning || externalHolders.ContainsKey(holderId))
            return false;
        if (!requiresTwoHands && externalHolders.Count > 0) return false;
        return externalHolders.Count < 2;
    }

    public void BeginExternalGrab(int holderId, Pose pose)
    {
        if (!CanExternalGrab(holderId)) return;
        externalHolders[holderId] = pose;
        body.isKinematic = true;
        returning = false;
        // V58: carry the piece at its true assembled (1:1) scale so it matches
        // the socket exactly and seats flush, instead of the small tray
        // miniature that then grew on snap.
        if (grabAtAssembledScaleV58)
        {
            if (!assembledScaleCaptured)
                CaptureAssembledScaleForRuntime();
            ApplyWorldScale(assembledWorldScale);
        }
        // V60: capture the hand->piece rotation offset once, on the first hand
        // to grab. MoveExternalGrab reapplies this offset every frame so the
        // piece keeps its current orientation relative to the hand instead of
        // being forced to the hand's absolute rotation. This removes the "piece
        // suddenly rotates when picked up" jolt for every piece, whether or not
        // stabilizeHeldOrientation is enabled.
        if (externalHolders.Count == 1)
        {
            grabRotationOffsetV60 =
                Quaternion.Inverse(pose.rotation) * transform.rotation;
            grabRotationOffsetCapturedV60 = true;
            grabPositionOffsetV69 = Quaternion.Inverse(pose.rotation) *
                (transform.position - pose.position);
            grabPositionOffsetCapturedV69 = true;
            grabPositionOffsetHolderCountV69 = 1;
        }
        else if (requiresTwoHands && externalHolders.Count == 2)
        {
            // Re-anchor once when the second hand joins so a heavy Part does
            // not jump from the first hand to the two-hand midpoint.
            Vector3 midpoint = Vector3.zero;
            foreach (Pose holder in externalHolders.Values)
                midpoint += holder.position;
            midpoint *= 0.5f;
            grabPositionOffsetV69 = transform.position - midpoint;
            grabPositionOffsetCapturedV69 = true;
            grabPositionOffsetHolderCountV69 = 2;
        }
        RuntimeAudioRouter.PlayCue(RuntimeAudioCue.Grab, transform.position);
        if (!IsAvailable)
        {
            RuntimeHaptics.PulseAll(0.10f, 0.035f);
            RuntimeAudioRouter.PlayCue(RuntimeAudioCue.SocketBreath, TargetPosition);
        }
        if (requiresTwoHands && externalHolders.Count >= 2)
            RuntimeAudioRouter.PlayCue(RuntimeAudioCue.HeavyLift, transform.position);
    }

    public void MoveExternalGrab(int holderId, Pose pose)
    {
        if (!externalHolders.ContainsKey(holderId) || IsSnapped) return;
        externalHolders[holderId] = pose;
        bool oneHand = PersistentPuzzleBootstrap.Instance && PersistentPuzzleBootstrap.Instance.Save.singleHandMode;
        if (requiresTwoHands && externalHolders.Count < 2 && !oneHand) return;
        Vector3 position = Vector3.zero;
        Quaternion rotation = Quaternion.identity;
        int count = 0;
        foreach (Pose holder in externalHolders.Values)
        {
            position += holder.position;
            rotation = count == 0 ? holder.rotation : Quaternion.Slerp(rotation, holder.rotation, 1f / (count + 1f));
            count++;
        }
        position /= Mathf.Max(1, count);
        if (grabPositionOffsetCapturedV69)
        {
            if (requiresTwoHands && grabPositionOffsetHolderCountV69 == 2)
                position += grabPositionOffsetV69;
            else
                position += rotation * grabPositionOffsetV69;
        }
        transform.position = Vector3.Lerp(transform.position, position, 1f - Mathf.Exp(-(requiresTwoHands ? 7f : 18f) * Time.unscaledDeltaTime));
        // V60: default carry rotation keeps the hand->piece offset captured at
        // grab (rotation * offset) so the piece rotates WITH the hand from its
        // current orientation, never jumping to the hand's absolute rotation.
        Quaternion desiredRotation =
            grabRotationOffsetCapturedV60 && !requiresTwoHands
                ? rotation * grabRotationOffsetV60
                : rotation;
        if (stabilizeHeldOrientation)
        {
            float distance = targetSocket
                ? Vector3.Distance(position, targetSocket.position)
                : float.PositiveInfinity;
            desiredRotation = targetSocket && distance <= hintOuterDistance && IsApproachValid()
                ? targetSocket.rotation
                : authoredRotation;
        }
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            1f - Mathf.Exp(
                -(stabilizeHeldOrientation
                    ? Mathf.Max(1f, stableOrientationSpeed)
                    : requiresTwoHands ? 5f : 14f) *
                Time.unscaledDeltaTime));
        SyncKinematicBodyPose();
    }

    /// <summary>
    /// Converts a desired piece pose into the hand pose needed by the V69
    /// offset-preserving grab path. Runtime hands naturally discover this by
    /// moving the grabbed surface point; automated interaction tests need the
    /// same inverse mapping so they do not assume the old pivot-at-fingertip
    /// behaviour.
    /// </summary>
    public Pose HolderPoseForDesiredPiecePoseV69(Pose desiredPiecePose)
    {
        if (!grabPositionOffsetCapturedV69)
            return desiredPiecePose;
        if (requiresTwoHands && grabPositionOffsetHolderCountV69 == 2)
            return new Pose(
                desiredPiecePose.position - grabPositionOffsetV69,
                desiredPiecePose.rotation);
        Quaternion holderRotation = grabRotationOffsetCapturedV60
            ? desiredPiecePose.rotation *
                Quaternion.Inverse(grabRotationOffsetV60)
            : desiredPiecePose.rotation;
        return new Pose(
            desiredPiecePose.position -
                holderRotation * grabPositionOffsetV69,
            holderRotation);
    }

    public void EndExternalGrab(int holderId)
    {
        if (!externalHolders.Remove(holderId)) return;
        if (externalHolders.Count == 0)
        {
            // V60: forget the carry offset so the next grab recaptures from the
            // piece's then-current orientation.
            grabRotationOffsetCapturedV60 = false;
            grabPositionOffsetCapturedV69 = false;
            grabPositionOffsetHolderCountV69 = 0;
            TrySnapOrReturn();
        }
    }

    public void ShowExactHint(float seconds)
    {
        hintRemaining = Mathf.Max(hintRemaining, seconds);
        SetGhost(true, Vector3.Distance(transform.position, TargetPosition));
    }

    public void PulsePickupHighlight(float seconds)
    {
        if (IsSnapped)
            return;
        pickupHighlightRemaining = Mathf.Max(
            pickupHighlightRemaining,
            Mathf.Max(0.4f, seconds));
    }

    public void SetRecommended(bool value)
    {
        recommended = value && !IsSnapped;
        if (!showCompleteReferenceV36 &&
            !recommended && !IsHeld && hintRemaining <= 0f)
            SetGhost(false, 1f);
    }

    public void ForceSnapForSmoke()
    {
        if (!targetSocket || IsSnapped) return;
        Cache();
        externalHolders.Clear();
        IsSnapped = true;
        Vector3 snapScale = assembledScaleCaptured
            ? assembledWorldScale
            : authoredWorldScale;
        transform.SetParent(targetSocket, true);
        transform.SetPositionAndRotation(
            targetSocket.position,
            targetSocket.rotation);
        ApplyWorldScale(snapScale);
        body.isKinematic = true;
        SyncKinematicBodyPose();
        grab.enabled = false;
        SetGhost(false, 0f);
        level?.NotifySnapped(this);
    }

    /// <summary>
    /// Exercises the real release/snap decision near the authored tolerance
    /// edge. Unlike ForceSnapForSmoke this does not bypass availability,
    /// distance, rotation or approach-cone checks.
    /// </summary>
    public bool TryForgivingSnapForSmoke(
        float distanceRatio = 0.82f,
        float angleRatio = 0.82f)
    {
        if (!targetSocket || IsSnapped || !IsAvailable)
            return false;
        Cache();
        externalHolders.Clear();
        xriHeld = false;
        returning = false;
        RestoreAuthoredParent();
        Vector3 direction = requiresApproachCone
            ? targetSocket.TransformDirection(
                localApproachAxis.sqrMagnitude > 0.0001f
                    ? localApproachAxis.normalized
                    : Vector3.up)
            : targetSocket.right;
        transform.SetPositionAndRotation(
            targetSocket.position +
            direction * snapDistance * Mathf.Clamp(
                distanceRatio, 0.05f, 0.98f),
            Quaternion.AngleAxis(
                maxAssistAngle * Mathf.Clamp(
                    angleRatio, 0.05f, 0.98f),
                targetSocket.up) *
            targetSocket.rotation);
        if (assembledScaleCaptured)
            ApplyWorldScale(assembledWorldScale);
        Transform cameraTransform = null;
        Pose cameraPose = default;
        if (requiresPlayerFacing &&
            Camera.main &&
            !IsSocketFacingPlayer())
        {
            cameraTransform = Camera.main.transform;
            cameraPose = new Pose(
                cameraTransform.position,
                cameraTransform.rotation);
            Vector3 expectedPlayerDirection =
                targetSocket.TransformDirection(
                    localApproachAxis.sqrMagnitude > 0.0001f
                        ? localApproachAxis.normalized
                        : Vector3.up);
            cameraTransform.position =
                targetSocket.position +
                expectedPlayerDirection * 0.58f;
        }
        TrySnapOrReturn();
        if (cameraTransform)
            cameraTransform.SetPositionAndRotation(
                cameraPose.position,
                cameraPose.rotation);
        return IsSnapped;
    }

    public void RefreshAvailability()
    {
        Cache();
        grab.enabled = !externalPinchOnlyV36 &&
                       !IsSnapped &&
                       (IsAvailable || allowInspectionWhileLocked);
        bool visible = !hideWhileLocked || IsSnapped || IsAvailable ||
                       allowInspectionWhileLocked;
        if (solidRenderers != null)
            foreach (Renderer renderer in solidRenderers)
                if (renderer) renderer.enabled = visible;
    }

    public void ApplyNearSocketReferencePose()
    {
        if (!targetSocket) return;
        IsSnapped = false;
        RestoreAuthoredParent();
        Vector3 offset = requiresApproachCone
            ? targetSocket.TransformDirection(localApproachAxis.normalized) * 0.085f
            : targetSocket.right * 0.085f;
        transform.SetPositionAndRotation(targetSocket.position + offset,
            Quaternion.AngleAxis(18f, Vector3.up) * targetSocket.rotation);
        if (assembledScaleCaptured)
            ApplyWorldScale(assembledWorldScale);
        SetGhost(true, 0.085f);
    }

    public void ApplyAuthoredStartPose()
    {
        IsSnapped = false;
        RestoreAuthoredParent();
        transform.SetPositionAndRotation(authoredPosition, authoredRotation);
        ApplyWorldScale(TableScaleV61);
        SetGhost(showCompleteReferenceV36, 1f);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (externalPinchOnlyV36)
        {
            xriHeld = false;
            return;
        }
        if (IsSnapped || (!IsAvailable && !allowInspectionWhileLocked))
        {
            xriHeld = false;
            return;
        }
        xriHeld = true;
        body.isKinematic = false;
        body.useGravity = false;
        // V59: same as the native grab path — no instant rotation snap on grab.
        RuntimeAudioRouter.PlayCue(RuntimeAudioCue.Grab, transform.position);
        if (!IsAvailable)
        {
            RuntimeHaptics.PulseAll(0.10f, 0.035f);
            RuntimeAudioRouter.PlayCue(RuntimeAudioCue.SocketBreath, TargetPosition);
        }
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        if (externalPinchOnlyV36)
            return;
        xriHeld = false;
        TrySnapOrReturn();
    }

    void TrySnapOrReturn()
    {
        if (!targetSocket || IsSnapped) return;
        if (!IsAvailable)
        {
            returning = true;
            returnStartedV36 = Time.unscaledTime;
            RestoreAuthoredParent();
            if (strictReturnToAuthoredPoseV36)
                ApplyWorldScale(authoredWorldScale);
            body.isKinematic = true;
            hintRemaining = Mathf.Max(hintRemaining, 0.65f);
            RuntimeHaptics.PulseAll(0.14f, 0.040f);
            RuntimeAudioRouter.PlayCue(RuntimeAudioCue.Miss, transform.position);
            return;
        }
        float distance = Vector3.Distance(transform.position, targetSocket.position);
        Quaternion snapRotation = targetSocket.rotation;
        float angle = Quaternion.Angle(transform.rotation, snapRotation);
        Debug.Log(
            "[PPVR V69 Snap] event=release-decision target=" + name +
            " distance=" + distance.ToString("F4") +
            " tolerance=" + snapDistance.ToString("F4") +
            " angle=" + angle.ToString("F1") +
            " angleTolerance=" + maxAssistAngle.ToString("F1") +
            " available=" + IsAvailable +
            " approach=" + IsApproachValid());
        if (distance <= snapDistance && angle <= maxAssistAngle && IsApproachValid())
        {
            IsSnapped = true;
            Vector3 snapScale = assembledScaleCaptured
                ? assembledWorldScale
                : authoredWorldScale;
            transform.SetParent(targetSocket, true);
            transform.SetPositionAndRotation(targetSocket.position, snapRotation);
            ApplyWorldScale(snapScale);
            body.isKinematic = true;
            SyncKinematicBodyPose();
            grab.enabled = false;
            SetGhost(false, 0f);
            RuntimeHaptics.PulseAll(0.52f, 0.085f);
            RuntimeAudioRouter.PlayCue(RuntimeAudioCue.Snap, transform.position);
            level?.NotifySnapped(this);
        }
        else
        {
            missFlash = 0.32f;
            returning = true;
            returnStartedV36 = Time.unscaledTime;
            RestoreAuthoredParent();
            if (strictReturnToAuthoredPoseV36)
                ApplyWorldScale(authoredWorldScale);
            body.isKinematic = true;
            RuntimeHaptics.PulseAll(0.18f, 0.045f);
            RuntimeAudioRouter.PlayCue(RuntimeAudioCue.Miss, transform.position);
        }
    }

    public bool IsApproachValid()
    {
        if (requiresPlayerFacing && !IsSocketFacingPlayer()) return false;
        if (!requiresApproachCone || !targetSocket) return true;
        Vector3 fromSocket = transform.position - targetSocket.position;
        if (fromSocket.sqrMagnitude < 0.000001f) return true;
        Vector3 expected = targetSocket.TransformDirection(localApproachAxis.normalized);
        return Vector3.Angle(expected, fromSocket.normalized) <= maxApproachAngle;
    }

    void SetGhost(bool visible, float distance)
    {
        Renderer[] ghosts = ghostRenderers != null && ghostRenderers.Length > 0
            ? ghostRenderers
            : ghostRenderer ? new[] { ghostRenderer } : System.Array.Empty<Renderer>();
        if (ghosts.Length == 0) return;
        // V59: a workshop bell tap can hide all guidance; never show ghosts then.
        if (!HintsEnabledV59)
            visible = false;
        if (!showCompleteReferenceV36 &&
            visible && requiresPlayerFacing && !IsSocketFacingPlayer())
            visible = false;
        foreach (Renderer renderer in ghosts)
            if (renderer) renderer.enabled = visible;
        if (!visible) return;
        // V59: allow a small corrective scale on the placement ghost (the worn
        // test read it as slightly oversized). Applied once relative to each
        // ghost's captured base local scale so repeated frames do not drift.
        ApplyGhostScaleFactorV59(ghosts);
        float alpha = recommended && !IsHeld && hintRemaining <= 0f
            ? 0.075f + 0.045f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.4f))
            : distance <= snapDistance
                ? 0.78f
                : 0.18f + 0.08f * Mathf.Sin(Time.unscaledTime * 5.4f);
        Color color = IsAvailable
            ? new Color(1f, 0.56f, 0.10f, alpha)
            : new Color(0.62f, 0.54f, 0.44f, alpha * 0.72f);
        var ghostBlock = new MaterialPropertyBlock();
        Renderer primary = ghosts.FirstOrDefault(value => value);
        if (!primary) return;
        bool cyanSemanticHint = ghosts.Any(renderer =>
            renderer && renderer.sharedMaterials.Any(material =>
                material &&
                ((material.shader &&
                  material.shader.name ==
                  "PersonallyPuzzled/V10SemanticModuleClipGhost") ||
                 material.name.Contains(
                     "CyanGhost",
                     System.StringComparison.OrdinalIgnoreCase))));
        Color hintColor = showCompleteReferenceV36
            ? new Color(0.83f, 0.74f, 0.58f, 0.30f)
            : warmSolidGhostV59
            // V60: warm, more opaque translucent body for EVERY ghost type
            // (gold, cyan or plain). V59 only recolored the cyan-shader ghost,
            // so the gold-ghost levels (e.g. Cottage) saw no change at all.
            // A solid warm body at the socket reads as "the piece goes here".
            ? new Color(1f, 0.62f, 0.20f, Mathf.Max(0.40f, alpha))
            : cyanSemanticHint
            ? new Color(0.02f, 0.72f, 1f, alpha)
            : color;
        primary.GetPropertyBlock(ghostBlock);
        ghostBlock.SetColor("_BaseColor", hintColor);
        ghostBlock.SetColor("_Color", hintColor);
        ghostBlock.SetColor(
            "_EdgeColor",
            cyanSemanticHint && !warmSolidGhostV59
                ? new Color(0.03f, 0.82f, 1f, 1f)
                : warmSolidGhostV59 || IsAvailable
                ? new Color(1f, 0.72f, 0.18f, 1f)
                : new Color(0.64f, 0.58f, 0.48f, 1f));
        ghostBlock.SetFloat(
            "_GhostStrength",
            recommended && !IsHeld && hintRemaining <= 0f
                ? 0.20f
                : Mathf.Clamp01(alpha / 0.78f));
        foreach (Renderer renderer in ghosts)
            if (renderer) renderer.SetPropertyBlock(ghostBlock);
    }

    readonly Dictionary<Transform, Vector3> ghostBaseScaleV59 =
        new Dictionary<Transform, Vector3>();
    readonly HashSet<Transform> ghostRootScratchV60 = new HashSet<Transform>();

    void ApplyGhostScaleFactorV59(Renderer[] ghosts)
    {
        // V60.2: the socket ghost is the COMPLETE-module reference the player
        // sights the piece against, so it must stay at (roughly) its authored
        // size at the socket. V60/V60.1 wrongly drove it down to the miniature
        // placed-piece scale (~0.63), which the user reported as "so small I
        // can't see it". Keep the authored ghost size, only allowing the small
        // optional manual trim (ghostScaleFactorV59, default 1.0 = unchanged).
        // No assembled-scale matching: "same proportion as before", not shrunk.
        float factor = Mathf.Clamp(ghostScaleFactorV59, 0.8f, 1.1f);
        if (Mathf.Abs(factor - 1f) < 0.0005f)
            return; // default: leave the ghost exactly as authored
        ghostRootScratchV60.Clear();
        foreach (Renderer renderer in ghosts)
        {
            if (!renderer)
                continue;
            Transform root = GhostRootUnderSocketV60(renderer.transform);
            if (!root || !ghostRootScratchV60.Add(root))
                continue;
            if (!ghostBaseScaleV59.TryGetValue(root, out Vector3 baseScale))
            {
                baseScale = root.localScale;
                ghostBaseScaleV59[root] = baseScale;
            }
            Vector3 desired = baseScale * factor;
            if ((root.localScale - desired).sqrMagnitude > 1e-10f)
                root.localScale = desired;
        }
    }

    Transform GhostRootUnderSocketV60(Transform node)
    {
        if (!targetSocket)
            return node;
        Transform current = node;
        while (current && current.parent && current.parent != targetSocket)
            current = current.parent;
        return current;
    }

    public bool IsSocketFacingPlayer()
    {
        if (!targetSocket || !Camera.main) return true;
        Vector3 expected = targetSocket.TransformDirection(localApproachAxis.normalized);
        Vector3 toPlayer = Camera.main.transform.position - targetSocket.position;
        if (toPlayer.sqrMagnitude < 0.000001f) return true;
        return Vector3.Angle(expected, toPlayer.normalized) <= maxPlayerFacingAngle;
    }

    public bool IsAtAuthoredPoseV45(
        float positionTolerance = 0.003f,
        float angleTolerance = 0.5f,
        float scaleTolerance = 0.003f)
    {
        return !IsSnapped &&
            !IsHeld &&
            !returning &&
            Vector3.Distance(
                transform.position,
                authoredPosition) <= positionTolerance &&
            Quaternion.Angle(
                transform.rotation,
                authoredRotation) <= angleTolerance &&
            Vector3.Distance(
                transform.lossyScale,
                TableScaleV61) <= scaleTolerance;
    }

    public string AuthoredPoseDiagnosticV67() =>
        "held=" + IsHeld +
        " returning=" + returning +
        " positionDelta=" + Vector3.Distance(transform.position, authoredPosition).ToString("F5") +
        " angleDelta=" + Quaternion.Angle(transform.rotation, authoredRotation).ToString("F3") +
        " scaleDelta=" + Vector3.Distance(transform.lossyScale, TableScaleV61).ToString("F5") +
        " current=" + transform.position.ToString("F3") +
        " authored=" + authoredPosition.ToString("F3") +
        " target=" + TargetPosition.ToString("F3") +
        " targetDelta=" + Vector3.Distance(transform.position, TargetPosition).ToString("F5");

    void RestoreAuthoredParent()
    {
        if (authoredParent && transform.parent != authoredParent)
            transform.SetParent(authoredParent, true);
    }

    void ApplyWorldScale(Vector3 worldScale)
    {
        Transform parent = transform.parent;
        if (!parent)
        {
            transform.localScale = worldScale;
            return;
        }
        Vector3 parentScale = parent.lossyScale;
        transform.localScale = new Vector3(
            DivideScale(worldScale.x, parentScale.x),
            DivideScale(worldScale.y, parentScale.y),
            DivideScale(worldScale.z, parentScale.z));
    }

    static float DivideScale(float value, float parentValue)
    {
        return Mathf.Abs(parentValue) <= 0.00001f
            ? value
            : value / parentValue;
    }

    void Cache()
    {
        if (!body) body = GetComponent<Rigidbody>();
        if (!grab) grab = GetComponent<XRGrabInteractable>();
        if (solidRenderers == null || solidRenderers.Length == 0)
        {
            var ghosts = new HashSet<Renderer>(
                ghostRenderers != null && ghostRenderers.Length > 0
                    ? ghostRenderers.Where(value => value)
                    : ghostRenderer ? new[] { ghostRenderer } : System.Array.Empty<Renderer>());
            solidRenderers = GetComponentsInChildren<Renderer>(true)
                .Where(value => !ghosts.Contains(value))
                .ToArray();
        }
        if (block == null) block = new MaterialPropertyBlock();
    }

    void SyncKinematicBodyPose()
    {
        if (!body || !body.isKinematic)
            return;
        body.position = transform.position;
        body.rotation = transform.rotation;
    }

    void SetSolidColor(Color color)
    {
        solidColorOverrideActiveV80 = true;
        foreach (Renderer renderer in solidRenderers)
        {
            if (!renderer) continue;
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(block);
        }
    }

    void ClearSolidColor()
    {
        solidColorOverrideActiveV80 = false;
        foreach (Renderer renderer in solidRenderers) if (renderer) renderer.SetPropertyBlock(null);
    }
}
