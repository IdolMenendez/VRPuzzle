using UnityEngine;

/// <summary>
/// PlayerManual deliberately separates browsing from entering a level. The
/// first confirmation focuses the physical exhibit and lights its plaque; a
/// second confirmation after a short inspection dwell commits it. AutoDemo
/// and validation smoke retain their deterministic single-step flow.
/// </summary>
public sealed class SelectionPreviewGateV07 : MonoBehaviour
{
    public SelectionCarouselController selection;
    public Light previewLight;
    public TextMesh physicalStatusPlaque;
    [Min(0.35f)] public float minimumInspectionSeconds = 0.85f;
    [Min(2f)] public float previewTimeoutSeconds = 9f;
    [Tooltip("V20 opt-in: a centered exhibit enters on one explicit confirmation. Candidate exhibits still require a separate focus action.")]
    public bool singleConfirmWhenCentered;

    int primedIndex = -1;
    float primedAt = -1f;
    int observedIndex = -1;
    string restingText;
    bool commitVisualActive;

    public bool IsPreviewPrimed => primedIndex >= 0;
    public bool IsReadyToCommit =>
        IsPreviewPrimed &&
        Time.unscaledTime - primedAt >= minimumInspectionSeconds;

    void Awake()
    {
        if (!selection)
            selection = GetComponent<SelectionCarouselController>();
        observedIndex = selection ? selection.currentIndex : -1;
        restingText = physicalStatusPlaque ? physicalStatusPlaque.text : string.Empty;
        ResetPreview();
    }

    void Update()
    {
        if (!selection)
            return;
        if (selection.currentIndex != observedIndex || selection.IsTransitioning)
        {
            observedIndex = selection.currentIndex;
            ResetPreview();
            return;
        }
        if (IsPreviewPrimed &&
            Time.unscaledTime - primedAt > previewTimeoutSeconds)
        {
            ResetPreview();
            return;
        }
        if (previewLight)
        {
            float pulse = IsPreviewPrimed
                ? 0.72f + Mathf.Sin(Time.unscaledTime * 3.4f) * 0.14f
                : 0f;
            previewLight.intensity = pulse;
        }
        if (physicalStatusPlaque && IsPreviewPrimed)
            physicalStatusPlaque.text = commitVisualActive
                ? "ENTERING WORKSHOP…"
                : IsReadyToCommit
                    ? "FOCUSED\nTOUCH AGAIN TO ENTER"
                    : "FOCUSING…";
    }

    public bool RequestCommit(int exhibitIndex)
    {
        if (!PersonallyPuzzledExperienceModeState.IsPlayerManual)
            return true;
        if (!selection || selection.IsTransitioning || selection.IsCommitting)
            return false;
        if (primedIndex != exhibitIndex)
        {
            if (singleConfirmWhenCentered)
                return true;
            primedIndex = exhibitIndex;
            primedAt = Time.unscaledTime;
            observedIndex = exhibitIndex;
            RuntimeAudioRouter.PlayCue(
                RuntimeAudioCue.SocketBreath,
                selection.CurrentExhibit
                    ? selection.CurrentExhibit.position
                    : transform.position);
            return false;
        }
        return IsReadyToCommit;
    }

    public void NotifyCommitStarted()
    {
        commitVisualActive = true;
        if (physicalStatusPlaque)
            physicalStatusPlaque.text = "ENTERING WORKSHOP…";
        if (previewLight)
            previewLight.intensity = 1.05f;
    }

    public void ResetPreview()
    {
        primedIndex = -1;
        primedAt = -1f;
        commitVisualActive = false;
        if (previewLight)
            previewLight.intensity = 0f;
        if (physicalStatusPlaque)
            physicalStatusPlaque.text = restingText;
    }
}
