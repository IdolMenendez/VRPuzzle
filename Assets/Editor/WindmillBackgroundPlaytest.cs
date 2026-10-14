using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batch-only entry point: never opens or focuses an editor window.
public static class WindmillBackgroundPlaytest
{
    public const string Evidence = "Artifacts/Checkpoints/177_WindmillHandsOn_20260910";
    public static void Run() => Launch("start");
    public static void Resume() => Launch("resume");
    public static void ValidateRegression()
    {
        LatestProjectTools.Validate();
        foreach(var entry in EditorBuildSettings.scenes)
        {
            if(!entry.enabled) continue;
            EditorSceneManager.OpenScene(entry.path);
            foreach(var level in UnityEngine.Object.FindObjectsByType<RuntimePuzzleLevelController>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(!level.keyboardShortcutsEnabled) throw new InvalidOperationException("Release keyboard default changed: "+entry.path);
        }
        WindmillStudyBuilder.Validate();
        Debug.Log("[Background Input] INDEPENDENT REGRESSION PASSED - release keyboard defaults, references, font and isolated study");
    }
    static void Launch(string phase)
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Background playtest requires -batchmode.");
        WindmillStudyBuilder.Validate();
        Directory.CreateDirectory(Evidence);
        var flow = UnityEngine.Object.FindFirstObjectByType<WindmillStudyDirector>();
        flow.saveOverride = Path.GetFullPath(Path.Combine(Evidence,"input-progress.json"));
        flow.inputEnabled = true;
        Environment.SetEnvironmentVariable("PPVR_BACKGROUND_PLAYTEST",phase);
        EditorApplication.isPlaying = true;
    }
}
