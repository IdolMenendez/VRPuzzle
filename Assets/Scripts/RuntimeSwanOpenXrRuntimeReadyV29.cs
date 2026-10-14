using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using ByteDance.PICO.XR;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// V29 makes the runtime-provided OpenXR Floor reference space the only
/// vertical coordinate authority. It never writes the tracked camera pose,
/// never applies a human-height offset, never falls back to Device space, and
/// never invokes a private PICO settings activity.
/// </summary>
[DefaultExecutionOrder(-9200)]
[DisallowMultipleComponent]
public sealed class RuntimeSwanOpenXrRuntimeReadyV29 : MonoBehaviour
{
    const string DiagnosticExtra =
        "ppvr_v29_device_diagnostic";

    public PersistentPuzzleBootstrap bootstrap;
    [Min(2)] public int requiredConsecutiveTrackedFrames = 8;
    [Min(0.2f)] public float diagnosticInterval = 0.75f;
    [Tooltip(
        "Diagnostic only in Unity OpenXR mode. The legacy PICO XR boundary " +
        "API is not a valid readiness gate when ENABLE_PICO_OPENXR_SDK is " +
        "active.")]
    public bool requireConfiguredBoundaryOnPico;
    public bool allowAdbDeviceDiagnostic = true;
    [Tooltip(
        "Shows the camera-following OpenXR Floor diagnostic text. V30 " +
        "disables this presentation-only overlay while retaining all " +
        "Floor readiness checks and logs.")]
    public bool showRuntimeStatus = true;

    readonly List<XRInputSubsystem> inputSubsystems =
        new List<XRInputSubsystem>();

    RuntimePuzzleStage pendingStage;
    string pendingScene;
    bool requestPending;
    bool currentSessionReady;
    bool realPicoSwan;
    bool deviceDiagnostic;
    bool deviceDiagnosticStarted;
    int consecutiveTrackedFrames;
    float nextFloorRequest;
    float nextDiagnostic;
    string lastDiagnostic;
    GameObject statusRoot;
    TextMesh statusText;

    Vector3 authoredBootstrapPosition;
    Quaternion authoredBootstrapRotation;
    Vector3 authoredBootstrapScale;
    Vector3 authoredOriginLocalPosition;
    Quaternion authoredOriginLocalRotation;
    Vector3 authoredOriginLocalScale;
    bool authoredRigCaptured;
    bool rigMutationReported;

    public bool CurrentSessionReady => currentSessionReady;
    public bool IsRealPicoSwan => realPicoSwan;

    void Awake()
    {
        Resolve();
        CaptureAuthoredRig();
        realPicoSwan = DetectRealPicoSwan();
        deviceDiagnostic =
            allowAdbDeviceDiagnostic &&
            ReadIntentBooleanExtra(DiagnosticExtra);
        ApplyOfficialFloorRequest("awake");
        if (!realPicoSwan)
            currentSessionReady = true;
        else
            CreateStatus();
    }

    void OnEnable()
    {
        ApplyOfficialFloorRequest("enable");
    }

    void Start()
    {
        ApplyOfficialFloorRequest("start");
        LogState("start", true);
    }

    void Update()
    {
        if (Time.unscaledTime >= nextFloorRequest)
        {
            nextFloorRequest = Time.unscaledTime + 1f;
            ApplyOfficialFloorRequest("poll");
        }

        AuditRigWithoutCorrection();
        if (!realPicoSwan)
            return;

        ObserveOfficialFloorState();
        if (Time.unscaledTime >= nextDiagnostic)
        {
            nextDiagnostic =
                Time.unscaledTime + Mathf.Max(0.2f, diagnosticInterval);
            LogState("sample", false);
        }

        if (deviceDiagnostic &&
            currentSessionReady &&
            !deviceDiagnosticStarted &&
            bootstrap &&
            bootstrap.CurrentStage == RuntimePuzzleStage.Splash &&
            !bootstrap.IsTransitioning &&
            !string.IsNullOrEmpty(bootstrap.LoadedStageScene))
        {
            deviceDiagnosticStarted = true;
            StartCoroutine(RunDeviceDiagnostic());
        }
    }

    void OnApplicationFocus(bool focused)
    {
        if (!realPicoSwan)
            return;
        ResetSessionReadiness(focused ? "focus-return" : "focus-lost");
        if (focused)
            ApplyOfficialFloorRequest("focus-return");
    }

    void OnApplicationPause(bool paused)
    {
        if (!realPicoSwan)
            return;
        ResetSessionReadiness(paused ? "pause" : "resume");
        if (!paused)
            ApplyOfficialFloorRequest("resume");
    }

