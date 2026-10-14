using System;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Project Swan 6.1 test-only semantic input bridge.
///
/// The current macOS emulator can render the PICO/OpenXR app while failing to
/// forward its documented host controller buttons into Unity XR InputDevices.
/// This bridge is therefore created only after the existing Swan emulator
/// detector succeeds. It consumes one command file exactly once and maps that
/// command to one normal PlayerManual interaction. It never runs on a headset,
/// never selects AutoDemo, and never chains a complete level automatically.
/// </summary>
public sealed class RuntimeProjectSwanManualStepBridgeV01 : MonoBehaviour
{
    public const string CommandFileName =
        "project_swan_player_manual_step.command";
    public const string AckFileName =
        "project_swan_player_manual_step.ack";

    const int PrimaryHolderId = 61001;
    const int SecondaryHolderId = 61002;
    const float PollSeconds = 0.08f;

    PersistentPuzzleBootstrap bootstrap;
    RuntimePuzzlePiece heldPiece;
    RuntimePuzzleStage heldStage;
    bool primaryHolding;
    bool secondaryHolding;
    float nextPoll;
    string lastRequestId;

    string CommandPath =>
        Path.Combine(Application.persistentDataPath, CommandFileName);
    string AckPath =>
        Path.Combine(Application.persistentDataPath, AckFileName);

    void Awake()
    {
        bootstrap = GetComponent<PersistentPuzzleBootstrap>();
        if (!bootstrap)
            bootstrap = PersistentPuzzleBootstrap.Instance;
        DontDestroyOnLoad(gameObject);
        Debug.Log(
            "[Personally Puzzled Swan Manual] single-step bridge ready · " +
            "PlayerManual only · command=" + CommandPath);
    }

    void Update()
    {
        if (!bootstrap ||
            !bootstrap.SwanEmulatorDiagnosticsEnabled ||
            Time.unscaledTime < nextPoll)
            return;
        nextPoll = Time.unscaledTime + PollSeconds;

        if (heldPiece &&
            (heldStage != bootstrap.CurrentStage || heldPiece.IsSnapped))
            ClearHeldState();

        if (!File.Exists(CommandPath))
            return;

        string request = null;
        try
        {
            request = File.ReadAllText(CommandPath).Trim();
            File.Delete(CommandPath);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[Personally Puzzled Swan Manual] command read deferred: " +
                exception.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(request))
            return;

        string[] fields = request.Split('|');
        string requestId = fields.Length > 0 ? fields[0].Trim() : string.Empty;
        string action = fields.Length > 1
            ? fields[1].Trim().ToLowerInvariant()
            : string.Empty;
        string argument = fields.Length > 2 ? fields[2].Trim() : string.Empty;
        if (string.IsNullOrEmpty(requestId) ||
            string.IsNullOrEmpty(action))
        {
            WriteAck(
                string.IsNullOrEmpty(requestId) ? "invalid" : requestId,
                false,
                action,
                "invalid-command-format");
            return;
        }
        if (requestId == lastRequestId)
        {
            WriteAck(requestId, false, action, "duplicate-request-id");
            return;
        }
        lastRequestId = requestId;

        bool ok;
        string detail;
        if (!PersonallyPuzzledExperienceModeState.IsPlayerManual)
        {
            ok = false;
            detail = "release-mode-is-not-PlayerManual";
        }
        else
        {
            try
            {
                ok = Execute(action, argument, out detail);
            }
            catch (Exception exception)
            {
                ok = false;
                detail = "exception-" + exception.GetType().Name;
                Debug.LogException(exception, this);
            }
        }

        WriteAck(requestId, ok, action, detail);
        Debug.Log(
            "[Personally Puzzled Swan Manual] request=" + requestId +
            " action=" + action +
            " ok=" + ok +
            " stage=" + bootstrap.CurrentStage +
            " detail=" + detail);
    }

