using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

public enum RuntimePuzzleStage
{
    Splash,
    Selection,
    Cottage,
    Ship,
    Eiffel,
    Completion,
    Collection,
    Pagoda,
    Windmill
}

[Serializable]
public sealed class PersonallyPuzzledSaveData
{
    public int version = 1;
    public int[] stars = { 0, 0, 0, 0, 0 };
    public int highestUnlockedLevel;
    public int handedness;
    public int heightMode;
    public bool singleHandMode;
    public float masterVolume = 0.85f;
    public float musicVolume = 0.72f;
    public float effectsVolume = 0.88f;
}

/// <summary>
/// The only persistent runtime object. It owns the XR origin, save data and
/// two-source audio conductor while visual stages stream additively around the
/// unmoving player origin.
/// </summary>
public sealed class PersistentPuzzleBootstrap : MonoBehaviour
{
    public const int SaveVersion = 1;
    public static PersistentPuzzleBootstrap Instance { get; private set; }

    [Header("Persistent rig")]
    public XROrigin xrOrigin;
    public Camera xrCamera;
    public RuntimeAudioConductor audioConductor;
    public RuntimeComfortTransitionV01 comfortTransition;
    [Tooltip(
        "V24+ real-device policy. Keep the native OpenXR tracked-pose driver " +
        "authoritative on PICO hardware; the legacy direct-pose fallback " +
        "remains available only to the Project Swan desktop emulator.")]
    public bool nativeOpenXrTrackingOnlyV24;
    [Tooltip(
        "V25 application lifecycle policy. A resumed Android activity returns " +
        "to the welcome/home stage with progress preserved instead of " +
        "revealing the previously open assembly scene.")]
    public bool returnHomeOnResumeV25;
    [Tooltip(
        "V57: unlock all five levels on load so every exhibit in Selection can " +
        "be entered for testing. The worn log proved only the already-unlocked " +
        "level (Ship) accepted entry; the others were gated by progress, not a " +
        "bug. This preserves earned stars and only widens access.")]
    public bool unlockAllLevelsV57 = true;
    [Tooltip(
        "V26 Swan spatial calibration. The real headset always uses a " +
        "floor-relative OpenXR origin; legacy saved Device-height mode is " +
        "migrated away so the authored 1.68 m eye line is not reduced to " +
        "1.18 m.")]
    public bool forceFloorTrackingOnPicoSwanV26;
    [Tooltip(
        "V27 Swan device-ground gate. Non-home stages are held until the " +
        "PICO system ground-height flow has completed and the native Floor " +
        "head pose has remained stable after focus/resume.")]
    public bool requireDeviceGroundCalibrationOnPicoSwanV27;
    [Tooltip(
        "V28 official OpenXR floor policy. Non-home stages are held until " +
        "the active XR input subsystem reports Floor space and a tracked " +
        "head pose. No authored eye-height offset, camera correction, " +
        "private system activity, or Device-space fallback is permitted.")]
    public bool requireOfficialOpenXrFloorSpaceV28;
    [Tooltip(
        "V29 uses the active Unity OpenXR Floor reference space and tracked " +
        "head pose as the readiness authority. It does not block stage entry " +
        "on the legacy PICO XR boundary API, which is unavailable in the " +
        "Unity OpenXR development mode.")]
    public bool requireSwanOpenXrRuntimeReadyV29;
    [Tooltip(
        "V31 requires a new PICO/OpenXR session on every entry. The initial " +
        "stage remains behind the comfort cover until platform recenter and " +
        "stable Floor tracking complete; pausing the Android Activity ends " +
        "the process so the next entry cannot resume a stale spatial origin.")]
    public bool requireFreshSwanSessionV31;
    [Tooltip(
        "V32 keeps the PICO system Floor reference space authoritative. The " +
        "application never issues its own recenter; it waits for a coherent " +
        "head/camera pose, keeps lifecycle recovery covered, and registers " +
        "each streamed room to the user's current horizontal pose.")]
    public bool requireStableSwanOriginV32;

    [Header("Additive runtime scenes")]
    public string splashScene = "Runtime_Splash_20260716_V01";
    public string selectionScene = "Runtime_Selection_20260716_V01";
    public string cottageScene = "Runtime_Level_Cottage_20260716_V01";
    public string shipScene = "Runtime_Level_Ship_20260716_V01";
    public string eiffelScene = "Runtime_Level_Eiffel_20260716_V01";
    public string pagodaScene = "Runtime_Level_Pagoda_20260722_V09";
    public string windmillScene = "Runtime_Level_Windmill_20260722_V09";
    public string completionScene = "Runtime_Completion_20260716_V01";
    public string collectionScene = "Runtime_Collection_20260716_V01";

