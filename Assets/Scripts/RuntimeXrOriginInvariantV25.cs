using UnityEngine;

/// <summary>
/// Keeps the authored XR rig hierarchy invariant while native OpenXR remains
/// solely responsible for the headset camera pose. Puzzle interaction may move
/// Parts and the assembly turntable, but never the bootstrap or XR Origin.
/// </summary>
[DisallowMultipleComponent]
public sealed class RuntimeXrOriginInvariantV25 : MonoBehaviour
{
    public PersistentPuzzleBootstrap bootstrap;
    [Min(0.00001f)] public float positionTolerance = 0.0005f;
    [Min(0.001f)] public float angleTolerance = 0.05f;

    Pose bootstrapPose;
    Vector3 bootstrapScale;
    Vector3 originLocalPosition;
    Quaternion originLocalRotation;
    Vector3 originLocalScale;
    Vector3 offsetLocalPosition;
    Quaternion offsetLocalRotation;
    Vector3 offsetLocalScale;
    bool captured;
    int correctionCount;

    public int CorrectionCount => correctionCount;

    void Start()
    {
        if (!bootstrap)
            bootstrap = GetComponent<PersistentPuzzleBootstrap>();
        CaptureInvariant();
    }

    void LateUpdate()
    {
        if (!captured)
            CaptureInvariant();
        EnforceInvariant();
    }

    public void BeginInteraction()
    {
        if (!captured)
            CaptureInvariant();
        EnforceInvariant();
    }

    public void EndInteraction()
    {
        EnforceInvariant();
    }

    public void CaptureInvariant()
    {
        if (!bootstrap || !bootstrap.xrOrigin)
            return;
        bootstrapPose = new Pose(
            bootstrap.transform.position,
            bootstrap.transform.rotation);
        bootstrapScale = bootstrap.transform.localScale;
        Transform origin = bootstrap.xrOrigin.transform;
        originLocalPosition = origin.localPosition;
        originLocalRotation = origin.localRotation;
        originLocalScale = origin.localScale;
        Transform offset = bootstrap.xrOrigin.CameraFloorOffsetObject
            ? bootstrap.xrOrigin.CameraFloorOffsetObject.transform
            : null;
        if (offset)
        {
            offsetLocalPosition = offset.localPosition;
            offsetLocalRotation = offset.localRotation;
            offsetLocalScale = offset.localScale;
        }
        captured = true;
    }

    public bool EnforceInvariant()
    {
        if (!captured || !bootstrap || !bootstrap.xrOrigin)
            return false;
        bool corrected = false;
        Transform persistent = bootstrap.transform;
        if (Vector3.Distance(
                persistent.position,
                bootstrapPose.position) > positionTolerance ||
            Quaternion.Angle(
                persistent.rotation,
                bootstrapPose.rotation) > angleTolerance ||
            Vector3.Distance(
                persistent.localScale,
                bootstrapScale) > positionTolerance)
        {
            persistent.SetPositionAndRotation(
                bootstrapPose.position,
                bootstrapPose.rotation);
            persistent.localScale = bootstrapScale;
            corrected = true;
        }
        Transform origin = bootstrap.xrOrigin.transform;
        if (Vector3.Distance(
                origin.localPosition,
                originLocalPosition) > positionTolerance ||
            Quaternion.Angle(
                origin.localRotation,
                originLocalRotation) > angleTolerance ||
            Vector3.Distance(
                origin.localScale,
                originLocalScale) > positionTolerance)
        {
            origin.SetLocalPositionAndRotation(
                originLocalPosition,
                originLocalRotation);
            origin.localScale = originLocalScale;
            corrected = true;
        }
        Transform offset = bootstrap.xrOrigin.CameraFloorOffsetObject
            ? bootstrap.xrOrigin.CameraFloorOffsetObject.transform
            : null;
        if (offset &&
            (Vector3.Distance(
                 offset.localPosition,
                 offsetLocalPosition) > positionTolerance ||
             Quaternion.Angle(
                 offset.localRotation,
                 offsetLocalRotation) > angleTolerance ||
             Vector3.Distance(
                 offset.localScale,
                 offsetLocalScale) > positionTolerance))
        {
            offset.SetLocalPositionAndRotation(
                offsetLocalPosition,
                offsetLocalRotation);
            offset.localScale = offsetLocalScale;
            corrected = true;
        }
        if (corrected)
        {
            correctionCount++;
            Debug.LogWarning(
                "[Personally Puzzled V25] Restored invariant XR rig " +
                "hierarchy without touching the native tracked camera.");
        }
        return corrected;
    }
}
