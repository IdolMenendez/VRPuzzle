using System;
using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// Requests the V46 Unity-only interaction regression. The runner uses the
/// real V46 intent components and RuntimePuzzleHandInteractor path.
/// </summary>
[DisallowMultipleComponent]
public sealed class PinchIntegritySmokeV46 : MonoBehaviour
{
    public const string EnvironmentVariable =
        "PPVR_V46_COHERENT_INTERACTION_SMOKE";
    public const string V76SelectionOnlyEnvironmentVariable =
        "PPVR_V76_SELECTION_ONLY_SMOKE";
    public const string V77SelectionOnlyEnvironmentVariable =
        "PPVR_V77_SELECTION_ONLY_SMOKE";
    public const string V78SelectionOnlyEnvironmentVariable =
        "PPVR_V78_SELECTION_ONLY_SMOKE";

    static bool runnerCreated;

    void Start()
    {
#if UNITY_EDITOR
        if (runnerCreated ||
            Environment.GetEnvironmentVariable(EnvironmentVariable) != "1")
            return;
        runnerCreated = true;
        Environment.SetEnvironmentVariable(EnvironmentVariable, null);
        GameObject runnerObject =
            new GameObject("V46 coherent interaction validation runner");
        DontDestroyOnLoad(runnerObject);
        runnerObject.AddComponent<PinchIntegritySmokeRunnerV46>();
#endif
    }
}

#if UNITY_EDITOR
public sealed class PinchIntegritySmokeRunnerV46 : MonoBehaviour
{
    static readonly RuntimePuzzleStage[] PuzzleStages =
    {
        RuntimePuzzleStage.Cottage,
        RuntimePuzzleStage.Ship,
        RuntimePuzzleStage.Eiffel,
        RuntimePuzzleStage.Pagoda,
        RuntimePuzzleStage.Windmill,
    };

    static readonly int[] ExpectedCounts =
    {
        13,
        13,
        10,
        9,
        8,
    };

    IEnumerator Start()
    {
        bool selectionBoundaryOnly =
            Environment.GetEnvironmentVariable(
                PinchIntegritySmokeV46.V76SelectionOnlyEnvironmentVariable) == "1" ||
            Environment.GetEnvironmentVariable(
                PinchIntegritySmokeV46.V77SelectionOnlyEnvironmentVariable) == "1" ||
            Environment.GetEnvironmentVariable(
                PinchIntegritySmokeV46.V78SelectionOnlyEnvironmentVariable) == "1";
        Environment.SetEnvironmentVariable(
            PinchIntegritySmokeV46.V76SelectionOnlyEnvironmentVariable, null);
        Environment.SetEnvironmentVariable(
            PinchIntegritySmokeV46.V77SelectionOnlyEnvironmentVariable, null);
        Environment.SetEnvironmentVariable(
            PinchIntegritySmokeV46.V78SelectionOnlyEnvironmentVariable, null);
        yield return WaitForStage(RuntimePuzzleStage.Splash, 15f);
        PersistentPuzzleBootstrap bootstrap =
            PersistentPuzzleBootstrap.Instance;
        Require(bootstrap, "persistent bootstrap is unavailable");
        RuntimeRealHandPinchV47.Instance?.ClearAutomationV47();

        yield return ValidateSplash();
        yield return WaitForStage(RuntimePuzzleStage.Selection, 15f);
        bool v78SelectionMode =
            FindFirstObjectByType<SelectionCenterThenConfirmV78>();
        bool v77SelectionMode = !v78SelectionMode &&
            FindFirstObjectByType<SelectionDeliberateConfirmV77>();
        yield return ValidateSelection();
        if (selectionBoundaryOnly)
        {
            RuntimeRealHandPinchV47.Instance?.ClearAutomationV47();
            if (v78SelectionMode)
                Debug.Log("[PPVR V78 Smoke] CENTRE-THEN-CONFIRM PLAYMODE PASSED · " +
                    "first pinch centres and locks · held pinch cannot enter · " +
                    "no timeout · different target recentres · second pinch enters");
            else if (v77SelectionMode)
                Debug.Log("[PPVR V77 Smoke] DELIBERATE-CONFIRM PLAYMODE PASSED · " +
                    "first pinch locks · held pinch cannot enter · no timeout · " +
                    "different target re-locks · release + second pinch enters");
            else
                Debug.Log("[PPVR V76 Smoke] SELECTION-BOUNDARY PLAYMODE PASSED · " +
                    "Splash release/fresh-press · Selection held-before-release blocked · " +
                    "5/5 stable gaze · grace preserved · one pinch -> Cottage");
            Finish(0);
            yield break;
        }

        // The smoke walks every unlocked level in sequence. Use the real
        // BeginLevel path so CurrentLevelIndex remains authoritative for the
        // Completion portal instead of bypassing it with raw scene requests.
        bootstrap.Save.highestUnlockedLevel = 4;

        for (int levelIndex = 0;
             levelIndex < PuzzleStages.Length;
             levelIndex++)
        {
            RuntimePuzzleStage stage = PuzzleStages[levelIndex];
            if (bootstrap.CurrentStage != stage)
            {
                bootstrap.BeginLevel(levelIndex);
                yield return WaitForStage(stage, 15f);
            }
            yield return ValidatePuzzle(
                stage,
                ExpectedCounts[levelIndex]);
        }

        if (RuntimeRealHandPinchV47.Instance)
            yield return ValidateCompletionAndCollectionV47(bootstrap);

        RuntimeRealHandPinchV47.Instance?.ClearAutomationV47();

        Debug.Log(
            "[Pinch Integrity V46] COMPLETE UNITY INTERACTION " +
            "SMOKE PASSED · Splash whole MODEL BUILDER 9/9 ray samples · " +
            "Selection gaze inert + 6/6 model rays + side-focus/center-enter · " +
            "Puzzle touch-pinch bad-release return + valid-release snap " +
            "Cottage 13/13 · Ship 13/13 · Eiffel 10/10 · " +
            "Pagoda 9/9 · Windmill 8/8");
        Finish(0);
    }

