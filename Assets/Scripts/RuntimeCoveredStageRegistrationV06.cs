using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registers a newly streamed room to the player's retained horizontal
/// room-scale position while the persistent comfort cover is fully opaque.
/// The camera, tracking origin, floor height and head rotation are never
/// changed.  This keeps real 6DoF intact while preventing a close final socket
/// pose from placing the next stage's hero behind or inside the player.
/// </summary>
public sealed class RuntimeCoveredStageRegistrationV06 : MonoBehaviour
{
    public RuntimePuzzleStage stage;
    public Transform contentRoot;
    public Transform authoredReferenceEye;
    public Transform focusAnchor;
    [Min(0f)] public float horizontalDeadZone = 0.025f;
    [Min(0.10f)] public float maximumHorizontalRegistration = 0.58f;
    [Tooltip(
        "V32 opt-in. Rotate the streamed room around its authored eye marker " +
        "so the authored forward direction matches the current headset yaw. " +
        "The XR Origin, camera, floor and head pose remain untouched.")]
    public bool alignYawToCurrentHeadV32;
    [Tooltip(
        "V32 opt-in. Register the authored eye marker exactly to the current " +
        "horizontal head position instead of clamping room-scale displacement.")]
    public bool unlimitedHorizontalRegistrationV32;
    [Tooltip(
        "V33 opt-in. Apply the same covered XZ/yaw registration to every root " +
        "in the streamed stage scene. This keeps environment, lighting, " +
        "interaction systems and puzzle parts in one coherent room.")]
    public bool registerWholeSceneV33;
    [Tooltip(
        "Reachability opt-in. After exact covered registration, move the " +
        "entire streamed room horizontally toward the player. Floor, XR " +
        "Origin, camera and tracked head remain untouched.")]
    [Min(0f)]
    public float bringRoomTowardPlayer;
    [Tooltip(
        "V45 opt-in. Apply the same covered yaw/translation to every puzzle " +
        "Part's cached authored world pose. Later level reset and failed " +
        "release therefore stay registered with the moved room.")]
    public bool migratePuzzleAuthoredPosesV45;

    public bool Applied { get; private set; }
    public Vector3 AppliedTranslation { get; private set; }
    public float AppliedYawDegrees { get; private set; }
    public Vector3 FocusViewportAfterRegistration { get; private set; }
    public int RegisteredRigidbodyCount { get; private set; }
    public int MigratedPuzzlePieceCountV45 { get; private set; }

