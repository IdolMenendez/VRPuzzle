using System.Collections;
using UnityEngine;

/// <summary>
/// Resolves Selection's ambiguous pinch semantics. Open-hand swipes continue
/// to browse the carousel, while explicitly targeting any exhibit expresses
/// one intent: enter that exhibit's assembly. A side exhibit first follows
/// the authored carousel motion to center, then commits automatically.
/// </summary>
[DefaultExecutionOrder(-95)]
[DisallowMultipleComponent]
public sealed class SelectionDirectPuzzleIntentV31 : MonoBehaviour
{
    public SelectionCarouselController selection;
    public PolishedRuntimeStagePortalV02 portal;
    public SelectionStandaloneEntryV20 standaloneEntry;
    public bool directTargetEntersPuzzle = true;
    [Min(0f)] public float centeredSettleSeconds = 0.08f;

    Coroutine pendingEntry;
    int pendingExhibit = -1;

    public bool EntryPending => pendingEntry != null;
    public int PendingExhibit => pendingExhibit;
#if UNITY_EDITOR
    public bool SuppressStageEntryForAutomation { get; set; }
    public int CompletedAutomationEntries { get; private set; }
#endif

    void OnDisable()
    {
        if (pendingEntry != null)
            StopCoroutine(pendingEntry);
        pendingEntry = null;
        pendingExhibit = -1;
    }

    public bool RequestEnterExhibit(int exhibitIndex)
    {
        if (!directTargetEntersPuzzle ||
            !selection ||
            !portal ||
            pendingEntry != null ||
            selection.IsTransitioning ||
            selection.IsCommitting ||
            selection.exhibits == null ||
            exhibitIndex < 0 ||
            exhibitIndex >= selection.exhibits.Length ||
            !selection.exhibits[exhibitIndex])
            return false;

        pendingExhibit = exhibitIndex;
        pendingEntry = StartCoroutine(
            FocusThenEnter(exhibitIndex));
        return true;
    }

    IEnumerator FocusThenEnter(int exhibitIndex)
    {
        if (selection.currentIndex != exhibitIndex)
        {
            if (!selection.BeginSwapToIndex(exhibitIndex))
            {
                ClearPending();
                yield break;
            }
            while (selection &&
                   selection.IsTransitioning)
                yield return null;
        }

        float deadline =
            Time.unscaledTime +
            Mathf.Max(0f, centeredSettleSeconds);
        while (Time.unscaledTime < deadline)
            yield return null;

        if (selection &&
            portal &&
            selection.currentIndex == exhibitIndex)
        {
            bool accepted;
#if UNITY_EDITOR
            if (SuppressStageEntryForAutomation)
            {
                accepted = true;
                CompletedAutomationEntries++;
            }
            else if (!PersistentPuzzleBootstrap.Instance &&
                standaloneEntry)
                accepted =
                    standaloneEntry
                        .EnterCenteredExhibitForAutomation();
            else
#endif
                accepted =
                    portal.TryConfirmCurrentSelectionForV31(
                        "v31-direct-exhibit-" + exhibitIndex);
            Debug.Log(
                "[Personally Puzzled V31 Selection Intent] " +
                "direct-enter exhibit=" + exhibitIndex +
                " accepted=" + accepted,
                this);
        }
        ClearPending();
    }

    void ClearPending()
    {
        pendingEntry = null;
        pendingExhibit = -1;
    }
}