    static IEnumerator ValidateCompletionAndCollectionV47(
        PersistentPuzzleBootstrap bootstrap)
    {
        RuntimeRealHandPinchV47 provider =
            RuntimeRealHandPinchV47.Instance;
        Require(
            provider,
            "V47 shared provider is unavailable before Completion");
        provider.ClearAutomationV47();
        bootstrap.RequestStage(
            RuntimePuzzleStage.Completion,
            bootstrap.completionScene);
        yield return WaitForStage(RuntimePuzzleStage.Completion, 15f);

        CompletionInteractionController freePlay =
            FindFirstObjectByType<CompletionInteractionController>();
        RuntimeCompletionExitV69 explicitExitV69 =
            FindFirstObjectByType<RuntimeCompletionExitV69>();
        if (explicitExitV69)
        {
            Require(
                freePlay && freePlay.resilientRealHandPinchV47 &&
                explicitExitV69.touchCollider,
                "V69 Completion free-play/explicit exit bindings are incomplete");

            Vector3 beforeV69 = freePlay.heroModel.position;
            Quaternion rotationBeforeGrabV69 = freePlay.heroModel.rotation;
            Pose grabPoseV69 = new Pose(
                beforeV69,
                rotationBeforeGrabV69);
            SetProviderHandWorldV47(
                provider, false, true, false, grabPoseV69);
            yield return null;
            SetProviderHandWorldV47(
                provider, false, true, true, grabPoseV69);
            yield return null;
            Require(
                Quaternion.Angle(
                    rotationBeforeGrabV69,
                    freePlay.heroModel.rotation) < 1.0f,
                "V69 Completion model rotated on the first grab frame");
            Pose movedV69 = new Pose(
                beforeV69 + new Vector3(0.035f, 0f, 0.025f),
                Quaternion.Euler(0f, 12f, 0f) * rotationBeforeGrabV69);
            SetProviderHandWorldV47(
                provider, false, true, true, movedV69);
            yield return WaitRealtime(0.08f);
            Require(
                Vector3.Distance(
                    beforeV69,
                    freePlay.heroModel.position) > 0.02f,
                "V69 Completion free-play move was not applied");
            SetProviderHandWorldV47(
                provider, false, true, false, movedV69);
            yield return WaitRealtime(0.08f);

            Pose exitPoseV69 = new Pose(
                explicitExitV69.touchCollider.bounds.center,
                Quaternion.identity);
            SetProviderHandWorldV47(
                provider, false, true, false, exitPoseV69);
            yield return WaitForStage(RuntimePuzzleStage.Selection, 15f);
            Debug.Log(
                "[PPVR V69 Smoke] COMPLETION PASSED · first-grab yaw offset " +
                "stable · physical return button -> Selection");
            yield break;
        }
        PolishedRuntimeStagePortalV02 portal =
            FindObjectsByType<PolishedRuntimeStagePortalV02>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Single(value =>
                    value.stage == RuntimePuzzleStage.Completion);
        Require(
            freePlay &&
            freePlay.resilientRealHandPinchV47 &&
            portal &&
            portal.resilientRealHandPinchV47,
            "V47 Completion shared-pinch consumers are incomplete");

        Vector3 before = freePlay.heroModel.position;
        Pose freePlayStart = new Pose(
            before,
            freePlay.heroModel.rotation);
        SetProviderHandWorldV47(
            provider,
            false,
            true,
            false,
            freePlayStart);
        yield return null;
        SetProviderHandWorldV47(
            provider,
            false,
            true,
            true,
            freePlayStart);
        yield return null;
        Pose freePlayMoved = new Pose(
            before + new Vector3(0.035f, 0f, 0.025f),
            Quaternion.Euler(0f, 12f, 0f) *
            freePlay.heroModel.rotation);
        SetProviderHandWorldV47(
            provider,
            false,
            true,
            true,
            freePlayMoved);
        yield return WaitRealtime(0.08f);
        Require(
            Vector3.Distance(before, freePlay.heroModel.position) > 0.02f,
            "V47 Completion shared-provider free-play move was not applied " +
            "through Update");
        SetProviderHandWorldV47(
            provider,
            false,
            true,
            false,
            freePlayMoved);
        yield return WaitRealtime(0.08f);
        Quaternion rotationBeforeAuto = freePlay.heroModel.rotation;
        yield return WaitRealtime(0.28f);
        Require(
            Quaternion.Angle(
                rotationBeforeAuto,
                freePlay.heroModel.rotation) > 0.10f,
            "V47 Completion free-play auto rotation did not resume " +
            "after provider release");

        yield return WaitRealtime(
            Mathf.Max(0.10f, portal.minimumStageSeconds + 0.05f));
        Pose portalPose = new Pose(
            portal.activationTarget.position,
            portal.activationTarget.rotation);
        SetProviderHandWorldV47(
            provider,
            false,
            true,
            false,
            portalPose);
        yield return WaitRealtime(0.08f);
        SetProviderHandWorldV47(
            provider,
            false,
            true,
            true,
            portalPose);
        yield return WaitRealtime(0.08f);
        Require(
            portal.IsStageAdvanceQueuedForSmoke,
            "V47 Completion shared-provider press did not queue Collection " +
            "through portal Update");
        SetProviderHandWorldV47(
            provider,
            false,
            true,
            false,
            portalPose);
        Debug.Log(
            "[PPVR V47 Smoke] COMPLETION FREE-PLAY/NEXT-STAGE PASSED · " +
            "SHARED-PROVIDER/NORMAL-UPDATE");

        yield return WaitForStage(RuntimePuzzleStage.Collection, 15f);
        CollectionArchiveRitualV06Controller ritual =
            FindFirstObjectByType<CollectionArchiveRitualV06Controller>();
        Require(
            ritual &&
            ritual.resilientRealHandInputV47 &&
            ritual.HasCompleteBindings,
            "V47 Collection shared-palm consumer is incomplete");
        ritual.ResetRitual();
        Vector3 towardCabinet =
            ritual.cabinetTarget.position - ritual.heroModel.position;
        towardCabinet = towardCabinet.sqrMagnitude > 0.0001f
            ? towardCabinet.normalized
            : Vector3.forward;
        Vector3 palmStart =
            ritual.heroModel.position - towardCabinet * 0.055f;
        Vector3 palmEnd =
            palmStart + towardCabinet * 0.035f;
        SetProviderPalmWorldV47(
            provider,
            true,
            true,
            new Pose(palmStart, Quaternion.identity));
        SetProviderPalmWorldV47(
            provider,
            false,
            false,
            default);
        yield return null;
        SetProviderPalmWorldV47(
            provider,
            true,
            true,
            new Pose(palmEnd, Quaternion.identity));
        yield return null;
        Require(
            ritual.RitualActive,
            "V47 Collection shared-provider Palm push did not start the " +
            "ritual through Update");
        SetProviderPalmWorldV47(
            provider,
            true,
            false,
            default);
        provider.ClearAutomationV47();
        float ritualDeadline =
            Time.realtimeSinceStartup + ritual.flightDuration + 1.0f;
        while (!ritual.Completed &&
               Time.realtimeSinceStartup < ritualDeadline)
            yield return null;
        Require(
            ritual.Completed && ritual.LitStarCount == 3,
            "V47 Collection normal Update ritual did not complete with " +
            "three stars");
        Debug.Log(
            "[PPVR V47 Smoke] COLLECTION SHARED-PALM/RITUAL PASSED · " +
            "SHARED-PROVIDER/NORMAL-UPDATE");
    }

    static IEnumerator ValidateSplash()
    {
        SplashWorkshopController splash =
            FindFirstObjectByType<SplashWorkshopController>();
        RuntimeIntentRayV46 intent =
            FindFirstObjectByType<RuntimeIntentRayV46>();
        SplashModelBuilderEntryV46 entry =
            FindFirstObjectByType<SplashModelBuilderEntryV46>();
        Camera camera =
            PersistentPuzzleBootstrap.Instance.xrCamera;
        Require(
            splash && intent && entry && camera,
            "V46 Splash wiring is incomplete");
        Require(
            intent.TryGetSplashBoundsV46(out Bounds bounds),
            "complete MODEL BUILDER bounds are unavailable");

        float[] fractions = { -0.34f, 0f, 0.34f };
        int hits = 0;
        foreach (float vertical in fractions)
        foreach (float horizontal in fractions)
        {
            Vector3 point =
                bounds.center +
                Vector3.right * bounds.extents.x * horizontal +
                Vector3.up * bounds.extents.y * vertical;
            Ray ray = new Ray(
                camera.transform.position,
                (point - camera.transform.position).normalized);
            if (intent.ResolveSplashRayV46(ray, out float distance) &&
                distance > 0f)
                hits++;
        }
        Require(
            hits == 9,
            "whole MODEL BUILDER ray coverage failed: " + hits + "/9");

        Ray outside = new Ray(
            camera.transform.position,
            (bounds.center +
             Vector3.right * bounds.size.x * 1.4f -
             camera.transform.position).normalized);
        Require(
            !intent.ResolveSplashRayV46(outside, out _),
            "Splash intent bounds accept a ray clearly outside MODEL BUILDER");

        Ray centerRay = new Ray(
            camera.transform.position,
            (bounds.center - camera.transform.position).normalized);
        RuntimeRealHandPinchV47 provider =
            RuntimeRealHandPinchV47.Instance;
        bool providerDrivenV47 =
            provider &&
            entry.resilientSignalGapV47 &&
            intent.sharedRealHandPinchV47;
        if (providerDrivenV47)
        {
            provider.ClearAutomationV47();
            intent.SetAutomationRayV47(centerRay);
            provider.SetAutomationHandStateV47(
                false,
                false,
                false,
                false,
                default);
            yield return WaitRealtime(0.16f);
            Require(
                entry.ArmedV46,
                "V47 Splash did not arm after a shared-provider " +
                "tracking gap");
            provider.SetAutomationHandStateV47(
                false,
                true,
                true,
                false,
                default);
            yield return WaitRealtime(0.08f);
        }
        else
        {
            if (entry.resilientSignalGapV47)
            {
                intent.SetAutomationInputV46(
                    centerRay,
                    false,
                    false);
                yield return WaitRealtime(0.16f);
                Require(
                    entry.ArmedV46,
                    "V47 Splash did not arm after a real tracking gap");
            }
            else
            {
                intent.SetAutomationInputV46(
                    centerRay,
                    true,
                    true);
                yield return WaitRealtime(0.08f);
                Require(
                    !splash.IsActivated,
                    "Splash accepted a held pinch before observing release");
                intent.SetAutomationInputV46(
                    centerRay,
                    true,
                    false);
                yield return WaitRealtime(0.16f);
                Require(
                    entry.ArmedV46,
                    "Splash did not arm after a tracked release");
            }
            intent.SetAutomationInputV46(
                centerRay,
                true,
                true);
            yield return WaitRealtime(0.08f);
        }
        Require(
            splash.IsActivated,
            "released-then-pinched MODEL BUILDER ray did not activate Splash through Update");
        if (providerDrivenV47)
        {
            provider.SetAutomationHandStateV47(
                false,
                true,
                false,
                false,
                default);
            intent.ClearAutomationRayV47();
            provider.ClearAutomationV47();
        }
        else
            intent.ClearAutomationInputV46();
        Debug.Log(
            "[Pinch Integrity V46] SPLASH PASSED · " +
            "whole MODEL BUILDER 9/9 · outside rejected · " +
            "unarmed pinch rejected · fresh pinch accepted");
        if (entry.resilientSignalGapV47)
            Debug.Log(
                "[PPVR V47 Smoke] SPLASH TRACKING-GAP RELEASE/" +
                "FRESH-PRESS PASSED");
        if (providerDrivenV47)
            Debug.Log(
                "[PPVR V47 Smoke] SPLASH SHARED-PROVIDER/" +
                "NORMAL-UPDATE PASSED");
        yield return null;
    }