    public bool TryAuthorizeStageRequest(
        RuntimePuzzleStage stage,
        string sceneName)
    {
        if (!realPicoSwan || currentSessionReady)
            return true;

        requestPending = true;
        pendingStage = stage;
        pendingScene = sceneName;
        SetStatus(
            "正在读取 PICO 地面坐标\n" +
            "请正常佩戴头显\n" +
            "若持续停留，请在系统中重新标定边界与地面");
        LogState(
            "stage-held " + stage + " scene=" + sceneName,
            true);
        return false;
    }

    void ApplyOfficialFloorRequest(string phase)
    {
        Resolve();
        if (bootstrap && bootstrap.xrOrigin)
        {
            bootstrap.xrOrigin.RequestedTrackingOriginMode =
                XROrigin.TrackingOriginMode.Floor;
            bootstrap.xrOrigin.CameraYOffset = 0f;
        }

        inputSubsystems.Clear();
        SubsystemManager.GetSubsystems(inputSubsystems);
        foreach (XRInputSubsystem input in inputSubsystems)
        {
            if (input == null || !input.running)
                continue;
            TrackingOriginModeFlags supported =
                input.GetSupportedTrackingOriginModes();
            if ((supported & TrackingOriginModeFlags.Floor) == 0)
            {
                Debug.LogError(
                    "[Personally Puzzled V29 Official Floor] Active XR " +
                    "runtime does not advertise Floor space during " +
                    phase + "; refusing Device-space height fallback.",
                    this);
                continue;
            }
            input.TrySetTrackingOriginMode(
                TrackingOriginModeFlags.Floor);
        }
    }

    void ObserveOfficialFloorState()
    {
        bool runningFloor = HasRunningFloorSubsystem(
            out TrackingOriginModeFlags activeMode);
        bool boundaryConfigured =
            GetLegacyPicoBoundaryConfiguredForDiagnostics();
        InputDevice head =
            InputDevices.GetDeviceAtXRNode(XRNode.Head);
        bool tracked = head.isValid &&
            head.TryGetFeatureValue(
                CommonUsages.isTracked,
                out bool trackedValue) &&
            trackedValue;
        bool hasPosition = head.isValid &&
            head.TryGetFeatureValue(
                CommonUsages.devicePosition,
                out _);

        bool valid =
            runningFloor &&
            tracked &&
            hasPosition;
        consecutiveTrackedFrames = valid
            ? consecutiveTrackedFrames + 1
            : 0;
        bool ready =
            consecutiveTrackedFrames >=
                Mathf.Max(2, requiredConsecutiveTrackedFrames);
        if (!ready)
        {
            currentSessionReady = false;
            return;
        }
        if (currentSessionReady)
            return;

        currentSessionReady = true;
        SetStatus(
            "PICO 地面坐标已就绪\n" +
            "OpenXR Floor · 相机无高度补偿");
        LogState(
            "official-floor-ready mode=" + activeMode,
            true);
        if (requestPending)
            ContinuePendingStage();
    }

    bool HasRunningFloorSubsystem(
        out TrackingOriginModeFlags activeMode)
    {
        activeMode = TrackingOriginModeFlags.Unknown;
        inputSubsystems.Clear();
        SubsystemManager.GetSubsystems(inputSubsystems);
        foreach (XRInputSubsystem input in inputSubsystems)
        {
            if (input == null || !input.running)
                continue;
            activeMode = input.GetTrackingOriginMode();
            if ((activeMode & TrackingOriginModeFlags.Floor) != 0)
                return true;
        }
        return false;
    }

    void ContinuePendingStage()
    {
        if (!bootstrap ||
            !requestPending ||
            string.IsNullOrEmpty(pendingScene))
            return;
        RuntimePuzzleStage stage = pendingStage;
        string scene = pendingScene;
        requestPending = false;
        pendingScene = null;
        HideStatusAfterDelay();
        bootstrap.ContinueStageRequestAfterSwanOpenXrRuntimeReadyV29(
            stage,
            scene);
    }

