using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class RuntimePuzzleLevelController : MonoBehaviour
{
    public int levelIndex;
    public string levelTitle;
    public RuntimePuzzlePiece[] pieces;
    public RuntimeHintLamp hintLamp;
    public RuntimePuzzleGuidanceV09 guidance;
    public TextMesh progressLabel;
    public TextMesh accessibilityLabel;
    [Min(1.6f)] public float completionCeremonySeconds = 2.15f;

    [Header("V79 completion without in-level UI")]
    [Tooltip(
        "Keeps the earned completion cue/haptic and stage handoff, but never " +
        "shows an in-level COMPLETE label, progress text, guide plaque or FX.")]
    public bool suppressInLevelCompletionUiV79;
    [Tooltip(
        "Physical guide/plaque roots that remain useful during assembly but " +
        "must disappear as soon as the final Part snaps.")]
    public GameObject[] completionUiRootsV79;

    [Header("V61 in-level completion FX")]
    [Tooltip(
        "V61 #4: played the instant the last piece snaps, so completion has an " +
        "explicit in-level reward (particles + glow + cue + 完成 label) before " +
        "the Completion stage. Wired by the V61 builder.")]
    public Transform completionFxRootV61;
    public ParticleSystem completionBurstV61;
    public Light completionGlowV61;
    public TextMesh completionLabelV61;
    // Fired the instant the level becomes complete (last piece snapped).
    public System.Action OnLevelCompletedV61;

    [Header("Optional player-requested assistance")]
    [Tooltip("Duration of the explicit H-key / physical-lamp cue. Preserved scenes keep the two-second default; later scene revisions may author a quieter value.")]
    [Min(0.4f)] public float explicitHintSeconds = 2f;
    [Tooltip("Whether an explicit request also draws the short Part-to-socket path. Ordinary pickup never uses this path.")]
    public bool explicitHintShowsPath = true;
    [Header("V80 event-driven status")]
    public bool eventDrivenStatusV80;
    // Alternate input directors can own shortcuts without double-handling H/R.
    public bool keyboardShortcutsEnabled = true;

    int snapped;
    int hintsUsed;
    float startedAt;
    bool completed;
    int lastAccessibilitySignatureV80 = int.MinValue;

    public int SnappedCount => snapped;
    public int TotalCount => pieces != null ? pieces.Length : 0;
    public int HintsUsed => hintsUsed;
    public bool Completed => completed;
    public bool CompletionUiSuppressedV79 =>
        suppressInLevelCompletionUiV79 &&
        (completionUiRootsV79 == null ||
         completionUiRootsV79.All(value =>
             !value || !value.activeInHierarchy)) &&
        (!completionFxRootV61 ||
         !completionFxRootV61.gameObject.activeInHierarchy) &&
        (!completionLabelV61 ||
         !completionLabelV61.gameObject.activeInHierarchy);

    void Start()
    {
        if (pieces == null || pieces.Length == 0) pieces = GetComponentsInChildren<RuntimePuzzlePiece>(true);
        foreach (RuntimePuzzlePiece piece in pieces) piece.level = this;
        if (hintLamp) hintLamp.level = this;
        if (!guidance) guidance = GetComponent<RuntimePuzzleGuidanceV09>();
        if (guidance) guidance.level = this;
        ResetLevel();
    }

    void Update()
    {
        if (keyboardShortcutsEnabled && Keyboard.current != null)
        {
            if (!PersonallyPuzzledExperienceModeState.IsPlayerManual &&
                Keyboard.current.nKey.wasPressedThisFrame)
            {
                RuntimePuzzlePiece next = pieces.FirstOrDefault(value => value && !value.IsSnapped && value.IsAvailable);
                if (next) next.ForceSnapForSmoke();
            }
            if (Keyboard.current.hKey.wasPressedThisFrame) PulseNextHint();
            if (Keyboard.current.rKey.wasPressedThisFrame) ResetLevel();
        }
        if (accessibilityLabel && PersistentPuzzleBootstrap.Instance)
        {
            var save = PersistentPuzzleBootstrap.Instance.Save;
            int signature =
                (save.singleHandMode ? 1 : 0) |
                save.handedness << 1 |
                save.heightMode << 3;
            if (!eventDrivenStatusV80 ||
                signature != lastAccessibilitySignatureV80)
            {
                lastAccessibilitySignatureV80 = signature;
                accessibilityLabel.text =
                    (save.singleHandMode
                        ? "ONE HAND"
                        : "TWO HANDS") + " · " +
                    (save.handedness == 0
                        ? "RIGHT DOMINANT"
                        : "LEFT DOMINANT") + " · " +
                    (save.heightMode == 0
                        ? "STANDING"
                        : "SEATED");
            }
        }
    }

    public void ResetLevel()
    {
        StopAllCoroutines();
        snapped = hintsUsed = 0;
        completed = false;
        startedAt = Time.unscaledTime;
        if (suppressInLevelCompletionUiV79 &&
            completionUiRootsV79 != null)
        {
            foreach (GameObject root in completionUiRootsV79)
                if (root) root.SetActive(true);
        }
        foreach (RuntimePuzzlePiece piece in pieces) if (piece) piece.ResetForPlay();
        foreach (RuntimePuzzlePiece piece in pieces) if (piece) piece.RefreshAvailability();
        guidance?.RefreshRecommendation();
        RefreshProgress();
    }

    public void NotifySnapped(RuntimePuzzlePiece piece)
    {
        snapped = pieces.Count(value => value && value.IsSnapped);
        foreach (RuntimePuzzlePiece candidate in pieces) if (candidate) candidate.RefreshAvailability();
        guidance?.RefreshRecommendation();
        RefreshProgress();
        if (completed || snapped < TotalCount) return;
        completed = true;
        OnLevelCompletedV61?.Invoke();
        if (suppressInLevelCompletionUiV79)
            PlayCompletionFeedbackWithoutUiV79(piece);
        else
            PlayCompletionFxV61();
        if (Application.isPlaying) StartCoroutine(CompleteAfterCeremony());
        else if (!suppressInLevelCompletionUiV79 && progressLabel)
            progressLabel.text = levelTitle + " · COMPLETE · ★★★";
    }

    void PlayCompletionFeedbackWithoutUiV79(RuntimePuzzlePiece finalPiece)
    {
        HideCompletionUiV79();
        Vector3 at = finalPiece
            ? finalPiece.TargetPosition
            : transform.position;
        RuntimeAudioRouter.PlayCue(RuntimeAudioCue.Complete, at);
        RuntimeHaptics.PulseAll(0.55f, 0.12f);
    }

    public void HideCompletionUiV79()
    {
        if (completionFxRootV61)
            completionFxRootV61.gameObject.SetActive(false);
        if (completionBurstV61)
            completionBurstV61.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        if (completionGlowV61)
            completionGlowV61.intensity = 0f;
        if (completionLabelV61)
            completionLabelV61.gameObject.SetActive(false);
        if (progressLabel)
            progressLabel.gameObject.SetActive(false);
        if (accessibilityLabel)
            accessibilityLabel.gameObject.SetActive(false);
        foreach (RuntimeAssemblyGuideV22 guide in
                 GetComponents<RuntimeAssemblyGuideV22>())
            if (guide && guide.instructionLabel)
                guide.instructionLabel.gameObject.SetActive(false);
        foreach (RuntimeAssemblyGuideV23 guide in
                 GetComponents<RuntimeAssemblyGuideV23>())
            if (guide && guide.instructionLabel)
                guide.instructionLabel.gameObject.SetActive(false);
        if (completionUiRootsV79 == null)
            return;
        foreach (GameObject root in completionUiRootsV79)
            if (root) root.SetActive(false);
    }

    // V61 #4: explicit in-level completion reward (particles + glow bloom + cue
    // + 完成 label + haptic) the instant the last piece snaps.
    void PlayCompletionFxV61()
    {
        Vector3 at = completionFxRootV61
            ? completionFxRootV61.position
            : transform.position;
        if (completionFxRootV61 && !completionFxRootV61.gameObject.activeSelf)
            completionFxRootV61.gameObject.SetActive(true);
        if (completionBurstV61) completionBurstV61.Play(true);
        if (completionLabelV61) completionLabelV61.text = "完成\nCOMPLETE";
        RuntimeAudioRouter.PlayCue(RuntimeAudioCue.Complete, at);
        RuntimeHaptics.PulseAll(0.55f, 0.12f);
        if (Application.isPlaying && completionGlowV61)
            StartCoroutine(BloomGlowV61());
    }

    IEnumerator BloomGlowV61()
    {
        float t = 0f;
        while (t < 1.4f && completionGlowV61)
        {
            t += Time.unscaledDeltaTime;
            completionGlowV61.intensity = 2.4f * Mathf.Exp(-t * 1.4f);
            yield return null;
        }
        if (completionGlowV61) completionGlowV61.intensity = 0f;
    }

    public void PulseNextHint()
    {
        RuntimePuzzlePiece next = pieces.FirstOrDefault(value => value && !value.IsSnapped && value.IsAvailable);
        if (!next) return;
        hintsUsed++;
        float seconds = Mathf.Max(0.4f, explicitHintSeconds);
        next.ShowExactHint(seconds);
        if (explicitHintShowsPath)
            guidance?.ShowHintPath(next, seconds);
        RuntimeAssemblyGuideV23 assemblyGuide =
            GetComponent<RuntimeAssemblyGuideV23>();
        assemblyGuide?.ShowExplicitHintV25(seconds);
        RuntimeAudioRouter.PlayCue(RuntimeAudioCue.GhostReveal, next.TargetPosition);
    }

    IEnumerator CompleteAfterCeremony()
    {
        if (!suppressInLevelCompletionUiV79 && progressLabel)
            progressLabel.text = levelTitle + " · COMPLETE · ★★★";
        // Keep the final complete model in the player's hands long enough for
        // the last snap, haptic bloom and progress plaque to read as one earned
        // beat before the shared scene handoff begins.
        yield return new WaitForSecondsRealtime(
            Mathf.Max(1.6f, completionCeremonySeconds));
        PersistentPuzzleBootstrap.Instance?.CompleteCurrentLevel(hintsUsed, Time.unscaledTime - startedAt);
    }

    void RefreshProgress()
    {
        if (progressLabel) progressLabel.text = levelTitle + "  " + snapped + "/" + Mathf.Max(1, TotalCount);
    }

    public void ApplyReferencePoseForEditor(int snappedPieces)
    {
        snapped = 0;
        for (int i = 0; i < pieces.Length; i++)
        {
            if (!pieces[i]) continue;
            if (i < snappedPieces) pieces[i].ForceSnapForSmoke();
            else if (i == snappedPieces) pieces[i].ApplyNearSocketReferencePose();
            else pieces[i].ApplyAuthoredStartPose();
        }
        snapped = pieces.Count(value => value && value.IsSnapped);
        RefreshProgress();
    }
}