    static IEnumerator ValidateSelection()
    {
        SelectionCenterThenConfirmV78 v78CentreThenConfirm =
            FindFirstObjectByType<SelectionCenterThenConfirmV78>();
        if (v78CentreThenConfirm)
        {
            yield return ValidateSelectionV78(v78CentreThenConfirm);
            yield break;
        }
        SelectionDeliberateConfirmV77 v77DeliberateConfirm =
            FindFirstObjectByType<SelectionDeliberateConfirmV77>();
        if (v77DeliberateConfirm)
        {
            yield return ValidateSelectionV77(v77DeliberateConfirm);
            yield break;
        }
        SelectionFluidGazePinchV76 v76FluidInput =
            FindFirstObjectByType<SelectionFluidGazePinchV76>();
        if (v76FluidInput)
        {
            yield return ValidateSelectionV76(v76FluidInput);
            yield break;
        }
        SelectionGazeBoundsPinchDirectInputV72 v72GazePinch =
            FindFirstObjectByType<SelectionGazeBoundsPinchDirectInputV72>();
        if (v72GazePinch)
        {
            yield return ValidateSelectionV72(v72GazePinch);
            yield break;
        }
        SelectionGazePinchDirectInputV71 v71GazePinch =
            FindFirstObjectByType<SelectionGazePinchDirectInputV71>();
        if (v71GazePinch)
        {
            yield return ValidateSelectionV71(v71GazePinch);
            yield break;
        }
        SelectionHandDirectInputV70 v70HandDirect =
            FindFirstObjectByType<SelectionHandDirectInputV70>();
        if (v70HandDirect)
        {
            yield return ValidateSelectionV70(v70HandDirect);
            yield break;
        }
        SelectionHandDirectInputV69 v69HandDirect =
            FindFirstObjectByType<SelectionHandDirectInputV69>();
        if (v69HandDirect)
        {
            yield return ValidateSelectionV69(v69HandDirect);
            yield break;
        }
        SelectionCarouselController carousel =
            FindFirstObjectByType<SelectionCarouselController>();
        RuntimeIntentRayV46 intent =
            FindFirstObjectByType<RuntimeIntentRayV46>();
        SelectionGazePinchInputV46 input =
            FindFirstObjectByType<SelectionGazePinchInputV46>();
        PolishedRuntimeStagePortalV02 portal =
            FindObjectsByType<PolishedRuntimeStagePortalV02>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Single(value =>
                    value.stage == RuntimePuzzleStage.Selection);
        Camera camera =
            PersistentPuzzleBootstrap.Instance.xrCamera;
        Require(
            carousel && intent && input && portal && camera,
            "V46 Selection wiring is incomplete");
        input.RefreshTargetsV46();
        SelectionExhibitTargetV09[] targets =
            input.targets
                .Where(value => value)
                .OrderBy(value => value.exhibitIndex)
                .ToArray();
        Require(
            targets.Length == 6,
            "Selection does not contain six semantic exhibit targets");

        int initialIndex = carousel.currentIndex;
        foreach (SelectionExhibitTargetV09 target in targets)
        {
            Require(
                intent.TryGetSelectionBoundsV46(target, out Bounds bounds),
                "visible Selection model bounds are unavailable: " +
                target.exhibitIndex);
            Ray ray = new Ray(
                camera.transform.position,
                (bounds.center - camera.transform.position).normalized);
            SelectionExhibitTargetV09 resolved =
                intent.ResolveSelectionRayV46(
                    ray,
                    out float distance);
            Require(
                resolved == target && distance > 0f,
                "model ray resolved wrong target: requested=" +
                target.exhibitIndex +
                " resolved=" +
                (resolved ? resolved.exhibitIndex : -1));
        }
        yield return new WaitForSecondsRealtime(0.25f);
        Require(
            carousel.currentIndex == initialIndex &&
            !carousel.IsTransitioning &&
            input.AcceptedActionCountV46 == 0,
            "gaze alone changed Selection");

        int firstLevelExhibit = Array.FindIndex(
            portal.selectionLevelByExhibit,
            value => value == 0);
        Require(
            firstLevelExhibit >= 0,
            "Selection has no mapped Cottage exhibit");
        int[] focusOrder = targets
            .Select(value => value.exhibitIndex)
            .Where(value =>
                value != carousel.currentIndex &&
                value != firstLevelExhibit)
            .Concat(
                carousel.currentIndex == firstLevelExhibit
                    ? Array.Empty<int>()
                    : new[] { carousel.currentIndex })
            .Concat(new[] { firstLevelExhibit })
            .Distinct()
            .ToArray();

        foreach (int requested in focusOrder)
        {
            if (requested == carousel.currentIndex)
                continue;
            SelectionExhibitTargetV09 target =
                targets.Single(value =>
                    value.exhibitIndex == requested);
            Require(
                intent.TryGetSelectionBoundsV46(
                    target,
                    out Bounds targetBounds),
                "fresh pinch target bounds are unavailable: " + requested);
            Ray targetRay = new Ray(
                camera.transform.position,
                (targetBounds.center - camera.transform.position).normalized);
            int actionsBeforeFocus = input.AcceptedActionCountV46;
            yield return DriveFreshPinchV46(intent, targetRay);
            Require(
                input.AcceptedActionCountV46 == actionsBeforeFocus + 1,
                "fresh pinch did not focus side target through Update " + requested);
            float timeout = Time.realtimeSinceStartup + 5f;
            while (carousel.IsTransitioning &&
                   Time.realtimeSinceStartup < timeout)
                yield return null;
            Require(
                !carousel.IsTransitioning &&
                carousel.currentIndex == requested,
                "side target did not settle at center: " + requested);
        }

        Require(
            carousel.currentIndex == firstLevelExhibit,
            "Cottage exhibit was not centered before entry");
        SelectionExhibitTargetV09 centered =
            targets.Single(value =>
                value.exhibitIndex == firstLevelExhibit);
        int actionsBefore = input.AcceptedActionCountV46;
        Require(
            intent.TryGetSelectionBoundsV46(
                centered,
                out Bounds centeredBounds),
            "centered target bounds are unavailable");
        Ray centeredRay = new Ray(
            camera.transform.position,
            (centeredBounds.center - camera.transform.position).normalized);
        yield return DriveFreshPinchV46(intent, centeredRay);
        Require(
            input.AcceptedActionCountV46 == actionsBefore + 1,
            "center pinch did not consume exactly one Update-driven action");
        Debug.Log(
            "[Pinch Integrity V46] SELECTION PASSED · " +
            "gaze inert · 6/6 model rays exact · " +
            "side pinch focus · release · center pinch enter");
        yield return null;
    }