    IEnumerator RunDeviceDiagnostic()
    {
        Debug.Log(
            "[Personally Puzzled V29 Official Floor] " +
            "device-diagnostic-home-ready; entering Selection",
            this);
        bootstrap.ContinueStageRequestAfterSwanOpenXrRuntimeReadyV29(
            RuntimePuzzleStage.Selection,
            bootstrap.selectionScene);
        float deadline = Time.unscaledTime + 25f;
        while ((bootstrap.IsTransitioning ||
                bootstrap.CurrentStage != RuntimePuzzleStage.Selection) &&
               Time.unscaledTime < deadline)
            yield return null;
        if (bootstrap.CurrentStage != RuntimePuzzleStage.Selection)
        {
            Debug.LogError(
                "[Personally Puzzled V29 Official Floor] " +
                "device-diagnostic failed to reach Selection",
                this);
            yield break;
        }

        yield return new WaitForSecondsRealtime(1f);
        bootstrap.BeginLevel(0);
        deadline = Time.unscaledTime + 25f;
        while ((bootstrap.IsTransitioning ||
                bootstrap.CurrentStage != RuntimePuzzleStage.Cottage) &&
               Time.unscaledTime < deadline)
            yield return null;
        if (bootstrap.CurrentStage == RuntimePuzzleStage.Cottage)
        {
            LogState("device-diagnostic-cottage-ready", true);
            SetStatus(
                "真机流程已到第一关\n" +
                "请检查真实地面、桌面距离与双目稳定性");
        }
        else
        {
            Debug.LogError(
                "[Personally Puzzled V29 Official Floor] " +
                "device-diagnostic failed to reach Cottage",
                this);
        }
    }

    void CaptureAuthoredRig()
    {
        if (!bootstrap || !bootstrap.xrOrigin)
            return;
        Transform persistent = bootstrap.transform;
        authoredBootstrapPosition = persistent.position;
        authoredBootstrapRotation = persistent.rotation;
        authoredBootstrapScale = persistent.localScale;
        Transform origin = bootstrap.xrOrigin.transform;
        authoredOriginLocalPosition = origin.localPosition;
        authoredOriginLocalRotation = origin.localRotation;
        authoredOriginLocalScale = origin.localScale;
        authoredRigCaptured = true;
    }

    void AuditRigWithoutCorrection()
    {
        if (!authoredRigCaptured ||
            rigMutationReported ||
            !bootstrap ||
            !bootstrap.xrOrigin)
            return;
        Transform persistent = bootstrap.transform;
        Transform origin = bootstrap.xrOrigin.transform;
        bool mutated =
            Vector3.Distance(
                persistent.position,
                authoredBootstrapPosition) > 0.0005f ||
            Quaternion.Angle(
                persistent.rotation,
                authoredBootstrapRotation) > 0.05f ||
            Vector3.Distance(
                persistent.localScale,
                authoredBootstrapScale) > 0.0005f ||
            Vector3.Distance(
                origin.localPosition,
                authoredOriginLocalPosition) > 0.0005f ||
            Quaternion.Angle(
                origin.localRotation,
                authoredOriginLocalRotation) > 0.05f ||
            Vector3.Distance(
                origin.localScale,
                authoredOriginLocalScale) > 0.0005f;
        if (!mutated)
            return;
        rigMutationReported = true;
        Debug.LogError(
            "[Personally Puzzled V29 Official Floor] Persistent rig or " +
            "XR Origin was mutated. V29 reports this fault but deliberately " +
            "does not correct it; native tracked camera space remains " +
            "authoritative.",
            this);
    }

    void ResetSessionReadiness(string phase)
    {
        currentSessionReady = false;
        consecutiveTrackedFrames = 0;
        LogState(phase, true);
    }

