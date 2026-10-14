using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// V25-only PlayerManual regression for the lifecycle, XR-origin invariant and
/// no-pinch fingertip lamp affordance. It is inert in normal builds.
/// </summary>
[DisallowMultipleComponent]
public sealed class GroundedGuidedHomeV25Smoke : MonoBehaviour
{
    const string EnvironmentKey = "PPVR_V25_INTERACTION_SMOKE";
    bool runningSmoke;

    IEnumerator Start()
    {
#if UNITY_EDITOR
        if (Environment.GetEnvironmentVariable(EnvironmentKey) != "1")
            yield break;
        Environment.SetEnvironmentVariable(EnvironmentKey, null);
        runningSmoke = true;
        Application.logMessageReceived += OnLog;
        yield return Run();
#else
        yield break;
#endif
    }

    void OnDestroy()
    {
#if UNITY_EDITOR
        Application.logMessageReceived -= OnLog;
#endif
    }

#if UNITY_EDITOR
    void OnLog(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (!runningSmoke ||
            type != LogType.Exception ||
            (!condition.Contains(
                 nameof(GroundedGuidedHomeV25Smoke),
                 StringComparison.Ordinal) &&
             !stackTrace.Contains(
                 nameof(GroundedGuidedHomeV25Smoke),
                 StringComparison.Ordinal)))
            return;
        runningSmoke = false;
        Debug.LogError(
            "[Grounded Guided Home V25] INTERACTION SMOKE FAILED · " +
            condition);
        UnityEditor.EditorApplication.delayCall +=
            () => UnityEditor.EditorApplication.Exit(1);
    }

    IEnumerator Run()
    {
        PersistentPuzzleBootstrap bootstrap =
            PersistentPuzzleBootstrap.Instance;
        Require(bootstrap, "V25 bootstrap unavailable");
        bootstrap.SetPersistenceSuspendedForAutomation(true);
        bootstrap.Save.highestUnlockedLevel = 4;
        yield return WaitForStage(
            bootstrap,
            RuntimePuzzleStage.Splash,
            12f);

        bootstrap.BeginLevel(0);
        yield return WaitForStage(
            bootstrap,
            RuntimePuzzleStage.Cottage,
            12f);
        RuntimePuzzleLevelController level =
            FindFirstObjectByType<RuntimePuzzleLevelController>();
        RuntimeHintLampPullV13 pull =
            FindFirstObjectByType<RuntimeHintLampPullV13>();
        RuntimePuzzleGuidanceV09 guidance =
            FindFirstObjectByType<RuntimePuzzleGuidanceV09>();
        RuntimeXrOriginInvariantV25 invariant =
            bootstrap.GetComponent<RuntimeXrOriginInvariantV25>();
        Require(
            level && pull && guidance && invariant,
            "V25 assembly interaction contract unavailable");

        int hintsBefore = level.HintsUsed;
        Require(
            pull.TryFingertipTapV25(pull.transform.position),
            "Index fingertip did not trigger the lamp bead");
        yield return null;
        Require(
            level.HintsUsed == hintsBefore + 1 &&
            guidance.PathVisible &&
            level.pieces[0] &&
            !level.pieces[0].IsSnapped,
            "No-pinch fingertip tap did not reveal one exact placement hint");
        Require(
            !pull.TryFingertipTapV25(pull.transform.position) &&
            level.HintsUsed == hintsBefore + 1,
            "Held fingertip contact retriggered and spent multiple hints");
        pull.TryFingertipTapV25(
            pull.transform.position + Vector3.one * 1.0f);

        Vector3 originBefore =
            bootstrap.xrOrigin.transform.localPosition;
        Quaternion originRotationBefore =
            bootstrap.xrOrigin.transform.localRotation;
        Vector3 cameraLocalBefore =
            bootstrap.xrCamera.transform.localPosition;
        Quaternion cameraLocalRotationBefore =
            bootstrap.xrCamera.transform.localRotation;
        bootstrap.xrOrigin.transform.localPosition +=
            new Vector3(0.04f, 0.03f, -0.02f);
        Require(
            invariant.EnforceInvariant(),
            "XR-origin invariant did not detect a hierarchy displacement");
        Require(
            Vector3.Distance(
                bootstrap.xrOrigin.transform.localPosition,
                originBefore) <= 0.00001f &&
            Quaternion.Angle(
                bootstrap.xrOrigin.transform.localRotation,
                originRotationBefore) <= 0.001f &&
            Vector3.Distance(
                bootstrap.xrCamera.transform.localPosition,
                cameraLocalBefore) <= 0.00001f &&
            Quaternion.Angle(
                bootstrap.xrCamera.transform.localRotation,
                cameraLocalRotationBefore) <= 0.001f,
            "Interaction invariant touched the native tracked camera");

        bootstrap.SimulateResumeReturnForSmokeV25();
        yield return WaitForStage(
            bootstrap,
            RuntimePuzzleStage.Splash,
            12f);
        Require(
            bootstrap.ResumeWasInterruptedV25 &&
            !FindFirstObjectByType<RuntimePuzzleLevelController>(),
            "Resumed activity did not return to a clean welcome/home stage");
        bootstrap.SetPersistenceSuspendedForAutomation(false);
        Debug.Log(
            "[Grounded Guided Home V25] INTERACTION SMOKE PASSED · " +
            "index-tip lamp tap without pinch · contact latch spends one hint · " +
            "XR origin restored while native camera local pose remains untouched · " +
            "resume returns assembly to welcome/home");
        runningSmoke = false;
        Application.logMessageReceived -= OnLog;
        UnityEditor.EditorApplication.Exit(0);
    }

    static IEnumerator WaitForStage(
        PersistentPuzzleBootstrap bootstrap,
        RuntimePuzzleStage stage,
        float timeout)
    {
        float started = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - started < timeout)
        {
            if (bootstrap &&
                bootstrap.CurrentStage == stage &&
                !bootstrap.IsTransitioning &&
                !string.IsNullOrEmpty(bootstrap.LoadedStageScene))
                yield break;
            yield return null;
        }
        throw new InvalidOperationException(
            "Timed out waiting for V25 stage " + stage +
            "; current=" +
            (bootstrap ? bootstrap.CurrentStage.ToString() : "missing"));
    }

    static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
#endif
}
