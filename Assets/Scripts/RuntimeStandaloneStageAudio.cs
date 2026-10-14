using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Starts the correct BGM when a versioned stage scene is opened on its own.
/// In the additive full game the persistent bootstrap remains authoritative.
/// </summary>
public sealed class RuntimeStandaloneStageAudio : MonoBehaviour
{
    public RuntimePuzzleStage stage;
    public RuntimeAudioConductor conductor;
    [Tooltip(
        "When a versioned level is played directly in the Unity Editor, " +
        "enable its authored reference camera and desktop controls. The " +
        "persistent Bootstrap camera remains authoritative in the full game.")]
    public bool provideEditorPreviewFallback = true;

    void Awake()
    {
#if UNITY_EDITOR
        EnsureEditorPreviewCamera();
#endif
    }

    void Start()
    {
        if (!conductor) conductor = GetComponent<RuntimeAudioConductor>();
        if (!conductor) return;
        if (PersistentPuzzleBootstrap.Instance &&
            PersistentPuzzleBootstrap.Instance.audioConductor &&
            PersistentPuzzleBootstrap.Instance.audioConductor != conductor)
        {
            conductor.gameObject.SetActive(false);
            return;
        }
        conductor.ApplyVolumes(0.85f, 0.72f, 0.88f);
        conductor.CrossfadeTo(stage);
    }

#if UNITY_EDITOR
    void EnsureEditorPreviewCamera()
    {
        if (!provideEditorPreviewFallback ||
            PersistentPuzzleBootstrap.Instance)
            return;

        Camera activeCamera = FindObjectsByType<Camera>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(value =>
                value &&
                value.enabled &&
                value.targetTexture == null);
        if (activeCamera)
            return;

        Camera fallback = FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(value =>
                value &&
                value.name.Contains(
                    "RuntimeReferenceCamera",
                    StringComparison.OrdinalIgnoreCase));
        if (!fallback)
        {
            Debug.LogError(
                "[Personally Puzzled Direct Play] No authored reference " +
                "camera is available for standalone Editor preview.",
                this);
            return;
        }

        fallback.gameObject.SetActive(true);
        fallback.targetTexture = null;
        fallback.enabled = true;
        fallback.tag = "MainCamera";

        AudioListener listener = FindObjectsByType<AudioListener>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(value => value && value.enabled);
        if (!listener)
            fallback.gameObject.AddComponent<AudioListener>();

        RuntimeDesktopPuzzleInteractorV01 interactor =
            FindFirstObjectByType<RuntimeDesktopPuzzleInteractorV01>();
        if (!interactor)
            interactor =
                fallback.gameObject.AddComponent<
                    RuntimeDesktopPuzzleInteractorV01>();
        interactor.sceneCamera = fallback;

        RuntimeDesktopHeadNavigatorV01 navigator =
            FindFirstObjectByType<RuntimeDesktopHeadNavigatorV01>();
        if (!navigator)
            navigator =
                fallback.gameObject.AddComponent<
                    RuntimeDesktopHeadNavigatorV01>();
        navigator.rigRoot = fallback.transform;
        navigator.sceneCamera = fallback;
        navigator.neutralEye = fallback.transform.localPosition;
        navigator.ResetToNeutralForAutomation();

        Debug.Log(
            "[Personally Puzzled Direct Play] Standalone Editor camera, " +
            "AudioListener and desktop controls activated: " +
            fallback.name);
    }
#endif
}