    static IEnumerator ValidateSelectionV78(
        SelectionCenterThenConfirmV78 input)
    {
        SelectionGazeBoundsPinchDirectInputV72 gaze = input.gazeResolver;
        SelectionCarouselController carousel = input.selection;
        RuntimeRealHandPinchV47 provider = RuntimeRealHandPinchV47.Instance;
        Camera camera = gaze && gaze.sceneCamera ? gaze.sceneCamera : Camera.main;
        Require(gaze && carousel && provider && camera,
            "V78 Selection resolver/carousel/provider/camera is missing");
        gaze.RefreshTargets();
        SelectionExhibitTargetV09[] active = gaze.targets
            .Where(value => value && value.enabled)
            .OrderBy(value => value.exhibitIndex)
            .ToArray();
        Require(active.Length == 5 &&
                !FindObjectsByType<SelectionDeliberateConfirmV77>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Any() &&
                !FindObjectsByType<SelectionFluidGazePinchV76>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Any() &&
                !FindObjectsByType<SelectionGazePinchConfirmV73>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Any() &&
                !gaze.enabled,
            "V78 centre-then-confirm authority is invalid");

        provider.ClearAutomationV47();
        SelectionExhibitTargetV09 cottage =
            active.Single(value => value.exhibitIndex == 5);
        SelectionExhibitTargetV09 alternate =
            active.First(value => value.exhibitIndex != carousel.currentIndex);
        Pose handPose = new Pose(
            camera.transform.position, camera.transform.rotation);

        Require(gaze.TryGetVisibleBoundsV72(
                cottage, out Bounds initialCottageBounds),
            "V78 initial Cottage bounds are unavailable");
        gaze.SetAutomationGazeRayV72(new Ray(camera.transform.position,
            (initialCottageBounds.center - camera.transform.position).normalized));
        SetProviderHandWorldV47(provider, false, true, true, handPose);
        yield return new WaitForSecondsRealtime(
            input.stableGazeSeconds + input.confirmHoldSeconds + 0.08f);
        Require(input.CentreActionCountV78 == 0 &&
                input.AcceptedActionCountV78 == 0 &&
                !carousel.IsTransitioning && !carousel.IsCommitting,
            "V78 accepted a held-before-release pinch");

        SetProviderHandWorldV47(provider, false, true, false, handPose);
        yield return new WaitForSecondsRealtime(input.releaseArmSeconds + 0.03f);

        foreach (SelectionExhibitTargetV09 target in active)
        {
            Require(gaze.TryGetVisibleBoundsV72(target, out Bounds bounds),
                "V78 visible bounds missing for exhibit=" + target.exhibitIndex);
            gaze.SetAutomationGazeRayV72(new Ray(camera.transform.position,
                (bounds.center - camera.transform.position).normalized));
            yield return new WaitForSecondsRealtime(
                input.switchGazeSeconds + 0.04f);
            Require(input.StableTargetV78 == target &&
                    input.feedback &&
                    input.feedback.SelectedIndex == target.exhibitIndex,
                "V78 stable gaze preview failed for exhibit=" +
                target.exhibitIndex);
            Require(!carousel.IsTransitioning && !carousel.IsCommitting &&
                    input.AcceptedActionCountV78 == 0,
                "V78 gaze alone entered Selection");
        }

        Require(gaze.TryGetVisibleBoundsV72(
                alternate, out Bounds alternateBounds),
            "V78 alternate bounds are unavailable");
        gaze.SetAutomationGazeRayV72(new Ray(camera.transform.position,
            (alternateBounds.center - camera.transform.position).normalized));
        yield return new WaitForSecondsRealtime(input.switchGazeSeconds + 0.04f);
        SetProviderHandWorldV47(provider, false, true, true, handPose);
        yield return new WaitForSecondsRealtime(input.confirmHoldSeconds + 0.03f);
        float transitionDeadline = Time.realtimeSinceStartup + 6f;
        while (carousel.IsTransitioning &&
               Time.realtimeSinceStartup < transitionDeadline)
            yield return null;
        Require(input.LockedTargetV78 == alternate &&
                input.CentreActionCountV78 == 1 &&
                input.AcceptedActionCountV78 == 0 &&
                carousel.currentIndex == alternate.exhibitIndex &&
                !carousel.IsTransitioning && !carousel.IsCommitting,
            "V78 first pinch did not centre and lock without entering");

        yield return new WaitForSecondsRealtime(0.30f);
        Require(input.AcceptedActionCountV78 == 0 &&
                !carousel.IsTransitioning && !carousel.IsCommitting,
            "V78 a continuous first pinch entered the level");
        SetProviderHandWorldV47(provider, false, true, false, handPose);
        yield return new WaitForSecondsRealtime(
            Mathf.Max(input.releaseArmSeconds + 0.03f,
                input.actionCooldownSeconds + 0.02f));
        gaze.SetAutomationGazeRayV72(
            new Ray(camera.transform.position, -camera.transform.forward));
        yield return new WaitForSecondsRealtime(0.75f);
        Require(input.LockedTargetV78 == alternate &&
                input.AcceptedActionCountV78 == 0,
            "V78 centred lock expired while the player hesitated");

        Require(gaze.TryGetVisibleBoundsV72(
                cottage, out Bounds movedCottageBounds),
            "V78 moved Cottage bounds are unavailable");
        gaze.SetAutomationGazeRayV72(new Ray(camera.transform.position,
            (movedCottageBounds.center - camera.transform.position).normalized));
        yield return new WaitForSecondsRealtime(input.switchGazeSeconds + 0.04f);
        SetProviderHandWorldV47(provider, false, true, true, handPose);
        yield return new WaitForSecondsRealtime(input.confirmHoldSeconds + 0.03f);
        transitionDeadline = Time.realtimeSinceStartup + 6f;
        while (carousel.IsTransitioning &&
               Time.realtimeSinceStartup < transitionDeadline)
            yield return null;
        Require(input.LockedTargetV78 == cottage &&
                input.CentreActionCountV78 == 2 &&
                input.AcceptedActionCountV78 == 0 &&
                carousel.currentIndex == cottage.exhibitIndex &&
                !carousel.IsTransitioning && !carousel.IsCommitting,
            "V78 reconsideration did not recenter Cottage without entering");

        SetProviderHandWorldV47(provider, false, true, false, handPose);
        yield return new WaitForSecondsRealtime(
            Mathf.Max(input.releaseArmSeconds + 0.03f,
                input.actionCooldownSeconds + 0.02f));
        Require(gaze.TryGetVisibleBoundsV72(
                cottage, out Bounds centredCottageBounds),
            "V78 centred Cottage bounds are unavailable");
        gaze.SetAutomationGazeRayV72(new Ray(camera.transform.position,
            (centredCottageBounds.center - camera.transform.position).normalized));
        yield return new WaitForSecondsRealtime(input.switchGazeSeconds + 0.04f);
        SetProviderHandWorldV47(provider, false, true, true, handPose);
        yield return new WaitForSecondsRealtime(input.confirmHoldSeconds + 0.03f);
        Require(input.AcceptedActionCountV78 == 1,
            "V78 second separate pinch did not confirm centred Cottage");
        yield return WaitForStage(RuntimePuzzleStage.Cottage, 15f);
        provider.ClearAutomationV47();
        gaze.ClearAutomationGazeRayV72();
        Debug.Log("[PPVR V78 Smoke] SELECTION PASSED · " +
            "held-before-release=blocked · stable-visible-bounds=5/5 · " +
            "first-pinch=centre-and-lock · hold=no-entry · no-timeout · " +
            "different-target=recenter · second-pinch=Cottage");
        yield return null;
    }

    static IEnumerator ValidateSelectionV77(
        SelectionDeliberateConfirmV77 input)
    {
        SelectionGazeBoundsPinchDirectInputV72 gaze = input.gazeResolver;
        SelectionCarouselController carousel = input.selection;
        RuntimeRealHandPinchV47 provider = RuntimeRealHandPinchV47.Instance;
        Camera camera = gaze && gaze.sceneCamera ? gaze.sceneCamera : Camera.main;
        Require(gaze && carousel && provider && camera,
            "V77 deliberate Selection resolver/carousel/provider/camera is missing");
        gaze.RefreshTargets();
        SelectionExhibitTargetV09[] active = gaze.targets
            .Where(value => value && value.enabled)
            .OrderBy(value => value.exhibitIndex)
            .ToArray();
        Require(active.Length == 5 &&
                !FindObjectsByType<SelectionFluidGazePinchV76>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Any() &&
                !FindObjectsByType<SelectionGazePinchConfirmV73>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Any() &&
                !gaze.enabled,
            "V77 deliberate authority or playable target split is invalid");

        provider.ClearAutomationV47();
        SelectionExhibitTargetV09 cottage =
            active.Single(value => value.exhibitIndex == 5);
        SelectionExhibitTargetV09 alternate =
            active.Single(value => value.exhibitIndex == 0);
        bool cottageBoundsAvailable =
            gaze.TryGetVisibleBoundsV72(cottage, out Bounds cottageBounds);
        bool alternateBoundsAvailable =
            gaze.TryGetVisibleBoundsV72(alternate, out Bounds alternateBounds);
        Require(cottageBoundsAvailable && alternateBoundsAvailable,
            "V77 Cottage or alternate visible bounds are unavailable");
        PolishedRuntimeStagePortalV02 portal =
            alternate.directPuzzleIntentV31
                ? alternate.directPuzzleIntentV31.portal
                : null;
        Require(portal && portal.selectionLevelByExhibit != null &&
                alternate.exhibitIndex < portal.selectionLevelByExhibit.Length,
            "V77 alternate exhibit level mapping is unavailable");
        int alternateLevel =
            portal.selectionLevelByExhibit[alternate.exhibitIndex];
        RuntimePuzzleStage alternateStage =
            alternateLevel == 0 ? RuntimePuzzleStage.Cottage :
            alternateLevel == 1 ? RuntimePuzzleStage.Ship :
            alternateLevel == 2 ? RuntimePuzzleStage.Eiffel :
            alternateLevel == 3 ? RuntimePuzzleStage.Pagoda :
            RuntimePuzzleStage.Windmill;
        Pose handPose = new Pose(
            camera.transform.position, camera.transform.rotation);

        gaze.SetAutomationGazeRayV72(new Ray(camera.transform.position,
            (cottageBounds.center - camera.transform.position).normalized));
        SetProviderHandWorldV47(provider, false, true, true, handPose);
        yield return new WaitForSecondsRealtime(
            input.stableGazeSeconds + input.confirmHoldSeconds + 0.08f);
        Require(input.LockActionCountV77 == 0 &&
                input.AcceptedActionCountV77 == 0 &&
                !carousel.IsTransitioning && !carousel.IsCommitting,
            "V77 accepted a held-before-release pinch");

        SetProviderHandWorldV47(provider, false, true, false, handPose);
        yield return new WaitForSecondsRealtime(input.releaseArmSeconds + 0.03f);

        int initialIndex = carousel.currentIndex;
        foreach (SelectionExhibitTargetV09 target in active)
        {
            Require(gaze.TryGetVisibleBoundsV72(target, out Bounds bounds),
                "V77 visible bounds missing for exhibit=" + target.exhibitIndex);
            gaze.SetAutomationGazeRayV72(new Ray(camera.transform.position,
                (bounds.center - camera.transform.position).normalized));
            yield return new WaitForSecondsRealtime(
                input.switchGazeSeconds + 0.04f);
            Require(input.StableTargetV77 == target &&
                    input.feedback &&
                    input.feedback.SelectedIndex == target.exhibitIndex,
                "V77 stable gaze preview failed for exhibit=" +
                target.exhibitIndex);
            Require(!carousel.IsTransitioning && !carousel.IsCommitting &&
                    carousel.currentIndex == initialIndex,
                "V77 gaze alone moved or entered Selection");
        }

        gaze.SetAutomationGazeRayV72(new Ray(camera.transform.position,
            (cottageBounds.center - camera.transform.position).normalized));
        yield return new WaitForSecondsRealtime(input.switchGazeSeconds + 0.04f);
        SetProviderHandWorldV47(provider, false, true, true, handPose);
        yield return new WaitForSecondsRealtime(input.confirmHoldSeconds + 0.03f);
        Require(input.LockedTargetV77 == cottage &&
                input.LockActionCountV77 == 1 &&
                input.AcceptedActionCountV77 == 0 &&
                !carousel.IsTransitioning && !carousel.IsCommitting,
            "V77 first pinch did not lock Cottage without entering");

        yield return new WaitForSecondsRealtime(0.30f);
        Require(input.AcceptedActionCountV77 == 0 &&
                !carousel.IsTransitioning && !carousel.IsCommitting,
            "V77 a continuous first pinch entered the level");
        SetProviderHandWorldV47(provider, false, true, false, handPose);
        yield return new WaitForSecondsRealtime(
            Mathf.Max(input.releaseArmSeconds + 0.03f,
                input.actionCooldownSeconds + 0.02f));

        gaze.SetAutomationGazeRayV72(
            new Ray(camera.transform.position, -camera.transform.forward));
        yield return new WaitForSecondsRealtime(0.75f);
        Require(input.LockedTargetV77 == cottage &&
                input.AcceptedActionCountV77 == 0,
            "V77 locked choice expired while the player hesitated");

        gaze.SetAutomationGazeRayV72(new Ray(camera.transform.position,
            (alternateBounds.center - camera.transform.position).normalized));
        yield return new WaitForSecondsRealtime(input.switchGazeSeconds + 0.04f);
        SetProviderHandWorldV47(provider, false, true, true, handPose);
        yield return new WaitForSecondsRealtime(input.confirmHoldSeconds + 0.03f);
        Require(input.LockedTargetV77 == alternate &&
                input.LockActionCountV77 == 2 &&
                input.AcceptedActionCountV77 == 0 &&
                !carousel.IsTransitioning && !carousel.IsCommitting,
            "V77 pinching a different target entered instead of re-locking");

        yield return new WaitForSecondsRealtime(0.20f);
        Require(input.AcceptedActionCountV77 == 0,
            "V77 held re-lock pinch entered Ship");
        SetProviderHandWorldV47(provider, false, true, false, handPose);
        yield return new WaitForSecondsRealtime(
            Mathf.Max(input.releaseArmSeconds + 0.03f,
                input.actionCooldownSeconds + 0.02f));
        SetProviderHandWorldV47(provider, false, true, true, handPose);
        yield return new WaitForSecondsRealtime(input.confirmHoldSeconds + 0.03f);
        Require(input.AcceptedActionCountV77 == 1,
            "V77 second separate pinch did not confirm the alternate exhibit");
        yield return WaitForStage(alternateStage, 15f);
        provider.ClearAutomationV47();
        gaze.ClearAutomationGazeRayV72();
        Debug.Log("[PPVR V77 Smoke] SELECTION PASSED · " +
            "held-before-release=blocked · stable-visible-bounds=5/5 · " +
            "first-pinch=lock-only · hold=no-entry · no-timeout · " +
            "different-target=re-lock · second-pinch=" + alternateStage);
        yield return null;
    }

