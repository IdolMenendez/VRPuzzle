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
public sealed class PolishedRuntimeFlowSmokeProbeV02 : MonoBehaviour
{
    IEnumerator Start()
    {
#if UNITY_EDITOR
        string smoke = Environment.GetEnvironmentVariable("PPVR_FLOW_SMOKE");
        string soak = Environment.GetEnvironmentVariable("PPVR_SOAK_SECONDS");
        if (smoke == "1")
        {
            Environment.SetEnvironmentVariable("PPVR_FLOW_SMOKE", null);
            yield return RunFullFlow();
        }
        if (float.TryParse(soak, out float seconds) && seconds > 0f)
            yield return RunSoak(seconds);
#else
        yield break;
#endif
    }

#if UNITY_EDITOR
    IEnumerator RunFullFlow()
    {
        yield return WaitForBootstrap(12f);
        PersistentPuzzleBootstrap bootstrap = PersistentPuzzleBootstrap.Instance;
        Require(bootstrap, "bootstrap unavailable");
        SaveFileSnapshot saveSnapshot =
            SaveFileSnapshot.Capture(bootstrap.SavePath);
        bool passed = false;
        try
        {
            bootstrap.Save.highestUnlockedLevel = 2;
            yield return WaitForStage(
                RuntimePuzzleStage.Splash,
                12f);
            yield return WaitForAudioStage(
                bootstrap,
                RuntimePuzzleStage.Splash);
            ValidateSceneResidency(bootstrap);

            bootstrap.EnterSelection();
            yield return WaitForStage(
                RuntimePuzzleStage.Selection,
                12f);
            yield return WaitForAudioStage(
                bootstrap,
                RuntimePuzzleStage.Selection);
            ValidateSceneResidency(bootstrap);
            yield return ExerciseQuadraticSelectionSwap();

            int[] expected = { 13, 13, 16 };
            for (int levelIndex = 0; levelIndex < expected.Length; levelIndex++)
            {
                bootstrap.BeginLevel(levelIndex);
                RuntimePuzzleStage stage =
                    LevelStage(levelIndex);
                yield return WaitForStage(stage, 12f);
                yield return WaitForAudioStage(
                    bootstrap,
                    stage);
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
                            value.prerequisiteSequences.Length == 0) == 4,
                        "Eiffel level lost its four nonlinear opening feet");
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

                bootstrap.EnterCollection();
                yield return WaitForStage(
                    RuntimePuzzleStage.Collection,
                    12f);
                yield return WaitForAudioStage(
                    bootstrap,
                    RuntimePuzzleStage.Collection);
                ValidateSceneResidency(bootstrap);
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
                bootstrap.Save.highestUnlockedLevel == 2 &&
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
                "[Polished Full Game V02] PLAY FLOW SMOKE PASSED · " +
                "Stage1 Logo→Stage2 selection→Stage3 quadratic arc swap→" +
                "Stage4 13-Part cottage + Stage6 physical lamp hint→" +
                "Stage5 latest PASS V08 13-Part whole-hull heavy lift→" +
                "separate nonlinear 16-Part Eiffel→Stage7 completion/free-play→" +
                "Stage8 V07 archive→selection · load-before-unload · " +
                "1.45s BGM crossfades · all BGM/SFX routed · save-v1 roundtrip · no legacy ritual");
            UnityEditor.EditorApplication.Exit(0);
        }
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
            $"[Polished Full Game V02] SOAK PASSED · " +
            $"realtimeSeconds={Time.realtimeSinceStartup - started:F1} · " +
            $"completeLoops={transitions} · " +
            $"memoryMB={UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576f:F1}");
        UnityEditor.EditorApplication.Exit(0);
    }

    static void CompleteCollectionForSmoke()
    {
        CollectionArchiveRitualV06Controller v06 =
            FindFirstObjectByType<CollectionArchiveRitualV06Controller>();
        Require(v06, "Stage 8 V07 collection ritual unavailable");
        Require(
            !FindFirstObjectByType<CollectionRitualController>(),
            "legacy low-detail archive ritual was loaded");
        v06.BeginRitualForSmoke();
        v06.AdvanceRitualForSmoke(1f);
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
        AudioClip expected = ExpectedMusic(audio, stage);
        Require(expected, "authored BGM missing for " + stage);
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
                "\"highestUnlockedLevel\": 2",
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
        return levelIndex == 0
            ? RuntimePuzzleStage.Cottage
            : levelIndex == 1
                ? RuntimePuzzleStage.Ship
                : RuntimePuzzleStage.Eiffel;
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
        while ((!PersistentPuzzleBootstrap.Instance ||
                PersistentPuzzleBootstrap.Instance.CurrentStage != stage ||
                PersistentPuzzleBootstrap.Instance.IsTransitioning) &&
               Time.realtimeSinceStartup - started < timeout)
            yield return null;
        Require(
            PersistentPuzzleBootstrap.Instance &&
            PersistentPuzzleBootstrap.Instance.CurrentStage == stage &&
            !PersistentPuzzleBootstrap.Instance.IsTransitioning,
            "stage timeout: " + stage);
        yield return null;
    }

    static void Require(UnityEngine.Object value, string message)
    {
        if (!value)
            throw new InvalidOperationException(
                "[Polished Full Game V02 Smoke] " + message);
    }

    static void Require(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(
                "[Polished Full Game V02 Smoke] " + message);
    }
#endif
}
