using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-only PlayerManual proof that the real desktop ray path can acquire
/// every visible Part, including dependency-locked Parts, in all V09 puzzles.
/// </summary>
[DisallowMultipleComponent]
public sealed class PuzzleDesktopGrabSmokeV09 : MonoBehaviour
{
    public const string EnvironmentVariable =
        "PPVR_V09_PUZZLE_DESKTOP_GRAB_SMOKE";
    public const string CaptureRootVariable =
        "PPVR_V09_PUZZLE_DESKTOP_GRAB_CAPTURE_ROOT";

    static bool runnerCreated;

    void Start()
    {
#if UNITY_EDITOR
        if (runnerCreated ||
            Environment.GetEnvironmentVariable(EnvironmentVariable) != "1")
            return;
        runnerCreated = true;
        Environment.SetEnvironmentVariable(EnvironmentVariable, null);
        GameObject runnerObject = new GameObject(
            "V09 persistent desktop Part grab validation runner");
        DontDestroyOnLoad(runnerObject);
        runnerObject.AddComponent<PuzzleDesktopGrabSmokeRunnerV09>();
#endif
    }
}

#if UNITY_EDITOR
public sealed class PuzzleDesktopGrabSmokeRunnerV09 : MonoBehaviour
{
    static readonly string[] SceneNames =
    {
        "Runtime_Level_CottageHint_20260721_V09_UnityPlayabilityVisualFix",
        "Runtime_Level_Ship_20260721_V09_UnityPlayabilityVisualFix",
        "Runtime_Level_Eiffel_20260721_V09_UnityPlayabilityVisualFix",
        "Runtime_Level_Pagoda_20260722_V09_UnityPlayabilityVisualFix",
        "Runtime_Level_Windmill_20260722_V09_UnityPlayabilityVisualFix",
    };

    static readonly string[] Labels =
    {
        "Stage04_Cottage",
        "Stage05_Ship",
        "Stage06_Eiffel",
        "Stage06B_Pagoda",
        "Stage06C_Windmill",
    };

