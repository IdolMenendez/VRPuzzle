using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-play fallback for artists and reviewers who start directly from the
/// V20 Selection scene. The production bootstrap remains authoritative when
/// present; this adapter only prevents an apparently clickable exhibit from
/// becoming a dead end in standalone scene play.
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class SelectionStandaloneEntryV20 : MonoBehaviour
{
    public SelectionCarouselController selection;
    public PolishedRuntimeStagePortalV02 portal;
    public string[] levelSceneNames;
    public int[] levelByExhibit;
    public float minimumSceneSeconds = 0.8f;

    float enteredAt;

    void Awake()
    {
        enteredAt = Time.unscaledTime;
    }

    void Update()
    {
#if UNITY_EDITOR
        if (PersistentPuzzleBootstrap.Instance ||
            !selection ||
            !portal ||
            selection.IsTransitioning ||
            selection.IsCommitting ||
            Time.unscaledTime - enteredAt < minimumSceneSeconds)
            return;

        bool pointerConfirm =
            Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame &&
            portal.ScreenPointerTargetsActivationForAutomation(
                Mouse.current.position.ReadValue());
        bool keyboardConfirm =
            Keyboard.current != null &&
            (Keyboard.current.enterKey.wasPressedThisFrame ||
             Keyboard.current.spaceKey.wasPressedThisFrame);
        if (pointerConfirm || keyboardConfirm)
            EnterCenteredExhibit();
#endif
    }

#if UNITY_EDITOR
    public bool EnterCenteredExhibitForAutomation()
    {
        return EnterCenteredExhibit();
    }

    bool EnterCenteredExhibit()
    {
        int exhibit = selection
            ? Mathf.Clamp(
                selection.currentIndex,
                0,
                Mathf.Max(0, selection.exhibits.Length - 1))
            : -1;
        int level =
            levelByExhibit != null &&
            exhibit >= 0 &&
            exhibit < levelByExhibit.Length
                ? levelByExhibit[exhibit]
                : -1;
        if (level < 0 ||
            levelSceneNames == null ||
            level >= levelSceneNames.Length ||
            string.IsNullOrEmpty(levelSceneNames[level]))
        {
            RuntimeAudioRouter.PlayCue(
                RuntimeAudioCue.Error,
                portal && portal.activationTarget
                    ? portal.activationTarget.position
                    : transform.position);
            Debug.LogWarning(
                "[Reliable Exhibit Selection V20] standalone entry rejected · " +
                "exhibit=" + exhibit + " level=" + level);
            return false;
        }

        string sceneName = levelSceneNames[level];
        Debug.Log(
            "[Reliable Exhibit Selection V20] standalone entry accepted · " +
            "exhibit=" + exhibit + " level=" + level +
            " scene=" + sceneName);
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        return true;
    }
#endif
}
