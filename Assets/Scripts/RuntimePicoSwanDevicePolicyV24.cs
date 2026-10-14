using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

/// <summary>
/// V24 real-device policy for PICO Swan. It keeps native OpenXR head tracking
/// authoritative, prevents display sleep while the experience is active and
/// emits bounded device diagnostics for stereo and tracked-hand verification.
/// </summary>
public sealed class RuntimePicoSwanDevicePolicyV24 : MonoBehaviour
{
    public PersistentPuzzleBootstrap bootstrap;
    public Camera xrCamera;
    public bool keepScreenAwake = true;
    [Min(60)] public int targetFrameRate = 72;
    [Min(0.5f)] public float diagnosticInterval = 2f;
    [Min(1)] public int maximumDiagnosticSamples = 8;

    readonly List<XRDisplaySubsystem> displays =
        new List<XRDisplaySubsystem>();
    readonly List<XRHandSubsystem> hands =
        new List<XRHandSubsystem>();
    float nextDiagnostic;
    int diagnosticSamples;

    void Awake()
    {
        ResolveReferences();
        ApplyDevicePolicy();
    }

    void OnEnable()
    {
        ApplyDevicePolicy();
    }

    void Start()
    {
        ApplyDevicePolicy();
        LogDeviceState("start");
    }

    void Update()
    {
        if (diagnosticSamples >= maximumDiagnosticSamples ||
            Time.unscaledTime < nextDiagnostic)
            return;
        nextDiagnostic =
            Time.unscaledTime + Mathf.Max(0.5f, diagnosticInterval);
        LogDeviceState("sample");
    }

    void OnApplicationFocus(bool focused)
    {
        if (focused)
            ApplyDevicePolicy();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
            ReleaseDeviceWakePolicy();
        else
            ApplyDevicePolicy();
    }

    void OnDisable()
    {
        ReleaseDeviceWakePolicy();
    }

    public void ApplyDevicePolicy()
    {
        ResolveReferences();
        if (keepScreenAwake)
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            SetAndroidKeepScreenOn(true);
        }
        else
            ReleaseDeviceWakePolicy();
        Application.targetFrameRate =
            Mathf.Max(60, targetFrameRate);
        if (bootstrap)
            bootstrap.EnforceNativeOpenXrTrackingV24();
        if (xrCamera)
        {
            xrCamera.stereoTargetEye =
                StereoTargetEyeMask.Both;
            TrackedPoseDriver driver =
                xrCamera.GetComponent<TrackedPoseDriver>();
            if (driver)
                driver.enabled = true;
        }
    }

    void ResolveReferences()
    {
        if (!bootstrap)
            bootstrap =
                GetComponent<PersistentPuzzleBootstrap>();
        if (!bootstrap)
            bootstrap =
                PersistentPuzzleBootstrap.Instance;
        if (!xrCamera && bootstrap)
            xrCamera = bootstrap.xrCamera;
        if (!xrCamera)
            xrCamera = Camera.main;
    }

    void LogDeviceState(string phase)
    {
        diagnosticSamples++;
        displays.Clear();
        SubsystemManager.GetSubsystems(displays);
        hands.Clear();
        SubsystemManager.GetSubsystems(hands);
        XRHandSubsystem runningHands =
            hands.Find(value => value != null && value.running);
        TrackedPoseDriver driver = xrCamera
            ? xrCamera.GetComponent<TrackedPoseDriver>()
            : null;
        StringBuilder state = new StringBuilder(384);
        state.Append("[Personally Puzzled V24 Swan XR] phase=")
            .Append(phase)
            .Append(" xrEnabled=").Append(XRSettings.enabled)
            .Append(" deviceActive=").Append(XRSettings.isDeviceActive)
            .Append(" displays=").Append(displays.Count)
            .Append(" displayRunning=")
            .Append(displays.Exists(value => value != null && value.running))
            .Append(" eyeTexture=")
            .Append(XRSettings.eyeTextureWidth)
            .Append('x')
            .Append(XRSettings.eyeTextureHeight)
            .Append(" cameraStereo=")
            .Append(xrCamera && xrCamera.stereoEnabled)
            .Append(" targetEyes=")
            .Append(xrCamera
                ? xrCamera.stereoTargetEye.ToString()
                : "none")
            .Append(" poseDriver=")
            .Append(driver && driver.enabled)
            .Append(" handSubsystem=")
            .Append(runningHands != null)
            .Append(" leftTracked=")
            .Append(runningHands != null &&
                runningHands.leftHand.isTracked)
            .Append(" rightTracked=")
            .Append(runningHands != null &&
                runningHands.rightHand.isTracked)
            .Append(" sleepTimeout=")
            .Append(Screen.sleepTimeout);
        Debug.Log(state.ToString(), this);
    }

    void ReleaseDeviceWakePolicy()
    {
        Screen.sleepTimeout = SleepTimeout.SystemSetting;
        SetAndroidKeepScreenOn(false);
    }

    static void SetAndroidKeepScreenOn(bool enabled)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using AndroidJavaClass unityPlayer =
                new AndroidJavaClass(
                    "com.unity3d.player.UnityPlayer");
            AndroidJavaObject activity =
                unityPlayer.GetStatic<AndroidJavaObject>(
                    "currentActivity");
            if (activity == null)
                return;
            activity.Call(
                "runOnUiThread",
                new AndroidJavaRunnable(() =>
                {
                    using AndroidJavaObject window =
                        activity.Call<AndroidJavaObject>(
                            "getWindow");
                    window?.Call(
                        enabled ? "addFlags" : "clearFlags",
                        128);
                }));
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                "[Personally Puzzled V24 Swan XR] " +
                "Unable to update Android FLAG_KEEP_SCREEN_ON: " +
                exception.Message);
        }
#endif
    }
}
