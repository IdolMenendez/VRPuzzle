using System;
using System.Collections;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CompletionDisplayParitySmokeV78 : MonoBehaviour
{
    public const string EnvironmentVariable =
        "PPVR_V78_COMPLETION_DISPLAY_SMOKE";
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
            "V78 completion display parity smoke runner");
        DontDestroyOnLoad(runner);
        runner.AddComponent<CompletionDisplayParitySmokeRunnerV78>();
#endif
    }
}

#if UNITY_EDITOR
public sealed class CompletionDisplayParitySmokeRunnerV78 : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return WaitUntil(() => PersistentPuzzleBootstrap.Instance, 12f,
            "persistent bootstrap");
        PersistentPuzzleBootstrap bootstrap =
            PersistentPuzzleBootstrap.Instance;
        yield return WaitUntil(() =>
            bootstrap.CurrentStage == RuntimePuzzleStage.Splash &&
            !bootstrap.IsTransitioning, 15f, "initial Splash");
        bootstrap.RequestStage(
            RuntimePuzzleStage.Completion, bootstrap.completionScene);
        yield return WaitUntil(() =>
            bootstrap.CurrentStage == RuntimePuzzleStage.Completion &&
            FindFirstObjectByType<RuntimeCompletionDisplayParityV78>() &&
            !bootstrap.IsTransitioning, 15f, "V78 Completion");

        RuntimeCompletionDisplayParityV78 parity =
            FindFirstObjectByType<RuntimeCompletionDisplayParityV78>();
        RuntimeLevelHeroContinuityV04 continuity =
            FindFirstObjectByType<RuntimeLevelHeroContinuityV04>();
        CompletionInteractionController completion =
            FindFirstObjectByType<CompletionInteractionController>();
        RuntimeCompletionShowcaseAlignmentV73 alignment =
            FindFirstObjectByType<RuntimeCompletionShowcaseAlignmentV73>();
        Require(parity && continuity && completion && alignment,
            "V78 completion runtime bindings are incomplete");
        Require(!continuity.presentationMaterial,
            "V78 completion still forces the low single gold material");
        Require(!FindFirstObjectByType<RuntimeCompletionPresentationV23>(),
            "V78 legacy completion UI presentation remains active");
        Require(!FindObjectsByType<Canvas>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None).Any(),
            "V78 Completion still has an active Canvas");
        Require(!FindObjectsByType<TextMesh>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None).Any(label =>
                    label && label.name !=
                        "V75 return-selection unscaled brass label"),
            "V78 Completion still has active non-return text UI");

        string[] names =
        {
            "CLOCKMAKER COTTAGE", "MASTER SHIP", "EIFFEL TOWER",
            "GOLDEN PAGODA", "DUTCH WINDMILL",
        };
        for (int index = 0; index < names.Length; index++)
        {
            Require(parity.ApplyLevelAndFit(index),
                "V78 could not fit " + names[index]);
            yield return null;
            yield return null;
            Require(parity.AppliedLevelIndex == index &&
                    continuity.AppliedLevelIndex == index &&
                    Mathf.Abs(parity.LastSupportGap -
                        parity.contactClearance) <= 0.001f,
                "V78 support gap failed for " + names[index] +
                ": " + parity.LastSupportGap.ToString("F6"));
            Transform visual =
                RuntimeCompletionDisplayParityV78.CurrentVisual(continuity);
            Require(visual &&
                    parity.LastAppliedPaletteBindings > 0 &&
                    visual.GetComponentsInChildren<Renderer>(true)
                        .Any(renderer => renderer && renderer.enabled) &&
                    visual.GetComponentsInChildren<Renderer>(true)
                        .Where(renderer => renderer && renderer.enabled)
                        .SelectMany(renderer => renderer.sharedMaterials)
                        .Where(material => material).Distinct().Count() >= 1,
                "V78 Selection-matched visible model is missing for " +
                names[index]);
            Require(!visual.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer && renderer.enabled)
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material &&
                        material.HasProperty("_EmissionColor"))
                    .Any(material =>
                    {
                        Color emission =
                            material.GetColor("_EmissionColor");
                        return emission.r >= 0.5f &&
                               emission.g >= 0.5f &&
                               emission.b >= 0.5f;
                    }),
                "V78 white-emission material remains for " + names[index]);
            Debug.Log("[PPVR V78 Completion Smoke] model=" + names[index] +
                " gap=" + parity.LastSupportGap.ToString("F6") +
                " paletteBindings=" + parity.LastAppliedPaletteBindings +
                " materials=selection-matched");
        }

        float yBefore = completion.heroModel.position.y;
        float rotated = completion.ApplyAutoRotationForSmoke(0.25f);
        Require(rotated > 0f &&
                Mathf.Abs(completion.heroModel.position.y - yBefore) <= 0.0001f,
            "V78 initial free rotation broke pedestal seating");
        Debug.Log("[PPVR V78 Completion Smoke] PLAYMODE PASSED · " +
            "five-selection-matched-PBR-models · support=5/5 · active-ui=0 · " +
            "physical-return-only · initial-rotation-stable");
        Finish(0);
    }

    static IEnumerator WaitUntil(
        Func<bool> condition, float seconds, string label)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (!condition() && Time.realtimeSinceStartup < deadline)
            yield return null;
        Require(condition(), "Timed out waiting for " + label);
    }

    static void Require(bool condition, string message)
    {
        if (condition)
            return;
        Debug.LogError("[PPVR V78 Completion Smoke] FAILED · " + message);
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
