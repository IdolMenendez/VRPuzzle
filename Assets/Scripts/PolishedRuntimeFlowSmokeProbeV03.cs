using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-only deterministic smoke for the polished 13/13/16 complete-module
/// additive flow. It never substitutes the Stage 7 completion presentation for
/// the separate Eiffel assembly level.
/// </summary>
public sealed class PolishedRuntimeFlowSmokeProbeV03 : MonoBehaviour
{
    IEnumerator Start()
    {
#if UNITY_EDITOR
        string smoke = Environment.GetEnvironmentVariable("PPVR_FLOW_SMOKE");
        string soak = Environment.GetEnvironmentVariable("PPVR_SOAK_SECONDS");
        string manualProbe = Environment.GetEnvironmentVariable(
            "PPVR_MANUAL_MODE_PROBE");
        string handoffSmoke = Environment.GetEnvironmentVariable(
            "PPVR_HANDOFF_SMOKE");
        string spatialCapture = Environment.GetEnvironmentVariable(
            "PPVR_SPATIAL_CAPTURE");
        string retainedHandoffSmoke = Environment.GetEnvironmentVariable(
            "PPVR_RETAINED_HANDOFF_SMOKE");
        if (manualProbe == "1")
        {
            Environment.SetEnvironmentVariable(
                "PPVR_MANUAL_MODE_PROBE",
                null);
            yield return RunManualModeGuardProbe();
            yield break;
        }
        if (handoffSmoke == "1")
        {
            Environment.SetEnvironmentVariable(
                "PPVR_HANDOFF_SMOKE",
                null);
            yield return RunSceneHandoffFlow();
            yield break;
        }
        if (spatialCapture == "1")
        {
            Environment.SetEnvironmentVariable(
                "PPVR_SPATIAL_CAPTURE",
                null);
            yield return RunSpatialInspectionFlow();
            yield break;
        }
        if (retainedHandoffSmoke == "1")
        {
            Environment.SetEnvironmentVariable(
                "PPVR_RETAINED_HANDOFF_SMOKE",
                null);
            yield return RunRetainedPoseHandoffFlow();
            yield break;
        }
        if (smoke == "1" ||
            PersonallyPuzzledExperienceModeState.IsValidationSmoke)
        {
            Environment.SetEnvironmentVariable("PPVR_FLOW_SMOKE", null);
            Environment.SetEnvironmentVariable(
                PersonallyPuzzledExperienceModeState.EnvironmentVariable,
                PersonallyPuzzledExperienceMode.PlayerManual.ToString());
            yield return RunFullFlow();
            yield break;
        }
        if (float.TryParse(soak, out float seconds) && seconds > 0f)
        {
            yield return RunSoak(seconds);
            yield break;
        }
        if (PersonallyPuzzledExperienceModeState.IsAutoDemo)
            yield return RunAutoDemo();
#else
        yield break;
#endif
    }

#if UNITY_EDITOR
    IEnumerator RunManualModeGuardProbe()
    {
        yield return WaitForBootstrap(12f);
        PersistentPuzzleBootstrap bootstrap = PersistentPuzzleBootstrap.Instance;
        Require(bootstrap, "bootstrap unavailable for player-manual guard");
        SaveFileSnapshot saveSnapshot =
            SaveFileSnapshot.Capture(bootstrap.SavePath);
        bootstrap.SetPersistenceSuspendedForAutomation(true);
        try
        {
            Require(
                PersonallyPuzzledExperienceModeState.IsPlayerManual,
                "player-manual guard launched in a non-manual mode");
            bootstrap.Save.highestUnlockedLevel = 2;
            yield return WaitForStage(RuntimePuzzleStage.Splash, 12f);
            yield return new WaitForSecondsRealtime(2.25f);
            Require(
                bootstrap.CurrentStage == RuntimePuzzleStage.Splash &&
                !bootstrap.IsTransitioning,
                "player-manual mode advanced without player input");
            RuntimeDesktopPuzzleInteractorV01 desktop =
                FindFirstObjectByType<RuntimeDesktopPuzzleInteractorV01>();
            RuntimeDesktopHeadNavigatorV01 desktopNavigator =
                FindFirstObjectByType<RuntimeDesktopHeadNavigatorV01>();
            Require(
                desktop && desktop.sceneCamera == bootstrap.xrCamera,
                "player-manual Unity Game-view drag interaction is unavailable");
            Require(
                desktopNavigator &&
                desktopNavigator.DesktopPreviewActive &&
                desktopNavigator.sceneCamera == bootstrap.xrCamera,
                "player-manual neutral head pose and spatial inspection are unavailable");
            Require(
                !FindFirstObjectByType<RuntimePuzzleLevelController>(),
                "a puzzle level loaded before the player touched the splash logo");

            PolishedRuntimeStagePortalV02 splashPortal =
                FindStagePortal(RuntimePuzzleStage.Splash);
            yield return WaitForSecondsPastPortalMinimum(splashPortal);
            Require(splashPortal.TriggerConfirmedActionForSmoke(),
                "PlayerManual Splash confirmation was rejected");
            yield return WaitForStage(RuntimePuzzleStage.Selection, 12f);
            RequireComfortClear(bootstrap, "manual gate Splash to Selection");

            PolishedRuntimeStagePortalV02 selectionPortal =
                FindStagePortal(RuntimePuzzleStage.Selection);
            int cottageExhibit = Array.FindIndex(
                selectionPortal.selectionLevelByExhibit,
                value => value == 0);
            Require(cottageExhibit >= 0 &&
                    selectionPortal.selection.SelectIndexImmediately(cottageExhibit),
                "PlayerManual Cottage exhibit could not be focused");
            yield return WaitForSecondsPastPortalMinimum(selectionPortal);
            Require(selectionPortal.TriggerConfirmedActionForSmoke(),
                "PlayerManual first Selection confirmation was rejected");
            SelectionPreviewGateV07 gate =
                selectionPortal.selectionPreviewGate;
            Require(gate && gate.IsPreviewPrimed &&
                    !gate.IsReadyToCommit &&
                    !selectionPortal.IsStageAdvanceQueuedForSmoke &&
                    bootstrap.CurrentStage == RuntimePuzzleStage.Selection,
                "Selection did not stop at the explicit physical preview gate");
            yield return CaptureHandoffFrame(
                bootstrap,
                "ManualGate_01_FocusedPreview.png");
            yield return new WaitForSecondsRealtime(
                gate.minimumInspectionSeconds + 0.08f);
            Require(gate.IsReadyToCommit,
                "Selection preview dwell never became ready for explicit commit");
            Require(selectionPortal.TriggerConfirmedActionForSmoke() &&
                    selectionPortal.IsStageAdvanceQueuedForSmoke,
                "PlayerManual second Selection confirmation did not commit");
            yield return new WaitForSecondsRealtime(0.22f);
            Require(bootstrap.CurrentStage == RuntimePuzzleStage.Selection,
                "Selection skipped the visible commit beat");
            yield return CaptureHandoffFrame(
                bootstrap,
                "ManualGate_02_ExplicitCommit.png");

            Debug.Log(
                "[Personally Puzzled VR] PLAYER MANUAL GUARD PASSED · " +
                "no-input remains at Splash · desktop drag fallback present · " +
                "first Selection confirmation only previews · timed second confirmation commits · " +
                "release default contains no automatic stage or Part advancement");
        }
        finally
        {
            bootstrap.SetPersistenceSuspendedForAutomation(false);
            saveSnapshot.Restore();
        }
        FinishEditorAutomation(0);
    }

