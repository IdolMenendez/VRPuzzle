using System;
using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// Editor-only proof for V31 Selection semantics: targeting the centered
/// exhibit enters it without browsing away, while targeting a candidate
/// focuses that exact exhibit and then enters it automatically.
/// </summary>
[DisallowMultipleComponent]
public sealed class SelectionDirectPuzzleIntentSmokeV31 : MonoBehaviour
{
    public const string EnvironmentVariable =
        "PPVR_V31_SELECTION_DIRECT_INTENT_SMOKE";

    public SelectionCarouselController selection;
    public SelectionExhibitInputV09 input;
    public SelectionDirectPuzzleIntentV31 directIntent;

    IEnumerator Start()
    {
#if UNITY_EDITOR
        if (Environment.GetEnvironmentVariable(
                EnvironmentVariable) != "1")
            yield break;
        Environment.SetEnvironmentVariable(
            EnvironmentVariable,
            null);
        Require(
            selection &&
            input &&
            directIntent,
            "V31 direct-intent smoke bindings are incomplete");
        directIntent.SuppressStageEntryForAutomation = true;
        yield return null;

        int centeredIndex = selection.currentIndex;
        int completedBefore =
            directIntent.CompletedAutomationEntries;
        Require(
            RequestVisibleTarget(centeredIndex),
            "centered exhibit pinch was not routed to direct entry");
        yield return WaitForEntry();
        Require(
            selection.currentIndex == centeredIndex,
            "centered exhibit pinch browsed to a different model");
        Require(
            directIntent.CompletedAutomationEntries ==
            completedBefore + 1,
            "centered exhibit did not complete one direct-entry intent");

        int candidateIndex =
            Enumerable.Range(0, selection.exhibits.Length)
                .First(value => value != centeredIndex);
        completedBefore =
            directIntent.CompletedAutomationEntries;
        Require(
            RequestVisibleTarget(candidateIndex),
            "candidate exhibit pinch was not routed to direct entry");
        yield return WaitForEntry();
        Require(
            selection.currentIndex == candidateIndex,
            "candidate pinch did not focus the requested model");
        Require(
            directIntent.CompletedAutomationEntries ==
            completedBefore + 1,
            "candidate exhibit did not auto-enter after centering");

        Debug.Log(
            "[Swan Fresh Session V31] " +
            "SELECTION DIRECT INTENT SMOKE PASSED · " +
            "center pinch enters without browsing · " +
            "candidate pinch focuses exact target then enters");
        Finish(0);
#else
        yield break;
#endif
    }

#if UNITY_EDITOR
    bool RequestVisibleTarget(int exhibitIndex)
    {
        SelectionExhibitTargetV09 target =
            input.targets.Single(value =>
                value &&
                value.exhibitIndex == exhibitIndex);
        Collider collider =
            target.GetComponent<Collider>();
        Require(
            collider,
            "semantic target has no collider: " +
            exhibitIndex);
        Vector3 screen =
            input.sceneCamera.WorldToScreenPoint(
                collider.bounds.center);
        Require(
            screen.z > 0f,
            "semantic target is behind the camera: " +
            exhibitIndex);
        return input.TryFocusScreenPointForAutomation(
            new Vector2(screen.x, screen.y));
    }

    IEnumerator WaitForEntry()
    {
        float deadline =
            Time.realtimeSinceStartup + 5f;
        while (directIntent.EntryPending &&
               Time.realtimeSinceStartup < deadline)
            yield return null;
        Require(
            !directIntent.EntryPending,
            "direct-entry intent timed out");
    }

    static void Require(
        bool condition,
        string message)
    {
        if (condition)
            return;
        Debug.LogError(
            "[Swan Fresh Session V31] " +
            "SELECTION DIRECT INTENT SMOKE FAILED · " +
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
#endif
}
