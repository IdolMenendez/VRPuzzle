using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;

/// <summary>
/// V32 leaves PICO's calibrated Floor space and recenter gesture authoritative.
/// It never calls XRInputSubsystem.TryRecenter. Cold start and lifecycle resume
/// remain behind the persistent comfort cover until the native head pose and
/// Unity camera agree, then the streamed room is registered to the current
/// horizontal head pose without moving the XR Origin.
/// </summary>
[DefaultExecutionOrder(-9175)]
[DisallowMultipleComponent]
public sealed class RuntimeSwanStableOriginV32 : MonoBehaviour
{
    public PersistentPuzzleBootstrap bootstrap;
    public RuntimeSwanOpenXrRuntimeReadyV29 runtimeReady;
    public bool enableSystemFloorRecentering = true;
    public bool returnHomeOnResume = true;
    [Min(4)] public int requiredStableFrames = 12;
    [Min(0.005f)] public float maximumStablePositionDelta = 0.035f;
    [Min(0.1f)] public float maximumStableYawDelta = 2.5f;
    [Min(0.005f)] public float maximumCameraPositionError = 0.035f;
    [Min(0.1f)] public float maximumCameraRotationError = 2.5f;
    [Min(0.5f)] public float minimumPlausibleEyeHeight = 0.95f;
    [Min(1f)] public float maximumPlausibleEyeHeight = 2.35f;
    [Min(1f)] public float recenterFallbackFloorOffset = 1.65f;
    [Tooltip(
        "V49 convergence guard. If the per-frame stability streak keeps being " +
        "interrupted by an ordinary moving head while a coherent Floor pose is " +
        "already readable, accept that current pose after this many seconds so " +
        "cold start deterministically reaches the first stage. The XR Origin, " +
        "camera, floor and head pose are never moved.")]
    [Min(1f)] public float maxRecoverySeconds = 6f;

    Coroutine recoveryWorkflow;
    bool realPicoSwan;
    bool currentSessionReady;
    bool recoveryPending;
    bool applicationPaused;
    bool applicationFocused = true;
    int recoveryGeneration;
    string recoveryReason = "none";
    Vector3 lastStableHeadPosition;
    float lastStableHeadYaw;
    bool hasStableSample;
    int stableFrames;

    public bool CurrentSessionReady => currentSessionReady;
    public bool RecoveryPending => recoveryPending;
    public string RecoveryReason => recoveryReason;

    void Awake()
    {
        Resolve();
        realPicoSwan =
            runtimeReady &&
            runtimeReady.IsRealPicoSwan;
        ApplySystemFloorRecenteringSupport();
        if (!realPicoSwan)
        {
            currentSessionReady = true;
            SetCoverLock(false);
            return;
        }
        ArmRecovery("cold-start");
    }

    void OnEnable()
    {
        ApplySystemFloorRecenteringSupport();
    }

    void Start()
    {
        if (realPicoSwan)
            BeginResumeRecoveryV32("cold-start");
    }

    void OnApplicationFocus(bool focused)
    {
        applicationFocused = focused;
        if (!realPicoSwan)
            return;
        if (!focused)
        {
            ArmRecovery("focus-lost");
            return;
        }
        ApplySystemFloorRecenteringSupport();
        if (!applicationPaused)
            BeginResumeRecoveryV32("focus-return");
    }

    void OnApplicationPause(bool paused)
    {
        applicationPaused = paused;
        if (!realPicoSwan)
            return;
        if (paused)
        {
            ArmRecovery("application-pause");
            return;
        }
        ApplySystemFloorRecenteringSupport();
        if (applicationFocused)
            BeginResumeRecoveryV32("application-resume");
    }

    public bool TryAuthorizeStageRequest(
        RuntimePuzzleStage stage,
        string sceneName)
    {
        if (!realPicoSwan || currentSessionReady)
            return true;
        Debug.Log(
            "[Personally Puzzled V32 Stable Origin] stage-held " +
            stage + " scene=" + sceneName +
            " reason=" + recoveryReason,
            this);
        return false;
    }

    public void BeginResumeRecoveryV32(
        string reason = "explicit-recovery")
    {
        if (!realPicoSwan)
            return;
        if (!recoveryPending)
            ArmRecovery(reason);
        if (applicationPaused ||
            !applicationFocused ||
            recoveryWorkflow != null)
            return;
        int generation = recoveryGeneration;
        recoveryWorkflow =
            StartCoroutine(RecoverSystemFloorSession(
                generation,
                reason));
    }

    void ArmRecovery(string reason)
    {
        recoveryGeneration++;
        recoveryReason = reason;
        currentSessionReady = false;
        recoveryPending = true;
        stableFrames = 0;
        hasStableSample = false;
        if (recoveryWorkflow != null)
        {
            StopCoroutine(recoveryWorkflow);
            recoveryWorkflow = null;
        }
        SetCoverLock(true);
        CancelActiveHandInteractions();
        Debug.Log(
            "[Personally Puzzled V32 Stable Origin] recovery-armed " +
            "generation=" + recoveryGeneration +
            " reason=" + reason,
            this);
    }