    static IEnumerator ValidateSelectionV76(
        SelectionFluidGazePinchV76 input)
    {
        SelectionGazeBoundsPinchDirectInputV72 gaze = input.gazeResolver;
        SelectionCarouselController carousel = input.selection;
        RuntimeRealHandPinchV47 provider = RuntimeRealHandPinchV47.Instance;
        Camera camera = gaze && gaze.sceneCamera ? gaze.sceneCamera : Camera.main;
        Require(gaze && carousel && provider && camera,
            "V76 fluid Selection resolver/carousel/provider/camera is missing");
        gaze.RefreshTargets();
        SelectionExhibitTargetV09[] active = gaze.targets
            .Where(value => value && value.enabled)
            .OrderBy(value => value.exhibitIndex)
            .ToArray();
        Require(active.Length == 5 &&
                !FindObjectsByType<SelectionGazePinchConfirmV73>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Any() &&
                !gaze.enabled,
            "V76 single-pinch authority or playable target split is invalid");

        provider.ClearAutomationV47();
        SelectionExhibitTargetV09 cottage =
            active.Single(value => value.exhibitIndex == 5);
        Require(gaze.TryGetVisibleBoundsV72(cottage, out Bounds cottageBounds),
            "V76 Cottage visible bounds are unavailable");
        gaze.SetAutomationGazeRayV72(new Ray(camera.transform.position,
            (cottageBounds.center - camera.transform.position).normalized));

        // A pinch already held before Selection sees a released hand must
        // never auto-enter, even after gaze stabilization.
        SetProviderHandWorldV47(provider, false, true, true,
            new Pose(camera.transform.position, camera.transform.rotation));
        yield return new WaitForSecondsRealtime(
            input.stableGazeSeconds + input.confirmHoldSeconds + 0.08f);
        Require(input.AcceptedActionCountV76 == 0 &&
                !carousel.IsTransitioning && !carousel.IsCommitting,
            "V76 accepted a held-before-release pinch");

        SetProviderHandWorldV47(provider, false, true, false,
            new Pose(camera.transform.position, camera.transform.rotation));
        yield return new WaitForSecondsRealtime(input.releaseArmSeconds + 0.03f);

        // All five rendered silhouettes must acquire a stable preview without
        // moving the carousel or entering a stage.
        int initialIndex = carousel.currentIndex;
        foreach (SelectionExhibitTargetV09 target in active)
        {
            Require(gaze.TryGetVisibleBoundsV72(target, out Bounds bounds),
                "V76 visible bounds missing for exhibit=" + target.exhibitIndex);
            gaze.SetAutomationGazeRayV72(new Ray(camera.transform.position,
                (bounds.center - camera.transform.position).normalized));
            yield return new WaitForSecondsRealtime(
                input.switchGazeSeconds + 0.04f);
            Require(input.StableTargetV76 == target &&
                    input.feedback &&
                    input.feedback.SelectedIndex == target.exhibitIndex,
                "V76 stable gaze preview failed for exhibit=" +
                target.exhibitIndex);
            Require(!carousel.IsTransitioning && !carousel.IsCommitting &&
                    carousel.currentIndex == initialIndex,
                "V76 gaze alone moved or entered Selection");
        }

        gaze.SetAutomationGazeRayV72(new Ray(camera.transform.position,
            (cottageBounds.center - camera.transform.position).normalized));
        yield return new WaitForSecondsRealtime(input.switchGazeSeconds + 0.04f);
        Require(input.StableTargetV76 == cottage,
            "V76 Cottage did not become the stable target");

        // A sub-grace gaze loss must preserve the selected target. This is the
        // head-jitter tolerance that the V75 state machine lacked.
        gaze.SetAutomationGazeRayV72(
            new Ray(camera.transform.position, -camera.transform.forward));
        yield return new WaitForSecondsRealtime(input.gazeGraceSeconds * 0.60f);
        Require(input.StableTargetV76 == cottage,
            "V76 brief gaze loss cleared the stable target");
        gaze.SetAutomationGazeRayV72(new Ray(camera.transform.position,
            (cottageBounds.center - camera.transform.position).normalized));
        yield return new WaitForSecondsRealtime(0.03f);

        int actionsBefore = input.AcceptedActionCountV76;
        SetProviderHandWorldV47(provider, false, true, true,
            new Pose(camera.transform.position, camera.transform.rotation));
        yield return new WaitForSecondsRealtime(input.confirmHoldSeconds + 0.03f);
        Require(input.AcceptedActionCountV76 == actionsBefore + 1,
            "V76 one fresh pinch did not consume exactly one action");
        yield return WaitForStage(RuntimePuzzleStage.Cottage, 15f);
        provider.ClearAutomationV47();
        gaze.ClearAutomationGazeRayV72();
        Debug.Log("[PPVR V76 Smoke] SELECTION PASSED · held-before-release=blocked · " +
            "stable-visible-bounds=5/5 · gaze-grace=preserved · " +
            "one-fresh-pinch=direct-entry");
        yield return null;
    }

