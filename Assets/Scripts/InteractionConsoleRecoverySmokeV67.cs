using System;
using System.Collections;
using UnityEngine;

/// <summary>Unity PlayMode regression for the V67 tabletop console.</summary>
[DisallowMultipleComponent]
public sealed class InteractionConsoleRecoverySmokeV67 : MonoBehaviour
{
    public const string EnvironmentVariable =
        "PPVR_V67_INTERACTION_CONSOLE_SMOKE";
    static bool created;

    void Start()
    {
#if UNITY_EDITOR
        if (created || Environment.GetEnvironmentVariable(EnvironmentVariable) != "1")
            return;
        created = true;
        Environment.SetEnvironmentVariable(EnvironmentVariable, null);
        GameObject runner = new GameObject("V67 interaction console smoke runner");
        DontDestroyOnLoad(runner);
        runner.AddComponent<InteractionConsoleRecoverySmokeRunnerV67>();
#endif
    }
}

#if UNITY_EDITOR
public sealed class InteractionConsoleRecoverySmokeRunnerV67 : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return WaitUntil(() => PersistentPuzzleBootstrap.Instance, 12f,
            "persistent bootstrap");
        PersistentPuzzleBootstrap bootstrap = PersistentPuzzleBootstrap.Instance;
        yield return WaitUntil(() =>
            bootstrap.CurrentStage == RuntimePuzzleStage.Splash &&
            FindFirstObjectByType<SplashWorkshopController>() &&
            !bootstrap.IsTransitioning,
            15f, "initial Splash handoff");
        bootstrap.Save.highestUnlockedLevel = 4;
        bootstrap.BeginLevel(0);
        yield return WaitUntil(() =>
            FindFirstObjectByType<RuntimePuzzleLevelController>() &&
            FindFirstObjectByType<RuntimeAssemblyConsoleV61>(), 15f,
            "Cottage level and V67 console");

        RuntimePuzzleLevelController level =
            FindFirstObjectByType<RuntimePuzzleLevelController>();
        RuntimeAssemblyConsoleV61 console =
            FindFirstObjectByType<RuntimeAssemblyConsoleV61>();
        RuntimeAudioConductor audio = RuntimeAudioRouter.Conductor;
        Require(level && console && audio && audio.effects,
            "V67 console/audio runtime wiring unavailable");
        Require(console.buttons != null && console.buttons.Length == 3,
            "V67 runtime console buttons missing");

        RuntimeAssemblyConsoleV61.ConsoleButton hint =
            Array.Find(console.buttons,
                value => value.id == RuntimeAssemblyConsoleV61.ButtonId.Hint);
        RuntimeAssemblyConsoleV61.ConsoleButton reset =
            Array.Find(console.buttons,
                value => value.id == RuntimeAssemblyConsoleV61.ButtonId.Reset);
        RuntimeAssemblyConsoleV61.ConsoleButton home =
            Array.Find(console.buttons,
                value => value.id == RuntimeAssemblyConsoleV61.ButtonId.Home);
        Require(hint != null && reset != null && home != null,
            "V67 runtime console action ids missing");

        RuntimePuzzlePiece.HintsEnabledV59 = false;
        int hintsBefore = level.HintsUsed;
        bool hintPressed = console.TryFingertipPressV61(
            hint.touchCollider.bounds.center);
        yield return null;
        Require(hintPressed && RuntimePuzzlePiece.HintsEnabledV59 &&
                level.HintsUsed == hintsBefore + 1,
            "V67 Hint did not enable guidance and pulse the next exact hint");
        Require(audio.effects.isPlaying,
            "V67 Hint/Touch did not start the effects AudioSource");
        Require(!console.TryFingertipPressV61(hint.touchCollider.bounds.center),
            "V67 held fingertip repeated the same button");
        console.TryFingertipPressV61(
            hint.touchCollider.bounds.center + Vector3.up * 0.20f);

        yield return new WaitForSecondsRealtime(0.65f);
        Require(console.TryFingertipPressV61(reset.touchCollider.bounds.center),
            "V67 Reset button did not fire");
        yield return null;
        Require(audio.effects.isPlaying,
            "V67 Reset Touch cue did not start the effects AudioSource");
        console.TryFingertipPressV61(
            reset.touchCollider.bounds.center + Vector3.up * 0.20f);

        yield return new WaitForSecondsRealtime(0.65f);
        Require(console.TryFingertipPressV61(home.touchCollider.bounds.center),
            "V67 Home button did not fire");
        yield return WaitUntil(() =>
            PersistentPuzzleBootstrap.Instance &&
            PersistentPuzzleBootstrap.Instance.CurrentStage == RuntimePuzzleStage.Selection,
            15f, "Home transition to Selection");

        Debug.Log("[PPVR V67 Console Smoke] PLAYMODE PASSED · " +
            "hint=immediate · hintCount=+1 · touchAudio=hint/reset · " +
            "held-touch-latch=exclusive · reset=works · home=selection");
        Finish(0);
    }

    static IEnumerator WaitUntil(Func<bool> condition, float seconds, string label)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (!condition() && Time.realtimeSinceStartup < deadline)
            yield return null;
        Require(condition(), "Timed out waiting for " + label);
    }

    static void Require(bool condition, string message)
    {
        if (condition) return;
        Debug.LogError("[PPVR V67 Console Smoke] FAILED · " + message);
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