    IEnumerator RecoverSystemFloorSession(
        int generation,
        string reason)
    {
        SetCoverLock(true);
        while (generation == recoveryGeneration &&
               (applicationPaused || !applicationFocused))
            yield return null;

        while (generation == recoveryGeneration &&
               (!runtimeReady ||
                !runtimeReady.CurrentSessionReady))
            yield return null;

        float recoveryStartTime = Time.unscaledTime;
        while (generation == recoveryGeneration &&
               stableFrames <
               Mathf.Max(4, requiredStableFrames))
        {
            yield return new WaitForEndOfFrame();
            if (generation != recoveryGeneration)
                yield break;
            if (!runtimeReady.CurrentSessionReady ||
                !TryReadCoherentFloorPose(
                    out Vector3 headPosition,
                    out Quaternion headRotation,
                    out _,
                    out _))
            {
                stableFrames = 0;
                hasStableSample = false;
                continue;
            }

            // V49: an ordinary moving head keeps resetting the streak. Once a
            // coherent Floor pose has been readable for maxRecoverySeconds,
            // accept the current pose so cold start reliably reaches the first
            // stage instead of hanging behind the comfort cover forever.
            if (Time.unscaledTime - recoveryStartTime >=
                Mathf.Max(1f, maxRecoverySeconds))
            {
                Debug.Log(
                    "[Personally Puzzled V32 Stable Origin] " +
                    "V49-timeout-accept generation=" + generation +
                    " reason=" + reason +
                    " elapsed=" +
                    (Time.unscaledTime - recoveryStartTime).ToString("F2") +
                    " stableFrames=" + stableFrames,
                    this);
                break;
            }

            float headYaw = headRotation.eulerAngles.y;
            bool stable =
                !hasStableSample ||
                (Vector3.Distance(
                     headPosition,
                     lastStableHeadPosition) <=
                 Mathf.Max(
                     0.005f,
                     maximumStablePositionDelta) &&
                 Mathf.Abs(
                     Mathf.DeltaAngle(
                         headYaw,
                         lastStableHeadYaw)) <=
                 Mathf.Max(
                     0.1f,
                     maximumStableYawDelta));
            lastStableHeadPosition = headPosition;
            lastStableHeadYaw = headYaw;
            hasStableSample = true;
            stableFrames = stable
                ? stableFrames + 1
                : 1;
        }

        if (generation != recoveryGeneration)
            yield break;

        bool resumingLoadedStage =
            bootstrap &&
            !string.IsNullOrEmpty(
                bootstrap.LoadedStageScene);
        if (resumingLoadedStage &&
            returnHomeOnResume)
        {
            while (generation == recoveryGeneration &&
                   bootstrap.IsTransitioning)
                yield return null;
            if (generation != recoveryGeneration)
                yield break;

            if (bootstrap.CurrentStage !=
                    RuntimePuzzleStage.Splash ||
                bootstrap.LoadedStageScene !=
                    bootstrap.splashScene)
            {
                bootstrap.RequestStage(
                    RuntimePuzzleStage.Splash,
                    bootstrap.splashScene);
                while (generation == recoveryGeneration &&
                       (bootstrap.IsTransitioning ||
                        bootstrap.CurrentStage !=
                            RuntimePuzzleStage.Splash ||
                        bootstrap.LoadedStageScene !=
                            bootstrap.splashScene))
                    yield return null;
            }
            if (generation != recoveryGeneration)
                yield break;
            ReapplyCurrentStagePlacement();
        }

        if (!TryReadCoherentFloorPose(
                out Vector3 readyHead,
                out Quaternion readyRotation,
                out float cameraPositionError,
                out float cameraRotationError))
        {
            stableFrames = 0;
            hasStableSample = false;
            recoveryWorkflow = null;
            BeginResumeRecoveryV32(
                reason + "-final-pose-retry");
            yield break;
        }

        currentSessionReady = true;
        recoveryPending = false;
        recoveryReason = reason;
        recoveryWorkflow = null;
        SetCoverLock(false);

        Debug.Log(
            "[Personally Puzzled V32 Stable Origin] " +
            "PPVR_V32_SYSTEM_FLOOR_READY generation=" +
            generation +
            " reason=" + reason +
            " head=" + readyHead.ToString("F3") +
            " yaw=" +
            readyRotation.eulerAngles.y.ToString("F2") +
            " cameraPositionError=" +
            cameraPositionError.ToString("F4") +
            " cameraRotationError=" +
            cameraRotationError.ToString("F3") +
            " floor=True allowRecentering=" +
            ReadAllowRecentering(),
            this);

        if (resumingLoadedStage &&
            bootstrap &&
            bootstrap.comfortTransition)
        {
            yield return bootstrap.comfortTransition
                .HoldCovered();
            yield return bootstrap.comfortTransition
                .Reveal();
        }
    }

