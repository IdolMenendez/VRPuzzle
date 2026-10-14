using System;
using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

/// <summary>
/// Intent-only Swan visual-parity probe. It enters Cottage through the normal
/// covered stage transition, then records the final render-time coordinate and
/// stereo state without writing the camera, head pose or XR Origin.
/// </summary>
[DefaultExecutionOrder(-9100)]
[DisallowMultipleComponent]
public sealed class RuntimeSwanVisualParityProbeV30 : MonoBehaviour
{
    const string VisualParityExtra = "ppvr_v30_visual_parity";

    public PersistentPuzzleBootstrap bootstrap;
    public RuntimeSwanOpenXrRuntimeReadyV29 runtimeReady;
    [Min(4)] public int requiredStableFrames = 12;
    [Min(5f)] public float stageTimeoutSeconds = 30f;

    readonly List<XRInputSubsystem> inputSubsystems =
        new List<XRInputSubsystem>();

    Coroutine workflow;
    RuntimeCoveredStageRegistrationV06 stableRegistration;
    int workflowGeneration;
    int stableFrameCount;
    bool intentRequested;
    bool captureQueued;
    bool readyEmitted;
    string lastToken;

    public bool IntentRequested => intentRequested;
    public bool ReadyEmitted => readyEmitted;
    public string LastToken => lastToken;

    void Awake()
    {
        intentRequested = ReadIntentBooleanExtra(VisualParityExtra);
        if (!intentRequested)
        {
            enabled = false;
            return;
        }
        Resolve();
    }

    void Start()
    {
        StartWorkflow();
    }

    void OnDisable()
    {
        CancelWorkflow();
    }

    void OnDestroy()
    {
        Application.onBeforeRender -= CaptureBeforeRender;
    }

    void OnApplicationPause(bool paused)
    {
        if (!intentRequested || readyEmitted)
            return;
        if (paused)
            CancelWorkflow();
        else
            StartWorkflow();
    }

    void OnApplicationFocus(bool focused)
    {
        if (!intentRequested || readyEmitted)
            return;
        if (!focused)
            CancelWorkflow();
        else
            StartWorkflow();
    }

    void StartWorkflow()
    {
        if (!isActiveAndEnabled || readyEmitted)
            return;
        CancelWorkflow();
        int generation = ++workflowGeneration;
        workflow = StartCoroutine(RunVisualParityWorkflow(generation));
    }

    void CancelWorkflow()
    {
        workflowGeneration++;
        if (workflow != null)
            StopCoroutine(workflow);
        workflow = null;
        captureQueued = false;
        stableRegistration = null;
        stableFrameCount = 0;
        Application.onBeforeRender -= CaptureBeforeRender;
    }

