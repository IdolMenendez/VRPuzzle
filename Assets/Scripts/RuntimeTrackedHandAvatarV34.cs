using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

/// <summary>
/// V34 visual-only hand stabilizer. Interaction continues to read the raw
/// XRHandSubsystem poses in RuntimePuzzleHandInteractor; this component only
/// smooths the authored skinned mesh after the skeleton driver has consumed
/// the current Dynamic hand sample.
/// </summary>
[DefaultExecutionOrder(1000)]
public sealed class RuntimeTrackedHandAvatarV34 : MonoBehaviour
{
    public bool leftHand;
    public XRHandTrackingEvents trackingEvents;
    public XRHandSkeletonDriver skeletonDriver;
    public SkinnedMeshRenderer authoredHandRenderer;
    public RuntimePuzzleHandInteractor rawInteractor;
    [Min(1f)] public float positionResponse = 28f;
    [Min(1f)] public float rotationResponse = 32f;
    [Min(0.005f)] public float reacquireSnapDistance = 0.055f;
    public string visualContract =
        "V34 authored 26-joint hand; Dynamic visual smoothing only; raw interaction";

    readonly List<Transform> joints = new List<Transform>();
    readonly List<Vector3> smoothedPositions = new List<Vector3>();
    readonly List<Quaternion> smoothedRotations = new List<Quaternion>();
    bool initialized;
    bool lastVisible;

    void Awake()
    {
        if (trackingEvents)
            trackingEvents.updateType =
                XRHandTrackingEvents.UpdateTypes.Dynamic;
        CacheJoints();
        SetVisible(false);
    }

    void OnEnable()
    {
        initialized = false;
        CacheJoints();
    }

    void LateUpdate()
    {
        bool visible =
            trackingEvents &&
            trackingEvents.handIsTracked &&
            skeletonDriver &&
            authoredHandRenderer;
        if (!visible)
        {
            initialized = false;
            SetVisible(false);
            return;
        }

        CacheJoints();
        float delta = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        float positionAlpha =
            1f - Mathf.Exp(-Mathf.Max(1f, positionResponse) * delta);
        float rotationAlpha =
            1f - Mathf.Exp(-Mathf.Max(1f, rotationResponse) * delta);
        for (int index = 0; index < joints.Count; index++)
        {
            Transform joint = joints[index];
            if (!joint)
                continue;
            Vector3 rawPosition = joint.localPosition;
            Quaternion rawRotation = joint.localRotation;
            bool snap =
                !initialized ||
                Vector3.Distance(
                    smoothedPositions[index],
                    rawPosition) >=
                Mathf.Max(0.005f, reacquireSnapDistance);
            if (snap)
            {
                smoothedPositions[index] = rawPosition;
                smoothedRotations[index] = rawRotation;
            }
            else
            {
                smoothedPositions[index] = Vector3.Lerp(
                    smoothedPositions[index],
                    rawPosition,
                    positionAlpha);
                smoothedRotations[index] = Quaternion.Slerp(
                    smoothedRotations[index],
                    rawRotation,
                    rotationAlpha);
            }
            joint.SetLocalPositionAndRotation(
                smoothedPositions[index],
                smoothedRotations[index]);
        }
        initialized = true;
        SetVisible(true);
    }

    void CacheJoints()
    {
        if (!skeletonDriver ||
            skeletonDriver.jointTransformReferences == null)
            return;
        List<JointToTransformReference> references =
            skeletonDriver.jointTransformReferences;
        if (joints.Count == references.Count && joints.Count > 0)
            return;
        joints.Clear();
        smoothedPositions.Clear();
        smoothedRotations.Clear();
        foreach (JointToTransformReference reference in references)
        {
            Transform joint = reference.jointTransform;
            joints.Add(joint);
            smoothedPositions.Add(
                joint ? joint.localPosition : Vector3.zero);
            smoothedRotations.Add(
                joint ? joint.localRotation : Quaternion.identity);
        }
        initialized = false;
    }

    void SetVisible(bool visible)
    {
        if (!authoredHandRenderer || lastVisible == visible)
            return;
        authoredHandRenderer.enabled = visible;
        lastVisible = visible;
    }
}
