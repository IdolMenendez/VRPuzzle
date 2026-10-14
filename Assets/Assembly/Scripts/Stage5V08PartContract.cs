using UnityEngine;

/// <summary>
/// Immutable authored metadata for one Stage 5 V08 complete construction
/// module. V08 has exactly one continuous bow-to-stern lower-hull gameplay
/// Part; the remaining Parts are complete upper construction assemblies.
/// </summary>
public sealed class Stage5V08PartContract : MonoBehaviour
{
    public int partIndex;
    public string objectName;
    public string semanticName;
    public int difficultyTier;
    public string assemblySurface;
    public string designedInterface;
    public string sourceCombinedAssetPath;
    public string sourceIndividualAssetPath;
    public string manifestPath;
    public bool completeConstructionModule = true;
    public bool standaloneDisplayReady = true;
    public bool providerVisibleGeometryOnly = true;
    public bool hiddenClosedMatingProxy = true;
    public bool heavyTwoHand;
    public bool continuousFullLengthHullCore;
    public int[] dependencyPartIndices;

    public bool Matches(RuntimePuzzlePiece piece)
    {
        if (!piece || partIndex != piece.sequence + 1) return false;
        if (heavyTwoHand != piece.requiresTwoHands) return false;
        int[] expected = dependencyPartIndices ?? System.Array.Empty<int>();
        int[] actual = piece.prerequisiteSequences ?? System.Array.Empty<int>();
        if (expected.Length != actual.Length) return false;
        for (int index = 0; index < expected.Length; index++)
            if (expected[index] - 1 != actual[index]) return false;
        return completeConstructionModule && standaloneDisplayReady &&
            providerVisibleGeometryOnly && hiddenClosedMatingProxy;
    }
}