    IEnumerator RunVisualParityWorkflow(int generation)
    {
        while (generation == workflowGeneration)
        {
            Resolve();
            if (bootstrap && runtimeReady)
                break;
            yield return null;
        }

        while (generation == workflowGeneration &&
               !runtimeReady.CurrentSessionReady)
            yield return null;

        if (!StageIsSettled(RuntimePuzzleStage.Cottage))
        {
            if (!PrepareStageDeadline(
                    generation,
                    out float stageReadyDeadline))
                yield break;
            while (generation == workflowGeneration &&
                   (bootstrap.IsTransitioning ||
                    string.IsNullOrEmpty(bootstrap.LoadedStageScene)) &&
                   Time.unscaledTime < stageReadyDeadline)
                yield return null;
            if (generation != workflowGeneration)
                yield break;
            if (bootstrap.IsTransitioning ||
                string.IsNullOrEmpty(bootstrap.LoadedStageScene))
            {
                ReportTimeout("current stage");
                yield break;
            }

            bootstrap.BeginLevel(0);
            if (!PrepareStageDeadline(
                    generation,
                    out float cottageDeadline))
                yield break;
            while (generation == workflowGeneration &&
                   (bootstrap.IsTransitioning ||
                    bootstrap.CurrentStage != RuntimePuzzleStage.Cottage) &&
                   Time.unscaledTime < cottageDeadline)
                yield return null;
        }
        if (!StageIsSettled(RuntimePuzzleStage.Cottage))
        {
            ReportTimeout("Cottage");
            yield break;
        }

        RuntimeCoveredStageRegistrationV06 previousRegistration = null;
        Vector3 previousTranslation = Vector3.zero;
        Vector3 previousContentPosition = Vector3.zero;
        Quaternion previousContentRotation = Quaternion.identity;
        stableFrameCount = 0;
        while (generation == workflowGeneration)
        {
            RuntimeCoveredStageRegistrationV06 registration =
                FindCottageRegistration();
            bool uncovered =
                !bootstrap.comfortTransition ||
                !bootstrap.comfortTransition.IsCovered;
            bool stable =
                StageIsSettled(RuntimePuzzleStage.Cottage) &&
                runtimeReady.CurrentSessionReady &&
                uncovered &&
                registration &&
                registration.Applied &&
                registration.contentRoot;
            if (stable)
            {
                Transform content = registration.contentRoot;
                bool same =
                    previousRegistration == registration &&
                    Vector3.Distance(
                        previousTranslation,
                        registration.AppliedTranslation) <= 0.0001f &&
                    Vector3.Distance(
                        previousContentPosition,
                        content.position) <= 0.0001f &&
                    Quaternion.Angle(
                        previousContentRotation,
                        content.rotation) <= 0.01f;
                stableFrameCount = same ? stableFrameCount + 1 : 1;
                previousRegistration = registration;
                previousTranslation = registration.AppliedTranslation;
                previousContentPosition = content.position;
                previousContentRotation = content.rotation;
                if (stableFrameCount >= Mathf.Max(4, requiredStableFrames))
                {
                    stableRegistration = registration;
                    QueueBeforeRenderCapture();
                    workflow = null;
                    yield break;
                }
            }
            else
            {
                stableFrameCount = 0;
                previousRegistration = null;
            }
            yield return null;
        }
    }

    bool PrepareStageDeadline(
        int generation,
        out float deadline)
    {
        deadline = Time.unscaledTime + Mathf.Max(5f, stageTimeoutSeconds);
        return generation == workflowGeneration &&
            bootstrap &&
            runtimeReady &&
            runtimeReady.CurrentSessionReady;
    }

    bool StageIsSettled(RuntimePuzzleStage stage)
    {
        return bootstrap &&
            !bootstrap.IsTransitioning &&
            bootstrap.CurrentStage == stage &&
            !string.IsNullOrEmpty(bootstrap.LoadedStageScene);
    }

    void QueueBeforeRenderCapture()
    {
        if (captureQueued || readyEmitted)
            return;
        captureQueued = true;
        Application.onBeforeRender -= CaptureBeforeRender;
        Application.onBeforeRender += CaptureBeforeRender;
    }

    void CaptureBeforeRender()
    {
        Application.onBeforeRender -= CaptureBeforeRender;
        captureQueued = false;
        if (readyEmitted ||
            !StageIsSettled(RuntimePuzzleStage.Cottage) ||
            !runtimeReady ||
            !runtimeReady.CurrentSessionReady ||
            !stableRegistration ||
            !stableRegistration.Applied)
        {
            StartWorkflow();
            return;
        }

        lastToken = Guid.NewGuid().ToString("N");
        VisualParitySnapshot snapshot =
            BuildSnapshot(lastToken, stableRegistration);
        string json = JsonUtility.ToJson(snapshot, false);
        readyEmitted = true;
        if (!snapshot.rig.passed)
        {
            Debug.LogError(
                "PPVR_VISUAL_PARITY_RIG_INVARIANT_FAIL token=" +
                lastToken + " " +
                FormatRigInvariant(snapshot.rig),
                this);
            Debug.LogError(
                "PPVR_VISUAL_PARITY_FAILED token=" + lastToken +
                " reason=rig-invariant snapshot=" + json,
                this);
            return;
        }
        Debug.Log(
            "PPVR_VISUAL_PARITY_RIG_INVARIANT_PASS token=" +
            lastToken + " " +
            FormatRigInvariant(snapshot.rig),
            this);
        Debug.Log(
            "PPVR_VISUAL_PARITY_READY token=" + lastToken +
            " snapshot=" + json,
            this);
    }