    static IEnumerator ValidateSelectionV72(
        SelectionGazeBoundsPinchDirectInputV72 gazePinch)
    {
        SelectionCarouselController carousel = gazePinch.selection;
        RuntimeRealHandPinchV47 provider = RuntimeRealHandPinchV47.Instance;
        Camera camera = gazePinch.sceneCamera ? gazePinch.sceneCamera : Camera.main;
        Require(carousel && provider && camera,
            "V72 gaze Selection carousel/provider/camera is missing");
        gazePinch.RefreshTargets();
        SelectionExhibitTargetV09[] active = gazePinch.targets
            .Where(value => value && value.enabled)
            .OrderBy(value => value.exhibitIndex)
            .ToArray();
        SelectionExhibitTargetV09 plane =
            FindObjectsByType<SelectionExhibitTargetV09>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Single(value => value.exhibitIndex == 4);
        Require(active.Length == 5 && !plane.enabled,
            "V72 playable/preview exhibit split is not 5+1");
        Require(!FindObjectsByType<SelectionGazePinchDirectInputV71>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Any() &&
                !FindObjectsByType<SelectionHandDirectInputV70>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Any() &&
                !FindObjectsByType<SelectionHandDirectInputV69>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Any(),
            "V72 contains a competing Selection authority");

        // Exercise every visible silhouette. In particular this catches the
        // V71 regression where target.transform.position pointed to a shared
        // carousel origin and only the centre model could be gaze-selected.
        int initialIndex = carousel.currentIndex;
        foreach (SelectionExhibitTargetV09 target in active)
        {
            Require(gazePinch.TryGetVisibleBoundsV72(target, out Bounds bounds),
                "V72 visible model bounds are unavailable: " + target.exhibitIndex);
            Ray gaze = new Ray(camera.transform.position,
                (bounds.center - camera.transform.position).normalized);
            gazePinch.SetAutomationGazeRayV72(gaze);
            SetProviderHandWorldV47(provider, false, true, false,
                new Pose(camera.transform.position, camera.transform.rotation));
            yield return new WaitForSecondsRealtime(0.10f);
            Require(gazePinch.ResolveGazeTargetV72() == target,
                "V72 visible gaze resolved wrong exhibit: requested=" +
                target.exhibitIndex);
            Require(gazePinch.feedback &&
                    gazePinch.feedback.SelectedIndex == target.exhibitIndex,
                "V72 visible gaze did not drive selector for exhibit=" +
                target.exhibitIndex);
            Require(!carousel.IsTransitioning && !carousel.IsCommitting &&
                    carousel.currentIndex == initialIndex,
                "V72 gaze alone moved or entered Selection");
        }

        // Direct entry remains one fresh tracked pinch. Cottage's routed stage
        // is stable across these runtime versions, while the preceding loop
        // independently proves side-model gaze selection.
        SelectionExhibitTargetV09 cottage = active.Single(value =>
            value.exhibitIndex == 5);
        Require(gazePinch.TryGetVisibleBoundsV72(cottage, out Bounds cottageBounds),
            "V72 Cottage visible bounds are unavailable");
        gazePinch.SetAutomationGazeRayV72(new Ray(camera.transform.position,
            (cottageBounds.center - camera.transform.position).normalized));
        SetProviderHandWorldV47(provider, false, true, false,
            new Pose(camera.transform.position, camera.transform.rotation));
        yield return new WaitForSecondsRealtime(0.12f);
        SetProviderHandWorldV47(provider, false, true, true,
            new Pose(camera.transform.position, camera.transform.rotation));
        yield return WaitForStage(RuntimePuzzleStage.Cottage, 15f);
        provider.ClearAutomationV47();
        gazePinch.ClearAutomationGazeRayV72();
        Debug.Log("[PPVR V72 Smoke] SELECTION PASSED · 5/5 visible bounds gaze " +
            "preview · no centre-only origin dependency · fresh pinch direct-entry");
        yield return null;
    }

    static IEnumerator ValidateSelectionV71(
        SelectionGazePinchDirectInputV71 gazePinch)
    {
        SelectionCarouselController carousel = gazePinch.selection;
        RuntimeRealHandPinchV47 provider = RuntimeRealHandPinchV47.Instance;
        Camera camera = gazePinch.sceneCamera ? gazePinch.sceneCamera : Camera.main;
        Require(carousel && provider && camera,
            "V71 gaze Selection carousel/provider/camera is missing");
        gazePinch.RefreshTargets();
        SelectionExhibitTargetV09[] active = gazePinch.targets
            .Where(value => value && value.enabled)
            .OrderBy(value => value.exhibitIndex)
            .ToArray();
        SelectionExhibitTargetV09 plane =
            FindObjectsByType<SelectionExhibitTargetV09>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Single(value => value.exhibitIndex == 4);
        Require(active.Length == 5 && !plane.enabled,
            "V71 playable/preview exhibit split is not 5+1");
        Require(!FindObjectsByType<SelectionHandDirectInputV70>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Any() &&
                !FindObjectsByType<SelectionHandDirectInputV69>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Any(),
            "V71 contains a hand-direction selection authority");

        // This is deliberately a side model when possible: gazing at it must
        // visibly select it without either grabbing it or first centering it.
        SelectionExhibitTargetV09 cottage = active.Single(value =>
            value.exhibitIndex == 5);
        Ray gaze = new Ray(camera.transform.position,
            (cottage.transform.position - camera.transform.position).normalized);
        gazePinch.SetAutomationGazeRayV71(gaze);
        SetProviderHandWorldV47(provider, false, true, false,
            new Pose(camera.transform.position, camera.transform.rotation));
        yield return new WaitForSecondsRealtime(0.12f);
        Require(gazePinch.ResolveGazeTargetV71() == cottage,
            "V71 camera gaze did not resolve the visible Cottage model");
        Require(gazePinch.feedback &&
                gazePinch.feedback.SelectedIndex == cottage.exhibitIndex,
            "V71 camera gaze did not drive the mechanical selected effect");
        Require(!carousel.IsTransitioning && !carousel.IsCommitting,
            "V71 gaze alone entered or moved a level");

        // A fresh pinch from either tracked hand confirms the currently gazed
        // target. It must not use RequestFocus's old side-focus/two-pinch path.
        SetProviderHandWorldV47(provider, false, true, true,
            new Pose(camera.transform.position, camera.transform.rotation));
        yield return WaitForStage(RuntimePuzzleStage.Cottage, 15f);
        provider.ClearAutomationV47();
        gazePinch.ClearAutomationGazeRayV71();
        Debug.Log("[PPVR V71 Smoke] SELECTION PASSED · gaze previews model · " +
            "one fresh pinch enters directly · no hand ray/grab · splash ray hidden");
        yield return null;
    }

    static IEnumerator ValidateSelectionV69(
        SelectionHandDirectInputV69 direct)
    {
        SelectionCarouselController carousel = direct.selection;
        Require(carousel, "V69 hand-direct Selection carousel is missing");
        direct.RefreshTargets();
        SelectionExhibitTargetV09[] active = direct.targets
            .Where(value => value && value.enabled)
            .OrderBy(value => value.exhibitIndex)
            .ToArray();
        SelectionExhibitTargetV09[] all =
            FindObjectsByType<SelectionExhibitTargetV09>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        SelectionExhibitTargetV09 plane = all.Single(value =>
            value.exhibitIndex == 4);
        Require(active.Length == 5 && !plane.enabled,
            "V69 playable/preview exhibit split is not 5+1");
        Require(!FindObjectsByType<SelectionEnterConfirmV61>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Any(),
            "V69 hidden gaze/UI confirmation still exists");
        Require(!FindObjectsByType<SelectionGazePinchInputV46>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Any(value => value.enabled),
            "V69 legacy head-gaze Selection input is still enabled");

        SelectionExhibitTargetV09 side = active.First(value =>
            value.exhibitIndex != carousel.currentIndex);
        Require(side.RequestFocus(),
            "V69 direct hand semantic target did not accept focus");
        float timeout = Time.realtimeSinceStartup + 5f;
        while (carousel.IsTransitioning && Time.realtimeSinceStartup < timeout)
            yield return null;
        Require(!carousel.IsTransitioning &&
                carousel.currentIndex == side.exhibitIndex,
            "V69 direct hand side target did not settle at centre");

        SelectionExhibitTargetV09 cottage = active.Single(value =>
            value.exhibitIndex == 5);
        if (!cottage.IsCurrent)
        {
            Require(cottage.RequestFocus(),
                "V69 direct hand Cottage target did not accept focus");
            timeout = Time.realtimeSinceStartup + 5f;
            while (carousel.IsTransitioning && Time.realtimeSinceStartup < timeout)
                yield return null;
        }
        Require(cottage.IsCurrent && cottage.RequestFocus(),
            "V69 centred playable exhibit did not enter directly");
        Debug.Log(
            "[Pinch Integrity V46] V69 SELECTION PASSED · " +
            "hand-point authority · no head-gaze UI gate · " +
            "5 playable + 1 explicit preview · direct centre entry");
        yield return null;
    }

    static IEnumerator ValidateSelectionV70(
        SelectionHandDirectInputV70 direct)
    {
        SelectionCarouselController carousel = direct.selection;
        Require(carousel, "V70 native-tip Selection carousel is missing");
        direct.RefreshTargets();
        SelectionExhibitTargetV09[] active = direct.targets
            .Where(value => value && value.enabled)
            .OrderBy(value => value.exhibitIndex)
            .ToArray();
        SelectionExhibitTargetV09 plane =
            FindObjectsByType<SelectionExhibitTargetV09>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Single(value => value.exhibitIndex == 4);
        Require(active.Length == 5 && !plane.enabled,
            "V70 playable/preview exhibit split is not 5+1");
        Require(!FindObjectsByType<SelectionHandDirectInputV69>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Any(),
            "V70 still contains the camera-parent hand input");

        RuntimeRealHandPinchV47 provider = RuntimeRealHandPinchV47.Instance;
        Camera camera = direct.sceneCamera ? direct.sceneCamera : Camera.main;
        SelectionExhibitTargetV09 side = active.First(value =>
            value.exhibitIndex != carousel.currentIndex);
        Require(provider && camera,
            "V70 native-tip provider/camera is missing");
        SetProviderHandWorldV47(provider, false, true, false,
            new Pose(side.transform.position, camera.transform.rotation));
        yield return null;
        Require(direct.ResolveTrackedHandForSmokeV70(false) == side,
            "V70 native CameraFloorOffset index tip did not resolve the aimed exhibit");
        provider.ClearAutomationV47();

        Require(side.RequestFocus(),
            "V70 direct hand semantic target did not accept focus");
        float timeout = Time.realtimeSinceStartup + 5f;
        while (carousel.IsTransitioning && Time.realtimeSinceStartup < timeout)
            yield return null;
        Require(!carousel.IsTransitioning &&
                carousel.currentIndex == side.exhibitIndex,
            "V70 direct hand side target did not settle at centre");

        SelectionExhibitTargetV09 cottage = active.Single(value =>
            value.exhibitIndex == 5);
        if (!cottage.IsCurrent)
        {
            Require(cottage.RequestFocus(),
                "V70 direct hand Cottage target did not accept focus");
            timeout = Time.realtimeSinceStartup + 5f;
            while (carousel.IsTransitioning && Time.realtimeSinceStartup < timeout)
                yield return null;
        }
        Require(cottage.IsCurrent && cottage.RequestFocus(),
            "V70 centred playable exhibit did not enter directly");
        Debug.Log(
            "[PPVR V70 Smoke] SELECTION PASSED · native index-tip tracking · " +
            "mechanical preview feedback · direct centre entry");
        yield return null;
    }

