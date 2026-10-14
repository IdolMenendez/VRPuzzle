using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using ByteDance.PICO.XR;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// True-device Swan ground gate. V26 only proved that Unity accepted the
/// Floor flag; it could accept a transient 0.08/0.48 m pose immediately after
/// focus. V27 launches PICO's exported system height setup, verifies native
/// Floor plus configured boundary, and requires a stable tracked head window
/// before any non-home stage can load.
/// </summary>
[DefaultExecutionOrder(-9100)]
[DisallowMultipleComponent]
public sealed class RuntimePicoSwanDeviceGroundCalibrationV27 :
    MonoBehaviour
{
    const string CalibrationKey =
        "ppvr_swan_system_ground_calibrated_v27";
    const string DiagnosticExtra =
        "ppvr_v27_device_diagnostic";
    const float MinimumPlausibleEyeHeight = 0.95f;
    const float MaximumPlausibleEyeHeight = 2.35f;

    public PersistentPuzzleBootstrap bootstrap;
    [Min(0.5f)] public float stableWindowSeconds = 1.8f;
    [Min(0.005f)] public float maximumStableHeightRange = 0.035f;
    [Min(8)] public int minimumStableSamples = 24;
    [Min(0.01f)] public float sampleInterval = 0.05f;
    [Min(0.25f)] public float diagnosticInterval = 0.5f;
    public bool launchPicoSystemGroundFlow = true;
    public bool allowAdbDeviceDiagnostic = true;

    readonly List<XRInputSubsystem> inputs =
        new List<XRInputSubsystem>();
    readonly Queue<PoseSample> samples =
        new Queue<PoseSample>();

    RuntimePuzzleStage pendingStage;
    string pendingScene;
    float nextSample;
    float nextDiagnostic;
    bool requestPending;
    bool nativeFloorRequested;
    bool systemGroundFlowLaunched;
    bool returnedFromSystemGroundFlow;
    bool currentSessionReady;
    bool realPicoSwan;
    bool deviceDiagnostic;
    bool deviceDiagnosticStarted;
    string lastState;
    TextMesh statusText;
    GameObject statusRoot;

    struct PoseSample
    {
        public float time;
        public float y;
    }

    public bool CurrentSessionReady => currentSessionReady;
    public bool IsRealPicoSwan => realPicoSwan;
    public bool HasPersistentSystemGroundCalibration =>
        PlayerPrefs.GetInt(CalibrationKey, 0) == 1;

    void Awake()
    {
        Resolve();
        realPicoSwan = DetectRealPicoSwan();
        deviceDiagnostic =
            allowAdbDeviceDiagnostic &&
            ReadIntentBooleanExtra(DiagnosticExtra);
        if (!realPicoSwan)
        {
            currentSessionReady = true;
            return;
        }
        ResetStability("awake");
        CreateStatus();
    }

    void OnEnable()
    {
        if (realPicoSwan)
            RequestNativeFloorOnce("enable");
    }

    void Start()
    {
        if (!realPicoSwan)
            return;
        RequestNativeFloorOnce("start");
        LogState("start", true);
        if (deviceDiagnostic)
        {
            SetStatus(
                "真机地面诊断\n请正常佩戴头显并站稳\n正在确认 PICO 地面…");
        }
    }

    void Update()
    {
        if (!realPicoSwan)
            return;
        if (Time.unscaledTime >= nextSample)
        {
            nextSample =
                Time.unscaledTime + Mathf.Max(0.01f, sampleInterval);
            ObserveDevicePose();
        }
        if (Time.unscaledTime >= nextDiagnostic)
        {
            nextDiagnostic =
                Time.unscaledTime + Mathf.Max(0.25f, diagnosticInterval);
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
        if (!focused)
        {
            currentSessionReady = false;
            samples.Clear();
            LogState("focus-lost", true);
            return;
        }
        ResetStability("focus-return");
        nativeFloorRequested = false;
        RequestNativeFloorOnce("focus-return");
    }

    void OnApplicationPause(bool paused)
    {
        if (!realPicoSwan)
            return;
        if (paused)
        {
            currentSessionReady = false;
            samples.Clear();
            LogState("pause", true);
            return;
        }
        if (systemGroundFlowLaunched)
            returnedFromSystemGroundFlow = true;
        ResetStability("resume");
        nativeFloorRequested = false;
        RequestNativeFloorOnce("resume");
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
        if (!HasPersistentSystemGroundCalibration &&
            !systemGroundFlowLaunched &&
            launchPicoSystemGroundFlow)
        {
            SetStatus(
                "进入关卡前需要标定地面\n即将打开 PICO 官方地面高度设置\n完成后会自动回到首页");
            LaunchSystemGroundCalibration();
        }
        else
        {
            SetStatus(
                "正在确认 PICO 地面\n请正常佩戴头显并保持站稳\n确认完成后自动继续");
        }
        LogState(
            "stage-held " + stage + " scene=" + sceneName,
            true);
        return false;
    }

    public void ForceSystemGroundCalibrationFromHome()
    {
        if (!realPicoSwan)
            return;
        requestPending = false;
        LaunchSystemGroundCalibration();
    }

    void ObserveDevicePose()
    {
        RequestNativeFloorOnce("observe");
        bool configured = GetBoundaryConfigured();
        bool enabled = GetBoundaryEnabled();
        bool nativeFloor = GetNativeFloor(out PxrTrackingOrigin origin);
        InputDevice head =
            InputDevices.GetDeviceAtXRNode(XRNode.Head);
        Vector3 position = Vector3.zero;
        bool tracked = head.isValid &&
            head.TryGetFeatureValue(
                CommonUsages.isTracked,
                out bool isTracked) &&
            isTracked;
        bool hasPosition = head.isValid &&
            head.TryGetFeatureValue(
                CommonUsages.devicePosition,
                out position);
        float y = hasPosition ? position.y : -1f;
        bool plausible =
            tracked &&
            hasPosition &&
            y >= MinimumPlausibleEyeHeight &&
            y <= MaximumPlausibleEyeHeight;

        if (!configured || !enabled || !nativeFloor || !plausible)
        {
            samples.Clear();
            currentSessionReady = false;
            return;
        }

        float now = Time.unscaledTime;
        samples.Enqueue(new PoseSample { time = now, y = y });
        float cutoff = now - Mathf.Max(0.5f, stableWindowSeconds);
        while (samples.Count > 0 &&
               samples.Peek().time < cutoff)
            samples.Dequeue();

        float minimum = float.MaxValue;
        float maximum = float.MinValue;
        float oldest = now;
        foreach (PoseSample sample in samples)
        {
            minimum = Mathf.Min(minimum, sample.y);
            maximum = Mathf.Max(maximum, sample.y);
            oldest = Mathf.Min(oldest, sample.time);
        }
        bool fullWindow =
            samples.Count >= Mathf.Max(8, minimumStableSamples) &&
            now - oldest >=
                Mathf.Max(0.5f, stableWindowSeconds) - 0.12f;
        bool stable =
            fullWindow &&
            maximum - minimum <=
                Mathf.Max(0.005f, maximumStableHeightRange);
        if (!stable)
        {
            currentSessionReady = false;
            return;
        }

        if (!currentSessionReady)
        {
            currentSessionReady = true;
            if (returnedFromSystemGroundFlow ||
                systemGroundFlowLaunched)
            {
                PlayerPrefs.SetInt(CalibrationKey, 1);
                PlayerPrefs.Save();
            }
            SetStatus(
                "地面确认完成\nPICO Floor 稳定 · " +
                y.ToString("F2") + " m");
            LogState(
                "stable-ready range=" +
                (maximum - minimum).ToString("F4") +
                " samples=" + samples.Count +
                " origin=" + origin,
                true);
            if (requestPending)
                ContinuePendingStage();
        }
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
        bootstrap
            .ContinueStageRequestAfterDeviceGroundCalibrationV27(
                stage,
                scene);
    }

    IEnumerator RunDeviceDiagnostic()
    {
        Debug.Log(
            "[Personally Puzzled V27 Swan Ground] " +
            "device-diagnostic-home-ready; entering Selection first",
            this);
        bootstrap
            .ContinueStageRequestAfterDeviceGroundCalibrationV27(
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
                "[Personally Puzzled V27 Swan Ground] " +
                "device-diagnostic failed to reach Selection",
                this);
            yield break;
        }

        Debug.Log(
            "[Personally Puzzled V27 Swan Ground] " +
            "device-diagnostic-selection-ready; entering Cottage",
            this);
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
                "真机地面诊断完成\n首页 → 选关 → 第一关\n请检查桌面高度与距离");
        }
        else
        {
            Debug.LogError(
                "[Personally Puzzled V27 Swan Ground] " +
                "device-diagnostic failed to reach Cottage",
                this);
        }
    }

    void RequestNativeFloorOnce(string phase)
    {
        if (nativeFloorRequested)
            return;
        nativeFloorRequested = true;
        if (bootstrap && bootstrap.xrOrigin)
        {
            XROrigin origin = bootstrap.xrOrigin;
            origin.RequestedTrackingOriginMode =
                XROrigin.TrackingOriginMode.Floor;
            origin.CameraYOffset = 0f;
        }
        inputs.Clear();
        SubsystemManager.GetSubsystems(inputs);
        foreach (XRInputSubsystem input in inputs)
        {
            if (input != null && input.running)
                input.TrySetTrackingOriginMode(
                    TrackingOriginModeFlags.Floor);
        }
        try
        {
#if ENABLE_PICO_XR_SDK
            PXR_System.SetTrackingOrigin(
                PxrTrackingOrigin.Floor);
#endif
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[Personally Puzzled V27 Swan Ground] Native Floor " +
                "request failed during " + phase + ": " +
                exception.Message,
                this);
        }
    }

    bool GetNativeFloor(out PxrTrackingOrigin origin)
    {
        origin = PxrTrackingOrigin.Eye;
        try
        {
#if ENABLE_PICO_XR_SDK
            PXR_System.GetTrackingOrigin(out origin);
            return origin == PxrTrackingOrigin.Floor;
#else
            return false;
#endif
        }
        catch (Exception)
        {
            return false;
        }
    }

    static bool GetBoundaryConfigured()
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

    static bool GetBoundaryEnabled()
    {
        try
        {
#if ENABLE_PICO_XR_SDK
            return PXR_Boundary.GetEnabled();
#else
            return false;
#endif
        }
        catch (Exception)
        {
            return false;
        }
    }

    void LaunchSystemGroundCalibration()
    {
        if (!realPicoSwan || systemGroundFlowLaunched)
            return;
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
                new AndroidJavaObject("android.content.Intent");
            intent.Call<AndroidJavaObject>(
                "setClassName",
                "com.pvr.seethrough.setting",
                "com.pvr.seethrough.setting.MainActivity");
            intent.Call<AndroidJavaObject>(
                "putExtra",
                "intent_extra_type",
                5);
            intent.Call<AndroidJavaObject>(
                "putExtra",
                "intent_extra_packagename",
                Application.identifier);
            intent.Call<AndroidJavaObject>(
                "addFlags",
                0x10000000);
            systemGroundFlowLaunched = true;
            returnedFromSystemGroundFlow = false;
            currentSessionReady = false;
            samples.Clear();
            Debug.Log(
                "[Personally Puzzled V27 Swan Ground] Launching PICO " +
                "system ground-height activity type=5 package=" +
                Application.identifier,
                this);
            activity.Call("startActivity", intent);
        }
        catch (Exception exception)
        {
            systemGroundFlowLaunched = false;
            Debug.LogError(
                "[Personally Puzzled V27 Swan Ground] Unable to launch " +
                "PICO system ground-height activity: " +
                exception,
                this);
        }