    static string FormatRigInvariant(RigInvariantSnapshot rig)
    {
        return
            "rootPos=" + rig.rootPositionError.ToString("F6") +
            " rootRot=" + rig.rootRotationError.ToString("F4") +
            " rootScale=" + rig.rootScaleError.ToString("F6") +
            " originPos=" +
                rig.originLocalPositionError.ToString("F6") +
            " originRot=" +
                rig.originLocalRotationError.ToString("F4") +
            " originScale=" +
                rig.originScaleError.ToString("F6") +
            " offsetPos=" +
                rig.floorOffsetLocalPositionError.ToString("F6") +
            " offsetRot=" +
                rig.floorOffsetLocalRotationError.ToString("F4") +
            " offsetScale=" +
                rig.floorOffsetScaleError.ToString("F6") +
            " cameraHeadLocal=" +
                rig.cameraHeadLocalError.ToString("F6") +
            " cameraHeadWorld=" +
                rig.cameraHeadWorldError.ToString("F6") +
            " audioDetached=" + rig.audioEffectsDetached;
    }

    VisualParitySnapshot BuildSnapshot(
        string token,
        RuntimeCoveredStageRegistrationV06 registration)
    {
        Camera camera = bootstrap ? bootstrap.xrCamera : null;
        XROrigin origin = bootstrap ? bootstrap.xrOrigin : null;
        Transform floorOffset =
            origin && origin.CameraFloorOffsetObject
                ? origin.CameraFloorOffsetObject.transform
                : null;
        InputDevice head =
            InputDevices.GetDeviceAtXRNode(XRNode.Head);
        HeadSnapshot headSnapshot = BuildHeadSnapshot(head);
        RenderTextureDescriptor eye = XRSettings.eyeTextureDesc;

        VisualParitySnapshot snapshot = new VisualParitySnapshot
        {
            token = token,
            frame = Time.frameCount,
            realtime = Time.realtimeSinceStartup,
            stage = bootstrap.CurrentStage.ToString(),
            scene = bootstrap.LoadedStageScene,
            currentSessionReady = runtimeReady.CurrentSessionReady,
            stableFrames = stableFrameCount,
            bootstrapRoot =
                TransformSnapshot.From(bootstrap.transform),
            camera = BuildCameraSnapshot(camera),
            head = headSnapshot,
            origin = new OriginSnapshot
            {
                available = origin,
                pose = TransformSnapshot.From(origin
                    ? origin.transform
                    : null),
                requestedTrackingOrigin = origin
                    ? origin.RequestedTrackingOriginMode.ToString()
                    : "none",
                cameraYOffset = origin ? origin.CameraYOffset : 0f
            },
            floorOffset = new FloorOffsetSnapshot
            {
                available = floorOffset,
                pose = TransformSnapshot.From(floorOffset)
            },
            floor = BuildFloorSnapshot(),
            registration = BuildRegistrationSnapshot(registration),
            audio = BuildAudioSnapshot(),
            rig = BuildRigInvariantSnapshot(
                camera,
                headSnapshot,
                origin,
                floorOffset),
            stereo = BuildStereoSnapshot(camera),
            render = new RenderSnapshot
            {
                xrEnabled = XRSettings.enabled,
                xrDeviceActive = XRSettings.isDeviceActive,
                loadedDevice = XRSettings.loadedDeviceName,
                stereoRenderingMode =
                    XRSettings.stereoRenderingMode.ToString(),
                eyeTextureWidth = XRSettings.eyeTextureWidth,
                eyeTextureHeight = XRSettings.eyeTextureHeight,
                eyeTextureResolutionScale =
                    XRSettings.eyeTextureResolutionScale,
                renderViewportScale = XRSettings.renderViewportScale,
                descriptorWidth = eye.width,
                descriptorHeight = eye.height,
                descriptorVolumeDepth = eye.volumeDepth,
                descriptorMsaa = eye.msaaSamples,
                descriptorDimension = eye.dimension.ToString(),
                descriptorVrUsage = eye.vrUsage.ToString(),
                descriptorGraphicsFormat = eye.graphicsFormat.ToString(),
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                targetFrameRate = Application.targetFrameRate,
                vSyncCount = QualitySettings.vSyncCount,
                antiAliasing = QualitySettings.antiAliasing,
                colorSpace = QualitySettings.activeColorSpace.ToString(),
                graphicsApi = SystemInfo.graphicsDeviceType.ToString()
            }
        };
        return snapshot;
    }