    bool Execute(string action, string argument, out string detail)
    {
        detail = string.Empty;
        if (action == "status")
        {
            detail = DescribeState();
            return true;
        }
        if (bootstrap.IsTransitioning)
        {
            detail = "stage-transition-in-progress";
            return false;
        }

        switch (action)
        {
            case "confirm":
                return ConfirmStage(out detail);
            case "selection_next":
                return SwitchSelection(1, out detail);
            case "selection_previous":
                return SwitchSelection(-1, out detail);
            case "hint":
                return TriggerHint(out detail);
            case "grab_next":
                return GrabNext(false, false, out detail);
            case "place_held":
                return PlaceHeld(false, out detail);
            case "heavy_grab_left":
                return GrabNext(true, false, out detail);
            case "heavy_grab_right":
                return GrabNext(true, true, out detail);
            case "heavy_place":
                return PlaceHeld(true, out detail);
            case "showcase_rotate":
                return RotateShowcase(argument, out detail);
            case "archive_push":
                return BeginArchive(out detail);
            default:
                detail = "unknown-action";
                return false;
        }
    }

    bool ConfirmStage(out string detail)
    {
        PolishedRuntimeStagePortalV02 portal =
            FindObjectsByType<PolishedRuntimeStagePortalV02>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(value =>
                    value && value.stage == bootstrap.CurrentStage);
        if (!portal)
        {
            detail = "stage-has-no-confirm-portal";
            return false;
        }
        bool accepted = portal.TriggerConfirmedActionForSwanEmulator();
        detail = accepted
            ? "confirmed-" + bootstrap.CurrentStage
            : "confirm-not-ready-or-rejected";
        return accepted;
    }

    bool SwitchSelection(int direction, out string detail)
    {
        if (bootstrap.CurrentStage != RuntimePuzzleStage.Selection)
        {
            detail = "selection-stage-required";
            return false;
        }
        SelectionCarouselController selection =
            FindFirstObjectByType<SelectionCarouselController>();
        if (!selection || selection.IsTransitioning || selection.IsCommitting)
        {
            detail = "selection-carousel-not-ready";
            return false;
        }
        int previous = selection.currentIndex;
        selection.BeginSwap(direction);
        bool began = selection.IsTransitioning;
        detail = began
            ? "selection-swap-started-from-" + previous
            : "selection-swap-rejected";
        return began;
    }

    bool TriggerHint(out string detail)
    {
        RuntimePuzzleLevelController level = CurrentLevel();
        if (!level || level.Completed)
        {
            detail = "active-incomplete-level-required";
            return false;
        }
        Stage6LampGhostHintV06Controller preciseHint =
            FindFirstObjectByType<Stage6LampGhostHintV06Controller>();
        if (preciseHint)
        {
            int before = preciseHint.ActiveSequence;
            bool visible = preciseHint.TriggerHintForSwanEmulator(
                out int sequence);
            detail = visible
                ? "exact-ghost-sequence-" + sequence
                : "hint-not-ready-before-" + before +
                  "-next-" + preciseHint.NextHintSequenceForValidation;
            return visible;
        }
        if (level.hintLamp)
        {
            int before = level.HintsUsed;
            level.hintLamp.Trigger();
            bool accepted = level.HintsUsed > before;
            detail = accepted ? "level-hint-triggered" : "hint-not-ready";
            return accepted;
        }
        detail = "hint-controller-missing";
        return false;
    }

