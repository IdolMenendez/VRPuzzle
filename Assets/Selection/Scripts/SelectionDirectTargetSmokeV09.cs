using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Editor-only PlayerManual proof that twenty non-sequential exhibit requests
/// each animate only the requested semantic target into the central slot.
/// </summary>
[DisallowMultipleComponent]
public sealed class SelectionDirectTargetSmokeV09 : MonoBehaviour
{
    public const string EnvironmentVariable =
        "PPVR_V09_SELECTION_DIRECT_SMOKE";

    public SelectionCarouselController selection;
    public SelectionExhibitInputV09 input;

    IEnumerator Start()
    {
#if UNITY_EDITOR
        if (Environment.GetEnvironmentVariable(EnvironmentVariable) != "1")
            yield break;
        Environment.SetEnvironmentVariable(EnvironmentVariable, null);
        if (!selection)
            selection = GetComponent<SelectionCarouselController>();
        if (!input)
            input = GetComponent<SelectionExhibitInputV09>();
        yield return null;

        int[] requests =
        {
            4, 1, 5, 2, 0, 3, 1, 4, 2, 5,
            0, 4, 3, 1, 5, 2, 4, 0, 3, 5,
        };
        for (int request = 0; request < requests.Length; request++)
        {
            int targetIndex = requests[request];
            SelectionExhibitTargetV09 target =
                Array.Find(
                    input.targets,
                    value => value && value.exhibitIndex == targetIndex);
            Require(target,
                "missing semantic target for exhibit " + targetIndex);
            Collider collider = target.GetComponent<Collider>();
            Require(collider,
                "missing stable hit collider for exhibit " + targetIndex);
            Vector3 screen = input.sceneCamera.WorldToScreenPoint(
                collider.bounds.center);
            Require(screen.z > 0f,
                "target is behind the Selection camera: " + targetIndex);
            bool focused = input.TryFocusScreenPointForAutomation(
                new Vector2(screen.x, screen.y));
            Require(focused,
                $"request {request + 1} did not focus target {targetIndex}");

            float timeout = Time.realtimeSinceStartup + 4f;
            while (selection.IsTransitioning &&
                   Time.realtimeSinceStartup < timeout)
                yield return null;
            Require(!selection.IsTransitioning,
                "Selection direct transition timed out");
            Require(selection.currentIndex == targetIndex,
                $"request {request + 1} expected {targetIndex} " +
                $"but focused {selection.currentIndex}");
            Require(selection.CurrentExhibit &&
                    Vector3.Distance(
                        selection.CurrentExhibit.position,
                        selection.centralAnchor.position) <= 0.002f,
                "focused exhibit did not finish at the central anchor");
            yield return null;
        }

        Debug.Log(
            "[Unity Playability Visual Fix V09] " +
            "SELECTION DIRECT TARGET SMOKE PASSED · " +
            "20/20 non-sequential semantic requests");
        Finish(0);
#else
        yield break;
#endif
    }

#if UNITY_EDITOR
    static void Require(bool condition, string message)
    {
        if (!condition)
        {
            Debug.LogError(
                "[Unity Playability Visual Fix V09] " +
                "SELECTION DIRECT TARGET SMOKE FAILED · " + message);
            Finish(1);
            throw new InvalidOperationException(message);
        }
    }

    static void Require(UnityEngine.Object value, string message)
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