    AudioSnapshot BuildAudioSnapshot()
    {
        AudioSource effects =
            bootstrap && bootstrap.audioConductor
                ? bootstrap.audioConductor.effects
                : null;
        return new AudioSnapshot
        {
            available = effects,
            detachedFromBootstrap =
                effects &&
                effects.gameObject != bootstrap.gameObject,
            pose = TransformSnapshot.From(
                effects ? effects.transform : null)
        };
    }

    RigInvariantSnapshot BuildRigInvariantSnapshot(
        Camera camera,
        HeadSnapshot head,
        XROrigin origin,
        Transform floorOffset)
    {
        Transform root = bootstrap ? bootstrap.transform : null;
        Transform originTransform =
            origin ? origin.transform : null;
        const float poseTolerance = 0.002f;
        const float angleTolerance = 0.1f;
        const float cameraHeadTolerance = 0.03f;
        RigInvariantSnapshot snapshot = new RigInvariantSnapshot
        {
            rootPositionError =
                root ? root.position.magnitude : float.PositiveInfinity,
            rootRotationError =
                root
                    ? Quaternion.Angle(
                        root.rotation,
                        Quaternion.identity)
                    : float.PositiveInfinity,
            rootScaleError =
                root
                    ? Vector3.Distance(
                        root.localScale,
                        Vector3.one)
                    : float.PositiveInfinity,
            originLocalPositionError =
                originTransform
                    ? originTransform.localPosition.magnitude
                    : float.PositiveInfinity,
            originLocalRotationError =
                originTransform
                    ? Quaternion.Angle(
                        originTransform.localRotation,
                        Quaternion.identity)
                    : float.PositiveInfinity,
            originScaleError =
                originTransform
                    ? Vector3.Distance(
                        originTransform.localScale,
                        Vector3.one)
                    : float.PositiveInfinity,
            floorOffsetLocalPositionError =
                floorOffset
                    ? floorOffset.localPosition.magnitude
                    : float.PositiveInfinity,
            floorOffsetLocalRotationError =
                floorOffset
                    ? Quaternion.Angle(
                        floorOffset.localRotation,
                        Quaternion.identity)
                    : float.PositiveInfinity,
            floorOffsetScaleError =
                floorOffset
                    ? Vector3.Distance(
                        floorOffset.localScale,
                        Vector3.one)
                    : float.PositiveInfinity,
            audioEffectsDetached =
                bootstrap &&
                bootstrap.audioConductor &&
                bootstrap.audioConductor.effects &&
                bootstrap.audioConductor.effects.gameObject !=
                    bootstrap.gameObject
        };
        if (camera && head.hasPosition)
        {
            snapshot.cameraHeadLocalError =
                Vector3.Distance(
                    camera.transform.localPosition,
                    head.position);
            snapshot.cameraHeadWorldError =
                Vector3.Distance(
                    camera.transform.position,
                    head.position);
            snapshot.cameraHeadCompared = true;
        }
        snapshot.passed =
            snapshot.rootPositionError <= poseTolerance &&
            snapshot.rootRotationError <= angleTolerance &&
            snapshot.rootScaleError <= poseTolerance &&
            snapshot.originLocalPositionError <= poseTolerance &&
            snapshot.originLocalRotationError <= angleTolerance &&
            snapshot.originScaleError <= poseTolerance &&
            snapshot.floorOffsetLocalPositionError <= poseTolerance &&
            snapshot.floorOffsetLocalRotationError <= angleTolerance &&
            snapshot.floorOffsetScaleError <= poseTolerance &&
            snapshot.audioEffectsDetached &&
            snapshot.cameraHeadCompared &&
            snapshot.cameraHeadLocalError <= cameraHeadTolerance &&
            snapshot.cameraHeadWorldError <= cameraHeadTolerance;
        return snapshot;
    }