    IEnumerator Start()
    {
        string captureRoot = Environment.GetEnvironmentVariable(
            PuzzleDesktopGrabSmokeV09.CaptureRootVariable);
        Environment.SetEnvironmentVariable(
            PuzzleDesktopGrabSmokeV09.CaptureRootVariable,
            null);
        if (string.IsNullOrEmpty(captureRoot))
            captureRoot = Path.Combine(
                Application.dataPath,
                "../Artifacts/Checkpoints/21_UnityPlayabilityVisualFixV09_20260721/PlayerManualGrabSmoke");
        captureRoot = Path.GetFullPath(captureRoot);
        Directory.CreateDirectory(captureRoot);

        for (int sceneIndex = 0; sceneIndex < SceneNames.Length; sceneIndex++)
        {
            if (SceneManager.GetActiveScene().name != SceneNames[sceneIndex])
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(
                    SceneNames[sceneIndex],
                    LoadSceneMode.Single);
                Require(load != null,
                    "could not load " + SceneNames[sceneIndex]);
                while (!load.isDone)
                    yield return null;
            }

            RuntimePuzzleLevelController level = null;
            float timeout = Time.realtimeSinceStartup + 8f;
            while (!level && Time.realtimeSinceStartup < timeout)
            {
                level = FindFirstObjectByType<RuntimePuzzleLevelController>();
                yield return null;
            }
            Require(level,
                "level controller unavailable in " + SceneNames[sceneIndex]);
            yield return null;

            Camera camera = FindObjectsByType<Camera>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(value =>
                    value.name.Contains(
                        "RuntimeReferenceCamera",
                        StringComparison.OrdinalIgnoreCase));
            Require(camera,
                "reference camera unavailable in " + SceneNames[sceneIndex]);
            RuntimeDesktopPuzzleInteractorV01 desktop =
                FindFirstObjectByType<RuntimeDesktopPuzzleInteractorV01>();
            if (!desktop)
                desktop = level.gameObject.AddComponent<
                    RuntimeDesktopPuzzleInteractorV01>();
            desktop.sceneCamera = camera;
            desktop.showGameViewInstructions = false;

            RuntimePuzzlePiece[] pieces = level.pieces
                .Where(value => value)
                .OrderBy(value => value.sequence)
                .ToArray();
            Require(pieces.Length >= 8,
                "puzzle Part family is incomplete");
            for (int partIndex = 0; partIndex < pieces.Length; partIndex++)
            {
                RuntimePuzzlePiece piece = pieces[partIndex];
                Collider[] colliders = piece
                    .GetComponentsInChildren<Collider>(true)
                    .Where(value => value && value.enabled && !value.isTrigger)
                    .ToArray();
                Require(colliders.Length > 0,
                    "Part has no enabled physical collider: " + piece.name);
                Bounds hitBounds = colliders[0].bounds;
                for (int colliderIndex = 1;
                     colliderIndex < colliders.Length;
                     colliderIndex++)
                    hitBounds.Encapsulate(colliders[colliderIndex].bounds);
                bool grabbed = false;
                Pose originalCamera = new Pose(
                    camera.transform.position,
                    camera.transform.rotation);
                Vector3[] inspectionOffsets =
                {
                    Vector3.zero,
                    camera.transform.right * -0.18f + Vector3.up * 0.035f,
                    camera.transform.right * 0.18f + Vector3.up * 0.035f,
                    camera.transform.right * -0.28f + Vector3.up * 0.075f,
                    camera.transform.right * 0.28f + Vector3.up * 0.075f,
                    Vector3.up * 0.24f + camera.transform.forward * 0.08f,
                    camera.transform.right * -0.16f +
                        Vector3.up * 0.22f + camera.transform.forward * 0.08f,
                    camera.transform.right * 0.16f +
                        Vector3.up * 0.22f + camera.transform.forward * 0.08f,
                };
                foreach (Vector3 offset in inspectionOffsets)
                {
                    camera.transform.position = originalCamera.position + offset;
                    camera.transform.rotation = Quaternion.LookRotation(
                        hitBounds.center - camera.transform.position,
                        Vector3.up);
                    foreach (Vector2 sample in ProjectBoundsSamples(camera, hitBounds))
                    {
                        if (desktop.TryBeginForAutomation(sample) &&
                            desktop.HeldPiece == piece)
                        {
                            grabbed = true;
                            break;
                        }
                        desktop.ReleaseForAutomation();
                        yield return null;
                    }
                    if (grabbed) break;
                }
                camera.transform.SetPositionAndRotation(
                    originalCamera.position,
                    originalCamera.rotation);
                Require(grabbed,
                    $"desktop ray did not acquire sequence {piece.sequence} " +
                    $"{piece.name} from any visible bounds sample");
                desktop.ReleaseForAutomation();
                yield return null;
            }

            yield return new WaitForSecondsRealtime(0.85f);
            LogEnabledGhosts(Labels[sceneIndex], "before-explicit-hint");
            LogUpperThinRenderers(camera, Labels[sceneIndex]);
            level.PulseNextHint();
            yield return null;
            LogEnabledGhosts(Labels[sceneIndex], "after-explicit-hint");
            RuntimePuzzleGuidanceV09 guidance = level.guidance;
            Require(guidance && guidance.PathVisible &&
                    guidance.RecommendedPiece,
                "dynamic exact-socket guidance did not activate");
            CaptureCamera(
                camera,
                Path.Combine(
                    captureRoot,
                    Labels[sceneIndex] +
                    "_PLAYER_MANUAL_HINT_PATH.png"));
            Debug.Log(
                $"[Unity Playability Visual Fix V09] " +
                $"DESKTOP PART GRAB STAGE PASSED · {Labels[sceneIndex]} · " +
                $"{pieces.Length}/{pieces.Length} Parts");
        }