    public bool ApplyNow(PersistentPuzzleBootstrap bootstrap)
    {
        if (Applied)
            return true;
        if (!bootstrap ||
            bootstrap.CurrentStage != stage ||
            !bootstrap.xrCamera ||
            !contentRoot ||
            !authoredReferenceEye)
            return false;

        RuntimeComfortTransitionV01 comfort = bootstrap.comfortTransition;
        if (comfort && !comfort.IsCovered)
            return false;

        // V49: never sample the head pose for the one-time room registration
        // until the stable-origin policy reports a settled, coherent Floor
        // pose. Registering while the pose is still converging is what made
        // the Selection room appear to swim/follow the head after entering
        // from Splash by pinch. This changes only the moment of registration;
        // XR Origin, camera, floor and head pose remain untouched.
        if (bootstrap.RequireStableSwanOriginV32)
        {
            RuntimeSwanStableOriginV32 stableOrigin =
                bootstrap.GetComponent<RuntimeSwanStableOriginV32>();
            if (stableOrigin && !stableOrigin.CurrentSessionReady)
                return false;
        }

        Transform head = bootstrap.xrCamera.transform;
        bool systemPlacementV32 =
            bootstrap.RequireStableSwanOriginV32 &&
            alignYawToCurrentHeadV32;
        Vector3 referencePosition = authoredReferenceEye.position;
        Quaternion referenceRotation = authoredReferenceEye.rotation;
        Quaternion yawRotation = Quaternion.identity;
        float yawDegrees = 0f;
        if (systemPlacementV32)
        {
            yawRotation = CalculateHorizontalAlignmentV32(
                authoredReferenceEye.rotation,
                head.rotation,
                out yawDegrees);
        }

        Vector3 translation = head.position - referencePosition;
        // Floor and real crouch/stand height stay physical.  Only the incoming
        // room's horizontal authoring registration follows retained room-scale
        // movement, and it is bounded to the intended mostly-stationary play
        // envelope.
        translation.y = 0f;
        if (!systemPlacementV32 ||
            !unlimitedHorizontalRegistrationV32)
        {
            if (translation.magnitude <= horizontalDeadZone)
                translation = Vector3.zero;
            else if (translation.magnitude >
                     maximumHorizontalRegistration)
                translation =
                    translation.normalized *
                    maximumHorizontalRegistration;
        }
        if (systemPlacementV32 &&
            registerWholeSceneV33 &&
            bringRoomTowardPlayer > 0.0001f)
        {
            Vector3 headForward =
                Vector3.ProjectOnPlane(
                    head.forward,
                    Vector3.up);
            if (headForward.sqrMagnitude >
                0.0001f)
            {
                translation -=
                    headForward.normalized *
                    bringRoomTowardPlayer;
            }
        }

        Transform[] registrationRoots =
            RegistrationRoots(systemPlacementV32);
        bool referenceMovesWithRoots =
            Array.Exists(
                registrationRoots,
                root =>
                    root &&
                    (authoredReferenceEye == root ||
                     authoredReferenceEye.IsChildOf(root)));

        // A kinematic Rigidbody keeps its own physics-world pose. Moving only
        // its parent Transform looks correct for the registration frame, then
        // the following physics update can write the old absolute pose back
        // into the child Transform. Capture and explicitly translate every
        // body so the whole incoming room remains registered after FixedUpdate
        // as well as during the opaque-cover frame.
        List<Rigidbody> bodyList = new List<Rigidbody>();
        foreach (Transform root in registrationRoots)
        {
            if (!root)
                continue;
            bodyList.AddRange(
                root.GetComponentsInChildren<Rigidbody>(true));
        }
        Rigidbody[] bodies = bodyList.ToArray();
        Vector3[] bodyPositions = new Vector3[bodies.Length];
        Quaternion[] bodyRotations = new Quaternion[bodies.Length];
        for (int index = 0; index < bodies.Length; index++)
        {
            Rigidbody body = bodies[index];
            if (!body)
                continue;
            bodyPositions[index] = body.position;
            bodyRotations[index] = body.rotation;
        }

        if (systemPlacementV32 &&
            Mathf.Abs(yawDegrees) > 0.0001f)
        {
            foreach (Transform root in registrationRoots)
            {
                if (root)
                    root.RotateAround(
                        referencePosition,
                        Vector3.up,
                        yawDegrees);
            }
        }
        foreach (Transform root in registrationRoots)
        {
            if (root)
                root.position += translation;
        }
        for (int index = 0; index < bodies.Length; index++)
        {
            Rigidbody body = bodies[index];
            if (!body)
                continue;
            body.position =
                referencePosition +
                yawRotation *
                (bodyPositions[index] - referencePosition) +
                translation;
            body.rotation =
                yawRotation *
                bodyRotations[index];
            RegisteredRigidbodyCount++;
        }
        if (migratePuzzleAuthoredPosesV45)
        {
            HashSet<RuntimePuzzlePiece> migrated =
                new HashSet<RuntimePuzzlePiece>();
            foreach (Transform root in registrationRoots)
            {
                if (!root)
                    continue;
                foreach (RuntimePuzzlePiece piece in
                         root.GetComponentsInChildren<
                             RuntimePuzzlePiece>(true))
                {
                    if (!piece || !migrated.Add(piece))
                        continue;
                    piece.ApplyCoveredRegistrationV45(
                        referencePosition,
                        yawRotation,
                        translation);
                    MigratedPuzzlePieceCountV45++;
                }
            }
        }
        if (systemPlacementV32 &&
            !referenceMovesWithRoots)
        {
            authoredReferenceEye.SetPositionAndRotation(
                referencePosition + translation,
                yawRotation * referenceRotation);
        }
        Physics.SyncTransforms();
        AppliedTranslation = translation;
        AppliedYawDegrees = yawDegrees;
        Applied = true;

        if (focusAnchor)
            FocusViewportAfterRegistration = bootstrap.xrCamera
                .WorldToViewportPoint(focusAnchor.position);

        Debug.Log(
            "[Personally Puzzled Handoff V06] covered room registration " +
            $"stage={stage} translation={AppliedTranslation} " +
            $"yaw={AppliedYawDegrees:F3} v32={systemPlacementV32} " +
            $"wholeSceneV33={registerWholeSceneV33} " +
            $"towardPlayer={bringRoomTowardPlayer:F3} " +
            $"roots={registrationRoots.Length} " +
            $"rigidbodies={RegisteredRigidbodyCount} " +
            $"migratedPuzzlePosesV45={MigratedPuzzlePieceCountV45} " +
            $"camera={head.position} reference={authoredReferenceEye.position} " +
            $"focusViewport={FocusViewportAfterRegistration}",
            this);
        return true;
    }

    public bool ReapplySystemPlacementV32(
        PersistentPuzzleBootstrap bootstrap)
    {
        if (!bootstrap ||
            !bootstrap.RequireStableSwanOriginV32 ||
            !alignYawToCurrentHeadV32)
            return false;
        Applied = false;
        AppliedTranslation = Vector3.zero;
        AppliedYawDegrees = 0f;
        FocusViewportAfterRegistration = Vector3.zero;
        RegisteredRigidbodyCount = 0;
        MigratedPuzzlePieceCountV45 = 0;
        return ApplyNow(bootstrap);
    }

    Transform[] RegistrationRoots(bool systemPlacementV32)
    {
        if (!systemPlacementV32 ||
            !registerWholeSceneV33)
            return new[] { contentRoot };

        GameObject[] sceneRoots =
            gameObject.scene.GetRootGameObjects();
        Transform[] roots =
            new Transform[sceneRoots.Length];
        for (int index = 0;
             index < sceneRoots.Length;
             index++)
        {
            roots[index] =
                sceneRoots[index]
                    ? sceneRoots[index].transform
                    : null;
        }
        return roots;
    }

    public static Quaternion CalculateHorizontalAlignmentV32(
        Quaternion authoredEyeRotation,
        Quaternion headRotation,
        out float yawDegrees)
    {
        Vector3 authoredForward = Vector3.ProjectOnPlane(
            authoredEyeRotation * Vector3.forward,
            Vector3.up);
        Vector3 headForward = Vector3.ProjectOnPlane(
            headRotation * Vector3.forward,
            Vector3.up);
        if (authoredForward.sqrMagnitude <= 0.000001f ||
            headForward.sqrMagnitude <= 0.000001f)
        {
            yawDegrees = 0f;
            return Quaternion.identity;
        }
        yawDegrees = Vector3.SignedAngle(
            authoredForward.normalized,
            headForward.normalized,
            Vector3.up);
        return Quaternion.AngleAxis(
            yawDegrees,
            Vector3.up);
    }
}