    CameraSnapshot BuildCameraSnapshot(Camera camera)
    {
        if (!camera)
            return new CameraSnapshot();
        return new CameraSnapshot
        {
            available = true,
            active = camera.gameObject.activeInHierarchy,
            enabled = camera.enabled,
            stereoEnabled = camera.stereoEnabled,
            stereoTargetEye = camera.stereoTargetEye.ToString(),
            pose = TransformSnapshot.From(camera.transform),
            nearClip = camera.nearClipPlane,
            farClip = camera.farClipPlane,
            fieldOfView = camera.fieldOfView,
            aspect = camera.aspect,
            cullingMask = camera.cullingMask,
            stereoSeparation = camera.stereoSeparation,
            stereoConvergence = camera.stereoConvergence
        };
    }

    static HeadSnapshot BuildHeadSnapshot(InputDevice head)
    {
        HeadSnapshot snapshot = new HeadSnapshot
        {
            valid = head.isValid,
            name = head.isValid ? head.name : string.Empty
        };
        snapshot.hasTrackedState =
            head.isValid &&
            head.TryGetFeatureValue(
                CommonUsages.isTracked,
                out snapshot.tracked);
        snapshot.hasPosition =
            head.isValid &&
            head.TryGetFeatureValue(
                CommonUsages.devicePosition,
                out snapshot.position);
        snapshot.hasRotation =
            head.isValid &&
            head.TryGetFeatureValue(
                CommonUsages.deviceRotation,
                out snapshot.rotation);
        return snapshot;
    }

    FloorSnapshot BuildFloorSnapshot()
    {
        FloorSnapshot snapshot = new FloorSnapshot();
        inputSubsystems.Clear();
        SubsystemManager.GetSubsystems(inputSubsystems);
        foreach (XRInputSubsystem input in inputSubsystems)
        {
            if (input == null || !input.running)
                continue;
            TrackingOriginModeFlags active =
                input.GetTrackingOriginMode();
            TrackingOriginModeFlags supported =
                input.GetSupportedTrackingOriginModes();
            snapshot.running = true;
            snapshot.activeMode = active.ToString();
            snapshot.supportedModes = supported.ToString();
            snapshot.floorActive =
                (active & TrackingOriginModeFlags.Floor) != 0;
            break;
        }
        return snapshot;
    }

    static RegistrationSnapshot BuildRegistrationSnapshot(
        RuntimeCoveredStageRegistrationV06 registration)
    {
        if (!registration)
            return new RegistrationSnapshot();
        return new RegistrationSnapshot
        {
            found = true,
            applied = registration.Applied,
            stage = registration.stage.ToString(),
            appliedTranslation = registration.AppliedTranslation,
            focusViewport =
                registration.FocusViewportAfterRegistration,
            registeredRigidbodyCount =
                registration.RegisteredRigidbodyCount,
            contentRoot =
                TransformSnapshot.From(registration.contentRoot),
            authoredReferenceEye =
                TransformSnapshot.From(registration.authoredReferenceEye),
            focusAnchor =
                TransformSnapshot.From(registration.focusAnchor)
        };
    }