    public PersonallyPuzzledSaveData Save { get; private set; } = new PersonallyPuzzledSaveData();
    public RuntimePuzzleStage CurrentStage { get; private set; } = RuntimePuzzleStage.Splash;
    public RuntimePuzzleStage PreviousStage { get; private set; } = RuntimePuzzleStage.Splash;
    public int CurrentLevelIndex { get; private set; }
    public bool IsTransitioning { get; private set; }
    public string LoadedStageScene { get; private set; }
    public bool PersistenceSuspended { get; private set; }
    public bool SwanEmulatorDiagnosticsEnabled => swanEmulatorDiagnostics;
    public bool NativeOpenXrTrackingOnlyV24 =>
        nativeOpenXrTrackingOnlyV24;
    public bool ResumeWasInterruptedV25 { get; private set; }
    public bool ForceFloorTrackingOnPicoSwanV26 =>
        forceFloorTrackingOnPicoSwanV26;
    public bool RequireDeviceGroundCalibrationOnPicoSwanV27 =>
        requireDeviceGroundCalibrationOnPicoSwanV27;
    public bool RequireOfficialOpenXrFloorSpaceV28 =>
        requireOfficialOpenXrFloorSpaceV28;
    public bool RequireSwanOpenXrRuntimeReadyV29 =>
        requireSwanOpenXrRuntimeReadyV29;
    public bool RequireFreshSwanSessionV31 =>
        requireFreshSwanSessionV31;
    public bool RequireStableSwanOriginV32 =>
        requireStableSwanOriginV32;
    public string SavePath => Path.Combine(Application.persistentDataPath, "personally_puzzled_save_v1.json");

    float nextTrackingPoseReconcile;
    bool trackingPoseReconciled;
    bool invalidTrackingPoseLogged;
    bool directHeadPoseFallback;
    bool swanEmulatorDiagnostics;
    float nextSwanInputLog;
    string lastSwanInputSignature;
    string lastSwanGeometryScene;
    readonly List<InputDevice> swanInputDevices = new List<InputDevice>();
    bool resumeReturnPendingV25;
    Coroutine resumeReturnCoroutineV25;

