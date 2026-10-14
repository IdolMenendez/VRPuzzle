#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Complements the input regression with progression, archive, audio and disk reload.
public sealed class CurrentReleaseFlowSmoke : MonoBehaviour
{
    string savePath;
    byte[] originalSave;
    string originalState;
    bool hadSave;
    bool failed;
    float deadline;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (Environment.GetEnvironmentVariable("PPVR_CURRENT_RELEASE_FLOW") != "1") return;
        Environment.SetEnvironmentVariable("PPVR_CURRENT_RELEASE_FLOW", null);
        var obj = new GameObject("Current release full flow validation");
        DontDestroyOnLoad(obj);
        obj.AddComponent<CurrentReleaseFlowSmoke>();
    }

    void Awake()
    {
        deadline = Time.realtimeSinceStartup + 240;
        Application.logMessageReceived += OnLog;
    }

    void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Assert || type == LogType.Error) failed = true;
    }

    void Update()
    {
        if (failed || Time.realtimeSinceStartup > deadline) Finish(1);
    }

    IEnumerator Start()
    {
        while (!PersistentPuzzleBootstrap.Instance) yield return null;
        var b = PersistentPuzzleBootstrap.Instance;
        savePath = b.SavePath;
        hadSave = File.Exists(savePath);
        originalSave = hadSave ? File.ReadAllBytes(savePath) : null;
        originalState = JsonUtility.ToJson(b.Save);
        yield return WaitStage(b, RuntimePuzzleStage.Splash);
        b.Save.highestUnlockedLevel = 0;
        Array.Clear(b.Save.stars, 0, b.Save.stars.Length);
        b.EnterSelection();
        yield return WaitStage(b, RuntimePuzzleStage.Selection);
        int[] counts = { 13, 13, 10, 9, 8 };
        RuntimePuzzleStage[] stages = { RuntimePuzzleStage.Cottage, RuntimePuzzleStage.Ship,
            RuntimePuzzleStage.Eiffel, RuntimePuzzleStage.Pagoda, RuntimePuzzleStage.Windmill };
        AudioClip previousMusic = null;
        for (int i = 0; i < 5; i++)
        {
            Require(b.Save.highestUnlockedLevel >= i, "progression did not unlock next level");
            b.BeginLevel(i);
            yield return WaitStage(b, stages[i]);
            var level = FindFirstObjectByType<RuntimePuzzleLevelController>();
            Require(level && level.TotalCount == counts[i] && level.levelIndex == i, "part count/stage mismatch");
            var audio = b.audioConductor;
            Require(audio && audio.HasPuzzleMusicPool && audio.CurrentMusicClip != previousMusic,
                "missing or consecutively repeated puzzle music");
            previousMusic = audio.CurrentMusicClip;
            foreach (var part in level.pieces.OrderBy(p => p.sequence)) part.ForceSnapForSmoke();
            Require(level.Completed, "assembly did not complete");
            yield return WaitStage(b, RuntimePuzzleStage.Completion);
            Require(FindFirstObjectByType<CompletionInteractionController>(), "completion display missing");
            Require(b.Save.stars[i] > 0, "completion did not award stars");
            b.EnterCollection();
            yield return WaitStage(b, RuntimePuzzleStage.Collection);
            var archive = FindFirstObjectByType<CollectionArchiveRitualV06Controller>();
            Require(archive, "archive controller missing");
            archive.BeginRitualForSmoke();
            archive.AdvanceRitualForSmoke(1);
            yield return WaitStage(b, RuntimePuzzleStage.Selection);
            Debug.Log("[Current Flow] LEVEL PASSED index=" + i + " parts=" + counts[i] + " completion/archive/return/audio");
        }
        b.SaveNow();
        var disk = JsonUtility.FromJson<PersonallyPuzzledSaveData>(File.ReadAllText(savePath));
        Require(disk.highestUnlockedLevel == 4 && disk.stars.All(s => s > 0), "disk progress missing");
        Array.Clear(b.Save.stars, 0, 5);
        typeof(PersistentPuzzleBootstrap).GetMethod("LoadSave", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(b, null);
        Require(b.Save.stars.SequenceEqual(disk.stars), "save reload mismatch");
        Debug.Log("[Current Flow] COMPLETE PASSED five levels/53 parts/completion/archive/return/audio/save disk reload; input separately tested");
        Finish(0);
    }

    IEnumerator WaitStage(PersistentPuzzleBootstrap b, RuntimePuzzleStage stage)
    {
        float until = Time.realtimeSinceStartup + 20;
        while (b.IsTransitioning || b.CurrentStage != stage || UnityEngine.SceneManagement.SceneManager.sceneCount < 2)
        {
            Require(Time.realtimeSinceStartup < until, "stage timeout " + stage);
            yield return null;
        }
        yield return new WaitForSecondsRealtime(1.6f);
        var audio = b.audioConductor;
        Require(audio && new[] { audio.musicA, audio.musicB }.Any(s => s && s.isPlaying && s.volume > .01f),
            "stage music not playing " + stage);
    }

    static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("[Current Flow] " + message);
    }

    void Finish(int code)
    {
        Application.logMessageReceived -= OnLog;
        // Bootstrap saves again on application quit; restore its in-memory
        // state too so that shutdown cannot overwrite the restored progress.
        if (PersistentPuzzleBootstrap.Instance && originalState != null)
            JsonUtility.FromJsonOverwrite(originalState, PersistentPuzzleBootstrap.Instance.Save);
        if (savePath != null)
        {
            if (hadSave) File.WriteAllBytes(savePath, originalSave);
            else if (File.Exists(savePath)) File.Delete(savePath);
        }
        EditorApplication.Exit(code);
    }
}
#endif
