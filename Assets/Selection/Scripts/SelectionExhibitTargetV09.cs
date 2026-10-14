using UnityEngine;

/// <summary>
/// Stable semantic hit target for one Selection exhibit. Candidate targets
/// focus their own index; the central target remains owned by the enter gate.
/// </summary>
[DisallowMultipleComponent]
public sealed class SelectionExhibitTargetV09 : MonoBehaviour
{
    public SelectionCarouselController carousel;
    public int exhibitIndex = -1;
    [Tooltip(
        "V31 opt-in. When assigned, directly targeting a model means " +
        "'build this' rather than leaving the user after a carousel swap.")]
    public SelectionDirectPuzzleIntentV31 directPuzzleIntentV31;
    [Tooltip(
        "V38 opt-in. A pinch on a side exhibit only moves it to center. " +
        "A later fresh pinch on that centered exhibit enters its puzzle.")]
    public bool centerOnlyEntersPuzzleV38;

    // V61 #2/#6: when true, focusing a CENTERED exhibit never enters the level
    // directly (that caused "I looked at it and it entered by itself"). It only
    // arms the 3D confirm plaque; SelectionEnterConfirmV61 performs the actual
    // enter when the plaque is deliberately touched. Side exhibits still just
    // center. Set by the V61 builder on every exhibit.
    public static bool RequireConfirmPlaqueV61;
    public static SelectionExhibitTargetV09 PendingConfirmTargetV61 { get; private set; }

    public static void ArmPendingConfirmV61(SelectionExhibitTargetV09 target)
    {
        PendingConfirmTargetV61 = target;
    }

    public static void ClearPendingConfirmV61()
    {
        PendingConfirmTargetV61 = null;
    }

    // Called by SelectionEnterConfirmV61 when the confirm plaque is touched.
    public bool ConfirmEnterV61()
    {
        if (directPuzzleIntentV31 && exhibitIndex >= 0)
            return directPuzzleIntentV31.RequestEnterExhibit(exhibitIndex);
        return false;
    }

    public bool IsCurrent =>
        carousel && carousel.currentIndex == exhibitIndex;

    public bool RequestFocus()
    {
        if (directPuzzleIntentV31)
        {
            if (centerOnlyEntersPuzzleV38 &&
                !IsCurrent)
                return carousel &&
                    exhibitIndex >= 0 &&
                    carousel.BeginSwapToIndex(exhibitIndex);
            // V61: a focus on the centered exhibit arms the confirm plaque
            // instead of entering; only the plaque enters.
            if (RequireConfirmPlaqueV61)
            {
                ArmPendingConfirmV61(this);
                return true;
            }
            return directPuzzleIntentV31.RequestEnterExhibit(
                exhibitIndex);
        }
        return carousel &&
               exhibitIndex >= 0 &&
               carousel.BeginSwapToIndex(exhibitIndex);
    }
}