    void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadSave();
        ApplyAccessibilitySettings();
        if (!comfortTransition)
            comfortTransition = GetComponent<RuntimeComfortTransitionV01>();
        if (!comfortTransition)
            comfortTransition = gameObject.AddComponent<RuntimeComfortTransitionV01>();
        comfortTransition.sceneCamera = xrCamera;
        comfortTransition.SetImmediate(true);
        swanEmulatorDiagnostics = IsProjectSwanEmulator();
        if (nativeOpenXrTrackingOnlyV24)
            EnforceNativeOpenXrTrackingV24();
        if (swanEmulatorDiagnostics)
        {
            Debug.Log(
                "[Personally Puzzled Swan Input] Emulator-only input " +
                "telemetry enabled; real Project Swan builds are unaffected.");
            if (!GetComponent<RuntimeProjectSwanManualStepBridgeV01>())
                gameObject.AddComponent<RuntimeProjectSwanManualStepBridgeV01>();
        }
    }

    void OnEnable()
    {
        Application.onBeforeRender += ApplyDirectHeadPoseFallback;
    }

    void OnDisable()
    {
        Application.onBeforeRender -= ApplyDirectHeadPoseFallback;
    }

    IEnumerator Start()
    {
        yield return null;
        if (requireStableSwanOriginV32)
        {
            RuntimeSwanStableOriginV32 stableOrigin =
                GetComponent<RuntimeSwanStableOriginV32>();
            if (!stableOrigin)
            {
                Debug.LogError(
                    "[Personally Puzzled V32 Stable Origin] required " +
                    "runtime policy is missing; keeping the initial stage " +
                    "covered.");
                yield break;
            }
            while (!stableOrigin.CurrentSessionReady)
                yield return null;
        }
        else if (requireFreshSwanSessionV31)
        {
            RuntimeSwanFreshSessionV31 freshSession =
                GetComponent<RuntimeSwanFreshSessionV31>();
            while (freshSession &&
                   !freshSession.CurrentSessionReady)
                yield return null;
        }
        if (string.IsNullOrEmpty(LoadedStageScene))
            yield return TransitionTo(RuntimePuzzleStage.Splash, splashScene);
    }

    void Update()
    {
        ReconcileSwanTrackingPose();
        LogSwanEmulatorInputState();
        if (UnityEngine.InputSystem.Keyboard.current == null)
            return;
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        bool changed = false;
        if (keyboard.f1Key.wasPressedThisFrame)
        {
            Save.handedness = (Save.handedness + 1) % 2;
            changed = true;
        }
        if (keyboard.f2Key.wasPressedThisFrame)
        {
            Save.heightMode = (Save.heightMode + 1) % 2;
            changed = true;
        }
        if (keyboard.f3Key.wasPressedThisFrame)
        {
            Save.singleHandMode = !Save.singleHandMode;
            changed = true;
        }
        if (keyboard.minusKey.wasPressedThisFrame)
        {
            Save.masterVolume = Mathf.Clamp01(Save.masterVolume - 0.05f);
            changed = true;
        }
        if (keyboard.equalsKey.wasPressedThisFrame)
        {
            Save.masterVolume = Mathf.Clamp01(Save.masterVolume + 0.05f);
            changed = true;
        }
        if (changed)
        {
            ApplyAccessibilitySettings();
            SaveNow();
        }
    }

    void LateUpdate()
    {
        ApplyDirectHeadPoseFallback();
    }

    /// <summary>
    /// Project Swan's current Unity OpenXR emulator can expose the correct
    /// floor-space head position through XR.InputDevices while the Input
    /// System tracked-pose driver still leaves the camera transform at the
    /// tracking origin.  That mismatch makes every registered stage appear
    /// 1.6 m too high.  Reconcile only a large positional disagreement; on a
    /// headset (or a future emulator build) whose camera already follows the
    /// head pose this method is a no-op.
    /// </summary>
    void ReconcileSwanTrackingPose()
    {
        // The fallback below exists for Project Swan's desktop emulator. On a
        // real PICO headset it can race OpenXR startup, disable the standard
        // TrackedPoseDriver and produce a head-locked or visibly swimming
        // world. V24 scenes explicitly opt into native device tracking.
        if (nativeOpenXrTrackingOnlyV24 && !swanEmulatorDiagnostics)
            return;
        if (trackingPoseReconciled ||
            Time.unscaledTime < nextTrackingPoseReconcile ||
            !XRSettings.isDeviceActive ||
            !xrOrigin ||
            !xrCamera ||
            !xrOrigin.CameraFloorOffsetObject)
            return;

        nextTrackingPoseReconcile = Time.unscaledTime + 0.10f;
        InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (!head.isValid ||
            !head.TryGetFeatureValue(
                CommonUsages.devicePosition,
                out Vector3 localHeadPosition))
            return;

        // A clean Project Swan floor-space pose is approximately (0, 1.65, 0).
        // The emulator can retain a corrupted navigation pose after a host-side
        // camera gesture (for example Y=-6 m).  Never use such a pose to move the
        // persistent XR origin; keep polling so a subsequent neutral pose can
        // still reconcile the camera without restarting the application.
        bool plausibleHumanPose =
            localHeadPosition.y >= 0.50f &&
            localHeadPosition.y <= 2.60f &&
            Mathf.Abs(localHeadPosition.x) <= 2.00f &&
            Mathf.Abs(localHeadPosition.z) <= 2.00f;
        if (!plausibleHumanPose)
        {
            if (!invalidTrackingPoseLogged)
            {
                invalidTrackingPoseLogged = true;
                Debug.LogWarning(
                    "[Personally Puzzled Swan] Ignoring implausible retained " +
                    $"head pose {localHeadPosition}; waiting for a neutral " +
                    "floor-space pose.");
            }
            return;
        }

        invalidTrackingPoseLogged = false;

        Vector3 expectedWorldPosition =
            xrOrigin.transform.TransformPoint(localHeadPosition);
        Vector3 cameraWorldPosition = xrCamera.transform.position;
        Vector3 correction = expectedWorldPosition - cameraWorldPosition;
        if (correction.magnitude < 0.25f)
        {
            trackingPoseReconciled = true;
            return;
        }

        TrackedPoseDriver driver = xrCamera.GetComponent<TrackedPoseDriver>();
        if (driver)
            driver.enabled = false;
        directHeadPoseFallback = true;
        ApplyDirectHeadPoseFallback();
        trackingPoseReconciled = true;
        Debug.Log(
            "[Personally Puzzled Swan] Enabled direct OpenXR head-pose " +
            "fallback because the Input System camera disagreed with the " +
            "floor-space device pose: " +
            $"head pose: cameraBefore={cameraWorldPosition}, " +
            $"head={expectedWorldPosition}, correction={correction}, " +
            $"cameraAfter={xrCamera.transform.position}");
    }

    void ApplyDirectHeadPoseFallback()
    {
        if (!directHeadPoseFallback || !xrCamera || !xrOrigin)
            return;

        InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (!head.isValid ||
            !head.TryGetFeatureValue(
                CommonUsages.devicePosition,
                out Vector3 localHeadPosition) ||
            localHeadPosition.y < 0.50f ||
            localHeadPosition.y > 2.60f ||
            Mathf.Abs(localHeadPosition.x) > 2.00f ||
            Mathf.Abs(localHeadPosition.z) > 2.00f)
            return;

        // The experience has no locomotion provider: room-scale movement must
        // come exclusively from the tracked head/hands.  Project Swan can emit
        // a tracking-origin update while an additive stage is unloading.  In
        // the affected runtime that update shifts the persistent hierarchy and
        // then the legacy floor-space head pose is applied again, doubling eye
        // height and pushing the whole authored composition below the view.
        // Keep the authoring origin invariant whenever this fallback is active;
        // localHeadPosition still supplies the complete 6DoF player movement.
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        xrOrigin.transform.SetLocalPositionAndRotation(
            Vector3.zero,
            Quaternion.identity);
        if (xrOrigin.CameraFloorOffsetObject)
        {
            Transform offset = xrOrigin.CameraFloorOffsetObject.transform;
            offset.SetLocalPositionAndRotation(
                new Vector3(
                    0f,
                    Save != null && Save.heightMode != 0 ? 1.18f : 0f,
                    0f),
                Quaternion.identity);
        }

        xrCamera.transform.localPosition = localHeadPosition;
        if (head.TryGetFeatureValue(
            CommonUsages.deviceRotation,
            out Quaternion localHeadRotation))
            xrCamera.transform.localRotation = localHeadRotation;
    }

    public void EnforceNativeOpenXrTrackingV24()
    {
        nativeOpenXrTrackingOnlyV24 = true;
        if (swanEmulatorDiagnostics)
            return;
        directHeadPoseFallback = false;
        trackingPoseReconciled = false;
        if (!xrCamera)
            return;
        TrackedPoseDriver driver =
            xrCamera.GetComponent<TrackedPoseDriver>();
        if (driver)
            driver.enabled = true;
    }

    static bool IsProjectSwanEmulator()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using AndroidJavaClass build =
                new AndroidJavaClass("android.os.Build");
            string product = build.GetStatic<string>("PRODUCT") ?? string.Empty;
            string model = build.GetStatic<string>("MODEL") ?? string.Empty;
            string hardware = build.GetStatic<string>("HARDWARE") ?? string.Empty;
            return product.IndexOf("emulator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                model.IndexOf("SDK Spaceos", StringComparison.OrdinalIgnoreCase) >= 0 ||
                hardware.Equals("ranchu", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[Personally Puzzled Swan Input] Emulator detection failed: " +
                exception.Message);
        }
