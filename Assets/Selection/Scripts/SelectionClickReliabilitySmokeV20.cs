using System;
using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// Editor-only proof that every V20 exhibit can be focused through its visible
/// display and that a centered exhibit accepts one explicit confirmation.
/// </summary>
[DisallowMultipleComponent]
public sealed class SelectionClickReliabilitySmokeV20 : MonoBehaviour
{
    public const string EnvironmentVariable =
        "PPVR_V20_SELECTION_CLICK_SMOKE";

    public SelectionCarouselController selection;
    public SelectionExhibitInputV09 input;
    public PolishedRuntimeStagePortalV02 portal;
    public SelectionPreviewGateV07 previewGate;
    public SelectionStandaloneEntryV20 standaloneEntry;

    IEnumerator Start()
    {
#if UNITY_EDITOR
        if (Environment.GetEnvironmentVariable(EnvironmentVariable) != "1")
            yield break;
        Environment.SetEnvironmentVariable(EnvironmentVariable, null);
        yield return null;

        Require(selection && input && portal && previewGate && standaloneEntry,
            "V20 Selection click bindings are incomplete");
        Require(previewGate.singleConfirmWhenCentered,
            "one-confirm centered-exhibit contract is disabled");

        int[] focusOrder = { 1, 2, 3, 4, 5, 0 };
        foreach (int index in focusOrder)
        {
            SelectionExhibitTargetV09 target = input.targets.Single(
                value => value && value.exhibitIndex == index);
            BoxCollider collider = target.GetComponent<BoxCollider>();
            Require(collider && collider.enabled,
                "exhibit " + index + " has no enabled hit volume");
            Vector3 screen = input.sceneCamera.WorldToScreenPoint(
                collider.bounds.center);
            Require(screen.z > 0f,
                "exhibit " + index + " hit volume is behind the camera");
            Require(input.TryFocusScreenPointForAutomation(
                    new Vector2(screen.x, screen.y)),
                "visible display center did not focus exhibit " + index);
            Debug.Log(
                "[Reliable Exhibit Selection V20] focus-start · requested=" +
                index + " current=" + selection.currentIndex +
                " transitioning=" + selection.IsTransitioning +
                " enabled=" + selection.enabled +
                " active=" + selection.gameObject.activeInHierarchy +
                " referenceT=" + selection.referenceTransitionT);
            float deadline = Time.realtimeSinceStartup + 4f;
            while (selection.IsTransitioning &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Require(!selection.IsTransitioning &&
                    selection.currentIndex == index,
                "exhibit " + index + " did not settle in the center · " +
                "current=" + selection.currentIndex +
                " transitioning=" + selection.IsTransitioning +
                " enabled=" + selection.enabled +
                " active=" + selection.gameObject.activeInHierarchy +
                " referenceT=" + selection.referenceTransitionT);
        }

        Bounds visual = Encapsulate(selection.CurrentExhibit
            .GetComponentsInChildren<Renderer>(true)
            .Where(value => value && value.enabled &&
                !(value is ParticleSystemRenderer))
            .ToArray());
        Rect screenRect = ProjectBounds(input.sceneCamera, visual);
        Camera portalCamera = portal.InteractionCameraForAutomation;
        Vector3 anchorScreen = portalCamera
            ? portalCamera.WorldToScreenPoint(portal.activationTarget.position)
            : Vector3.zero;
        Debug.Log(
            "[Reliable Exhibit Selection V20] center-diagnostic · " +
            "inputCamera=" + (input.sceneCamera ? input.sceneCamera.name : "none") +
            " portalCamera=" + (portalCamera ? portalCamera.name : "none") +
            " mainCamera=" + (Camera.main ? Camera.main.name : "none") +
            " screen=" + Screen.width + "x" + Screen.height +
            " visualRect=" + screenRect +
            " anchorScreen=" + anchorScreen +
            " radius=" + portal.activationScreenRadiusX + "x" +
            portal.activationScreenRadiusY);
        Vector2[] normalizedSamples =
        {
            new Vector2(0.50f, 0.50f),
            new Vector2(0.50f, 0.22f),
            new Vector2(0.50f, 0.78f),
            new Vector2(0.26f, 0.50f),
            new Vector2(0.74f, 0.50f),
            new Vector2(0.30f, 0.28f),
            new Vector2(0.70f, 0.28f),
            new Vector2(0.30f, 0.72f),
            new Vector2(0.70f, 0.72f),
        };
        foreach (Vector2 sample in normalizedSamples)
        {
            Vector2 point = new Vector2(
                Mathf.Lerp(screenRect.xMin, screenRect.xMax, sample.x),
                Mathf.Lerp(screenRect.yMin, screenRect.yMax, sample.y));
            Require(portal.ScreenPointerTargetsActivationForAutomation(point),
                "center exhibit visible-area sample missed: " + sample);
        }

        previewGate.ResetPreview();
        Require(previewGate.RequestCommit(selection.currentIndex),
            "centered exhibit still requires an invisible dwell/re-click");
        Debug.Log(
            "[Reliable Exhibit Selection V20] CLICK RELIABILITY SMOKE PASSED · " +
            "6/6 semantic exhibit centers · 9/9 centered-display area samples · " +
            "one explicit centered confirmation enters");
        if (Environment.GetEnvironmentVariable(
                SelectionStandaloneEntryArrivalSmokeV20.EnvironmentVariable) == "1")
        {
            Require(standaloneEntry.EnterCenteredExhibitForAutomation(),
                "standalone centered exhibit did not start its mapped level");
            yield break;
        }
        Finish(0);
#else
        yield break;
#endif
    }

#if UNITY_EDITOR
    static Rect ProjectBounds(Camera camera, Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z),
        };
        Vector2 low = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 high = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (Vector3 corner in corners)
        {
            Vector3 screen = camera.WorldToScreenPoint(corner);
            Require(screen.z > 0f, "center exhibit bounds cross behind camera");
            Vector2 point = new Vector2(screen.x, screen.y);
            low = Vector2.Min(low, point);
            high = Vector2.Max(high, point);
        }
        return Rect.MinMaxRect(low.x, low.y, high.x, high.y);
    }

    static Bounds Encapsulate(Renderer[] renderers)
    {
        Require(renderers.Length > 0, "center exhibit has no visible renderers");
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);
        return bounds;
    }

    static void Require(bool condition, string message)
    {
        if (condition)
            return;
        Debug.LogError(
            "[Reliable Exhibit Selection V20] CLICK RELIABILITY SMOKE FAILED · " +
            message);
        Finish(2);
        throw new InvalidOperationException(message);
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