    bool GrabNext(bool requireHeavy, bool secondary, out string detail)
    {
        RuntimePuzzleLevelController level = CurrentLevel();
        if (!level || level.Completed)
        {
            detail = "active-incomplete-level-required";
            return false;
        }

        if (secondary)
        {
            if (!heldPiece || !heldPiece.requiresTwoHands ||
                !primaryHolding || secondaryHolding)
            {
                detail = "heavy-left-grip-required-first";
                return false;
            }
            Pose secondPose = new Pose(
                heldPiece.transform.position + Vector3.right * 0.04f,
                heldPiece.transform.rotation);
            heldPiece.BeginExternalGrab(SecondaryHolderId, secondPose);
            secondaryHolding = heldPiece.ExternalHolderCount >= 2;
            detail = secondaryHolding
                ? "two-hand-weight-engaged-sequence-" + heldPiece.sequence
                : "second-heavy-grip-rejected";
            return secondaryHolding;
        }

        if (heldPiece)
        {
            detail = "a-piece-is-already-held";
            return false;
        }

        RuntimePuzzlePiece next = level.pieces
            .Where(value =>
                value && !value.IsSnapped && value.IsAvailable &&
                value.requiresTwoHands == requireHeavy)
            .OrderBy(value => value.sequence)
            .ThenBy(value => value.name)
            .FirstOrDefault();
        if (!next)
        {
            RuntimePuzzlePiece anyNext = level.pieces
                .Where(value => value && !value.IsSnapped && value.IsAvailable)
                .OrderBy(value => value.sequence)
                .FirstOrDefault();
            detail = anyNext && anyNext.requiresTwoHands
                ? "next-available-piece-requires-two-hands"
                : "no-matching-available-piece";
            return false;
        }

        Pose firstPose = new Pose(
            next.transform.position - Vector3.right * 0.04f,
            next.transform.rotation);
        next.BeginExternalGrab(PrimaryHolderId, firstPose);
        if (next.ExternalHolderCount < 1)
        {
            detail = "piece-grab-rejected";
            return false;
        }
        heldPiece = next;
        heldStage = bootstrap.CurrentStage;
        primaryHolding = true;
        secondaryHolding = false;
        detail = (requireHeavy ? "heavy-left-grabbed-sequence-" :
            "grabbed-sequence-") + next.sequence;
        return true;
    }

    bool PlaceHeld(bool requireHeavy, out string detail)
    {
        if (!heldPiece || !primaryHolding || heldPiece.IsSnapped)
        {
            detail = "no-held-piece";
            return false;
        }
        if (requireHeavy &&
            (!heldPiece.requiresTwoHands || !secondaryHolding ||
             heldPiece.ExternalHolderCount < 2))
        {
            detail = "both-heavy-grips-required";
            return false;
        }
        if (!requireHeavy && heldPiece.requiresTwoHands)
        {
            detail = "heavy-place-action-required";
            return false;
        }
        if (!heldPiece.targetSocket)
        {
            detail = "held-piece-has-no-socket";
            return false;
        }

        RuntimePuzzlePiece placing = heldPiece;
        Pose target = new Pose(
            placing.targetSocket.position,
            placing.targetSocket.rotation);
        placing.MoveExternalGrab(PrimaryHolderId, target);
        if (secondaryHolding)
            placing.MoveExternalGrab(SecondaryHolderId, target);
        // The host bridge supplies the semantic endpoint that the emulator's
        // missing tracked controller pose would normally provide. The normal
        // release path still owns socket validation, snap audio and progress.
        placing.transform.SetPositionAndRotation(
            placing.targetSocket.position,
            placing.targetSocket.rotation);
        placing.EndExternalGrab(PrimaryHolderId);
        if (secondaryHolding)
            placing.EndExternalGrab(SecondaryHolderId);
        bool snapped = placing.IsSnapped;
        int sequence = placing.sequence;
        ClearHeldState();
        detail = snapped
            ? "placed-sequence-" + sequence
            : "socket-rejected-sequence-" + sequence;
        return snapped;
    }

    bool RotateShowcase(string argument, out string detail)
    {
        if (bootstrap.CurrentStage != RuntimePuzzleStage.Completion)
        {
            detail = "completion-stage-required";
            return false;
        }
        CompletionInteractionController completion =
            FindFirstObjectByType<CompletionInteractionController>();
        if (!completion || !completion.heroModel)
        {
            detail = "completion-free-play-controller-missing";
            return false;
        }
        float degrees = 24f;
        if (!string.IsNullOrEmpty(argument) &&
            float.TryParse(
                argument,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float parsed))
            degrees = Mathf.Clamp(parsed, -75f, 75f);
        completion.SetGrabbedForSmoke(true);
        completion.ApplyFreePlayPoseForSmoke(
            Vector3.zero,
            Quaternion.Euler(0f, degrees, 0f));
        completion.SetGrabbedForSmoke(false);
        RuntimeAudioRouter.PlayCue(
            RuntimeAudioCue.Touch,
            completion.heroModel.position);
        detail = "showcase-rotated-degrees-" + degrees.ToString("F1");
        return true;
    }