#endif
    }

    void ResetStability(string phase)
    {
        currentSessionReady = false;
        samples.Clear();
        nextSample = Time.unscaledTime + 0.05f;
        LogState(phase, true);
    }

    void LogState(string phase, bool force)
    {
        if (!realPicoSwan)
            return;
        bool configured = GetBoundaryConfigured();
        bool enabled = GetBoundaryEnabled();
        bool nativeFloor = GetNativeFloor(out PxrTrackingOrigin origin);
        InputDevice head =
            InputDevices.GetDeviceAtXRNode(XRNode.Head);
        Vector3 position = Vector3.zero;
        bool tracked = head.isValid &&
            head.TryGetFeatureValue(
                CommonUsages.isTracked,
                out bool trackedValue) &&
            trackedValue;
        bool hasPosition = head.isValid &&
            head.TryGetFeatureValue(
                CommonUsages.devicePosition,
                out position);
        Camera camera = bootstrap ? bootstrap.xrCamera : null;
        StringBuilder builder = new StringBuilder(768);
        builder.Append(
                "[Personally Puzzled V27 Swan Ground] phase=")
            .Append(phase)
            .Append(" configured=").Append(configured)
            .Append(" enabled=").Append(enabled)
            .Append(" nativeOrigin=").Append(origin)
            .Append(" nativeFloor=").Append(nativeFloor)
            .Append(" tracked=").Append(tracked)
            .Append(" rawHead=")
            .Append(hasPosition
                ? position.ToString("F3")
                : "unavailable")
            .Append(" cameraWorld=")
            .Append(camera
                ? camera.transform.position.ToString("F3")
                : "none")
            .Append(" samples=").Append(samples.Count)
            .Append(" ready=").Append(currentSessionReady)
            .Append(" persisted=")
            .Append(HasPersistentSystemGroundCalibration)
            .Append(" flowLaunched=")
            .Append(systemGroundFlowLaunched)
            .Append(" returned=")
            .Append(returnedFromSystemGroundFlow)
            .Append(" requestPending=")
            .Append(requestPending)
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
        RuntimePuzzleLevelController level =
            FindFirstObjectByType<RuntimePuzzleLevelController>();
        if (level && camera)
        {
            Vector3 tableReference =
                new Vector3(0f, 1.245f, 0.64f);
            Vector3 delta =
                tableReference - camera.transform.position;
            builder.Append(" tableHorizontalDistance=")
                .Append(
                    new Vector2(delta.x, delta.z)
                        .magnitude.ToString("F3"))
                .Append(" tableVerticalDelta=")
                .Append(delta.y.ToString("F3"));
        }
        string state = builder.ToString();
        if (force || state != lastState)
        {
            lastState = state;
            Debug.Log(state, this);
        }
    }

    void CreateStatus()
    {
        if (!bootstrap || !bootstrap.xrCamera)
            return;
        statusRoot =
            new GameObject("V27 Swan ground calibration status");
        statusRoot.transform.SetParent(
            bootstrap.xrCamera.transform,
            false);
        statusRoot.transform.localPosition =
            new Vector3(0f, -0.13f, 1.05f);
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