    static StereoSnapshot BuildStereoSnapshot(Camera camera)
    {
        StereoSnapshot snapshot = new StereoSnapshot
        {
            cameraAvailable = camera,
            stereoEnabled = camera && camera.stereoEnabled
        };
        if (!camera)
            return snapshot;
        try
        {
            snapshot.leftView = MatrixSnapshot.From(
                camera.GetStereoViewMatrix(
                    Camera.StereoscopicEye.Left));
            snapshot.rightView = MatrixSnapshot.From(
                camera.GetStereoViewMatrix(
                    Camera.StereoscopicEye.Right));
            snapshot.leftProjection = MatrixSnapshot.From(
                camera.GetStereoProjectionMatrix(
                    Camera.StereoscopicEye.Left));
            snapshot.rightProjection = MatrixSnapshot.From(
                camera.GetStereoProjectionMatrix(
                    Camera.StereoscopicEye.Right));
            snapshot.matricesAvailable = true;
        }
        catch (Exception exception)
        {
            snapshot.error = exception.GetType().Name;
        }
        return snapshot;
    }

    RuntimeCoveredStageRegistrationV06 FindCottageRegistration()
    {
        if (!bootstrap ||
            string.IsNullOrEmpty(bootstrap.LoadedStageScene))
            return null;
        Scene scene =
            SceneManager.GetSceneByName(bootstrap.LoadedStageScene);
        if (!scene.IsValid() || !scene.isLoaded)
            return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            RuntimeCoveredStageRegistrationV06 registration =
                root.GetComponentInChildren<
                    RuntimeCoveredStageRegistrationV06>(true);
            if (registration &&
                registration.stage == RuntimePuzzleStage.Cottage)
                return registration;
        }
        return null;
    }

    void Resolve()
    {
        if (!bootstrap)
            bootstrap =
                GetComponent<PersistentPuzzleBootstrap>();
        if (!bootstrap)
            bootstrap = PersistentPuzzleBootstrap.Instance;
        if (!runtimeReady && bootstrap)
            runtimeReady =
                bootstrap.GetComponent<
                    RuntimeSwanOpenXrRuntimeReadyV29>();
    }

    void ReportTimeout(string stage)
    {
        Debug.LogWarning(
            "[Personally Puzzled V30 Visual Parity] Timed out waiting for " +
            stage + "; a later focus/resume may retry the Intent-only probe.",
            this);
        workflow = null;
    }

    static bool ReadIntentBooleanExtra(string key)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using AndroidJavaClass unityPlayer =
                new AndroidJavaClass(
                    "com.unity3d.player.UnityPlayer");
            using AndroidJavaObject activity =
                unityPlayer.GetStatic<AndroidJavaObject>(
                    "currentActivity");
            using AndroidJavaObject intent =
                activity.Call<AndroidJavaObject>("getIntent");
            return intent != null &&
                intent.Call<bool>(
                    "getBooleanExtra",
                    key,
                    false);
        }
        catch (Exception)
        {
            return false;
        }
#else
        return false;