    bool BeginArchive(out string detail)
    {
        if (bootstrap.CurrentStage != RuntimePuzzleStage.Collection)
        {
            detail = "collection-stage-required";
            return false;
        }
        CollectionArchiveRitualV06Controller archive =
            FindFirstObjectByType<CollectionArchiveRitualV06Controller>();
        if (!archive || archive.RitualActive || archive.Completed)
        {
            detail = "archive-ritual-not-ready";
            return false;
        }
        bool began = archive.BeginRitualForSwanEmulator();
        detail = began
            ? "archive-push-accepted"
            : "archive-push-rejected";
        return began;
    }

    RuntimePuzzleLevelController CurrentLevel()
    {
        if (bootstrap.CurrentStage != RuntimePuzzleStage.Cottage &&
            bootstrap.CurrentStage != RuntimePuzzleStage.Ship &&
            bootstrap.CurrentStage != RuntimePuzzleStage.Eiffel &&
            bootstrap.CurrentStage != RuntimePuzzleStage.Pagoda &&
            bootstrap.CurrentStage != RuntimePuzzleStage.Windmill)
            return null;
        return FindFirstObjectByType<RuntimePuzzleLevelController>();
    }

    string DescribeState()
    {
        string state = "stage-" + bootstrap.CurrentStage;
        if (bootstrap.IsTransitioning)
            state += "-transitioning";
        SelectionCarouselController selection =
            FindFirstObjectByType<SelectionCarouselController>();
        if (selection)
            state += "-selection-" + selection.currentIndex;
        RuntimePuzzleLevelController level = CurrentLevel();
        if (level)
        {
            state += "-parts-" + level.SnappedCount + "-of-" +
                level.TotalCount;
            RuntimePuzzlePiece next = level.pieces
                .Where(value => value && !value.IsSnapped && value.IsAvailable)
                .OrderBy(value => value.sequence)
                .FirstOrDefault();
            state += "-next-" + (next ? next.sequence : -1);
        }
        if (heldPiece)
            state += "-held-" + heldPiece.sequence + "-grips-" +
                heldPiece.ExternalHolderCount;
        Stage6LampGhostHintV06Controller preciseHint =
            FindFirstObjectByType<Stage6LampGhostHintV06Controller>();
        if (preciseHint)
            state += "-hint-next-" +
                preciseHint.NextHintSequenceForValidation +
                "-hint-active-" + preciseHint.ActiveSequence +
                "-hint-visible-" + preciseHint.GhostVisible;
        CollectionArchiveRitualV06Controller archive =
            FindFirstObjectByType<CollectionArchiveRitualV06Controller>();
        if (archive)
            state += "-archive-" + archive.Progress.ToString("F2");
        return state;
    }

    void ClearHeldState()
    {
        heldPiece = null;
        primaryHolding = false;
        secondaryHolding = false;
    }

    void WriteAck(
        string requestId,
        bool ok,
        string action,
        string detail)
    {
        string sanitized = (detail ?? string.Empty)
            .Replace('|', '-')
            .Replace('\n', ' ')
            .Replace('\r', ' ');
        string payload =
            requestId + "|" + (ok ? "ok" : "rejected") + "|" +
            action + "|stage=" +
            (bootstrap ? bootstrap.CurrentStage.ToString() : "none") +
            "|detail=" + sanitized +
            "|mode=" + PersonallyPuzzledExperienceModeState.Current;
        string temporary = AckPath + ".tmp";
        try
        {
            File.WriteAllText(temporary, payload + "\n");
            if (File.Exists(AckPath))
                File.Delete(AckPath);
            File.Move(temporary, AckPath);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[Personally Puzzled Swan Manual] ack write failed: " +
                exception.Message);
        }
    }
}