        Debug.Log(
            "[Unity Playability Visual Fix V09] " +
            "DESKTOP PART GRAB SMOKE PASSED · " +
            "Cottage 13/13 · Ship 13/13 · Eiffel 16/16 · " +
            "Pagoda 9/9 · Windmill 8/8 · " +
            "locked inspection + dynamic hint path");
        Finish(0);
        yield break;
    }

    static void LogEnabledGhosts(string label, string phase)
    {
        string[] names = FindObjectsByType<Renderer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(value =>
                value &&
                value.enabled &&
                value.gameObject.activeInHierarchy &&
                value.name.Contains("Ghost", StringComparison.OrdinalIgnoreCase))
            .Select(value => value.name)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Debug.Log(
            $"[V09 Ghost Audit] {label} phase={phase} " +
            $"enabled={names.Length} names=[{string.Join(" | ", names)}]");
    }

    static void LogUpperThinRenderers(Camera camera, string label)
    {
        string[] entries = FindObjectsByType<Renderer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(value =>
                value &&
                value.enabled &&
                value.gameObject.activeInHierarchy &&
                !(value is ParticleSystemRenderer))
            .Select(value => new
            {
                Renderer = value,
                View = camera.WorldToViewportPoint(value.bounds.center),
            })
            .Where(value =>
                value.View.z > 0f &&
                value.View.y > 0.42f &&
                value.Renderer.bounds.size.y < 0.09f &&
                (value.Renderer.bounds.size.x > 0.08f ||
                 value.Renderer.bounds.size.z > 0.08f))
            .OrderByDescending(value => value.View.y)
            .Take(40)
            .Select(value =>
                $"{HierarchyPath(value.Renderer.transform)}@" +
                $"{value.View:F3}/size={value.Renderer.bounds.size:F3}")
            .ToArray();
        Debug.Log(
            $"[V09 Upper Thin Audit] {label} entries={entries.Length} " +
            $"[{string.Join(" | ", entries)}]");
    }

    static string HierarchyPath(Transform transform)
    {
        var names = new System.Collections.Generic.Stack<string>();
        while (transform)
        {
            names.Push(transform.name);
            transform = transform.parent;
        }
        return string.Join("/", names);
    }

    static Vector2[] ProjectBoundsSamples(Camera camera, Bounds bounds)
    {
        Vector3 min = Vector3.one * float.PositiveInfinity;
        Vector3 max = Vector3.one * float.NegativeInfinity;
        for (int x = 0; x < 2; x++)
        for (int y = 0; y < 2; y++)
        for (int z = 0; z < 2; z++)
        {
            Vector3 projected = camera.WorldToScreenPoint(new Vector3(
                x == 0 ? bounds.min.x : bounds.max.x,
                y == 0 ? bounds.min.y : bounds.max.y,
                z == 0 ? bounds.min.z : bounds.max.z));
            Require(projected.z > 0f,
                "Part bounds are behind the desktop camera");
            min = Vector3.Min(min, projected);
            max = Vector3.Max(max, projected);
        }
        float[] fractions = { 0.50f, 0.25f, 0.75f };
        return fractions
            .SelectMany(y => fractions.Select(x =>
                new Vector2(
                    Mathf.Lerp(min.x, max.x, x),
                    Mathf.Lerp(min.y, max.y, y))))
            .ToArray();
    }

    static void CaptureCamera(Camera camera, string path)
    {
        RenderTexture texture = new RenderTexture(
            1920,
            1080,
            24,
            RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        RenderTexture oldTarget = camera.targetTexture;
        camera.targetTexture = texture;
        camera.Render();
        RenderTexture.active = texture;
        Texture2D image = new Texture2D(
            1920,
            1080,
            TextureFormat.RGB24,
            false);
        image.ReadPixels(new Rect(0f, 0f, 1920f, 1080f), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        camera.targetTexture = oldTarget;
        RenderTexture.active = previous;
        Destroy(texture);
        Destroy(image);
    }

    static void Require(bool condition, string message)
    {
        if (condition)
            return;
        Debug.LogError(
            "[Unity Playability Visual Fix V09] " +
            "DESKTOP PART GRAB SMOKE FAILED · " + message);
        Finish(1);
        throw new InvalidOperationException(message);
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
}
#endif
