using UnityEngine;

/// <summary>
/// Serialized provenance contract for the V31 Selection ship. The display is
/// assembled from the exact thirteen semantic meshes used by the playable
/// Ship puzzle; it is not the superseded one-piece look-alike.
/// </summary>
[DisallowMultipleComponent]
public sealed class SelectionAssembledShipV31 : MonoBehaviour
{
    public string sourceAssetPath;
    public Transform[] semanticParts;
    public int mappedLevel = 1;
    public float solvedLongestDimension = 0.205f;
}
