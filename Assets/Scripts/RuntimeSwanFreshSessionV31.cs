using System;
using System.Collections;
using System.Collections.Generic;
using ByteDance.PICO.Enterprise;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// V31 makes every user entry a fresh Swan spatial session. A cold process
/// recenters through the application-scoped OpenXR input subsystem, waits for
/// a stable Floor-space head pose behind the comfort cover, and only then
/// authorizes the first visible stage. The PICO system/Boundary recenter is an
/// explicit fallback only because it can reveal the system's yellow Guardian
/// frame. When the Android Activity pauses, V31 saves, removes its task, and
/// terminates the process so SpaceOS cannot resume a stale OpenXR origin.
/// </summary>
[DefaultExecutionOrder(-9150)]
[DisallowMultipleComponent]
public sealed class RuntimeSwanFreshSessionV31 : MonoBehaviour
{
    const float MinimumPlausibleEyeHeight = 0.95f;
    const float MaximumPlausibleEyeHeight = 2.35f;

    public PersistentPuzzleBootstrap bootstrap;
    public RuntimeSwanOpenXrRuntimeReadyV29 runtimeReady;
    public bool recenterOnColdStart = true;
    [Tooltip(
        "Keep false for release. PICO Enterprise Recenter is equivalent to " +
        "the system Home recenter and can summon the yellow Boundary frame.")]
    public bool allowPicoSystemRecenterFallback;
    public bool terminateProcessOnPause = true;
    [Min(1f)] public float enterpriseBindTimeoutSeconds = 4f;
    [Min(4)] public int requiredStableFrames = 12;
    [Min(0.005f)] public float maximumStableHeightRange = 0.04f;

    readonly Queue<float> stableHeights = new Queue<float>();
    readonly List<XRInputSubsystem> inputSubsystems =
        new List<XRInputSubsystem>();

    Coroutine startupWorkflow;
    bool realPicoSwan;
    bool currentSessionReady;
    bool enterpriseInitialized;
    bool enterpriseBindCompleted;
    bool enterpriseBound;
    bool exitScheduled;
    string sessionToken;
    string recenterAuthority = "none";
    int recenterResult = int.MinValue;

#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidJavaObject exitHandler;
    AndroidJavaRunnable exitRunnable;
#endif

    public bool CurrentSessionReady => currentSessionReady;
    public bool ExitScheduled => exitScheduled;
    public string SessionToken => sessionToken;
    public string RecenterAuthority => recenterAuthority;
    public int RecenterResult => recenterResult;

    void Awake()
    {
        Resolve();
        sessionToken = Guid.NewGuid().ToString("N");
        realPicoSwan =
            runtimeReady &&
            runtimeReady.IsRealPicoSwan;
        if (bootstrap && bootstrap.comfortTransition)
            bootstrap.comfortTransition.SetImmediate(true);
        if (!realPicoSwan)
            currentSessionReady = true;
    }

    void Start()
    {
        if (!realPicoSwan)
        {
            LogState("non-swan-ready");
            return;
        }
        SetStatus(
            "正在建立新的 PICO 空间会话\n" +
            "自动重置朝向并确认地面…");
        startupWorkflow = StartCoroutine(PrepareFreshSession());
    }