#endif
        return false;
    }

    void LogSwanEmulatorInputState()
    {
        if (!swanEmulatorDiagnostics || Time.unscaledTime < nextSwanInputLog)
            return;
        nextSwanInputLog = Time.unscaledTime + 0.20f;

        StringBuilder state = new StringBuilder(512);
        state.Append("stage=").Append(CurrentStage);
        UnityEngine.InputSystem.Mouse mouse =
            UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
        {
            state.Append(" mouse=").Append(mouse.position.ReadValue());
            state.Append(" mouseDown=").Append(mouse.leftButton.isPressed);
        }
        UnityEngine.InputSystem.Touchscreen touchscreen =
            UnityEngine.InputSystem.Touchscreen.current;
        if (touchscreen != null)
        {
            state.Append(" touch=")
                .Append(touchscreen.primaryTouch.position.ReadValue());
            state.Append(" touchDown=")
                .Append(touchscreen.primaryTouch.press.isPressed);
        }

        swanInputDevices.Clear();
        InputDevices.GetDevices(swanInputDevices);
        for (int i = 0; i < swanInputDevices.Count; i++)
        {
            InputDevice device = swanInputDevices[i];
            state.Append(" | ").Append(device.name)
                .Append('[').Append(device.characteristics).Append(']');
            if (device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked))
                state.Append(" tracked=").Append(tracked);
            if (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
                state.Append(" pos=").Append(position);
            if (device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
                state.Append(" rot=").Append(rotation.eulerAngles);
            if (device.TryGetFeatureValue(CommonUsages.primaryButton, out bool primary))
                state.Append(" primary=").Append(primary);
            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool trigger))
                state.Append(" trigger=").Append(trigger);
            if (device.TryGetFeatureValue(CommonUsages.gripButton, out bool grip))
                state.Append(" grip=").Append(grip);
        }

        string signature = state.ToString();
        if (signature == lastSwanInputSignature)
            return;
        lastSwanInputSignature = signature;
        Debug.Log("[Personally Puzzled Swan Input] " + signature);
    }

    public void EnterSelection() => RequestStage(RuntimePuzzleStage.Selection, selectionScene);

    public void BeginLevel(int index)
    {
        CurrentLevelIndex = Mathf.Clamp(index, 0, 4);
        if (CurrentLevelIndex > Save.highestUnlockedLevel)
        {
            audioConductor?.PlayCue(RuntimeAudioCue.Error, xrCamera ? xrCamera.transform.position : Vector3.zero);
            return;
        }
        RuntimePuzzleStage stage = CurrentLevelIndex == 0 ? RuntimePuzzleStage.Cottage :
            CurrentLevelIndex == 1 ? RuntimePuzzleStage.Ship :
            CurrentLevelIndex == 2 ? RuntimePuzzleStage.Eiffel :
            CurrentLevelIndex == 3 ? RuntimePuzzleStage.Pagoda : RuntimePuzzleStage.Windmill;
        string scene = CurrentLevelIndex == 0 ? cottageScene :
            CurrentLevelIndex == 1 ? shipScene :
            CurrentLevelIndex == 2 ? eiffelScene :
            CurrentLevelIndex == 3 ? pagodaScene : windmillScene;
        RequestStage(stage, scene);
    }

    // V50: begin the very first level regardless of current progress, for the
    // Splash "restart from the first level" entry. Progress/unlocks are kept.
    public void RestartFromFirstStageV50()
    {
        SaveNow();
        BeginLevel(0);
    }

    // V50: a real, complete application exit. Unlike the PICO home button
    // (which only pauses the process, so reopening resumes), this quits the
    // process so the next launch is a genuine cold start from the first stage.
    public void QuitApplicationV50()
    {
        SaveNow();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void CompleteCurrentLevel(int hintsUsed, float elapsedSeconds)
    {
        // V61 #5: 1-5 star rating from time + hints (monotonic, best-kept).
        float t = StarTime(CurrentLevelIndex);
        bool noHint = hintsUsed == 0;
        int stars;
        if (noHint && elapsedSeconds <= t * 0.6f) stars = 5;
        else if (noHint && elapsedSeconds <= t) stars = 4;
        else if (hintsUsed <= 1 && elapsedSeconds <= t * 1.4f) stars = 3;
        else if (hintsUsed <= 3) stars = 2;
        else stars = 1;
        Save.stars[CurrentLevelIndex] = Mathf.Max(Save.stars[CurrentLevelIndex], stars);
        Save.highestUnlockedLevel = Mathf.Max(Save.highestUnlockedLevel, Mathf.Min(4, CurrentLevelIndex + 1));
        SaveNow();
        audioConductor?.PlayCue(RuntimeAudioCue.Complete, xrCamera ? xrCamera.transform.position : Vector3.zero);
        RequestStage(RuntimePuzzleStage.Completion, completionScene);
    }

    public void EnterCollection() => RequestStage(RuntimePuzzleStage.Collection, collectionScene);

    public void RequestStage(RuntimePuzzleStage stage, string sceneName)
    {
        if (stage != RuntimePuzzleStage.Splash &&
            requireStableSwanOriginV32)
        {
            RuntimeSwanStableOriginV32 stableOrigin =
                GetComponent<RuntimeSwanStableOriginV32>();
            if (!stableOrigin ||
                !stableOrigin.TryAuthorizeStageRequest(
                    stage,
                    sceneName))
                return;
        }
        if (stage != RuntimePuzzleStage.Splash &&
            requireFreshSwanSessionV31)
        {
            RuntimeSwanFreshSessionV31 freshSession =
                GetComponent<RuntimeSwanFreshSessionV31>();
            if (freshSession &&
                !freshSession.TryAuthorizeStageRequest(stage, sceneName))
                return;
        }
        if (stage != RuntimePuzzleStage.Splash &&
            requireSwanOpenXrRuntimeReadyV29)
        {
            RuntimeSwanOpenXrRuntimeReadyV29 runtimeReady =
                GetComponent<RuntimeSwanOpenXrRuntimeReadyV29>();
            if (runtimeReady &&
                !runtimeReady.TryAuthorizeStageRequest(stage, sceneName))
                return;
        }
        if (stage != RuntimePuzzleStage.Splash &&
            requireOfficialOpenXrFloorSpaceV28)
        {
            RuntimeOfficialOpenXrFloorSpaceV28 floorSpace =
                GetComponent<RuntimeOfficialOpenXrFloorSpaceV28>();
            if (floorSpace &&
                !floorSpace.TryAuthorizeStageRequest(stage, sceneName))
                return;
        }
        if (stage != RuntimePuzzleStage.Splash &&
            requireDeviceGroundCalibrationOnPicoSwanV27)
        {
            RuntimePicoSwanDeviceGroundCalibrationV27 calibration =
                GetComponent<RuntimePicoSwanDeviceGroundCalibrationV27>();
            if (calibration &&
                !calibration.TryAuthorizeStageRequest(stage, sceneName))
                return;
        }
        if (!IsTransitioning)
            StartCoroutine(TransitionTo(stage, sceneName));
    }

    public void ContinueStageRequestAfterDeviceGroundCalibrationV27(
        RuntimePuzzleStage stage,
        string sceneName)
    {
        if (!IsTransitioning)
            StartCoroutine(TransitionTo(stage, sceneName));
    }

    public void ContinueStageRequestAfterOfficialFloorSpaceV28(
        RuntimePuzzleStage stage,
        string sceneName)
    {
        if (!IsTransitioning)
            StartCoroutine(TransitionTo(stage, sceneName));
    }

    public void ContinueStageRequestAfterSwanOpenXrRuntimeReadyV29(
        RuntimePuzzleStage stage,
        string sceneName)
    {
        if (!IsTransitioning)
            StartCoroutine(TransitionTo(stage, sceneName));
    }

    IEnumerator TransitionTo(RuntimePuzzleStage stage, string sceneName)
    {
        string requestedScene = sceneName;
        sceneName = ResolveLoadableStageScene(stage, sceneName);
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError(
                "[PPVR Startup Routing] BLOCKED BEFORE COVER · stage=" + stage +
                " requested=" + requestedScene +
                " · no matching enabled scene exists in the player build");
            yield break;
        }
        if (!string.Equals(requestedScene, sceneName, StringComparison.Ordinal))
        {
            Debug.LogWarning(
                "[PPVR Startup Routing] remapped stale route · stage=" + stage +
                " requested=" + requestedScene + " resolved=" + sceneName);
        }

        IsTransitioning = true;
        bool initialStage = string.IsNullOrEmpty(LoadedStageScene);
        RuntimePuzzleStage outgoingStage = CurrentStage;
        if (comfortTransition)
        {
            comfortTransition.PrepareHandoff(
                outgoingStage,
                stage,
                initialStage);
            if (initialStage)
                comfortTransition.SetImmediate(true);
            else
            {
                audioConductor?.PlayCue(
                    RuntimeAudioCue.Transition,
                    xrCamera ? xrCamera.transform.position : transform.position);
                yield return comfortTransition.Cover();
            }
        }
        audioConductor?.CrossfadeTo(stage);
        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (load == null)
        {
            if (comfortTransition)
                yield return comfortTransition.Reveal();
            IsTransitioning = false;
            yield break;
        }
        load.allowSceneActivation = true;
        while (!load.isDone)
            yield return null;

        Debug.Log(
            "[PPVR Startup Routing] loaded · stage=" + stage +
            " scene=" + sceneName);

        Scene next = SceneManager.GetSceneByName(sceneName);
        if (next.IsValid())
            SceneManager.SetActiveScene(next);
        string previous = LoadedStageScene;
        LoadedStageScene = sceneName;
        PreviousStage = outgoingStage;
        CurrentStage = stage;
        yield return null;

        ApplyCoveredStageRegistration(next);

        LogSwanStageGeometry(next);

        if (!string.IsNullOrEmpty(previous) && previous != sceneName)
        {
            AsyncOperation unload = SceneManager.UnloadSceneAsync(previous);
            if (unload != null)
                while (!unload.isDone) yield return null;
        }
        AsyncOperation cleanup = Resources.UnloadUnusedAssets();
        if (cleanup != null)
            while (!cleanup.isDone)
                yield return null;
        if (swanEmulatorDiagnostics)
        {
            // Re-audit after the old additive stage has actually gone away.
            // This distinguishes a camera/renderer failure in the new stage
            // from a transient overlap while both authored rooms are loaded.
            yield return null;
            LogSwanStageRendererDiagnostics(next, "settled");
        }
        if (comfortTransition)
        {
            yield return comfortTransition.HoldCovered();
            yield return comfortTransition.Reveal();
        }
        IsTransitioning = false;
    }

    static string ResolveLoadableStageScene(
        RuntimePuzzleStage stage,
        string requestedScene)
    {
        if (!string.IsNullOrWhiteSpace(requestedScene) &&
            Application.CanStreamedLevelBeLoaded(requestedScene))
            return requestedScene;

        string prefix;
        switch (stage)
        {
            case RuntimePuzzleStage.Splash:
                prefix = "Runtime_Splash_";
                break;
            case RuntimePuzzleStage.Selection:
                prefix = "Runtime_Selection_";
                break;
            case RuntimePuzzleStage.Cottage:
                prefix = "Runtime_Level_Cottage";
                break;
            case RuntimePuzzleStage.Ship:
                prefix = "Runtime_Level_Ship_";
                break;
            case RuntimePuzzleStage.Eiffel:
                prefix = "Runtime_Level_Eiffel_";
                break;
            case RuntimePuzzleStage.Pagoda:
                prefix = "Runtime_Level_Pagoda_";
                break;
            case RuntimePuzzleStage.Windmill:
                prefix = "Runtime_Level_Windmill_";
                break;
            case RuntimePuzzleStage.Completion:
                prefix = "Runtime_Completion_";
                break;
            case RuntimePuzzleStage.Collection:
                prefix = "Runtime_Collection_";
                break;
            default:
                return null;
        }

        for (int index = 0;
             index < SceneManager.sceneCountInBuildSettings;
             index++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(index);
            string candidate = Path.GetFileNameWithoutExtension(path);
            if (candidate.StartsWith(prefix, StringComparison.Ordinal) &&
                Application.CanStreamedLevelBeLoaded(candidate))
                return candidate;
        }
        return null;
    }

    void ApplyCoveredStageRegistration(Scene stageScene)
    {
        if (!stageScene.IsValid())
            return;
        RuntimeCoveredStageRegistrationV06 registration = null;
        foreach (GameObject root in stageScene.GetRootGameObjects())
        {
            registration = root.GetComponentInChildren<
                RuntimeCoveredStageRegistrationV06>(true);
            if (registration)
                break;
        }
        if (!registration)
            return;
        if (!registration.ApplyNow(this))
        {
            Debug.LogWarning(
                "[Personally Puzzled Handoff V06] incoming stage could not " +
                "be registered while covered: " + stageScene.name,
                registration);
        }
    }

    void LogSwanStageGeometry(Scene stageScene)
    {
        if (!swanEmulatorDiagnostics ||
            !xrCamera ||
            !stageScene.IsValid() ||
            lastSwanGeometryScene == stageScene.name)
            return;

        lastSwanGeometryScene = stageScene.name;
        Bounds rendererBounds = default;
        bool hasBounds = false;
        Transform registeredContent = null;
        PolishedRuntimeStagePortalV02 portal = null;
        foreach (GameObject root in stageScene.GetRootGameObjects())
        {
            if (!registeredContent &&
                root.name == "Persistent-eye registered polished stage content")
                registeredContent = root.transform;
            if (!portal)
                portal = root.GetComponentInChildren<PolishedRuntimeStagePortalV02>(true);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer || !renderer.enabled)
                    continue;
                if (!hasBounds)
                {
                    rendererBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                    rendererBounds.Encapsulate(renderer.bounds);
            }
        }

        Vector3 targetWorld = portal && portal.activationTarget
            ? portal.activationTarget.position
            : hasBounds
                ? rendererBounds.center
                : Vector3.zero;
        Vector3 viewport = xrCamera.WorldToViewportPoint(targetWorld);
        Debug.Log(
            "[Personally Puzzled Swan Geometry] " +
            $"stage={CurrentStage} scene={stageScene.name} " +
            $"cameraWorld={xrCamera.transform.position} " +
            $"cameraLocal={xrCamera.transform.localPosition} " +
            $"bootstrapWorld={transform.position} " +
            $"originWorld={(xrOrigin ? xrOrigin.transform.position : Vector3.zero)} " +
            $"offsetWorld={(xrOrigin && xrOrigin.CameraFloorOffsetObject ? xrOrigin.CameraFloorOffsetObject.transform.position : Vector3.zero)} " +
            $"offsetLocal={(xrOrigin && xrOrigin.CameraFloorOffsetObject ? xrOrigin.CameraFloorOffsetObject.transform.localPosition : Vector3.zero)} " +
            $"cameraRotation={xrCamera.transform.rotation.eulerAngles} " +
            $"fov={xrCamera.fieldOfView:F2} aspect={xrCamera.aspect:F3} " +
            $"targetWorld={targetWorld} targetViewport={viewport} " +
            $"boundsCenter={(hasBounds ? rendererBounds.center : Vector3.zero)} " +
            $"boundsSize={(hasBounds ? rendererBounds.size : Vector3.zero)} " +
            $"contentWorld={(registeredContent ? registeredContent.position : Vector3.zero)} " +
            $"contentRotation={(registeredContent ? registeredContent.rotation.eulerAngles : Vector3.zero)}");
        LogSwanStageRendererDiagnostics(stageScene, "loaded");
    }

    void LogSwanStageRendererDiagnostics(Scene stageScene, string phase)
    {
        if (!swanEmulatorDiagnostics || !xrCamera || !stageScene.IsValid())
            return;

        Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(xrCamera);
        List<KeyValuePair<float, Renderer>> ranked =
            new List<KeyValuePair<float, Renderer>>();
        int rendererCount = 0;
        int activeRendererCount = 0;
        int layerVisibleCount = 0;
        int frustumRendererCount = 0;
        int forceOffCount = 0;
        int activeLightCount = 0;
        float activeLightIntensity = 0f;

        foreach (GameObject root in stageScene.GetRootGameObjects())
        {
            foreach (Renderer renderer in
                     root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer)
                    continue;
                rendererCount++;
                if (renderer.forceRenderingOff)
                    forceOffCount++;
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;
                activeRendererCount++;
                if ((xrCamera.cullingMask & (1 << renderer.gameObject.layer)) == 0)
                    continue;
                layerVisibleCount++;
                if (!GeometryUtility.TestPlanesAABB(frustum, renderer.bounds))
                    continue;
                frustumRendererCount++;
                Vector3 viewport =
                    xrCamera.WorldToViewportPoint(renderer.bounds.center);
                float rank = viewport.z > 0f
                    ? Mathf.Abs(viewport.x - 0.5f) +
                      Mathf.Abs(viewport.y - 0.5f) +
                      Mathf.Min(viewport.z, 100f) * 0.0001f
                    : 1000f + Mathf.Abs(viewport.z);
                ranked.Add(new KeyValuePair<float, Renderer>(rank, renderer));
            }
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                if (!light || !light.enabled || !light.gameObject.activeInHierarchy)
                    continue;
                activeLightCount++;
                activeLightIntensity += light.intensity;
            }
        }

        ranked.Sort((left, right) => left.Key.CompareTo(right.Key));
        StringBuilder nearest = new StringBuilder(4096);
        int detailCount = Mathf.Min(18, ranked.Count);
        for (int index = 0; index < detailCount; index++)
        {
            Renderer renderer = ranked[index].Value;
            Vector3 viewport =
                xrCamera.WorldToViewportPoint(renderer.bounds.center);
            if (index > 0)
                nearest.Append(" || ");
            nearest.Append(renderer.name)
                .Append(" active=").Append(renderer.gameObject.activeInHierarchy)
                .Append(" enabled=").Append(renderer.enabled)
                .Append(" visible=").Append(renderer.isVisible)
                .Append(" forceOff=").Append(renderer.forceRenderingOff)
                .Append(" layer=").Append(renderer.gameObject.layer)
                .Append(" viewport=").Append(viewport)
                .Append(" bounds=").Append(renderer.bounds.size)
                .Append(" materials=");
            Material[] materials = renderer.sharedMaterials;
            for (int materialIndex = 0;
                 materialIndex < materials.Length;
                 materialIndex++)
            {
                if (materialIndex > 0)
                    nearest.Append('+');
                Material material = materials[materialIndex];
                nearest.Append(material ? material.name : "null")
                    .Append('/')
                    .Append(material && material.shader
                        ? material.shader.name
                        : "no-shader");
            }
        }

        Debug.Log(
            "[Personally Puzzled Swan Render Audit] " +
            $"phase={phase} stage={CurrentStage} scene={stageScene.name} " +
            $"cameraActive={xrCamera.gameObject.activeInHierarchy} " +
            $"cameraEnabled={xrCamera.enabled} " +
            $"cullingMask=0x{xrCamera.cullingMask:X8} " +
            $"near={xrCamera.nearClipPlane:F3} far={xrCamera.farClipPlane:F1} " +
            $"targetTexture={(xrCamera.targetTexture ? xrCamera.targetTexture.name : "screen")} " +
            $"renderers={rendererCount} active={activeRendererCount} " +
            $"layerVisible={layerVisibleCount} frustum={frustumRendererCount} " +
            $"forceOff={forceOffCount} lights={activeLightCount} " +
            $"lightIntensity={activeLightIntensity:F2} nearest=[{nearest}]");
    }

    void LoadSave()
    {
        try
        {
            if (File.Exists(SavePath))
                Save = JsonUtility.FromJson<PersonallyPuzzledSaveData>(File.ReadAllText(SavePath));
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Personally Puzzled Runtime] Save recovery: " + exception.Message);
            Save = new PersonallyPuzzledSaveData();
        }
        if (Save == null || Save.version != SaveVersion || Save.stars == null)
            Save = new PersonallyPuzzledSaveData();
        else if (Save.stars.Length != 5)
        {
            int[] migrated = new int[5];
            Array.Copy(Save.stars, migrated, Mathf.Min(Save.stars.Length, migrated.Length));
            Save.stars = migrated;
        }
        Save.highestUnlockedLevel = Mathf.Clamp(Save.highestUnlockedLevel, 0, 4);
        if (unlockAllLevelsV57)
            // V57: unlock every level so each puzzle can be entered and tested
            // from Selection without first clearing the ones before it. This
            // only widens access; stars/progress already earned are preserved.
            Save.highestUnlockedLevel = 4;
    }

    public void SaveNow()
    {
        if (PersistenceSuspended)
            return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
            string temporary = SavePath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(Save, true));
            if (File.Exists(SavePath)) File.Delete(SavePath);
            File.Move(temporary, SavePath);
        }
        catch (Exception exception)
        {
            Debug.LogError("[Personally Puzzled Runtime] Save failed: " + exception.Message);
        }
    }

    public void SetPersistenceSuspendedForAutomation(bool suspended)
    {
        PersistenceSuspended = suspended;
    }

    public void ApplyAccessibilitySettings()
    {
        if (xrOrigin)
        {
            if ((forceFloorTrackingOnPicoSwanV26 ||
                 requireOfficialOpenXrFloorSpaceV28 ||
                 requireSwanOpenXrRuntimeReadyV29) &&
                Save != null)
                Save.heightMode = 0;
            bool floorTracking =
                requireSwanOpenXrRuntimeReadyV29 ||
                requireOfficialOpenXrFloorSpaceV28 ||
                forceFloorTrackingOnPicoSwanV26 ||
                Save == null ||
                Save.heightMode == 0;
            xrOrigin.RequestedTrackingOriginMode = floorTracking
                ? XROrigin.TrackingOriginMode.Floor
                : XROrigin.TrackingOriginMode.Device;
            xrOrigin.CameraYOffset = floorTracking ? 0f : 1.18f;
        }
        if (audioConductor)
            audioConductor.ApplyVolumes(Save.masterVolume, Save.musicVolume, Save.effectsVolume);
    }

    public bool EnforceSwanFloorTrackingV26(bool persistMigration)
    {
        forceFloorTrackingOnPicoSwanV26 = true;
        bool migrated = Save != null && Save.heightMode != 0;
        if (Save != null)
            Save.heightMode = 0;
        ApplyAccessibilitySettings();
        if (persistMigration && migrated)
            SaveNow();
        return migrated;
    }

    static float StarTime(int level) =>
        level == 0 ? 150f :
        level == 1 ? 300f :
        level == 2 ? 480f :
        level == 3 ? 420f : 390f;

    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SaveNow();
            if (returnHomeOnResumeV25 &&
                !string.IsNullOrEmpty(LoadedStageScene) &&
                CurrentStage != RuntimePuzzleStage.Splash)
            {
                ResumeWasInterruptedV25 = true;
                resumeReturnPendingV25 = true;
                foreach (RuntimePuzzleHandInteractor interactor in
                         FindObjectsByType<RuntimePuzzleHandInteractor>(
                             FindObjectsInactive.Exclude,
                             FindObjectsSortMode.None))
                    interactor.CancelInteractionV25();
            }
            return;
        }
        if (returnHomeOnResumeV25 &&
            resumeReturnPendingV25 &&
            resumeReturnCoroutineV25 == null)
            resumeReturnCoroutineV25 =
                StartCoroutine(ReturnHomeAfterResumeV25());
    }

    IEnumerator ReturnHomeAfterResumeV25()
    {
        yield return null;
        yield return new WaitForSecondsRealtime(0.18f);
        resumeReturnPendingV25 = false;
        resumeReturnCoroutineV25 = null;
        if (!IsTransitioning &&
            CurrentStage != RuntimePuzzleStage.Splash)
            yield return TransitionTo(
                RuntimePuzzleStage.Splash,
                splashScene);
    }

#if UNITY_EDITOR
    public void SimulateResumeReturnForSmokeV25()
    {
        if (!returnHomeOnResumeV25)
            return;
        ResumeWasInterruptedV25 = true;
        resumeReturnPendingV25 = true;
        if (resumeReturnCoroutineV25 == null)
            resumeReturnCoroutineV25 =
                StartCoroutine(ReturnHomeAfterResumeV25());
    }
#endif

    void OnApplicationQuit() => SaveNow();
}