    static IEnumerator ValidatePuzzle(
        RuntimePuzzleStage stage,
        int expectedCount)
    {
        RuntimePuzzleLevelController level =
            FindFirstObjectByType<RuntimePuzzleLevelController>();
        RuntimePuzzleHandInteractor[] hands =
            FindObjectsByType<RuntimePuzzleHandInteractor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .OrderBy(value => value.leftHand ? 0 : 1)
                .ToArray();
        Require(
            level && hands.Length == 2,
            stage + " is missing its level or persistent hand pair");
        RuntimePuzzleHandInteractor left =
            hands.Single(value => value.leftHand);
        RuntimePuzzleHandInteractor right =
            hands.Single(value => !value.leftHand);
        RuntimeRealHandPinchV47 provider =
            RuntimeRealHandPinchV47.Instance;
        bool providerDrivenV47 =
            provider &&
            left.swanNativeHandsV24 &&
            right.swanNativeHandsV24 &&
            left.resilientRealHandPinchV47 &&
            right.resilientRealHandPinchV47;
        if (provider)
            Require(
                providerDrivenV47,
                stage +
                " persistent puzzle hands are not both routed through V47");
        if (providerDrivenV47)
            provider.ClearAutomationV47();
        RuntimePuzzlePiece[] pieces =
            level.pieces
                .Where(value => value)
                .OrderBy(value => value.sequence)
                .ToArray();
        Require(
            pieces.Length == expectedCount,
            stage + " expected " + expectedCount +
            " Parts, found " + pieces.Length);
        Require(
            pieces.All(value =>
                !value.IsSnapped &&
                value.IsAtAuthoredPoseV45()),
            stage + " did not open in the clean authored tabletop state");

        for (int index = 0; index < pieces.Length; index++)
        {
            RuntimePuzzlePiece piece = pieces[index];
            Require(
                piece.IsAvailable,
                stage + " sequence " + piece.sequence +
                " is unavailable after its prerequisites");
            Collider[] colliders =
                piece.GetComponentsInChildren<Collider>(true)
                    .Where(value =>
                        value &&
                        value.enabled &&
                        !value.isTrigger)
                    .ToArray();
            Require(
                colliders.Length > 0,
                stage + " sequence " + piece.sequence +
                " has no touch collider");
            Collider contactCollider = colliders
                .OrderByDescending(value =>
                    value.bounds.size.x *
                    value.bounds.size.y *
                    value.bounds.size.z)
                .First();
            Vector3 contactPoint =
                contactCollider.ClosestPoint(
                    contactCollider.bounds.center);
            Pose contactPose =
                new Pose(
                    contactPoint,
                    piece.transform.rotation);

            if (providerDrivenV47)
            {
                SetPuzzleHandsV47(
                    provider,
                    piece,
                    contactPose,
                    false);
                yield return null;
                SetPuzzleHandsV47(
                    provider,
                    piece,
                    contactPose,
                    true);
                yield return null;
                yield return null;
                Require(
                    right.HeldPieceForAutomationV45 == piece &&
                    (!piece.requiresTwoHands ||
                     left.HeldPieceForAutomationV45 == piece),
                    stage + " sequence " + piece.sequence +
                    " failed shared-provider touch-plus-pinch acquisition " +
                    "through normal Update");
            }
            else
            {
                Require(
                    right.TryContactPinchForAutomationV45(contactPose) &&
                    right.HeldPieceForAutomationV45 == piece,
                    stage + " sequence " + piece.sequence +
                    " failed touch-plus-pinch acquisition");
                if (piece.requiresTwoHands)
                {
                    Require(
                        left.TryContactPinchForAutomationV45(contactPose) &&
                        left.HeldPieceForAutomationV45 == piece,
                        stage + " sequence " + piece.sequence +
                        " failed second-hand acquisition");
                }
            }

            // Move explicitly away from this socket. The inherited fixed
            // up/right test vector could cross a nearby socket on newer table
            // layouts; the live assist would correctly pull the piece in and
            // the test would mislabel that valid snap as a failed return.
            Vector3 awayFromSocket =
                piece.AuthoredPositionV45 - piece.TargetPosition;
            if (awayFromSocket.sqrMagnitude < 0.0001f)
                awayFromSocket = Vector3.right;
            awayFromSocket.Normalize();
            Vector3 wrongPosition =
                piece.AuthoredPositionV45 +
                awayFromSocket * Mathf.Max(0.28f, piece.snapDistance * 3.2f) +
                Vector3.up * 0.12f;
            Pose wrongPose =
                new Pose(
                    wrongPosition,
                    piece.AuthoredRotationV45 *
                    Quaternion.Euler(0f, 35f, 0f));
            float wrongMoveDeadline =
                Time.unscaledTime + 0.45f;
            while (Time.unscaledTime <
                   wrongMoveDeadline)
            {
                if (providerDrivenV47)
                    SetPuzzleHandsV47(
                        provider,
                        piece,
                        wrongPose,
                        true);
                else
                {
                    right.MoveContactPinchForAutomationV45(wrongPose);
                    if (piece.requiresTwoHands)
                        left.MoveContactPinchForAutomationV45(wrongPose);
                }
                yield return null;
            }
            Require(
                Vector3.Distance(
                    piece.transform.position,
                    piece.AuthoredPositionV45) > 0.04f,
                stage + " sequence " + piece.sequence +
                " did not follow the pinched hand: moved=" +
                Vector3.Distance(
                    piece.transform.position,
                    piece.AuthoredPositionV45).ToString("F4") +
                " holders=" + piece.ExternalHolderCount +
                " twoHands=" + piece.requiresTwoHands);
            if (providerDrivenV47)
            {
                SetPuzzleHandsV47(
                    provider,
                    piece,
                    wrongPose,
                    false);
                yield return null;
                yield return null;
            }
            else
            {
                right.ReleaseContactPinchForAutomationV45();
                if (piece.requiresTwoHands)
                    left.ReleaseContactPinchForAutomationV45();
            }
            float returnDeadline =
                Time.realtimeSinceStartup + 1.25f;
            while (!piece.IsAtAuthoredPoseV45() &&
                   Time.realtimeSinceStartup < returnDeadline)
                yield return null;
            Require(
                piece.IsAtAuthoredPoseV45(),
                stage + " sequence " + piece.sequence +
                " failed exact wrong-release return · " +
                piece.AuthoredPoseDiagnosticV67());

            contactPoint =
                contactCollider.ClosestPoint(
                    contactCollider.bounds.center);
            contactPose =
                new Pose(
                    contactPoint,
                    piece.transform.rotation);
            if (providerDrivenV47)
            {
                SetPuzzleHandsV47(
                    provider,
                    piece,
                    contactPose,
                    false);
                yield return null;
                SetPuzzleHandsV47(
                    provider,
                    piece,
                    contactPose,
                    true);
                yield return null;
                yield return null;
                Require(
                    right.HeldPieceForAutomationV45 == piece &&
                    (!piece.requiresTwoHands ||
                     left.HeldPieceForAutomationV45 == piece),
                    stage + " sequence " + piece.sequence +
                    " failed shared-provider reacquisition after return");
            }
            else
            {
                Require(
                    right.TryContactPinchForAutomationV45(contactPose) &&
                    right.HeldPieceForAutomationV45 == piece,
                    stage + " sequence " + piece.sequence +
                    " failed reacquisition after return");
                if (piece.requiresTwoHands)
                {
                    Require(
                        left.TryContactPinchForAutomationV45(contactPose) &&
                        left.HeldPieceForAutomationV45 == piece,
                        stage + " sequence " + piece.sequence +
                        " failed second-hand reacquisition");
                }
            }

            Camera camera = Camera.main;
            Pose cameraPose = camera
                ? new Pose(
                    camera.transform.position,
                    camera.transform.rotation)
                : default;
            if (camera &&
                piece.requiresPlayerFacing &&
                piece.targetSocket)
            {
                Vector3 expected =
                    piece.targetSocket.TransformDirection(
                        piece.localApproachAxis.sqrMagnitude > 0.0001f
                            ? piece.localApproachAxis.normalized
                            : Vector3.up);
                camera.transform.position =
                    piece.targetSocket.position +
                    expected * 0.58f;
                camera.transform.rotation =
                    Quaternion.LookRotation(
                        piece.targetSocket.position -
                        camera.transform.position,
                        Vector3.up);
            }
            Pose targetPiecePose =
                new Pose(
                    piece.targetSocket.position,
                    piece.targetSocket.rotation);
            Pose targetPose =
                piece.HolderPoseForDesiredPiecePoseV69(targetPiecePose);
            float distanceBeforeTargetMove =
                Vector3.Distance(
                    piece.transform.position,
                    piece.targetSocket.position);
            float minimumTargetDistance =
                distanceBeforeTargetMove;
            float targetMoveDeadline =
                Time.unscaledTime + 0.85f;
            while (Time.unscaledTime <
                   targetMoveDeadline)
            {
                if (providerDrivenV47)
                    SetPuzzleHandsV47(
                        provider,
                        piece,
                        targetPose,
                        true);
                else
                {
                    right.MoveContactPinchForAutomationV45(targetPose);
                    if (piece.requiresTwoHands)
                        left.MoveContactPinchForAutomationV45(targetPose);
                }
                minimumTargetDistance =
                    Mathf.Min(
                        minimumTargetDistance,
                        Vector3.Distance(
                            piece.transform.position,
                            piece.targetSocket.position));
                yield return null;
            }
            float releaseDistance =
                Vector3.Distance(
                    piece.transform.position,
                    piece.targetSocket.position);
            float releaseAngle =
                Quaternion.Angle(
                    piece.transform.rotation,
                    piece.targetSocket.rotation);
            bool releaseApproach =
                piece.IsApproachValid();
            int releaseHolders =
                piece.ExternalHolderCount;
            if (providerDrivenV47)
            {
                SetPuzzleHandsV47(
                    provider,
                    piece,
                    targetPose,
                    false);
                yield return null;
                yield return null;
            }
            else
            {
                right.ReleaseContactPinchForAutomationV45();
                if (piece.requiresTwoHands)
                    left.ReleaseContactPinchForAutomationV45();
                yield return null;
            }
            if (camera)
                camera.transform.SetPositionAndRotation(
                    cameraPose.position,
                    cameraPose.rotation);
            Require(
                piece.IsSnapped,
                stage + " sequence " + piece.sequence +
                " failed valid socket release: distance=" +
                releaseDistance.ToString("F4") +
                " limit=" + piece.snapDistance.ToString("F4") +
                " angle=" + releaseAngle.ToString("F2") +
                " limit=" + piece.maxAssistAngle.ToString("F2") +
                " approach=" + releaseApproach +
                " holders=" + releaseHolders +
                " distanceBefore=" +
                distanceBeforeTargetMove.ToString("F4") +
                " minimumDistance=" +
                minimumTargetDistance.ToString("F4") +
                " rightHeld=" +
                (right.HeldPieceForAutomationV45 == piece) +
                " position=" + piece.transform.position +
                " target=" + piece.targetSocket.position +
                " authored=" + piece.AuthoredPositionV45);
            Require(
                Vector3.Distance(
                    piece.transform.lossyScale,
                    piece.AssembledWorldScaleV45) <= 0.004f,
                stage + " sequence " + piece.sequence +
                " snapped at the wrong assembled scale");
        }

        level.StopAllCoroutines();
        Require(
            level.SnappedCount == expectedCount &&
            pieces.All(value => value.IsSnapped),
            stage + " did not finish " +
            expectedCount + "/" + expectedCount);
        Debug.Log(
            "[Pinch Integrity V46] PUZZLE PASSED · " +
            stage + " " + expectedCount + "/" + expectedCount +
            " · touch-pinch pickup · moved with hand · " +
            "bad release returned · valid release snapped");
        if (providerDrivenV47)
        {
            provider.ClearAutomationV47();
            Debug.Log(
                "[PPVR V47 Smoke] PUZZLE SHARED-PROVIDER/" +
                "NORMAL-UPDATE PASSED · " +
                stage + " " + expectedCount + "/" + expectedCount);
        }
    }