    void OnDestroy()
    {
        if (startupWorkflow != null)
            StopCoroutine(startupWorkflow);
        if (enterpriseInitialized)
        {
            try
            {
                PXR_Enterprise.UnBindEnterpriseService();
            }
            catch (Exception)
            {
                // The process is already leaving; no recovery is needed.
            }
        }
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused || !realPicoSwan || !terminateProcessOnPause)
            return;
        currentSessionReady = false;
        if (bootstrap)
            bootstrap.SaveNow();
        ScheduleAndroidProcessExit("application-pause");
    }

    public bool TryAuthorizeStageRequest(
        RuntimePuzzleStage stage,
        string sceneName)
    {
        if (!realPicoSwan || currentSessionReady)
            return true;
        SetStatus(
            "正在建立新的 PICO 空间会话\n" +
            "尚未显示场景，避免沿用错误地面");
        Debug.Log(
            "[Personally Puzzled V31 Fresh Session] stage-held " +
            stage + " scene=" + sceneName +
            " token=" + sessionToken,
            this);
        return false;
    }

    IEnumerator PrepareFreshSession()
    {
        while (!runtimeReady.CurrentSessionReady)
            yield return null;

        bool recentered = !recenterOnColdStart;
        if (recenterOnColdStart)
        {
            // Try the application-scoped path first. It keeps head and hands
            // in the same OpenXR space without deliberately opening PICO's
            // system Boundary/Guardian visualization.
            recentered = TryOpenXrRecenter();
            if (recentered)
            {
                recenterAuthority = "openxr-application";
                recenterResult = 0;
            }

            if (!recentered &&
                allowPicoSystemRecenterFallback)
            {
                BeginEnterpriseBind();
                float deadline =
                    Time.unscaledTime +
                    Mathf.Max(1f, enterpriseBindTimeoutSeconds);
                while (!enterpriseBindCompleted &&
                       Time.unscaledTime < deadline)
                    yield return null;

                if (enterpriseBound)
                {
                    try
                    {
                        recenterResult =
                            PXR_Enterprise.Recenter();
                        if (recenterResult == 0)
                        {
                            recentered = true;
                            recenterAuthority =
                                "pico-system-fallback";
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(
                            "[Personally Puzzled V31 Fresh Session] " +
                            "PICO system recenter threw: " +
                            exception.Message,
                            this);
                    }
                }
            }
        }

        if (!recentered)
        {
            recenterAuthority = "failed";
            SetStatus(
                "无法自动重置 PICO 朝向\n" +
                "场景保持遮挡，退出后请重新进入");
            Debug.LogError(
                "[Personally Puzzled V31 Fresh Session] " +
                "PPVR_V31_RECENTER_FAILED token=" + sessionToken +
                " picoSystemFallbackAllowed=" +
                allowPicoSystemRecenterFallback +
                " enterpriseBound=" + enterpriseBound +
                " enterpriseResult=" + recenterResult,
                this);
            startupWorkflow = null;
            yield break;
        }

        Debug.Log(
            "[Personally Puzzled V31 Fresh Session] " +
            "PPVR_V31_RECENTER_REQUESTED token=" + sessionToken +
            " authority=" + recenterAuthority +
            " result=" + recenterResult,
            this);

        // Recenter is asynchronous at the system/runtime boundary. Require a
        // new stable, plausible Floor-space pose before revealing any scene.
        stableHeights.Clear();
        int stableFrames = 0;
        while (stableFrames < Mathf.Max(4, requiredStableFrames))
        {
            if (!runtimeReady.CurrentSessionReady ||
                !TryGetTrackedHeadHeight(out float height))
            {
                stableFrames = 0;
                stableHeights.Clear();
                yield return null;
                continue;
            }

            stableHeights.Enqueue(height);
            while (stableHeights.Count >
                   Mathf.Max(4, requiredStableFrames))
                stableHeights.Dequeue();
            float minimum = float.MaxValue;
            float maximum = float.MinValue;
            foreach (float sample in stableHeights)
            {
                minimum = Mathf.Min(minimum, sample);
                maximum = Mathf.Max(maximum, sample);
            }
            bool stable =
                stableHeights.Count >=
                    Mathf.Max(4, requiredStableFrames) &&
                maximum - minimum <=
                    Mathf.Max(0.005f, maximumStableHeightRange);
            stableFrames = stable ? stableFrames + 1 : 0;
            yield return null;
        }

        currentSessionReady = true;
        SetStatus(string.Empty);
        Debug.Log(
            "[Personally Puzzled V31 Fresh Session] " +
            "PPVR_V31_FRESH_SESSION_READY token=" + sessionToken +
            " pid=" + ReadAndroidPid() +
            " authority=" + recenterAuthority +
            " stableHeight=" + stableHeights.Peek().ToString("F3") +
            " floor=True",
            this);
        startupWorkflow = null;
    }

    void BeginEnterpriseBind()
    {
        try
        {
            enterpriseInitialized =
                PXR_Enterprise.InitEnterpriseService();
            if (!enterpriseInitialized)
            {
                enterpriseBindCompleted = true;
                return;
            }
            PXR_Enterprise.BindEnterpriseService(bound =>
            {
                enterpriseBound = bound;
                enterpriseBindCompleted = true;
                Debug.Log(
                    "[Personally Puzzled V31 Fresh Session] " +
                    "enterprise-bind=" + bound +
                    " token=" + sessionToken,
                    this);
            });
        }
        catch (Exception exception)
        {
            enterpriseBindCompleted = true;
            enterpriseBound = false;
            Debug.LogWarning(
                "[Personally Puzzled V31 Fresh Session] " +
                "PICO Enterprise bind unavailable: " +
                exception.Message,
                this);
        }
    }

    bool TryOpenXrRecenter()
    {
        inputSubsystems.Clear();
        SubsystemManager.GetSubsystems(inputSubsystems);
        foreach (XRInputSubsystem input in inputSubsystems)
        {
            if (input != null &&
                input.running &&
                input.TryRecenter())
                return true;
        }
        return false;
    }

    static bool TryGetTrackedHeadHeight(out float height)
    {
        height = 0f;
        InputDevice head =
            InputDevices.GetDeviceAtXRNode(XRNode.Head);
        bool isTracked = false;
        Vector3 position = Vector3.zero;
        bool tracked =
            head.isValid &&
            head.TryGetFeatureValue(
                CommonUsages.isTracked,
                out isTracked) &&
            isTracked;
        bool positioned =
            head.isValid &&
            head.TryGetFeatureValue(
                CommonUsages.devicePosition,
                out position);
        if (!tracked ||
            !positioned ||
            position.y < MinimumPlausibleEyeHeight ||
            position.y > MaximumPlausibleEyeHeight)
            return false;
        height = position.y;
        return true;
    }

    void ScheduleAndroidProcessExit(string reason)
    {
        if (exitScheduled)
            return;
        exitScheduled = true;
        Debug.Log(
            "[Personally Puzzled V31 Fresh Session] " +
            "PPVR_V31_EXIT_ON_PAUSE token=" + sessionToken +
            " pid=" + ReadAndroidPid() +
            " reason=" + reason,
            this);
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using AndroidJavaClass unityPlayer =
                new AndroidJavaClass(
                    "com.unity3d.player.UnityPlayer");
            AndroidJavaObject activity =
                unityPlayer.GetStatic<AndroidJavaObject>(
                    "currentActivity");
            activity?.Call("finishAndRemoveTask");

            using AndroidJavaClass looper =
                new AndroidJavaClass("android.os.Looper");
            AndroidJavaObject mainLooper =
                looper.CallStatic<AndroidJavaObject>(
                    "getMainLooper");
            exitHandler =
                new AndroidJavaObject(
                    "android.os.Handler",
                    mainLooper);
            exitRunnable = new AndroidJavaRunnable(() =>
            {
                using AndroidJavaClass process =
                    new AndroidJavaClass("android.os.Process");
                int pid = process.CallStatic<int>("myPid");
                process.CallStatic("killProcess", pid);
            });
            exitHandler.Call<bool>(
                "postDelayed",
                exitRunnable,
                350L);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[Personally Puzzled V31 Fresh Session] " +
                "Android hard-exit scheduling failed: " +
                exception.Message,
                this);
            Application.Quit();
        }
#endif
    }

    void SetStatus(string value)
    {
        Resolve();
        RuntimeComfortTransitionV01 comfort =
            bootstrap ? bootstrap.comfortTransition : null;
        if (comfort && !currentSessionReady)
            comfort.SetImmediate(true);
        if (string.IsNullOrEmpty(value))
            return;
        Debug.Log(
            "[Personally Puzzled V31 Fresh Session] status=" +
            value.Replace('\n', ' ') +
            " token=" + sessionToken,
            this);
    }

    void LogState(string phase)
    {
        Debug.Log(
            "[Personally Puzzled V31 Fresh Session] phase=" + phase +
            " token=" + sessionToken +
            " realPico=" + realPicoSwan +
            " ready=" + currentSessionReady,
            this);
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
                GetComponent<RuntimeSwanOpenXrRuntimeReadyV29>();
    }

    static int ReadAndroidPid()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using AndroidJavaClass process =
                new AndroidJavaClass("android.os.Process");
            return process.CallStatic<int>("myPid");
        }
        catch (Exception)
        {
            return -1;
        }
#else
        return -1;
#endif
    }
}
