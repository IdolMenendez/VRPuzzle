using System;
using System.Collections;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class InspectableShowcaseSmokeV79 : MonoBehaviour
{
    public const string EnvironmentVariable =
        "PPVR_V79_INSPECTABLE_SHOWCASE_SMOKE";
    static bool created;

    void Start()
    {
#if UNITY_EDITOR
        if (created ||
            Environment.GetEnvironmentVariable(EnvironmentVariable) != "1")
            return;
        created = true;
        Environment.SetEnvironmentVariable(EnvironmentVariable, null);
        GameObject runner = new GameObject(
            "V79 inspectable showcase smoke runner");
        DontDestroyOnLoad(runner);
        runner.AddComponent<InspectableShowcaseSmokeRunnerV79>();
#endif
    }
}

#if UNITY_EDITOR
public sealed class InspectableShowcaseSmokeRunnerV79 : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return WaitUntil(
            () => PersistentPuzzleBootstrap.Instance,
            12f,
            "persistent bootstrap");
        PersistentPuzzleBootstrap bootstrap =
            PersistentPuzzleBootstrap.Instance;
        yield return WaitUntil(
            () => bootstrap.CurrentStage == RuntimePuzzleStage.Splash &&
                  !bootstrap.IsTransitioning,
            15f,
            "initial Splash");

        bootstrap.Save.highestUnlockedLevel = 4;
        bootstrap.BeginLevel(2);
        yield return WaitUntil(
            () => bootstrap.CurrentStage == RuntimePuzzleStage.Eiffel &&
                  FindFirstObjectByType<RuntimePuzzleLevelController>() &&
                  !bootstrap.IsTransitioning,
            15f,
            "V79 Eiffel");

        RuntimePuzzleLevelController level =
            FindFirstObjectByType<RuntimePuzzleLevelController>();
        RuntimeAssemblyConsoleV61 console =
            FindFirstObjectByType<RuntimeAssemblyConsoleV61>();
        RuntimeAssemblyGuideV23 guide =
            FindFirstObjectByType<RuntimeAssemblyGuideV23>();
        Require(level && console && guide && guide.instructionLabel,
            "V79 Eiffel interaction bindings are incomplete");
        guide.Refresh();
        Require(level.suppressInLevelCompletionUiV79 &&
                guide.physicalHintButtonV79 &&
                guide.hideInstructionOnCompleteV79 &&
                guide.instructionLabel.text.Contains("提示按钮") &&
                !guide.instructionLabel.text.Contains("灯绳"),
            "V79 guide did not replace the lamp-pull instruction");
        Require(!FindObjectsByType<RuntimeHintLampPullV13>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None).Any(value =>
                    value && value.enabled) &&
                !FindObjectsByType<Stage6LampGhostHintV06Controller>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None).Any(value =>
                    value && value.enabled),
            "V79 legacy lamp hint authority remains enabled");

        RuntimeAssemblyConsoleV61.ConsoleButton hint =
            console.buttons.Single(value =>
                value != null &&
                value.id == RuntimeAssemblyConsoleV61.ButtonId.Hint);
        int hintsBefore = level.HintsUsed;
        Require(hint.touchCollider &&
                console.TryFingertipPressV61(
                    hint.touchCollider.bounds.center) &&
                level.HintsUsed == hintsBefore + 1,
            "V79 physical Hint button did not request the next hint");

        RuntimePuzzlePiece[] pieces = level.pieces
            .Where(value => value)
            .OrderBy(value => value.sequence)
            .ToArray();
        Require(pieces.Length == 10,
            "V79 Eiffel does not contain ten semantic Parts");
        RuntimePuzzlePiece finial =
            pieces.Single(value => value.sequence == 9);
        foreach (RuntimePuzzlePiece piece in pieces)
            if (piece != finial)
                piece.ForceSnapForSmoke();
        Require(finial.IsAvailable &&
                finial.snapDistance >= 0.179f &&
                finial.maxAssistAngle >= 179.9f &&
                !finial.requiresApproachCone &&
                !finial.requiresPlayerFacing,
            "V79 Eiffel finial capture profile is not forgiving");
        Require(finial.TryForgivingSnapForSmoke(0.92f, 0.99f) &&
                finial.IsSnapped &&
                level.Completed,
            "V79 Eiffel finial failed a 92%-distance / 178-degree release");
        level.StopAllCoroutines();
        yield return null;
        Require(level.CompletionUiSuppressedV79 &&
                (!guide.instructionLabel ||
                 !guide.instructionLabel.gameObject.activeInHierarchy) &&
                !FindObjectsByType<TextMesh>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None).Any(label =>
                    label &&
                    (label.text.IndexOf(
                         "完成",
                         StringComparison.OrdinalIgnoreCase) >= 0 ||
                     label.text.IndexOf(
                         "COMPLETE",
                         StringComparison.OrdinalIgnoreCase) >= 0)),
            "V79 in-level completion UI remains visible");
        Debug.Log(
            "[PPVR V79 Smoke] PUZZLE PASSED · hint=physical-button · " +
            "lamp-authority=disabled · Eiffel-finial=178deg/92% · " +
            "in-level-completion-ui=0");

        RuntimeRealHandPinchV47 provider =
            RuntimeRealHandPinchV47.Instance;
        Require(provider,
            "V79 shared pinch provider is unavailable");
        provider.ClearAutomationV47();
        bootstrap.RequestStage(
            RuntimePuzzleStage.Completion,
            bootstrap.completionScene);
        yield return WaitUntil(
            () => bootstrap.CurrentStage == RuntimePuzzleStage.Completion &&
                  FindFirstObjectByType<RuntimeCompletionDisplayParityV78>() &&
                  !bootstrap.IsTransitioning,
            15f,
            "V79 Completion");

        RuntimeCompletionDisplayParityV78 parity =
            FindFirstObjectByType<RuntimeCompletionDisplayParityV78>();
        CompletionInteractionController completion =
            FindFirstObjectByType<CompletionInteractionController>();
        Require(parity && completion && completion.heroModel &&
                completion.enableFreeLiftAndTwoHandScaleV79 &&
                parity.widthFill >= 0.83f &&
                parity.heightFill >= 0.89f &&
                parity.depthFill >= 0.77f,
            "V79 larger inspectable Completion is not configured");

        completion.RecacheInspectionBaselineV79();
        Vector3 baselinePosition =
            completion.InspectionBaselinePositionV79;
        Vector3 baselineScale =
            completion.InspectionBaselineScaleV79;
        Require(BoundsOf(completion.heroModel, out Bounds heroBounds),
            "V79 Completion hero bounds are unavailable");

        Pose pickup = new Pose(
            heroBounds.center,
            completion.heroModel.rotation);
        SetProviderWorldHand(
            provider, false, true, false, pickup);
        SetProviderWorldHand(
            provider, true, false, false, pickup);
        yield return null;
        SetProviderWorldHand(
            provider, false, true, true, pickup);
        yield return null;

        Vector3 towardPlayer = Camera.main
            ? Vector3.ProjectOnPlane(
                Camera.main.transform.position - heroBounds.center,
                Vector3.up).normalized
            : Vector3.back;
        Pose lifted = new Pose(
            pickup.position +
            Vector3.up * 0.18f +
            towardPlayer * 0.14f,
            Quaternion.Euler(0f, 18f, 0f) * pickup.rotation);
        float liftDeadline = Time.realtimeSinceStartup + 0.28f;
        while (Time.realtimeSinceStartup < liftDeadline)
        {
            SetProviderWorldHand(
                provider, false, true, true, lifted);
            yield return null;
        }
        Require(
            completion.heroModel.position.y >
                baselinePosition.y + 0.10f,
            "V79 one-hand inspection did not lift the model out");
        Require(completion.ShowcaseShellExtractedV79 &&
                completion.removableShowcaseShellV79 &&
                !completion.removableShowcaseShellV79
                    .GetComponentsInChildren<Renderer>(true)
                    .Any(renderer => renderer && renderer.enabled),
            "V79 first pickup did not remove the glass shell");

        Require(BoundsOf(completion.heroModel, out heroBounds),
            "V79 lifted hero bounds are unavailable");
        Vector3 handAxis = Camera.main
            ? Camera.main.transform.right
            : Vector3.right;
        Pose leftStart = new Pose(
            heroBounds.center - handAxis * 0.065f,
            Quaternion.identity);
        Pose rightStart = new Pose(
            heroBounds.center + handAxis * 0.065f,
            Quaternion.identity);
        SetProviderWorldHand(
            provider, true, true, true, leftStart);
        SetProviderWorldHand(
            provider, false, true, true, rightStart);
        yield return WaitRealtime(0.08f);

        Pose leftExpanded = new Pose(
            heroBounds.center - handAxis * 0.145f,
            Quaternion.identity);
        Pose rightExpanded = new Pose(
            heroBounds.center + handAxis * 0.145f,
            Quaternion.identity);
        float scaleDeadline = Time.realtimeSinceStartup + 0.34f;
        while (Time.realtimeSinceStartup < scaleDeadline)
        {
            SetProviderWorldHand(
                provider, true, true, true, leftExpanded);
            SetProviderWorldHand(
                provider, false, true, true, rightExpanded);
            yield return null;
        }
        Require(completion.CurrentScaleMultiplierV79 >= 1.70f &&
                completion.CurrentScaleMultiplierV79 <=
                    completion.maximumScaleMultiplierV79 + 0.01f,
            "V79 two-hand drag did not enlarge within the authored bounds: " +
            completion.CurrentScaleMultiplierV79.ToString("F3"));

        SetProviderWorldHand(
            provider, true, true, false, leftExpanded);
        SetProviderWorldHand(
            provider, false, true, false, rightExpanded);
        yield return WaitRealtime(0.08f);
        Vector3 releasedPosition = completion.heroModel.position;
        Vector3 releasedScale = completion.heroModel.localScale;
        yield return WaitRealtime(0.14f);
        Require(Vector3.Distance(
                    releasedPosition,
                    completion.heroModel.position) <= 0.002f &&
                Vector3.Distance(
                    releasedScale,
                    completion.heroModel.localScale) <= 0.002f &&
                Vector3.Distance(
                    baselineScale,
                    completion.heroModel.localScale) > 0.01f,
            "V79 inspected pose did not persist after release");
        provider.ClearAutomationV47();
        Debug.Log(
            "[PPVR V79 Smoke] COMPLETION PASSED · initial-size=larger · " +
            "one-hand=XYZ-lift · two-hand=bounded-scale · release=persistent");
        Debug.Log(
            "[PPVR V79 Smoke] PLAYMODE PASSED · no-in-level-completion-ui · " +
            "physical-Hint-button · Eiffel-finial-forgiving · " +
            "lift-and-two-hand-scale");
        Finish(0);
    }

    static void SetProviderWorldHand(
        RuntimeRealHandPinchV47 provider,
        bool leftHand,
        bool available,
        bool pressed,
        Pose worldPose)
    {
        Transform tracking = TrackingSpace();
        Pose local = tracking
            ? new Pose(
                tracking.InverseTransformPoint(worldPose.position),
                Quaternion.Inverse(tracking.rotation) * worldPose.rotation)
            : worldPose;
        provider.SetAutomationHandStateV47(
            leftHand,
            available,
            pressed,
            available,
            local);
    }

    static Transform TrackingSpace()
    {
        PersistentPuzzleBootstrap bootstrap =
            PersistentPuzzleBootstrap.Instance;
        if (bootstrap &&
            bootstrap.xrOrigin &&
            bootstrap.xrOrigin.CameraFloorOffsetObject)
            return bootstrap.xrOrigin.CameraFloorOffsetObject.transform;
        Camera camera = Camera.main;
        return camera && camera.transform.parent
            ? camera.transform.parent
            : null;
    }

    static bool BoundsOf(Transform root, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (Renderer renderer in
                 root.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer ||
                !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy ||
                renderer is ParticleSystemRenderer ||
                renderer.GetComponent<TextMesh>())
                continue;
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return found;
    }

    static IEnumerator WaitUntil(
        Func<bool> condition,
        float seconds,
        string label)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (!condition() &&
               Time.realtimeSinceStartup < deadline)
            yield return null;
        Require(condition(), "Timed out waiting for " + label);
    }

    static IEnumerator WaitRealtime(float seconds)
    {
        float deadline =
            Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
        while (Time.realtimeSinceStartup < deadline)
            yield return null;
    }

    static void Require(bool condition, string message)
    {
        if (condition)
            return;
        Debug.LogError("[PPVR V79 Smoke] FAILED · " + message);
        Finish(1);
        throw new InvalidOperationException(message);
    }

    static void Finish(int code)
    {
        UnityEditor.EditorApplication.isPlaying = false;
        UnityEditor.EditorApplication.Exit(code);
    }
}
#endif