#endif
    }

    [Serializable]
    sealed class VisualParitySnapshot
    {
        public string token;
        public int frame;
        public float realtime;
        public string stage;
        public string scene;
        public bool currentSessionReady;
        public int stableFrames;
        public TransformSnapshot bootstrapRoot;
        public CameraSnapshot camera;
        public HeadSnapshot head;
        public OriginSnapshot origin;
        public FloorOffsetSnapshot floorOffset;
        public FloorSnapshot floor;
        public RegistrationSnapshot registration;
        public AudioSnapshot audio;
        public RigInvariantSnapshot rig;
        public StereoSnapshot stereo;
        public RenderSnapshot render;
    }

    [Serializable]
    sealed class CameraSnapshot
    {
        public bool available;
        public bool active;
        public bool enabled;
        public bool stereoEnabled;
        public string stereoTargetEye;
        public TransformSnapshot pose;
        public float nearClip;
        public float farClip;
        public float fieldOfView;
        public float aspect;
        public int cullingMask;
        public float stereoSeparation;
        public float stereoConvergence;
    }

    [Serializable]
    sealed class HeadSnapshot
    {
        public bool valid;
        public string name;
        public bool hasTrackedState;
        public bool tracked;
        public bool hasPosition;
        public Vector3 position;
        public bool hasRotation;
        public Quaternion rotation;
    }

    [Serializable]
    sealed class OriginSnapshot
    {
        public bool available;
        public TransformSnapshot pose;
        public string requestedTrackingOrigin;
        public float cameraYOffset;
    }

    [Serializable]
    sealed class FloorOffsetSnapshot
    {
        public bool available;
        public TransformSnapshot pose;
    }

    [Serializable]
    sealed class FloorSnapshot
    {
        public bool running;
        public bool floorActive;
        public string activeMode;
        public string supportedModes;
    }

    [Serializable]
    sealed class RegistrationSnapshot
    {
        public bool found;
        public bool applied;
        public string stage;
        public Vector3 appliedTranslation;
        public Vector3 focusViewport;
        public int registeredRigidbodyCount;
        public TransformSnapshot contentRoot;
        public TransformSnapshot authoredReferenceEye;
        public TransformSnapshot focusAnchor;
    }

    [Serializable]
    sealed class AudioSnapshot
    {
        public bool available;
        public bool detachedFromBootstrap;
        public TransformSnapshot pose;
    }

    [Serializable]
    sealed class RigInvariantSnapshot
    {
        public bool passed;
        public bool audioEffectsDetached;
        public bool cameraHeadCompared;
        public float rootPositionError;
        public float rootRotationError;
        public float rootScaleError;
        public float originLocalPositionError;
        public float originLocalRotationError;
        public float originScaleError;
        public float floorOffsetLocalPositionError;
        public float floorOffsetLocalRotationError;
        public float floorOffsetScaleError;
        public float cameraHeadLocalError;
        public float cameraHeadWorldError;
    }

    [Serializable]
    sealed class StereoSnapshot
    {
        public bool cameraAvailable;
        public bool stereoEnabled;
        public bool matricesAvailable;
        public MatrixSnapshot leftView;
        public MatrixSnapshot rightView;
        public MatrixSnapshot leftProjection;
        public MatrixSnapshot rightProjection;
        public string error;
    }

    [Serializable]
    sealed class RenderSnapshot
    {
        public bool xrEnabled;
        public bool xrDeviceActive;
        public string loadedDevice;
        public string stereoRenderingMode;
        public int eyeTextureWidth;
        public int eyeTextureHeight;
        public float eyeTextureResolutionScale;
        public float renderViewportScale;
        public int descriptorWidth;
        public int descriptorHeight;
        public int descriptorVolumeDepth;
        public int descriptorMsaa;
        public string descriptorDimension;
        public string descriptorVrUsage;
        public string descriptorGraphicsFormat;
        public int screenWidth;
        public int screenHeight;
        public int targetFrameRate;
        public int vSyncCount;
        public int antiAliasing;
        public string colorSpace;
        public string graphicsApi;
    }

    [Serializable]
    sealed class TransformSnapshot
    {
        public bool available;
        public Vector3 worldPosition;
        public Quaternion worldRotation;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;

        public static TransformSnapshot From(Transform transform)
        {
            if (!transform)
                return new TransformSnapshot();
            return new TransformSnapshot
            {
                available = true,
                worldPosition = transform.position,
                worldRotation = transform.rotation,
                localPosition = transform.localPosition,
                localRotation = transform.localRotation,
                localScale = transform.localScale
            };
        }
    }

    [Serializable]
    sealed class MatrixSnapshot
    {
        public float[] values;

        public static MatrixSnapshot From(Matrix4x4 matrix)
        {
            return new MatrixSnapshot
            {
                values = new[]
                {
                    matrix.m00, matrix.m01, matrix.m02, matrix.m03,
                    matrix.m10, matrix.m11, matrix.m12, matrix.m13,
                    matrix.m20, matrix.m21, matrix.m22, matrix.m23,
                    matrix.m30, matrix.m31, matrix.m32, matrix.m33
                }
            };
        }
    }
}