    IEnumerator RunSceneHandoffFlow()
    {
        yield return WaitForBootstrap(12f);
        PersistentPuzzleBootstrap bootstrap = PersistentPuzzleBootstrap.Instance;
        Require(bootstrap, "bootstrap unavailable for handoff smoke");
        SaveFileSnapshot saveSnapshot =
            SaveFileSnapshot.Capture(bootstrap.SavePath);
        bootstrap.SetPersistenceSuspendedForAutomation(true);
        try
        {
            bootstrap.Save.highestUnlockedLevel = 2;
            yield return WaitForStage(RuntimePuzzleStage.Splash, 12f);
            RequireComfortClear(bootstrap, "initial Splash reveal");
            yield return CaptureHandoffFrame(
                bootstrap,
                "01_Splash_Ready.png");
            PolishedRuntimeStagePortalV02 splashPortal =
                FindStagePortal(RuntimePuzzleStage.Splash);
            yield return WaitForSecondsPastPortalMinimum(splashPortal);
            Require(
                splashPortal.TriggerConfirmedActionForSmoke(),
                "Splash player confirmation was not accepted");
            Require(
                splashPortal.splash &&
                splashPortal.splash.IsActivated &&
                splashPortal.IsStageAdvanceQueuedForSmoke,
                "Splash wake beat was not queued before Selection");
            yield return new WaitForSecondsRealtime(0.24f);
            Require(
                bootstrap.CurrentStage == RuntimePuzzleStage.Splash &&
                !bootstrap.IsTransitioning &&
                splashPortal.splash.ActivationProgress > 0f,
                "Splash skipped its authored wake beat and changed scene immediately");
            yield return CaptureHandoffFrame(
                bootstrap,
                "02_Splash_WakeBeat.png");
            yield return WaitForHandoffCover(bootstrap, 4f);
            yield return CaptureHandoffFrame(
                bootstrap,
                "02B_SplashToSelection_GoldHandoff.png");
            yield return WaitForStage(RuntimePuzzleStage.Selection, 12f);
            RequireComfortClear(bootstrap, "Splash to Selection");
            yield return new WaitForSecondsRealtime(0.18f);
            yield return CaptureHandoffFrame(
                bootstrap,
                "03_Selection_Ready.png");

            PolishedRuntimeStagePortalV02 selectionPortal =
                FindStagePortal(RuntimePuzzleStage.Selection);
            int lockedEiffelExhibit = Array.FindIndex(
                selectionPortal.selectionLevelByExhibit,
                value => value == 2);
            Require(lockedEiffelExhibit >= 0, "Eiffel semantic exhibit missing");
            bootstrap.Save.highestUnlockedLevel = 0;
            Require(
                selectionPortal.selection.SelectIndexImmediately(lockedEiffelExhibit),
                "locked Eiffel exhibit could not move into the central selection slot");
            yield return WaitForSecondsPastPortalMinimum(selectionPortal);
            Require(
                !selectionPortal.TriggerConfirmedActionForSmoke() &&
                !selectionPortal.IsStageAdvanceQueuedForSmoke,
                "locked Selection exhibit incorrectly advanced PlayerManual");
            bootstrap.Save.highestUnlockedLevel = 2;
            int cottageExhibit = Array.FindIndex(
                selectionPortal.selectionLevelByExhibit,
                value => value == 0);
            Require(cottageExhibit >= 0, "cottage semantic exhibit missing");
            Require(
                selectionPortal.selection.SelectIndexImmediately(cottageExhibit),
                "cottage semantic exhibit could not move into the central selection slot");
            Require(
                Vector3.Distance(
                    selectionPortal.selection.exhibits[cottageExhibit].position,
                    selectionPortal.selection.centralAnchor.position) < 0.001f,
                "cottage semantic exhibit is not physically centered before commit");
            yield return WaitForSecondsPastPortalMinimum(selectionPortal);
            Require(
                selectionPortal.TriggerConfirmedActionForSmoke(),
                "Selection player confirmation was not accepted");
            Require(
                selectionPortal.selection.IsCommitting &&
                selectionPortal.IsStageAdvanceQueuedForSmoke,
                "Selection commit animation was not queued");
            yield return new WaitForSecondsRealtime(0.48f);
            Require(
                bootstrap.CurrentStage == RuntimePuzzleStage.Selection &&
                !bootstrap.IsTransitioning,
                "Selection skipped its exhibit commit beat");
            yield return CaptureHandoffFrame(
                bootstrap,
                "04_Selection_CottageCommit.png");
            yield return WaitForHandoffCover(bootstrap, 4f);
            yield return CaptureHandoffFrame(
                bootstrap,
                "04B_SelectionToCottage_GoldHandoff.png");
            yield return WaitForStage(RuntimePuzzleStage.Cottage, 12f);
            RequireComfortClear(bootstrap, "Selection to Cottage");
            yield return new WaitForSecondsRealtime(0.18f);
            yield return CaptureHandoffFrame(
                bootstrap,
                "05_Cottage_Ready.png");

            RuntimePuzzleLevelController level =
                FindFirstObjectByType<RuntimePuzzleLevelController>();
            Require(
                level && level.levelIndex == 0 && level.TotalCount == 13,
                "Cottage level contract unavailable after Selection commit");
            for (int index = 0; index < level.pieces.Length; index++)
            {
                RuntimePuzzlePiece piece = level.pieces[index];
                if (piece && index < 7)
                    piece.ForceSnapForSmoke();
            }
            Require(
                level.SnappedCount == 7 && !level.Completed,
                "Cottage nonlinear mid-assembly checkpoint did not remain interactive");
            yield return CaptureHandoffFrame(
                bootstrap,
                "05B_Cottage_MidAssembly_7of13.png");
            for (int index = 7; index < level.pieces.Length; index++)
            {
                RuntimePuzzlePiece piece = level.pieces[index];
                if (piece)
                    piece.ForceSnapForSmoke();
            }
            Require(level.Completed, "Cottage completion ceremony did not start");
            yield return CaptureHandoffFrame(
                bootstrap,
                "06_Cottage_FinalPartCeremony.png");
            yield return WaitForHandoffCover(bootstrap, 5f);
            yield return CaptureHandoffFrame(
                bootstrap,
                "06B_CottageToCompletion_GoldHandoff.png");
            yield return WaitForStage(RuntimePuzzleStage.Completion, 12f);
            RequireComfortClear(bootstrap, "Cottage to Completion");
            yield return new WaitForSecondsRealtime(0.18f);
            RuntimeLevelHeroContinuityV04 completionContinuity =
                FindFirstObjectByType<RuntimeLevelHeroContinuityV04>();
            Require(
                !bootstrap.completionScene.Contains(
                    "V04_HandoffContinuity",
                    StringComparison.Ordinal) ||
                (completionContinuity &&
                 completionContinuity.AppliedLevelIndex == 0 &&
                 completionContinuity.AppliedModelName == "CLOCKMAKER COTTAGE"),
                "Completion did not preserve the selected Cottage model identity");
            yield return CaptureHandoffFrame(
                bootstrap,
                "07_Completion_Ready.png");

            PolishedRuntimeStagePortalV02 completionPortal =
                FindStagePortal(RuntimePuzzleStage.Completion);
            yield return WaitForSecondsPastPortalMinimum(completionPortal);
            Require(
                completionPortal.TriggerConfirmedActionForSmoke(),
                "Completion player confirmation was not accepted");
            Require(
                completionPortal.IsStageAdvanceQueuedForSmoke,
                "Completion-to-Collection handoff was not queued");
            yield return new WaitForSecondsRealtime(0.16f);
            Require(
                bootstrap.CurrentStage == RuntimePuzzleStage.Completion &&
                !bootstrap.IsTransitioning,
                "Completion skipped the player-confirmation settle beat");
            yield return WaitForHandoffCover(bootstrap, 4f);
            yield return CaptureHandoffFrame(
                bootstrap,
                "07B_CompletionToCollection_GoldHandoff.png");
            yield return WaitForStage(RuntimePuzzleStage.Collection, 12f);
            RequireComfortClear(bootstrap, "Completion to Collection");
            yield return new WaitForSecondsRealtime(0.18f);
            RuntimeLevelHeroContinuityV04 collectionContinuity =
                FindFirstObjectByType<RuntimeLevelHeroContinuityV04>();
            Require(
                !bootstrap.collectionScene.Contains(
                    "V04_HandoffContinuity",
                    StringComparison.Ordinal) ||
                (collectionContinuity &&
                 collectionContinuity.AppliedLevelIndex == 0 &&
                 collectionContinuity.AppliedModelName == "CLOCKMAKER COTTAGE"),
                "Collection did not preserve the selected Cottage model identity");
            yield return CaptureHandoffFrame(
                bootstrap,
                "08_Collection_Ready.png");

            CollectionArchiveRitualV06Controller archive =
                FindFirstObjectByType<CollectionArchiveRitualV06Controller>();
            Require(archive, "Collection archive ritual unavailable");
            archive.BeginRitualForSmoke();
            float ritualStarted = Time.realtimeSinceStartup;
            bool archiveFlightCaptured = false;
            while (!archive.Completed &&
                   Time.realtimeSinceStartup - ritualStarted < 5f)
            {
                if (!archiveFlightCaptured && archive.Progress >= 0.76f)
                {
                    archiveFlightCaptured = true;
                    yield return CaptureHandoffFrame(
                        bootstrap,
                        "09_Collection_ArchiveFlight.png");
                }
                yield return null;
            }
            Require(
                archiveFlightCaptured &&
                archive.Completed && archive.LitStarCount == 3,
                "Collection ritual did not complete with three stars");
            yield return WaitForHandoffCover(bootstrap, 5f);
            yield return CaptureHandoffFrame(
                bootstrap,
                "09B_CollectionToSelection_GoldHandoff.png");
            yield return WaitForStage(RuntimePuzzleStage.Selection, 12f);
            RequireComfortClear(bootstrap, "Collection to Selection");
            ValidateSceneResidency(bootstrap);
            yield return new WaitForSecondsRealtime(0.18f);
            yield return CaptureHandoffFrame(
                bootstrap,
                "10_Selection_ReturnedFromArchive.png");

            Debug.Log(
                "[Personally Puzzled VR] SCENE HANDOFF SMOKE PASSED · " +
                "Splash wake beat→persistent cover/reveal→visible Selection · " +
                "semantic exhibit commit→Cottage · final-Part ceremony→Completion · " +
                "player-confirmed Collection→three-star archive→Selection · " +
                "transition overlay cleared after every stage");
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "PPVR_HANDOFF_CAPTURE_ROOT",
                null);
            bootstrap.SetPersistenceSuspendedForAutomation(false);
            saveSnapshot.Restore();
        }
        FinishEditorAutomation(0);
    }

    IEnumerator RunRetainedPoseHandoffFlow()
    {
        yield return WaitForBootstrap(12f);
        PersistentPuzzleBootstrap bootstrap = PersistentPuzzleBootstrap.Instance;
        Require(bootstrap, "bootstrap unavailable for retained-pose handoff smoke");
        SaveFileSnapshot saveSnapshot =
            SaveFileSnapshot.Capture(bootstrap.SavePath);
        bootstrap.SetPersistenceSuspendedForAutomation(true);
        try
        {
            bootstrap.Save.highestUnlockedLevel = 2;
            yield return WaitForStage(RuntimePuzzleStage.Splash, 12f);
            RequireComfortClear(bootstrap, "retained-pose initial Splash reveal");
            RuntimeDesktopHeadNavigatorV01 navigator =
                FindFirstObjectByType<RuntimeDesktopHeadNavigatorV01>();
            Require(navigator && navigator.DesktopPreviewActive,
                "retained-pose handoff requires desktop 6DoF inspection");

            Vector3 splashPose = new Vector3(0.22f, 0f, 0.18f);
            navigator.SetInspectionPoseForAutomation(splashPose, 0f, 0f);
            yield return CaptureSpatialFrame(
                bootstrap,
                "01_Splash_RetainedRightForward.png",
                splashPose,
                0f,
                0f);
            PolishedRuntimeStagePortalV02 splashPortal =
                FindStagePortal(RuntimePuzzleStage.Splash);
            yield return WaitForSecondsPastPortalMinimum(splashPortal);
            Require(splashPortal.TriggerConfirmedActionForSmoke(),
                "retained-pose Splash confirmation rejected");
            yield return WaitForStage(RuntimePuzzleStage.Selection, 12f);
            RequireComfortClear(bootstrap, "retained Splash to Selection");
            ValidateCoveredRegistration(
                RuntimePuzzleStage.Selection,
                0.15f,
                "Splash→Selection retained pose");
            yield return CaptureSpatialFrame(
                bootstrap,
                "02_Selection_AfterRetainedRightForward.png",
                splashPose,
                0f,
                0f);

            PolishedRuntimeStagePortalV02 selectionPortal =
                FindStagePortal(RuntimePuzzleStage.Selection);
            int cottageExhibit = Array.FindIndex(
                selectionPortal.selectionLevelByExhibit,
                value => value == 0);
            Require(cottageExhibit >= 0 &&
                    selectionPortal.selection.SelectIndexImmediately(cottageExhibit),
                "retained-pose Cottage exhibit unavailable");
            Vector3 selectionPose = new Vector3(-0.24f, 0f, 0.22f);
            navigator.SetInspectionPoseForAutomation(selectionPose, 0f, 0f);
            yield return WaitForSecondsPastPortalMinimum(selectionPortal);
            Require(selectionPortal.TriggerConfirmedActionForSmoke(),
                "retained-pose Selection confirmation rejected");
            yield return WaitForStage(RuntimePuzzleStage.Cottage, 12f);
            RequireComfortClear(bootstrap, "retained Selection to Cottage");
            ValidateCoveredRegistration(
                RuntimePuzzleStage.Cottage,
                0.20f,
                "Selection→Cottage retained pose");
            yield return CaptureSpatialFrame(
                bootstrap,
                "03_Cottage_AfterRetainedLeftForward.png",
                selectionPose,
                0f,
                0f);

            RuntimePuzzleLevelController level =
                FindFirstObjectByType<RuntimePuzzleLevelController>();
            Require(level && level.TotalCount == 13,
                "retained-pose Cottage level unavailable");
            Vector3 closeAssemblyPose = new Vector3(-0.12f, 0f, 0.48f);
            navigator.SetInspectionPoseForAutomation(
                closeAssemblyPose,
                0f,
                0f);
            foreach (RuntimePuzzlePiece piece in level.pieces)
                piece?.ForceSnapForSmoke();
            Require(level.Completed,
                "retained-pose Cottage did not complete");
            yield return WaitForStage(RuntimePuzzleStage.Completion, 12f);
            RequireComfortClear(bootstrap, "retained Cottage to Completion");
            ValidateCoveredRegistration(
                RuntimePuzzleStage.Completion,
                0.40f,
                "close assembly→Completion retained pose");
            yield return CaptureSpatialFrame(
                bootstrap,
                "04_Completion_AfterCloseAssemblyPose.png",
                closeAssemblyPose,
                0f,
                0f);

            PolishedRuntimeStagePortalV02 completionPortal =
                FindStagePortal(RuntimePuzzleStage.Completion);
            yield return WaitForSecondsPastPortalMinimum(completionPortal);
            Require(completionPortal.TriggerConfirmedActionForSmoke(),
                "retained-pose Completion confirmation rejected");
            yield return WaitForStage(RuntimePuzzleStage.Collection, 12f);
            RequireComfortClear(bootstrap, "retained Completion to Collection");
            ValidateCoveredRegistration(
                RuntimePuzzleStage.Collection,
                0.40f,
                "Completion→Collection retained pose");
            yield return CaptureSpatialFrame(
                bootstrap,
                "05_Collection_AfterRetainedClosePose.png",
                closeAssemblyPose,
                0f,
                0f);

            CollectionArchiveRitualV06Controller archive =
                FindFirstObjectByType<CollectionArchiveRitualV06Controller>();
            Require(archive, "retained-pose archive ritual unavailable");
            archive.BeginRitualForSmoke();
            archive.AdvanceRitualForSmoke(1f);
            yield return WaitForStage(RuntimePuzzleStage.Selection, 12f);
            RequireComfortClear(bootstrap, "retained Collection to Selection");
            ValidateCoveredRegistration(
                RuntimePuzzleStage.Selection,
                0.40f,
                "Collection→Selection retained pose");
            yield return CaptureSpatialFrame(
                bootstrap,
                "06_Selection_ReturnedWithRetainedClosePose.png",
                closeAssemblyPose,
                0f,
                0f);

            Debug.Log(
                "[Personally Puzzled VR] RETAINED 6DOF HANDOFF PASSED · " +
                "camera/floor unchanged · Splash→Selection→Cottage→Completion→" +
                "Collection→Selection · close assembly pose remained physical · " +
                "every incoming focus stayed in front and visible");
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "PPVR_SPATIAL_CAPTURE_ROOT",
                null);
            bootstrap.SetPersistenceSuspendedForAutomation(false);
            saveSnapshot.Restore();
        }
        FinishEditorAutomation(0);
    }

    static void ValidateCoveredRegistration(
        RuntimePuzzleStage stage,
        float minimumHorizontalTranslation,
        string handoff)
    {
        RuntimeCoveredStageRegistrationV06 registration =
            FindFirstObjectByType<RuntimeCoveredStageRegistrationV06>();
        Require(registration &&
                registration.stage == stage &&
                registration.Applied,
            handoff + " did not apply covered stage registration");
        Vector3 translation = registration.AppliedTranslation;
        translation.y = 0f;
        Require(translation.magnitude >= minimumHorizontalTranslation &&
                translation.magnitude <= 0.585f,
            handoff + " horizontal registration outside bounded contract: " +
            translation);
        Vector3 viewport = registration.FocusViewportAfterRegistration;
        Require(viewport.z > 0.20f &&
                viewport.x >= 0.05f && viewport.x <= 0.95f &&
                viewport.y >= 0.05f && viewport.y <= 0.95f,
            handoff + " focus is not visible after retained registration: " +
            viewport);
        PersistentPuzzleBootstrap bootstrap =
            PersistentPuzzleBootstrap.Instance;
        Require(bootstrap && bootstrap.xrCamera && registration.focusAnchor,
            handoff + " live focus audit is unavailable");
        Vector3 liveViewport = bootstrap.xrCamera.WorldToViewportPoint(
            registration.focusAnchor.position);
        Require(liveViewport.z > 0.20f &&
                liveViewport.x >= 0.05f && liveViewport.x <= 0.95f &&
                liveViewport.y >= 0.05f && liveViewport.y <= 0.95f,
            handoff +
            " focus drifted after physics/runtime settle: snapshot=" +
            viewport + " live=" + liveViewport +
            " rigidbodies=" + registration.RegisteredRigidbodyCount);
    }

    IEnumerator RunSpatialInspectionFlow()
    {
        yield return WaitForBootstrap(12f);
        PersistentPuzzleBootstrap bootstrap = PersistentPuzzleBootstrap.Instance;
        Require(bootstrap, "bootstrap unavailable for spatial inspection");
        SaveFileSnapshot saveSnapshot =
            SaveFileSnapshot.Capture(bootstrap.SavePath);
        bootstrap.SetPersistenceSuspendedForAutomation(true);
        try
        {
            bootstrap.Save.highestUnlockedLevel = 2;
            yield return WaitForStage(RuntimePuzzleStage.Splash, 12f);
            RequireComfortClear(bootstrap, "spatial Splash reveal");
            yield return CaptureSpatialInspectionSet(
                bootstrap,
                "01_Splash");

            bootstrap.EnterSelection();
            yield return WaitForStage(RuntimePuzzleStage.Selection, 12f);
            RequireComfortClear(bootstrap, "spatial Selection reveal");
            yield return CaptureSpatialInspectionSet(
                bootstrap,
                "02_Selection");

            bool semanticEiffelV10 = bootstrap.eiffelScene.Contains(
                "_V10_VRSpatialSemanticAssembly",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V11_AllLevelReview",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V12_ReachEnvelope",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V13_PhysicalLampPull",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V14_ForgivingSnapMusic",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V15_OpenLicensedMusic",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V16_VisualPolish",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V17_WarmWorkshopEnclosure",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V18_EiffelCompletionContinuity",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V19_AssemblyTargetPreview",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
            "_V20_ReliableExhibitSelection",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V21_SpatialClosureDefaultFirstLevel",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
            "_V22_GuidedCenteredAssembly",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V23_SpatialIntegrityCelebration",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V24_SwanNativeXRHands",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V25_GroundedGuidedHome",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V26_SwanFloorScaleCalibration",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
            "_V27_SwanDeviceGroundCalibration",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V28_OfficialOpenXrFloorSpace",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V29_SwanOpenXrRuntimeReady",
                StringComparison.Ordinal);
            // V10 replaces sixteen fragment-like cuts with ten recognizable
            // architectural modules. Seven placed modules produce the same
            // readable lower-tower/mid-build evidence point.
            int[] midAssemblyCounts = { 7, 7, semanticEiffelV10 ? 7 : 12 };
            string[] labels =
            {
                "03_Cottage_MidAssembly_7of13",
                "05_Ship_MidAssembly_7of13",
                semanticEiffelV10
                    ? "06_Eiffel_MidAssembly_7of10"
                    : "06_Eiffel_MidAssembly_12of16",
            };
            int[] expectedCounts = { 13, 13, semanticEiffelV10 ? 10 : 16 };
            for (int levelIndex = 0; levelIndex < expectedCounts.Length; levelIndex++)
            {
                bootstrap.BeginLevel(levelIndex);
                RuntimePuzzleStage stage = LevelStage(levelIndex);
                yield return WaitForStage(stage, 12f);
                RequireComfortClear(bootstrap, "spatial " + stage + " reveal");
                RuntimePuzzleLevelController level =
                    FindFirstObjectByType<RuntimePuzzleLevelController>();
                Require(
                    level && level.TotalCount == expectedCounts[levelIndex],
                    "spatial puzzle contract unavailable for " + stage);
                for (int partIndex = 0;
                     partIndex < midAssemblyCounts[levelIndex];
                     partIndex++)
                    level.pieces[partIndex]?.ForceSnapForSmoke();
                Require(
                    level.SnappedCount == midAssemblyCounts[levelIndex] &&
                    !level.Completed,
                    "spatial mid-assembly checkpoint advanced too far for " + stage);
                yield return CaptureSpatialInspectionSet(
                    bootstrap,
                    labels[levelIndex]);

                if (levelIndex == 0)
                {
                    Stage6LampGhostHintV06Controller hint =
                        FindFirstObjectByType<Stage6LampGhostHintV06Controller>();
                    Require(hint, "physical lamp hint unavailable for spatial capture");
                    hint.TriggerHintForSmoke();
                    Require(
                        hint.GhostVisible && hint.HintRemaining > 1.9f,
                        "physical lamp did not reveal the exact complete-module ghost");
                    yield return CaptureSpatialFrame(
                        bootstrap,
                        "04_Cottage_LampHint_ExactGhost_Front.png",
                        Vector3.zero,
                        0f,
                        0f);
                    hint.AdvanceHintTimerForSmoke(2.1f);
                }

                if (levelIndex < expectedCounts.Length - 1)
                    continue;
                for (int partIndex = midAssemblyCounts[levelIndex];
                     partIndex < level.pieces.Length;
                     partIndex++)
                    level.pieces[partIndex]?.ForceSnapForSmoke();
                Require(level.Completed, "Eiffel spatial flow did not complete");
            }

            yield return WaitForStage(RuntimePuzzleStage.Completion, 12f);
            RequireComfortClear(bootstrap, "spatial Completion reveal");
            yield return CaptureSpatialInspectionSet(
                bootstrap,
                "07_Completion");

            bootstrap.EnterCollection();
            yield return WaitForStage(RuntimePuzzleStage.Collection, 12f);
            RequireComfortClear(bootstrap, "spatial Collection reveal");
            yield return CaptureSpatialInspectionSet(
                bootstrap,
                "08_Collection");
            CollectionArchiveRitualV06Controller archive =
                FindFirstObjectByType<CollectionArchiveRitualV06Controller>();
            Require(archive, "archive ritual unavailable for spatial capture");
            archive.BeginRitualForSmoke();
            float ritualStarted = Time.realtimeSinceStartup;
            bool flightCaptured = false;
            while (!archive.Completed &&
                   Time.realtimeSinceStartup - ritualStarted < 5f)
            {
                if (!flightCaptured && archive.Progress >= 0.76f)
                {
                    flightCaptured = true;
                    yield return CaptureSpatialFrame(
                        bootstrap,
                        "08B_Collection_ArchiveFlight_Front.png",
                        Vector3.zero,
                        0f,
                        0f);
                }
                yield return null;
            }
            Require(
                flightCaptured && archive.Completed && archive.LitStarCount == 3,
                "archive spatial flight did not finish with three stars");
            yield return WaitForStage(RuntimePuzzleStage.Selection, 12f);
            RequireComfortClear(bootstrap, "spatial archive return");
            yield return CaptureSpatialFrame(
                bootstrap,
                "09_Selection_ReturnedFromArchive_Front.png",
                Vector3.zero,
                0f,
                0f);

            Debug.Log(
                "[Personally Puzzled VR] SPATIAL INSPECTION CAPTURE PASSED · " +
                "all eight runtime stages + Cottage exact lamp hint + archive flight · " +
                "front/yaw/lateral/fore-aft/vertical evidence at 1920x1080");
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "PPVR_SPATIAL_CAPTURE_ROOT",
                null);
            bootstrap.SetPersistenceSuspendedForAutomation(false);
            saveSnapshot.Restore();
        }
        FinishEditorAutomation(0);
    }

    IEnumerator RunFullFlow()
    {
        yield return WaitForBootstrap(12f);
        PersistentPuzzleBootstrap bootstrap = PersistentPuzzleBootstrap.Instance;
        Require(bootstrap, "bootstrap unavailable");
        bool captureFinalRuntime = !string.IsNullOrEmpty(
            Environment.GetEnvironmentVariable("PPVR_FINAL_FLOW_CAPTURE"));
        SaveFileSnapshot saveSnapshot =
            SaveFileSnapshot.Capture(bootstrap.SavePath);
        bool semanticEiffelV10 = bootstrap.eiffelScene.Contains(
            "_V10_VRSpatialSemanticAssembly",
            StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
            "_V11_AllLevelReview",
            StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
            "_V12_ReachEnvelope",
            StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
            "_V13_PhysicalLampPull",
            StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
            "_V14_ForgivingSnapMusic",
            StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
            "_V15_OpenLicensedMusic",
            StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
            "_V16_VisualPolish",
            StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
            "_V17_WarmWorkshopEnclosure",
            StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
            "_V18_EiffelCompletionContinuity",
            StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
            "_V19_AssemblyTargetPreview",
            StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
            "_V20_ReliableExhibitSelection",
            StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
            "_V21_SpatialClosureDefaultFirstLevel",
            StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
            "_V22_GuidedCenteredAssembly",
            StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
            "_V23_SpatialIntegrityCelebration",
            StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
            "_V24_SwanNativeXRHands",
            StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
            "_V25_GroundedGuidedHome",
            StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
            "_V26_SwanFloorScaleCalibration",
            StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
            "_V27_SwanDeviceGroundCalibration",
            StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
            "_V28_OfficialOpenXrFloorSpace",
            StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
            "_V29_SwanOpenXrRuntimeReady",
            StringComparison.Ordinal);
        bool forgivingSnapMusicV14 =
            bootstrap.eiffelScene.Contains(
                "_V14_ForgivingSnapMusic",
                StringComparison.Ordinal) ||
            bootstrap.eiffelScene.Contains(
                "_V15_OpenLicensedMusic",
                StringComparison.Ordinal) ||
            bootstrap.eiffelScene.Contains(
                "_V16_VisualPolish",
                StringComparison.Ordinal) ||
            bootstrap.eiffelScene.Contains(
                "_V17_WarmWorkshopEnclosure",
                StringComparison.Ordinal) ||
            bootstrap.eiffelScene.Contains(
                "_V18_EiffelCompletionContinuity",
                StringComparison.Ordinal) ||
            bootstrap.eiffelScene.Contains(
                "_V19_AssemblyTargetPreview",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V20_ReliableExhibitSelection",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V21_SpatialClosureDefaultFirstLevel",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V22_GuidedCenteredAssembly",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V23_SpatialIntegrityCelebration",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V24_SwanNativeXRHands",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V25_GroundedGuidedHome",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V26_SwanFloorScaleCalibration",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V27_SwanDeviceGroundCalibration",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V28_OfficialOpenXrFloorSpace",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V29_SwanOpenXrRuntimeReady",
                StringComparison.Ordinal);
        AudioClip previousPuzzleMusic = null;
        bool passed = false;
        try
        {
            bootstrap.Save.highestUnlockedLevel = 4;
            yield return WaitForStage(
                RuntimePuzzleStage.Splash,
                12f);
            yield return WaitForAudioStage(
                bootstrap,
                RuntimePuzzleStage.Splash);
            ValidateSceneResidency(bootstrap);
            if (captureFinalRuntime)
                yield return CaptureHandoffFrame(
                    bootstrap,
                    "01_Splash_Ready.png");

            bootstrap.EnterSelection();
            yield return WaitForStage(
                RuntimePuzzleStage.Selection,
                12f);
            yield return WaitForAudioStage(
                bootstrap,
                RuntimePuzzleStage.Selection);
            ValidateSceneResidency(bootstrap);
            yield return ExerciseQuadraticSelectionSwap();
            if (captureFinalRuntime)
                yield return CaptureHandoffFrame(
                    bootstrap,
                    "03_Selection_Ready.png");

            int[] expected = { 13, 13, semanticEiffelV10 ? 10 : 16, 9, 8 };
            for (int levelIndex = 0; levelIndex < expected.Length; levelIndex++)
            {
                bootstrap.BeginLevel(levelIndex);
                RuntimePuzzleStage stage =
                    LevelStage(levelIndex);
                yield return WaitForStage(stage, 12f);
                yield return WaitForAudioStage(
                    bootstrap,
                    stage);
                if (forgivingSnapMusicV14)
                {
                    AudioClip selected =
                        bootstrap.audioConductor.CurrentMusicClip;
                    Require(
                        bootstrap.audioConductor.IsPuzzleMusic(selected),
                        "V14 puzzle stage did not select from the original " +
                        "light-music pool");
                    Require(
                        !previousPuzzleMusic ||
                        selected != previousPuzzleMusic,
                        "V14 puzzle BGM repeated consecutively");
                    previousPuzzleMusic = selected;
                }
                ValidateSceneResidency(bootstrap);
                RuntimePuzzleLevelController level =
                    FindFirstObjectByType<RuntimePuzzleLevelController>();
                Require(
                    level &&
                    level.TotalCount == expected[levelIndex] &&
                    level.levelIndex == levelIndex,
                    $"level {levelIndex} expected {expected[levelIndex]} complete modules");
                Require(
                    level.pieces != null &&
                    level.pieces.Length == expected[levelIndex] &&
                    level.pieces.All(value =>
                        value &&
                        value.solidRenderers != null &&
                        value.solidRenderers.Length > 0),
                    $"level {levelIndex} has missing visible complete-module Parts");
                if (levelIndex == 0)
                    ExercisePhysicalLampHint();
                if (levelIndex == 1)
                    Require(
                        level.pieces.Count(value =>
                            value.requiresTwoHands) >= 4,
                        "ship level lost the four authored two-hand modules");
                if (levelIndex == 2)
                    Require(
                        level.pieces.Count(value =>
                            value.prerequisiteSequences == null ||
                            value.prerequisiteSequences.Length == 0) ==
                        (semanticEiffelV10 ? 1 : 4),
                        semanticEiffelV10
                            ? "Eiffel V10 lost its foundation-first semantic dependency"
                            : "Eiffel level lost its four nonlinear opening feet");
                if (forgivingSnapMusicV14)
                    CompleteThroughForgivingSnap(level, levelIndex);
                else
                    foreach (RuntimePuzzlePiece piece in level.pieces)
                        piece.ForceSnapForSmoke();
                Require(
                    level.Completed,
                    $"level {levelIndex} did not complete");

                yield return WaitForStage(
                    RuntimePuzzleStage.Completion,
                    12f);
                yield return WaitForAudioStage(
                    bootstrap,
                    RuntimePuzzleStage.Completion);
                ValidateSceneResidency(bootstrap);
                Require(
                    FindFirstObjectByType<CompletionInteractionController>(),
                    "Stage 7 completion/free-play controller is missing");
                if (captureFinalRuntime)
                    yield return CaptureHandoffFrame(
                        bootstrap,
                        $"07_Completion_Level{levelIndex + 1}.png");

                bootstrap.EnterCollection();
                yield return WaitForStage(
                    RuntimePuzzleStage.Collection,
                    12f);
                yield return WaitForAudioStage(
                    bootstrap,
                    RuntimePuzzleStage.Collection);
                ValidateSceneResidency(bootstrap);
                if (captureFinalRuntime)
                    yield return CaptureHandoffFrame(
                        bootstrap,
                        $"08_Collection_Level{levelIndex + 1}.png");
                CompleteCollectionForSmoke();
                yield return WaitForStage(
                    RuntimePuzzleStage.Selection,
                    12f);
                yield return WaitForAudioStage(
                    bootstrap,
                    RuntimePuzzleStage.Selection);
                ValidateSceneResidency(bootstrap);
            }

            VerifySaveWrite(bootstrap);
            Require(
                bootstrap.Save.highestUnlockedLevel == 4 &&
                bootstrap.Save.stars.All(value => value >= 1),
                "full-flow progression/save stars did not persist in memory");
            passed = true;
        }
        finally
        {
            saveSnapshot.Restore();
        }

        if (passed)
        {
            Debug.Log(
                "[Polished Full Game V03] PLAY FLOW SMOKE PASSED · " +
                "Stage1 Logo→Stage2 selection→Stage3 quadratic arc swap→" +
                "Stage4 13-Part cottage + Stage6 physical lamp hint→" +
                "Stage5 V09 design-fidelity 13-Part whole-hull heavy lift→" +
                (semanticEiffelV10
                    ? "V10 foundation-first 10-module semantic Eiffel→"
                    : "separate nonlinear 16-Part Eiffel→") +
                "9-Part Pagoda→8-Part Windmill→" +
                "Stage7 completion/free-play→" +
                "Stage8 V12 design-fidelity archive→selection · load-before-unload · " +
                (forgivingSnapMusicV14
                    ? "V14 edge-of-tolerance snap path passed every Part · " +
                      "four-track randomized non-repeating puzzle BGM · "
                    : string.Empty) +
                "1.45s BGM crossfades · all BGM/SFX routed · save-v1 roundtrip · no legacy ritual");
            FinishEditorAutomation(0);
        }
    }

    static void CompleteThroughForgivingSnap(
        RuntimePuzzleLevelController level,
        int levelIndex)
    {
        int safety = level.TotalCount + 2;
        while (!level.Completed && safety-- > 0)
        {
            RuntimePuzzlePiece next = level.pieces
                .Where(value =>
                    value &&
                    !value.IsSnapped &&
                    value.IsAvailable)
                .OrderBy(value => value.sequence)
                .FirstOrDefault();
            Require(
                next,
                $"V14 level {levelIndex} has no available next Part");
            Require(
                next.TryForgivingSnapForSmoke(),
                $"V14 forgiving snap rejected {next.name} at " +
                $"{next.snapDistance * 0.82f:F3} m / " +
                $"{next.maxAssistAngle * 0.82f:F1} degrees");
        }
        Require(
            level.Completed,
            $"V14 forgiving snap did not complete level {levelIndex}");
    }

    IEnumerator RunAutoDemo()
    {
        yield return WaitForBootstrap(12f);
        PersistentPuzzleBootstrap bootstrap = PersistentPuzzleBootstrap.Instance;
        Require(bootstrap, "bootstrap unavailable for auto demo");
        SaveFileSnapshot saveSnapshot =
            SaveFileSnapshot.Capture(bootstrap.SavePath);
        bootstrap.SetPersistenceSuspendedForAutomation(true);
        bootstrap.Save.highestUnlockedLevel = 2;
        Debug.Log(
            "[Personally Puzzled VR] AUTO DEMO STARTED · " +
            "presentation pacing enabled · save writes suspended");
        try
        {
            yield return WaitForStage(RuntimePuzzleStage.Splash, 12f);
            yield return DemoWait(2.6f);
            SplashWorkshopController splash =
                FindFirstObjectByType<SplashWorkshopController>();
            splash?.ActivateWorkshop();
            RuntimeAudioRouter.PlayCue(
                RuntimeAudioCue.Touch,
                splash ? splash.transform.position : Vector3.zero);
            yield return DemoWait(1.1f);

            bootstrap.EnterSelection();
            yield return WaitForStage(RuntimePuzzleStage.Selection, 12f);
            yield return DemoWait(2.4f);
            yield return ExerciseQuadraticSelectionSwap();
            yield return DemoWait(1.8f);

            bool semanticEiffelV10 = bootstrap.eiffelScene.Contains(
                "_V10_VRSpatialSemanticAssembly",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V11_AllLevelReview",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V12_ReachEnvelope",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V13_PhysicalLampPull",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V14_ForgivingSnapMusic",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V15_OpenLicensedMusic",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V16_VisualPolish",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V17_WarmWorkshopEnclosure",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V18_EiffelCompletionContinuity",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V19_AssemblyTargetPreview",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V20_ReliableExhibitSelection",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V21_SpatialClosureDefaultFirstLevel",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V22_GuidedCenteredAssembly",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V23_SpatialIntegrityCelebration",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V24_SwanNativeXRHands",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V25_GroundedGuidedHome",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V26_SwanFloorScaleCalibration",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V27_SwanDeviceGroundCalibration",
                StringComparison.Ordinal) || bootstrap.eiffelScene.Contains(
                "_V28_OfficialOpenXrFloorSpace",
                StringComparison.Ordinal);
            int[] expected = { 13, 13, semanticEiffelV10 ? 10 : 16 };
            for (int levelIndex = 0;
                 levelIndex < expected.Length;
                 levelIndex++)
            {
                bootstrap.BeginLevel(levelIndex);
                yield return WaitForStage(LevelStage(levelIndex), 12f);
                yield return DemoWait(1.8f);
                RuntimePuzzleLevelController level =
                    FindFirstObjectByType<RuntimePuzzleLevelController>();
                Require(
                    level && level.TotalCount == expected[levelIndex],
                    $"auto demo level {levelIndex} contract unavailable");
                RuntimePuzzlePiece[] ordered = level.pieces
                    .Where(value => value)
                    .OrderBy(value => value.sequence)
                    .ToArray();
                for (int partIndex = 0;
                     partIndex < ordered.Length;
                     partIndex++)
                {
                    RuntimePuzzlePiece part = ordered[partIndex];
                    if (!part.IsAvailable)
                        continue;
                    if (levelIndex == 0 && partIndex == 2)
                    {
                        level.PulseNextHint();
                        yield return DemoWait(2.15f);
                    }
                    part.ApplyNearSocketReferencePose();
                    yield return DemoWait(
                        part.requiresTwoHands ? 0.72f : 0.48f);
                    part.ForceSnapForSmoke();
                    yield return DemoWait(
                        part.requiresTwoHands ? 0.82f : 0.58f);
                }
                Require(level.Completed, $"auto demo level {levelIndex} incomplete");

                yield return WaitForStage(RuntimePuzzleStage.Completion, 12f);
                yield return DemoWait(3.8f);
                bootstrap.EnterCollection();
                yield return WaitForStage(RuntimePuzzleStage.Collection, 12f);
                yield return DemoWait(2.2f);
                CollectionArchiveRitualV06Controller archive =
                    FindFirstObjectByType<CollectionArchiveRitualV06Controller>();
                Require(archive, "auto demo archive ritual unavailable");
                archive.BeginRitualForSmoke();
                float ritualStarted = Time.realtimeSinceStartup;
                while (!archive.Completed &&
                       Time.realtimeSinceStartup - ritualStarted < 5f)
                    yield return null;
                Require(archive.Completed, "auto demo archive ritual timeout");
                yield return WaitForStage(RuntimePuzzleStage.Selection, 12f);
                yield return DemoWait(1.8f);
            }

            Debug.Log(
                "[Personally Puzzled VR] AUTO DEMO PASSED · " +
                "Splash→Selection→three complete manual-equivalent assemblies→" +
                "Completion→Archive · presentation stopped without closing Unity");
            yield return DemoWait(2f);
        }
        finally
        {
            saveSnapshot.Restore();
        }
        FinishEditorAutomation(0);
    }

    static WaitForSecondsRealtime DemoWait(float presentationSeconds)
    {
        bool fast = Environment.GetEnvironmentVariable(
            "PPVR_DEMO_FAST") == "1";
        return new WaitForSecondsRealtime(
            fast
                ? Mathf.Max(0.035f, presentationSeconds * 0.055f)
                : presentationSeconds);
    }

    IEnumerator RunSoak(float seconds)
    {
        yield return WaitForBootstrap(12f);
        PersistentPuzzleBootstrap bootstrap = PersistentPuzzleBootstrap.Instance;
        Require(bootstrap, "bootstrap unavailable for soak");
        bootstrap.Save.highestUnlockedLevel = 2;
        yield return WaitForStage(RuntimePuzzleStage.Splash, 12f);
        bootstrap.EnterSelection();
        yield return WaitForStage(RuntimePuzzleStage.Selection, 12f);
        float started = Time.realtimeSinceStartup;
        int transitions = 0;
        while (Time.realtimeSinceStartup - started < seconds)
        {
            int levelIndex = transitions % 3;
            bootstrap.BeginLevel(levelIndex);
            yield return WaitForStage(LevelStage(levelIndex), 12f);
            RuntimePuzzleLevelController level =
                FindFirstObjectByType<RuntimePuzzleLevelController>();
            Require(level, "puzzle level unavailable during soak");
            foreach (RuntimePuzzlePiece piece in level.pieces)
                piece.ForceSnapForSmoke();
            yield return WaitForStage(RuntimePuzzleStage.Completion, 12f);
            bootstrap.EnterCollection();
            yield return WaitForStage(RuntimePuzzleStage.Collection, 12f);
            CompleteCollectionForSmoke();
            yield return WaitForStage(RuntimePuzzleStage.Selection, 12f);
            transitions++;
            yield return null;
        }
        Debug.Log(
            $"[Polished Full Game V03] SOAK PASSED · " +
            $"realtimeSeconds={Time.realtimeSinceStartup - started:F1} · " +
            $"completeLoops={transitions} · " +
            $"memoryMB={UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576f:F1}");
        FinishEditorAutomation(0);
    }

    static void FinishEditorAutomation(int exitCode)
    {
        if (Application.isBatchMode)
            UnityEditor.EditorApplication.Exit(exitCode);
        else
            UnityEditor.EditorApplication.isPlaying = false;
    }

    void OnGUI()
    {
        if (!PersonallyPuzzledExperienceModeState.IsAutoDemo)
            return;
        GUI.Box(
            new Rect(18f, 18f, 390f, 64f),
            "自动演示模式 · AUTO DEMO\n" +
            "正在展示完整八阶段流程 · 存档不会被修改 · F10 可停止");
    }

    static void CompleteCollectionForSmoke()
    {
        CollectionArchiveRitualV06Controller v06 =
            FindFirstObjectByType<CollectionArchiveRitualV06Controller>();
        Require(v06, "Stage 8 V12 collection ritual unavailable");
        Require(
            !FindFirstObjectByType<CollectionRitualController>(),
            "legacy low-detail archive ritual was loaded");
        v06.BeginRitualForSmoke();
        v06.AdvanceRitualForSmoke(1f);
    }

    static PolishedRuntimeStagePortalV02 FindStagePortal(
        RuntimePuzzleStage stage)
    {
        PolishedRuntimeStagePortalV02[] portals =
            FindObjectsByType<PolishedRuntimeStagePortalV02>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        PolishedRuntimeStagePortalV02 portal =
            portals.SingleOrDefault(value => value && value.stage == stage);
        Require(portal, "physical stage portal unavailable: " + stage);
        return portal;
    }

    static IEnumerator WaitForSecondsPastPortalMinimum(
        PolishedRuntimeStagePortalV02 portal)
    {
        Require(portal, "cannot wait for a missing physical portal");
        yield return new WaitForSecondsRealtime(
            Mathf.Max(0.05f, portal.minimumStageSeconds + 0.08f));
    }

    static void RequireComfortClear(
        PersistentPuzzleBootstrap bootstrap,
        string handoff)
    {
        Require(
            bootstrap &&
            bootstrap.comfortTransition &&
            bootstrap.comfortTransition.IsClear,
            handoff + " left the persistent comfort transition obscuring the player view");
    }

    static IEnumerator CaptureHandoffFrame(
        PersistentPuzzleBootstrap bootstrap,
        string fileName)
    {
        Require(
            bootstrap && bootstrap.xrCamera,
            "runtime handoff capture has no persistent camera");
        RuntimeDesktopHeadNavigatorV01 desktopNavigator =
            FindFirstObjectByType<RuntimeDesktopHeadNavigatorV01>();
        Require(
            desktopNavigator &&
            desktopNavigator.DesktopPreviewActive &&
            desktopNavigator.EditorTrackedPoseSuppressed,
            "runtime handoff capture has no neutral editor head pose or still contains a simulated TrackedPoseDriver");
        desktopNavigator.ResetToNeutralForAutomation();
        // Give the camera, late-bound stage controller and URP history three
        // presented frames after a scene load or authored animation sample.
        // This avoids capturing a transition frame while still preserving the
        // exact PlayerManual state being reviewed.
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        Require(
            Screen.width == 1920 && Screen.height == 1080,
            "Unity Game view must be the fixed 1920x1080 design-review frame; " +
            $"actual={Screen.width}x{Screen.height}");
        string configuredDirectory = Environment.GetEnvironmentVariable(
            "PPVR_HANDOFF_CAPTURE_ROOT");
        string directory = string.IsNullOrEmpty(configuredDirectory)
            ? Path.GetFullPath(
                "Artifacts/Checkpoints/10_UnitySceneHandoffs_20260718/RuntimeCaptures")
            : Path.GetFullPath(configuredDirectory);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, fileName);
        if (File.Exists(path))
            File.Delete(path);
        // Do not call Camera.Render from inside the active Unity 6 RenderGraph
        // player loop: that re-enters native rendering and can deadlock in
        // batch Play Mode. Capture the already-presented Game view instead.
        UnityEngine.ScreenCapture.CaptureScreenshot(path, 1);
        float captureStarted = Time.realtimeSinceStartup;
        while ((!File.Exists(path) ||
                new FileInfo(path).Length <= 32768) &&
               Time.realtimeSinceStartup - captureStarted < 8f)
            yield return null;
        Require(
            File.Exists(path) && new FileInfo(path).Length > 32768,
            "runtime handoff capture is missing or suspiciously small: " + path);
        Debug.Log(
            "[Personally Puzzled VR] HANDOFF CAPTURE · " + path +
            " · frame=" + Screen.width + "x" + Screen.height +
            " · camera=" + bootstrap.xrCamera.transform.position +
            " · rotation=" + bootstrap.xrCamera.transform.rotation.eulerAngles);
        yield return null;
    }

    static IEnumerator WaitForHandoffCover(
        PersistentPuzzleBootstrap bootstrap,
        float timeout)
    {
        float started = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - started < timeout)
        {
            RuntimeComfortTransitionV01 comfort = bootstrap
                ? bootstrap.comfortTransition
                : null;
            if (bootstrap &&
                bootstrap.IsTransitioning &&
                comfort &&
                comfort.Alpha >= 0.24f &&
                comfort.Alpha <= 0.92f)
                yield break;
            yield return null;
        }
        Require(false, "timed out waiting for visible gold handoff cover");
    }

    static IEnumerator CaptureSpatialInspectionSet(
        PersistentPuzzleBootstrap bootstrap,
        string prefix)
    {
        yield return CaptureSpatialFrame(
            bootstrap, prefix + "_00_Front.png", Vector3.zero, 0f, 0f);
        yield return CaptureSpatialFrame(
            bootstrap, prefix + "_01_YawLeft12.png", Vector3.zero, -12f, 0f);
        yield return CaptureSpatialFrame(
            bootstrap, prefix + "_02_YawRight12.png", Vector3.zero, 12f, 0f);
        yield return CaptureSpatialFrame(
            bootstrap, prefix + "_03_MoveLeft16cm.png", new Vector3(-0.16f, 0f, 0f), 0f, 0f);
        yield return CaptureSpatialFrame(
            bootstrap, prefix + "_04_MoveRight16cm.png", new Vector3(0.16f, 0f, 0f), 0f, 0f);
        yield return CaptureSpatialFrame(
            bootstrap, prefix + "_05_MoveForward14cm.png", new Vector3(0f, 0f, 0.14f), 0f, 0f);
        yield return CaptureSpatialFrame(
            bootstrap, prefix + "_06_MoveBackward14cm.png", new Vector3(0f, 0f, -0.14f), 0f, 0f);
        yield return CaptureSpatialFrame(
            bootstrap, prefix + "_07_MoveUp12cm.png", new Vector3(0f, 0.12f, 0f), 0f, 0f);
        yield return CaptureSpatialFrame(
            bootstrap, prefix + "_08_MoveDown12cm.png", new Vector3(0f, -0.12f, 0f), 0f, 0f);
        RuntimeDesktopHeadNavigatorV01 navigator =
            FindFirstObjectByType<RuntimeDesktopHeadNavigatorV01>();
        navigator?.ResetToNeutralForAutomation();
    }

    static IEnumerator CaptureSpatialFrame(
        PersistentPuzzleBootstrap bootstrap,
        string fileName,
        Vector3 roomOffset,
        float yawDegrees,
        float pitchDegrees)
    {
        Require(
            bootstrap && bootstrap.xrCamera,
            "spatial capture has no persistent camera");
        RuntimeDesktopHeadNavigatorV01 desktopNavigator =
            FindFirstObjectByType<RuntimeDesktopHeadNavigatorV01>();
        Require(
            desktopNavigator &&
            desktopNavigator.DesktopPreviewActive &&
            desktopNavigator.EditorTrackedPoseSuppressed,
            "spatial capture has no deterministic editor head navigator");
        desktopNavigator.SetInspectionPoseForAutomation(
            roomOffset,
            yawDegrees,
            pitchDegrees);
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        Require(
            Screen.width == 1920 && Screen.height == 1080,
            "spatial inspection Game view must remain 1920x1080; " +
            $"actual={Screen.width}x{Screen.height}");
        string configuredDirectory = Environment.GetEnvironmentVariable(
            "PPVR_SPATIAL_CAPTURE_ROOT");
        string directory = string.IsNullOrEmpty(configuredDirectory)
            ? Path.GetFullPath(
                "Artifacts/Checkpoints/10_UnitySceneHandoffs_20260718/RuntimeSpatialCaptures")
            : Path.GetFullPath(configuredDirectory);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, fileName);
        if (File.Exists(path))
            File.Delete(path);
        UnityEngine.ScreenCapture.CaptureScreenshot(path, 1);
        float captureStarted = Time.realtimeSinceStartup;
        while ((!File.Exists(path) ||
                new FileInfo(path).Length <= 32768) &&
               Time.realtimeSinceStartup - captureStarted < 8f)
            yield return null;
        Require(
            File.Exists(path) && new FileInfo(path).Length > 32768,
            "spatial capture is missing or suspiciously small: " + path);
        Debug.Log(
            "[Personally Puzzled VR] SPATIAL CAPTURE · " + path +
            " · frame=" + Screen.width + "x" + Screen.height +
            " · roomOffset=" + roomOffset +
            " · yaw=" + yawDegrees.ToString("F1") +
            " · pitch=" + pitchDegrees.ToString("F1") +
            " · camera=" + bootstrap.xrCamera.transform.position +
            " · rotation=" + bootstrap.xrCamera.transform.rotation.eulerAngles);
        yield return null;
    }

    static IEnumerator ExerciseQuadraticSelectionSwap()
    {
        SelectionCarouselController selection =
            FindFirstObjectByType<SelectionCarouselController>();
        Require(selection, "Stage 2 selection controller unavailable");
        Require(
            selection.exhibits != null &&
            selection.exhibits.Length >= 3 &&
            selection.transitionSeconds > 0f,
            "Stage 2 near-field carousel is incomplete");
        int previous = selection.currentIndex;
        selection.BeginSwap(1);
        Require(
            selection.IsTransitioning,
            "Stage 3 quadratic arc swap did not start");
        float started = Time.realtimeSinceStartup;
        while (selection.IsTransitioning &&
               Time.realtimeSinceStartup - started < 4f)
            yield return null;
        Require(
            !selection.IsTransitioning &&
            selection.currentIndex != previous,
            "Stage 3 quadratic arc swap did not finish");
    }

    static void ExercisePhysicalLampHint()
    {
        Stage6LampGhostHintV06Controller hint =
            FindFirstObjectByType<Stage6LampGhostHintV06Controller>();
        Require(
            hint &&
            Mathf.Abs(hint.hintDuration - 2f) <= 0.01f,
            "Stage 6 exact two-second physical lamp hint is missing");
        hint.TriggerHintForSmoke();
        Require(
            hint.GhostVisible &&
            hint.HintRemaining > 1.9f &&
            hint.exactHintGhosts.Count(value =>
                value &&
                value.enabled) == 1,
            "Stage 6 lamp did not reveal exactly one complete-module ghost");
        hint.AdvanceHintTimerForSmoke(2.1f);
        Require(
            !hint.GhostVisible &&
            hint.exactHintGhosts.All(value =>
                !value ||
                !value.enabled),
            "Stage 6 exact ghost did not clear after two seconds");
    }

    static IEnumerator WaitForAudioStage(
        PersistentPuzzleBootstrap bootstrap,
        RuntimePuzzleStage stage)
    {
        RuntimeAudioConductor audio =
            bootstrap.audioConductor;
        Require(
            audio &&
            Mathf.Abs(audio.crossfadeSeconds - 1.45f) <= 0.01f,
            "persistent 1.45-second audio conductor unavailable");
        AudioClip expected =
            IsPuzzleStage(stage) &&
            audio.HasPuzzleMusicPool
                ? audio.CurrentMusicClip
                : ExpectedMusic(audio, stage);
        Require(
            expected &&
            (!IsPuzzleStage(stage) ||
             !audio.HasPuzzleMusicPool ||
             audio.IsPuzzleMusic(expected)),
            "authored/randomized BGM missing for " + stage);
        float started = Time.realtimeSinceStartup;
        bool settled = false;
        while (Time.realtimeSinceStartup - started < 3.2f)
        {
            bool aExpected =
                audio.musicA &&
                audio.musicA.clip == expected &&
                audio.musicA.volume > 0.01f;
            bool bExpected =
                audio.musicB &&
                audio.musicB.clip == expected &&
                audio.musicB.volume > 0.01f;
            bool aQuiet =
                !audio.musicA ||
                audio.musicA.volume <= 0.005f;
            bool bQuiet =
                !audio.musicB ||
                audio.musicB.volume <= 0.005f;
            settled =
                (aExpected && bQuiet) ||
                (bExpected && aQuiet);
            if (settled)
                break;
            yield return null;
        }
        Require(
            settled,
            "BGM crossfade did not settle on authored " +
            stage + " music");
    }

    static bool IsPuzzleStage(RuntimePuzzleStage stage) =>
        stage == RuntimePuzzleStage.Cottage ||
        stage == RuntimePuzzleStage.Ship ||
        stage == RuntimePuzzleStage.Eiffel ||
        stage == RuntimePuzzleStage.Pagoda ||
        stage == RuntimePuzzleStage.Windmill;

    static AudioClip ExpectedMusic(
        RuntimeAudioConductor audio,
        RuntimePuzzleStage stage)
    {
        return stage switch
        {
            RuntimePuzzleStage.Splash =>
                audio.splashMusic,
            RuntimePuzzleStage.Selection =>
                audio.selectionMusic,
            RuntimePuzzleStage.Cottage =>
                audio.cottageMusic,
            RuntimePuzzleStage.Ship =>
                audio.shipMusic,
            RuntimePuzzleStage.Eiffel =>
                audio.eiffelMusic,
            RuntimePuzzleStage.Pagoda =>
                audio.cottageMusic ? audio.cottageMusic : audio.eiffelMusic,
            RuntimePuzzleStage.Windmill =>
                audio.selectionMusic ? audio.selectionMusic : audio.cottageMusic,
            RuntimePuzzleStage.Completion =>
                audio.completionMusic,
            RuntimePuzzleStage.Collection =>
                audio.collectionMusic,
            _ => null,
        };
    }

    static void ValidateSceneResidency(
        PersistentPuzzleBootstrap bootstrap)
    {
        string[] stageScenes =
        {
            bootstrap.splashScene,
            bootstrap.selectionScene,
            bootstrap.cottageScene,
            bootstrap.shipScene,
            bootstrap.eiffelScene,
            bootstrap.pagodaScene,
            bootstrap.windmillScene,
            bootstrap.completionScene,
            bootstrap.collectionScene,
        };
        int loaded = stageScenes.Count(value =>
        {
            Scene scene = SceneManager.GetSceneByName(value);
            return scene.IsValid() && scene.isLoaded;
        });
        Require(
            loaded == 1 &&
            stageScenes.Contains(bootstrap.LoadedStageScene) &&
            SceneManager.GetSceneByName(
                bootstrap.LoadedStageScene).isLoaded,
            "additive transition left a stale/duplicate stage resident");
        ValidateNoMissingScripts();
    }

    static void ValidateNoMissingScripts()
    {
        var missing = new List<string>();
        for (int sceneIndex = 0;
             sceneIndex < SceneManager.sceneCount;
             sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform transform in
                     root.GetComponentsInChildren<Transform>(true))
            {
                Component[] components =
                    transform.GetComponents<Component>();
                if (components.Any(value => value == null))
                    missing.Add(
                        scene.name + "/" +
                        GetHierarchyPath(transform));
            }
        }
        Require(
            missing.Count == 0,
            "runtime contains missing script components: " +
            string.Join(" | ", missing));
    }

    static string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }

    static void VerifySaveWrite(
        PersistentPuzzleBootstrap bootstrap)
    {
        bootstrap.SaveNow();
        Require(
            File.Exists(bootstrap.SavePath) &&
            !File.Exists(bootstrap.SavePath + ".tmp"),
            "atomic save-v1 write did not finish");
        string json =
            File.ReadAllText(bootstrap.SavePath);
        Require(
            json.Contains("\"version\": 1", StringComparison.Ordinal) &&
            json.Contains(
                "\"highestUnlockedLevel\": 4",
                StringComparison.Ordinal) &&
            json.Contains("\"stars\"", StringComparison.Ordinal),
            "save-v1 JSON roundtrip is incomplete");
    }

    readonly struct SaveFileSnapshot
    {
        readonly string path;
        readonly bool existed;
        readonly byte[] bytes;

        SaveFileSnapshot(
            string path,
            bool existed,
            byte[] bytes)
        {
            this.path = path;
            this.existed = existed;
            this.bytes = bytes;
        }

        public static SaveFileSnapshot Capture(string path)
        {
            bool existed = File.Exists(path);
            return new SaveFileSnapshot(
                path,
                existed,
                existed ? File.ReadAllBytes(path) : null);
        }

        public void Restore()
        {
            if (File.Exists(path))
                File.Delete(path);
            if (existed)
            {
                Directory.CreateDirectory(
                    Path.GetDirectoryName(path));
                File.WriteAllBytes(path, bytes);
            }
            string temporary = path + ".tmp";
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    static RuntimePuzzleStage LevelStage(int levelIndex)
    {
        return levelIndex switch
        {
            0 => RuntimePuzzleStage.Cottage,
            1 => RuntimePuzzleStage.Ship,
            2 => RuntimePuzzleStage.Eiffel,
            3 => RuntimePuzzleStage.Pagoda,
            4 => RuntimePuzzleStage.Windmill,
            _ => RuntimePuzzleStage.Selection,
        };
    }

    static IEnumerator WaitForBootstrap(float timeout)
    {
        float started = Time.realtimeSinceStartup;
        while (!PersistentPuzzleBootstrap.Instance &&
               Time.realtimeSinceStartup - started < timeout)
            yield return null;
        Require(PersistentPuzzleBootstrap.Instance, "bootstrap timeout");
    }

    static IEnumerator WaitForStage(RuntimePuzzleStage stage, float timeout)
    {
        float started = Time.realtimeSinceStartup;
        while (!IsStageReady(
                   PersistentPuzzleBootstrap.Instance,
                   stage) &&
               Time.realtimeSinceStartup - started < timeout)
            yield return null;
        Require(
            IsStageReady(
                PersistentPuzzleBootstrap.Instance,
                stage),
            "stage timeout: " + stage);
        yield return null;
    }

    static bool IsStageReady(
        PersistentPuzzleBootstrap bootstrap,
        RuntimePuzzleStage stage)
    {
        if (!bootstrap ||
            bootstrap.CurrentStage != stage ||
            bootstrap.IsTransitioning)
            return false;
        string expectedScene = stage switch
        {
            RuntimePuzzleStage.Splash => bootstrap.splashScene,
            RuntimePuzzleStage.Selection => bootstrap.selectionScene,
            RuntimePuzzleStage.Cottage => bootstrap.cottageScene,
            RuntimePuzzleStage.Ship => bootstrap.shipScene,
            RuntimePuzzleStage.Eiffel => bootstrap.eiffelScene,
            RuntimePuzzleStage.Pagoda => bootstrap.pagodaScene,
            RuntimePuzzleStage.Windmill => bootstrap.windmillScene,
            RuntimePuzzleStage.Completion => bootstrap.completionScene,
            RuntimePuzzleStage.Collection => bootstrap.collectionScene,
            _ => string.Empty,
        };
        if (string.IsNullOrEmpty(expectedScene) ||
            bootstrap.LoadedStageScene != expectedScene)
            return false;
        Scene loaded = SceneManager.GetSceneByName(expectedScene);
        return loaded.IsValid() && loaded.isLoaded;
    }

    static void Require(UnityEngine.Object value, string message)
    {
        if (!value)
            throw new InvalidOperationException(
                "[Polished Full Game V03 Smoke] " + message);
    }

    static void Require(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(
                "[Polished Full Game V03 Smoke] " + message);
    }
#endif
}
