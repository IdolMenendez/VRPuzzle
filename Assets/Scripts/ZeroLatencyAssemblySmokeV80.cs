using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class ZeroLatencyAssemblySmokeV80 : MonoBehaviour
{
    public const string EnvironmentVariable =
        "PPVR_V80_ZERO_LATENCY_SMOKE";
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
            "V80 zero-latency assembly smoke runner");
        DontDestroyOnLoad(runner);
        runner.AddComponent<ZeroLatencyAssemblySmokeRunnerV80>();
#endif
    }
}

#if UNITY_EDITOR
public sealed class ZeroLatencyAssemblySmokeRunnerV80 : MonoBehaviour
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

        RuntimeZeroLatencyPolicyV80 policy =
            FindFirstObjectByType<RuntimeZeroLatencyPolicyV80>();
        RuntimeRealHandPinchV47 provider =
            RuntimeRealHandPinchV47.Instance;
        RuntimePuzzleHandInteractor[] hands =
            FindObjectsByType<RuntimePuzzleHandInteractor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        Require(policy && provider &&
                policy.targetFrameRate == 72 &&
                policy.useLowFixedFoveation &&
                !provider.emitTelemetry &&
                hands.Length == 2 &&
                hands.All(value =>
                    value.cacheSceneObjectsV80 &&
                    !value.pinchEdgeTelemetryV49),
            "V80 persistent performance policy is incomplete");

        bootstrap.Save.highestUnlockedLevel = 4;
        bootstrap.BeginLevel(2);
        yield return WaitUntil(
            () => bootstrap.CurrentStage == RuntimePuzzleStage.Eiffel &&
                  FindFirstObjectByType<RuntimePuzzleLevelController>() &&
                  !bootstrap.IsTransitioning,
            15f,
            "V80 Eiffel");

        RuntimePuzzleLevelController level =
            FindFirstObjectByType<RuntimePuzzleLevelController>();
        RuntimeAssemblyGuideV23 guide =
            FindFirstObjectByType<RuntimeAssemblyGuideV23>();
        RuntimeAssemblyConsoleV61 console =
            FindFirstObjectByType<RuntimeAssemblyConsoleV61>();
        Require(level && guide && console &&
                level.eventDrivenStatusV80 &&
                level.guidance &&
                level.guidance.eventDrivenRecommendationV80 &&
                level.pieces.All(value =>
                    value && value.idleFastPathV80) &&
                guide.eventDrivenRefreshV80 &&
                guide.onDemandBlueprintV80,
            "V80 event-driven level bindings are incomplete");

        guide.Refresh();
        Require(ActiveBlueprints(guide) == 0,
            "V80 full high-poly blueprint renders at rest");
        UnityEngine.SceneManagement.Scene levelScene =
            level.gameObject.scene;
        Light[] lights = FindObjectsByType<Light>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .Where(value => value && value.enabled &&
                value.gameObject.activeInHierarchy &&
                value.gameObject.scene == levelScene)
            .ToArray();
        Debug.Log(
            "[PPVR V80 Smoke] runtimeLights=" + lights.Length +
            " values=" + string.Join(
                ";",
                lights.Select(value =>
                    value.name + "/" + value.shadows)));
        Require(lights.Length <= 6 &&
                lights.All(value =>
                    value.shadows == LightShadows.None) &&
                (lights.Length <= 5 ||
                 lights.Count(value =>
                     value.name.Contains(
                         "physical arrival glow",
                         StringComparison.OrdinalIgnoreCase)) == 1),
            "V80 runtime light/shadow budget failed");

        RuntimeAssemblyConsoleV61.ConsoleButton hint =
            console.buttons.Single(value =>
                value != null &&
                value.id == RuntimeAssemblyConsoleV61.ButtonId.Hint);
        Require(hint.touchCollider &&
                console.TryFingertipPressV61(
                    hint.touchCollider.bounds.center),
            "V80 physical Hint button did not fire");
        yield return null;
        Require(ActiveBlueprints(guide) == 1 &&
                level.guidance.PathVisible,
            "V80 Hint did not reveal exactly one target module");
        yield return WaitRealtime(
            Mathf.Max(0.4f, level.explicitHintSeconds) + 0.20f);
        Require(ActiveBlueprints(guide) == 0 &&
                !level.guidance.PathVisible,
            "V80 on-demand target did not return to zero cost");

        level.ResetLevel();
        RuntimePuzzlePiece first =
            level.pieces.Single(value =>
                value && value.sequence == 0);
        RuntimePuzzlePiece second =
            level.pieces.Single(value =>
                value && value.sequence == 1);
        first.ForceSnapForSmoke();
        Require(second.IsAvailable &&
                second.snapDistance >= 0.179f &&
                second.maxAssistAngle >= 179.9f &&
                !second.requiresApproachCone &&
                !second.requiresPlayerFacing,
            "V80 Eiffel Part 02 forgiving profile is invalid");
        Require(second.TryForgivingSnapForSmoke(0.92f, 0.99f) &&
                second.IsSnapped,
            "V80 Eiffel Part 02 failed a real " +
            "165.6mm / near-178-degree release");

        long allocatedBefore =
            GC.GetAllocatedBytesForCurrentThread();
        int frames = 0;
        float elapsed = 0f;
        float worst = 0f;
        while (frames < 120)
        {
            yield return null;
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;
            worst = Mathf.Max(worst, dt);
            frames++;
        }
        long allocated =
            GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        float averageMs =
            elapsed / Mathf.Max(1, frames) * 1000f;
        Require(policy.CompletedWindowsV80 > 0 &&
                averageMs < 33.4f,
            "V80 editor CPU frame window exceeded 30 FPS: " +
            averageMs.ToString("F2") + "ms");
        Debug.Log(
            "[PPVR V80 Smoke] PLAYMODE PASSED · " +
            "Eiffel-Part02=165.6mm/178deg · blueprint=0-at-rest/1-on-hint · " +
            "lights=" + lights.Length + " · shadows=0 · " +
            "idle120-averageMs=" + averageMs.ToString("F2") +
            " · idle120-worstMs=" + (worst * 1000f).ToString("F2") +
            " · mainThreadAllocatedBytes=" + allocated);
        Finish(0);
    }

    static int ActiveBlueprints(RuntimeAssemblyGuideV23 guide) =>
        guide.blueprintModules != null
            ? guide.blueprintModules.Count(value =>
                value && value.activeInHierarchy)
            : 0;

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
        Debug.LogError("[PPVR V80 Smoke] FAILED · " + message);
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
