using System.Collections;
using UnityEngine;

/// <summary>
/// Keeps the Selection carousel's physical exhibit, engraved identity plaque,
/// playable-level mapping and archive return focused on the same authored
/// model.  This is deliberately world-space scene dressing, not a virtual UI.
/// </summary>
public sealed class RuntimeSelectionContinuityV05 : MonoBehaviour
{
    public SelectionCarouselController selection;
    public TextMesh physicalIdentityPlaque;
    public int[] levelByExhibit;
    public string[] exhibitIdentity;
    public bool restoreArchivedLevelToCenter = true;
    public Color playableColor = new Color(0.72f, 0.91f, 1f, 1f);
    public Color lockedColor = new Color(1f, 0.68f, 0.24f, 1f);
    public Color previewColor = new Color(0.62f, 0.61f, 0.55f, 1f);

    int appliedIndex = int.MinValue;
    int appliedHighestUnlocked = int.MinValue;

    IEnumerator Start()
    {
        yield return null;
        PersistentPuzzleBootstrap bootstrap = PersistentPuzzleBootstrap.Instance;
        float timeout = Time.realtimeSinceStartup + 3f;
        while (bootstrap &&
               bootstrap.CurrentStage != RuntimePuzzleStage.Selection &&
               Time.realtimeSinceStartup < timeout)
        {
            yield return null;
        }

        if (restoreArchivedLevelToCenter && bootstrap && selection &&
            bootstrap.PreviousStage == RuntimePuzzleStage.Collection)
        {
            int exhibit = ExhibitForLevel(bootstrap.CurrentLevelIndex);
            if (exhibit >= 0)
                selection.SelectIndexImmediately(exhibit);
        }
        Refresh(true);
    }

    void Update()
    {
        Refresh(false);
    }

    public void RefreshImmediately()
    {
        Refresh(true);
    }

    void Refresh(bool force)
    {
        if (!selection || !physicalIdentityPlaque ||
            selection.exhibits == null || selection.exhibits.Length == 0)
            return;

        int index = Mathf.Clamp(
            selection.currentIndex,
            0,
            selection.exhibits.Length - 1);
        PersistentPuzzleBootstrap bootstrap = PersistentPuzzleBootstrap.Instance;
        int highestUnlocked = bootstrap && bootstrap.Save != null
            ? bootstrap.Save.highestUnlockedLevel
            : 0;
        if (!force && index == appliedIndex &&
            highestUnlocked == appliedHighestUnlocked)
            return;

        appliedIndex = index;
        appliedHighestUnlocked = highestUnlocked;
        int level = LevelForExhibit(index);
        string identity = exhibitIdentity != null &&
                          index < exhibitIdentity.Length &&
                          !string.IsNullOrWhiteSpace(exhibitIdentity[index])
            ? exhibitIdentity[index]
            : selection.exhibits[index].name.ToUpperInvariant();

        if (level < 0)
        {
            physicalIdentityPlaque.text = identity + "  ·  FUTURE WORKSHOP";
            physicalIdentityPlaque.color = previewColor;
        }
        else if (level > highestUnlocked)
        {
            physicalIdentityPlaque.text = identity + "  ·  LOCKED";
            physicalIdentityPlaque.color = lockedColor;
        }
        else
        {
            physicalIdentityPlaque.text = identity;
            physicalIdentityPlaque.color = playableColor;
        }
    }

    int LevelForExhibit(int exhibit)
    {
        return levelByExhibit != null &&
               exhibit >= 0 && exhibit < levelByExhibit.Length
            ? levelByExhibit[exhibit]
            : -1;
    }

    int ExhibitForLevel(int level)
    {
        if (levelByExhibit == null)
            return -1;
        for (int index = 0; index < levelByExhibit.Length; index++)
            if (levelByExhibit[index] == level)
                return index;
        return -1;
    }
}