    static void SetPuzzleHandsV47(
        RuntimeRealHandPinchV47 provider,
        RuntimePuzzlePiece piece,
        Pose worldPose,
        bool pressed)
    {
        SetProviderHandWorldV47(
            provider,
            false,
            true,
            pressed,
            worldPose);
        SetProviderHandWorldV47(
            provider,
            true,
            piece.requiresTwoHands,
            piece.requiresTwoHands && pressed,
            worldPose);
    }

    static void SetProviderHandWorldV47(
        RuntimeRealHandPinchV47 provider,
        bool leftHand,
        bool available,
        bool pressed,
        Pose worldPose)
    {
        provider.SetAutomationHandStateV47(
            leftHand,
            available,
            pressed,
            available,
            WorldToProviderLocalV47(worldPose));
    }

    static void SetProviderPalmWorldV47(
        RuntimeRealHandPinchV47 provider,
        bool leftHand,
        bool available,
        Pose worldPose)
    {
        provider.SetAutomationPalmV47(
            leftHand,
            available,
            WorldToProviderLocalV47(worldPose));
    }

    static Pose WorldToProviderLocalV47(Pose worldPose)
    {
        PersistentPuzzleBootstrap bootstrap =
            PersistentPuzzleBootstrap.Instance;
        Transform tracking =
            bootstrap &&
            bootstrap.xrOrigin &&
            bootstrap.xrOrigin.CameraFloorOffsetObject
                ? bootstrap.xrOrigin.CameraFloorOffsetObject.transform
                : Camera.main && Camera.main.transform.parent
                    ? Camera.main.transform.parent
                    : null;
        return tracking
            ? new Pose(
                tracking.InverseTransformPoint(worldPose.position),
                Quaternion.Inverse(tracking.rotation) *
                worldPose.rotation)
            : worldPose;
    }

    static IEnumerator WaitForStage(
        RuntimePuzzleStage stage,
        float timeoutSeconds)
    {
        float deadline =
            Time.realtimeSinceStartup + timeoutSeconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            PersistentPuzzleBootstrap bootstrap =
                PersistentPuzzleBootstrap.Instance;
            if (bootstrap &&
                bootstrap.CurrentStage == stage &&
                !bootstrap.IsTransitioning &&
                !string.IsNullOrEmpty(bootstrap.LoadedStageScene))
            {
                yield return null;
                yield break;
            }
            yield return null;
        }
        Require(
            false,
            "timed out waiting for stage " + stage);
    }

    static IEnumerator DriveFreshPinchV46(
        RuntimeIntentRayV46 intent,
        Ray ray)
    {
        SelectionGazePinchInputV46 input =
            FindFirstObjectByType<SelectionGazePinchInputV46>();
        bool trackingGap =
            input && input.resilientSignalGapV47;
        RuntimeRealHandPinchV47 provider =
            RuntimeRealHandPinchV47.Instance;
        bool providerDrivenV47 =
            provider &&
            trackingGap &&
            intent.sharedRealHandPinchV47;
        if (providerDrivenV47)
        {
            provider.ClearAutomationV47();
            intent.SetAutomationRayV47(ray);
            provider.SetAutomationHandStateV47(
                false,
                false,
                false,
                false,
                default);
            yield return WaitRealtime(0.17f);
            provider.SetAutomationHandStateV47(
                false,
                true,
                true,
                false,
                default);
            yield return WaitRealtime(0.09f);
            provider.SetAutomationHandStateV47(
                false,
                true,
                false,
                false,
                default);
            yield return WaitRealtime(0.04f);
            intent.ClearAutomationRayV47();
            provider.ClearAutomationV47();
        }
        else
        {
            intent.SetAutomationInputV46(
                ray,
                !trackingGap,
                false);
            yield return WaitRealtime(0.17f);
            intent.SetAutomationInputV46(ray, true, true);
            yield return WaitRealtime(0.09f);
            intent.SetAutomationInputV46(ray, true, false);
            yield return WaitRealtime(0.04f);
            intent.ClearAutomationInputV46();
        }
        if (trackingGap)
            Debug.Log(
                "[PPVR V47 Smoke] SELECTION TRACKING-GAP RELEASE/" +
                "FRESH-PRESS PASSED");
        if (providerDrivenV47)
            Debug.Log(
                "[PPVR V47 Smoke] SELECTION SHARED-PROVIDER/" +
                "NORMAL-UPDATE PASSED");
    }

    static IEnumerator WaitRealtime(float seconds)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < deadline)
            yield return null;
    }

    static void Require(
        bool condition,
        string message)
    {
        if (condition)
            return;
        Debug.LogError(
            "[Pinch Integrity V46] SMOKE FAILED · " +
            message);
        Finish(1);
        throw new InvalidOperationException(message);
    }

    static void Require(
        UnityEngine.Object value,
        string message)
    {
        Require(value != null, message);
    }

    static void Finish(int exitCode)
    {
        if (Application.isBatchMode)
            UnityEditor.EditorApplication.Exit(exitCode);
        else
            UnityEditor.EditorApplication.isPlaying = false;
    }
}
#endif