    void LogState(string phase, bool force)
    {
        if (!realPicoSwan)
            return;
        bool floor = HasRunningFloorSubsystem(
            out TrackingOriginModeFlags mode);
        bool configured =
            GetLegacyPicoBoundaryConfiguredForDiagnostics();
        InputDevice head =
            InputDevices.GetDeviceAtXRNode(XRNode.Head);
        Vector3 headPosition = Vector3.zero;
        bool tracked = head.isValid &&
            head.TryGetFeatureValue(
                CommonUsages.isTracked,
                out bool trackedValue) &&
            trackedValue;
        bool hasPosition = head.isValid &&
            head.TryGetFeatureValue(
                CommonUsages.devicePosition,
                out headPosition);
        Transform offset =
            bootstrap && bootstrap.xrOrigin &&
            bootstrap.xrOrigin.CameraFloorOffsetObject
                ? bootstrap.xrOrigin.CameraFloorOffsetObject.transform
                : null;
        StringBuilder state = new StringBuilder(768);
        state.Append(
                "[Personally Puzzled V29 Official Floor] phase=")
            .Append(phase)
            .Append(" runningFloor=").Append(floor)
            .Append(" activeMode=").Append(mode)
            .Append(" legacyBoundaryApiConfigured=").Append(configured)
            .Append(" tracked=").Append(tracked)
            .Append(" rawHead=")
            .Append(hasPosition
                ? headPosition.ToString("F3")
                : "unavailable")
            .Append(" cameraWorld=")
            .Append(bootstrap && bootstrap.xrCamera
                ? bootstrap.xrCamera.transform.position.ToString("F3")
                : "none")
            .Append(" originLocal=")
            .Append(bootstrap && bootstrap.xrOrigin
                ? bootstrap.xrOrigin.transform.localPosition.ToString("F3")
                : "none")
            .Append(" cameraOffsetLocal=")
            .Append(offset
                ? offset.localPosition.ToString("F3")
                : "none")
            .Append(" authoredCameraYOffset=")
            .Append(bootstrap && bootstrap.xrOrigin
                ? bootstrap.xrOrigin.CameraYOffset.ToString("F3")
                : "none")
            .Append(" readyFrames=")
            .Append(consecutiveTrackedFrames)
            .Append(" ready=").Append(currentSessionReady)
            .Append(" stage=")
            .Append(bootstrap
                ? bootstrap.CurrentStage.ToString()
                : "none")
            .Append(" scene=")
            .Append(bootstrap
                ? bootstrap.LoadedStageScene
                : "none")
            .Append(" deviceDiagnostic=")
            .Append(deviceDiagnostic);
        string value = state.ToString();
        if (!force && value == lastDiagnostic)
            return;
        lastDiagnostic = value;
        Debug.Log(value, this);
    }

    static bool GetLegacyPicoBoundaryConfiguredForDiagnostics()
    {
        try
        {
#if ENABLE_PICO_XR_SDK
            return PXR_Boundary.GetConfigured();
#else
            return false;
#endif
        }
        catch (Exception)
        {
            return false;
        }
    }

    void CreateStatus()
    {
        if (!showRuntimeStatus ||
            !bootstrap ||
            !bootstrap.xrCamera)
            return;
        statusRoot =
            new GameObject("V29 official OpenXR Floor status");
        statusRoot.transform.SetParent(
            bootstrap.xrCamera.transform,
            false);
        statusRoot.transform.localPosition =
            new Vector3(0f, -0.12f, 1.05f);
        statusRoot.transform.localRotation =
            Quaternion.identity;
        GameObject textObject =
            new GameObject("Status text");
        textObject.transform.SetParent(statusRoot.transform, false);
        statusText = textObject.AddComponent<TextMesh>();
        statusText.anchor = TextAnchor.MiddleCenter;
        statusText.alignment = TextAlignment.Center;
        statusText.fontSize = 64;
        statusText.characterSize = 0.0085f;
        statusText.color =
            new Color(1f, 0.78f, 0.34f, 1f);
        statusRoot.SetActive(false);
    }

    void SetStatus(string value)
    {
        if (!showRuntimeStatus)
        {
            if (statusRoot)
                statusRoot.SetActive(false);
            return;
        }
        if (!statusRoot || !statusText)
            CreateStatus();
        if (!statusRoot || !statusText)
            return;
        statusText.text = value;
        statusRoot.SetActive(true);
    }

    async void HideStatusAfterDelay()
    {
        await Awaitable.WaitForSecondsAsync(1.2f);
        if (statusRoot)
            statusRoot.SetActive(false);
    }

    void Resolve()
    {
        if (!bootstrap)
            bootstrap =
                GetComponent<PersistentPuzzleBootstrap>();
        if (!bootstrap)
            bootstrap = PersistentPuzzleBootstrap.Instance;
    }

    static bool DetectRealPicoSwan()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using AndroidJavaClass build =
                new AndroidJavaClass("android.os.Build");
            string model =
                build.GetStatic<string>("MODEL") ?? string.Empty;
            string product =
                build.GetStatic<string>("PRODUCT") ?? string.Empty;
            string manufacturer =
                build.GetStatic<string>("MANUFACTURER") ??
                string.Empty;
            bool emulator =
                product.IndexOf(
                    "emulator",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                model.IndexOf(
                    "SDK Spaceos",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            return !emulator &&
                (model.Equals(
                     "B3110",
                     StringComparison.OrdinalIgnoreCase) ||
                 product.Equals(
                     "swan",
                     StringComparison.OrdinalIgnoreCase) ||
                 manufacturer.IndexOf(
                     "pico",
                     StringComparison.OrdinalIgnoreCase) >= 0);
        }
        catch (Exception)
        {
            return false;
        }
#else
        return false;
#endif
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
}