    bool TryReadCoherentFloorPose(
        out Vector3 headPosition,
        out Quaternion headRotation,
        out float cameraPositionError,
        out float cameraRotationError)
    {
        headPosition = Vector3.zero;
        headRotation = Quaternion.identity;
        cameraPositionError = float.PositiveInfinity;
        cameraRotationError = float.PositiveInfinity;
        if (!bootstrap ||
            !bootstrap.xrOrigin ||
            !bootstrap.xrCamera ||
            bootstrap.xrOrigin.RequestedTrackingOriginMode !=
                XROrigin.TrackingOriginMode.Floor ||
            Mathf.Abs(
                bootstrap.xrOrigin.CameraYOffset) >
                0.001f)
            return false;

        InputDevice head =
            InputDevices.GetDeviceAtXRNode(XRNode.Head);
        bool tracked =
            head.isValid &&
            head.TryGetFeatureValue(
                CommonUsages.isTracked,
                out bool isTracked) &&
            isTracked;
        bool positioned =
            head.isValid &&
            head.TryGetFeatureValue(
                CommonUsages.devicePosition,
                out headPosition);
        bool rotated =
            head.isValid &&
            head.TryGetFeatureValue(
                CommonUsages.deviceRotation,
                out headRotation);
        if (!tracked ||
            !positioned ||
            !rotated ||
            headPosition.y <
                Mathf.Max(
                    0.5f,
                    minimumPlausibleEyeHeight) ||
            headPosition.y >
                Mathf.Max(
                    minimumPlausibleEyeHeight + 0.1f,
                    maximumPlausibleEyeHeight))
            return false;

        Transform cameraSpace =
            bootstrap.xrOrigin.CameraFloorOffsetObject
                ? bootstrap.xrOrigin.CameraFloorOffsetObject
                    .transform
                : bootstrap.xrCamera.transform.parent;
        if (!cameraSpace)
            return false;
        Vector3 cameraPosition =
            cameraSpace.InverseTransformPoint(
                bootstrap.xrCamera.transform.position);
        Quaternion cameraRotation =
            Quaternion.Inverse(cameraSpace.rotation) *
            bootstrap.xrCamera.transform.rotation;
        cameraPositionError =
            Vector3.Distance(
                cameraPosition,
                headPosition);
        cameraRotationError =
            Quaternion.Angle(
                cameraRotation,
                headRotation);
        return
            cameraPositionError <=
                Mathf.Max(
                    0.005f,
                    maximumCameraPositionError) &&
            cameraRotationError <=
                Mathf.Max(
                    0.1f,
                    maximumCameraRotationError);
    }

    void ReapplyCurrentStagePlacement()
    {
        if (!bootstrap ||
            string.IsNullOrEmpty(
                bootstrap.LoadedStageScene))
            return;
        Scene scene =
            SceneManager.GetSceneByName(
                bootstrap.LoadedStageScene);
        if (!scene.IsValid())
            return;
        foreach (GameObject root in
                 scene.GetRootGameObjects())
        {
            RuntimeCoveredStageRegistrationV06 registration =
                root.GetComponentInChildren<
                    RuntimeCoveredStageRegistrationV06>(
                    true);
            if (!registration)
                continue;
            if (!registration
                    .ReapplySystemPlacementV32(
                        bootstrap))
            {
                Debug.LogError(
                    "[Personally Puzzled V32 Stable Origin] " +
                    "unable to re-register resumed home stage " +
                    scene.name,
                    registration);
            }
            return;
        }
    }

    void SetCoverLock(bool locked)
    {
        Resolve();
        if (!bootstrap ||
            !bootstrap.comfortTransition)
            return;
        bootstrap.comfortTransition
            .SetExternalCoverLock(locked);
    }

    void CancelActiveHandInteractions()
    {
        foreach (RuntimePuzzleHandInteractor interactor in
                 FindObjectsByType<
                     RuntimePuzzleHandInteractor>(
                     FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
            interactor.CancelInteractionV25();
    }

    void ApplySystemFloorRecenteringSupport()
    {
        if (!enableSystemFloorRecentering)
            return;
        try
        {
            OpenXRSettings.SetAllowRecentering(
                true,
                Mathf.Max(
                    1f,
                    recenterFallbackFloorOffset));
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                "[Personally Puzzled V32 Stable Origin] " +
                "unable to enable runtime Floor recenter support: " +
                exception.Message,
                this);
        }
    }

    static bool ReadAllowRecentering()
    {
        try
        {
            return OpenXRSettings.AllowRecentering;
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    void Resolve()
    {
        if (!bootstrap)
            bootstrap =
                GetComponent<PersistentPuzzleBootstrap>();
        if (!bootstrap)
            bootstrap =
                PersistentPuzzleBootstrap.Instance;
        if (!runtimeReady)
            runtimeReady =
                GetComponent<
                    RuntimeSwanOpenXrRuntimeReadyV29>();
    }
}
