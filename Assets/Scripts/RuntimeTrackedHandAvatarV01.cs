using UnityEngine;
using UnityEngine.XR.Hands;

/// <summary>
/// Runtime-only presentation contract for the project's authored, skinned hand.
/// PICO supplies OpenXR/XR Hands joint poses; XRHandSkeletonDriver deforms this
/// mesh. No PICO sample/default hand renderer is required or instantiated.
/// </summary>
public sealed class RuntimeTrackedHandAvatarV01 : MonoBehaviour
{
    public bool leftHand;
    public XRHandTrackingEvents trackingEvents;
    public XRHandSkeletonDriver skeletonDriver;
    public SkinnedMeshRenderer authoredHandRenderer;
    public RuntimePuzzleHandInteractor interactor;
    public string sourceAsset;

    bool lastVisible;

    void Awake()
    {
        SetVisible(false);
    }

    void LateUpdate()
    {
        bool visible =
            trackingEvents &&
            trackingEvents.handIsTracked &&
            skeletonDriver &&
            authoredHandRenderer;
        SetVisible(visible);
    }

    void SetVisible(bool visible)
    {
        if (!authoredHandRenderer || lastVisible == visible)
            return;
        authoredHandRenderer.enabled = visible;
        lastVisible = visible;
    }
}
