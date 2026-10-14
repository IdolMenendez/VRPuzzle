using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

/// <summary>
/// Physical, UI-free stage bridge for the polished V02 additive flow.
/// It keeps the accepted Stage 1/2/7 interactions, supports the V06 archive
/// controller, and maps only authored selection exhibits to playable levels.
/// </summary>
public sealed class PolishedRuntimeStagePortalV02 : MonoBehaviour
{
    public RuntimePuzzleStage stage;
    public Transform activationTarget;
    public float activationRadius = 0.14f;
    [Min(0.12f)] public float activationScreenRadiusX = 0.18f;
    [Min(0.12f)] public float activationScreenRadiusY = 0.14f;
    public float minimumStageSeconds = 0.8f;
    public int[] selectionLevelByExhibit;
    public SelectionCarouselController selection;
    public SelectionPreviewGateV07 selectionPreviewGate;
    public SplashWorkshopController splash;
    public CompletionInteractionController completion;
    public CollectionArchiveRitualV06Controller collectionV06;
    public bool muteStageLocalAudio = true;
    [Tooltip(
        "V33 opt-in. When the semantic exhibit input already claimed a pinch, " +
        "do not also run the generic centered portal confirmation that frame.")]
    public bool preferDirectExhibitIntentV33;
    [Tooltip(
        "V35 Splash opt-in. The immersive Splash controller owns gaze + pinch " +
        "confirmation. Generic pinch/touch input cannot advance the stage.")]
    public bool splashActivationOwnsConfirmationV35;
    [Tooltip(
        "V36 Selection opt-in. SelectionRemoteConfirmV33 is the only component " +
        "allowed to commit a centered exhibit; generic portal input is ignored.")]
    public bool selectionRemoteConfirmOwnsConfirmationV36;
    [Tooltip(
        "V47 Completion/portal input reads the persistent OpenXR hand " +
        "interaction provider instead of independently polling XRHand.isTracked.")]
    public bool resilientRealHandPinchV47;

    readonly List<XRInputDevice> devices = new List<XRInputDevice>();
    readonly List<XRHandSubsystem> handSubsystems = new List<XRHandSubsystem>();
    XRHandSubsystem hands;
    float enteredAt;
    float nextScan;
    bool wasConfirming;
    float collectionCompletedAt = -1f;
    bool stageAdvanceQueued;
    float stageAdvanceAt;
    int queuedLevel = -1;
    bool swanDiagnostics;
    bool swanButtonDiagnosticLogged;
    bool touchPointerTracking;
    Vector2 touchPointerStart;

    void Start()
    {
        enteredAt = Time.unscaledTime;
        PersistentPuzzleBootstrap bootstrap = PersistentPuzzleBootstrap.Instance;
        swanDiagnostics = bootstrap && bootstrap.SwanEmulatorDiagnosticsEnabled;
        if (!ValidateBinding())
        {
            enabled = false;
            return;
        }
        Camera camera = Camera.main;
        if (!camera)
            camera = FindSceneCamera();
        if (selection)
        {
            // Direct editor play of the authored Selection scene has no
            // MainCamera-tagged persistent rig. Preserve its bound reference
            // camera instead of erasing the only valid pointer camera.
            if (camera)
                selection.sceneCamera = camera;
            selection.referenceTransitionT = -1f;
        }
        if (splash)
        {
            if (camera)
                splash.sceneCamera = camera;
            splash.nextSceneName = string.Empty;
        }
        if (completion)
        {
            if (camera)
                completion.sceneCamera = camera;
            completion.freezeReferencePose = false;
        }
        if (collectionV06)
        {
            if (camera)
                collectionV06.sceneCamera = camera;
            collectionV06.freezeReferencePose = false;
        }
        LogSwan(
            $"ready stage={stage} target={(activationTarget ? activationTarget.name : "none")} " +
            $"targetWorld={(activationTarget ? activationTarget.position.ToString("F2") : "none")} " +
            $"keyboard={(Keyboard.current != null)} touch={(Touchscreen.current != null)} " +
            SelectionState(bootstrap));
        if (!muteStageLocalAudio)
            return;
        foreach (AudioSource source in
                 FindObjectsByType<AudioSource>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (source.gameObject.scene == gameObject.scene)
            {
                source.Stop();
                source.playOnAwake = false;
                source.mute = true;
            }
        }
    }

    void Update()
    {
        PersistentPuzzleBootstrap bootstrap = PersistentPuzzleBootstrap.Instance;
        if (!bootstrap || bootstrap.IsTransitioning)
            return;

        if (stageAdvanceQueued)
        {
            if (Time.unscaledTime < stageAdvanceAt)
                return;
            stageAdvanceQueued = false;
            if (stage == RuntimePuzzleStage.Splash ||
                stage == RuntimePuzzleStage.Collection)
            {
                LogSwan($"advance stage={stage} target=Selection");
                bootstrap.EnterSelection();
            }
            else if (stage == RuntimePuzzleStage.Selection && queuedLevel >= 0)
            {
                LogSwan($"advance stage=Selection targetLevel={queuedLevel}");
                bootstrap.BeginLevel(queuedLevel);
            }
            else if (stage == RuntimePuzzleStage.Completion)
            {
                if (queuedLevel >= 0)
                {
                    LogSwan(
                        $"advance stage=Completion targetLevel={queuedLevel}");
                    bootstrap.BeginLevel(queuedLevel);
                }
                else
                {
                    LogSwan("advance stage=Completion target=Collection");
                    bootstrap.EnterCollection();
                }
            }
            queuedLevel = -1;
            return;
        }

        if (stage == RuntimePuzzleStage.Collection)
        {
            bool completed =
                collectionV06 &&
                collectionV06.Completed;
            if (completed)
            {
                if (collectionCompletedAt < 0f)
                    collectionCompletedAt = Time.unscaledTime;
                if (Time.unscaledTime - collectionCompletedAt >= 1.75f)
                    QueueStageAdvance(0.01f);
            }
            return;
        }

        if (Time.unscaledTime - enteredAt < minimumStageSeconds)
            return;

        if (stage ==
                RuntimePuzzleStage.Splash &&
            splashActivationOwnsConfirmationV35)
        {
            if (splash &&
                splash.IsActivated)
            {
                RuntimeAudioRouter.PlayCue(
                    RuntimeAudioCue.Touch,
                    activationTarget
                        ? activationTarget.position
                        : transform.position);
                QueueStageAdvance(
                    Mathf.Max(
                        0.92f,
                        splash.transitionDelay *
                        0.82f));
                LogSwan(
                    "confirm-accepted source=v35-gaze-new-pinch " +
                    "stage=Splash");
            }
            return;
        }

        if (stage == RuntimePuzzleStage.Selection &&
            selectionRemoteConfirmOwnsConfirmationV36)
            return;

        bool confirming = ReadConfirm(out string xrSource);
        bool pressed = confirming && !wasConfirming;
        wasConfirming = confirming;
        string source = pressed ? xrSource : null;
        if (string.IsNullOrEmpty(source) && ReadDesktopConfirm())
            source = "keyboard";
        if (string.IsNullOrEmpty(source) && ReadScreenPointerTarget(out string pointerSource))
            source = pointerSource;
        if (string.IsNullOrEmpty(source))
            return;
        if (resilientRealHandPinchV47)
            Debug.Log(
                "[PPVR V47 Portal] event=press-edge stage=" + stage +
                " source=" + source);
        if (stage == RuntimePuzzleStage.Selection &&
            preferDirectExhibitIntentV33 &&
            selection)
        {
            SelectionDirectPuzzleIntentV31 directIntent =
                selection.GetComponent<
                    SelectionDirectPuzzleIntentV31>();
            if (directIntent &&
                directIntent.EntryPending)
            {
                LogSwan(
                    $"confirm-consumed source={source} " +
                    "stage=Selection reason=direct-exhibit-intent");
                return;
            }
        }
        LogSwan($"confirm source={source} stage={stage} {SelectionState(bootstrap)}");
        HandleConfirmedAction(bootstrap, source);
    }

    bool HandleConfirmedAction(
        PersistentPuzzleBootstrap bootstrap,
        string source = "script")
    {
        if (!bootstrap || bootstrap.IsTransitioning || stageAdvanceQueued)
        {
            LogSwan(
                $"confirm-rejected source={source} stage={stage} " +
                $"bootstrap={(bootstrap != null)} transitioning={(bootstrap && bootstrap.IsTransitioning)} " +
                $"advanceQueued={stageAdvanceQueued}");
            return false;
        }
        if (stage == RuntimePuzzleStage.Splash)
        {
            splash?.ActivateWorkshop();
            RuntimeAudioRouter.PlayCue(
                RuntimeAudioCue.Touch,
                activationTarget ? activationTarget.position : transform.position);
            // Let the Logo settle/wake ceremony complete before the shared
            // comfort transition covers the eye and loads Selection.
            QueueStageAdvance(
                splash
                    ? Mathf.Max(0.92f, splash.transitionDelay * 0.82f)
                    : 0.96f);
            LogSwan($"confirm-accepted source={source} stage=Splash");
            return true;
        }

        if (stage == RuntimePuzzleStage.Selection)
        {
            if (selection && (selection.IsTransitioning || selection.IsCommitting))
            {
                LogSwan(
                    $"confirm-rejected source={source} stage=Selection " +
                    $"carouselTransitioning={selection.IsTransitioning} " +
                    $"carouselCommitting={selection.IsCommitting}");
                return false;
            }
            int exhibitCount =
                selection && selection.exhibits != null
                    ? selection.exhibits.Length
                    : 0;
            int exhibit = selection
                ? Mathf.Clamp(
                    selection.currentIndex,
                    0,
                    Mathf.Max(0, exhibitCount - 1))
                : 0;
            int level = ResolveSelectionLevel(exhibit);
            if (level < 0)
            {
                RuntimeAudioRouter.PlayCue(
                    RuntimeAudioCue.Error,
                    activationTarget ? activationTarget.position : transform.position);
                LogSwan(
                    $"confirm-rejected source={source} stage=Selection " +
                    $"exhibit={exhibit} reason=unplayable-placeholder");
                return false;
            }
            if (bootstrap.Save == null || level > bootstrap.Save.highestUnlockedLevel)
            {
                RuntimeAudioRouter.PlayCue(
                    RuntimeAudioCue.Error,
                    activationTarget ? activationTarget.position : transform.position);
                LogSwan(
                    $"confirm-rejected source={source} stage=Selection " +
                    $"exhibit={exhibit} level={level} " +
                    $"highestUnlocked={(bootstrap.Save != null ? bootstrap.Save.highestUnlockedLevel : -1)} " +
                    "reason=locked-level");
                return false;
            }
            if (selectionPreviewGate &&
                !selectionPreviewGate.RequestCommit(exhibit))
            {
                LogSwan(
                    $"confirm-consumed source={source} stage=Selection " +
                    $"exhibit={exhibit} level={level} reason=preview-before-commit");
                return true;
            }
            queuedLevel = level;
            bool commitStarted = selection && selection.BeginCommit();
            if (!commitStarted)
            {
                queuedLevel = -1;
                RuntimeAudioRouter.PlayCue(
                    RuntimeAudioCue.Error,
                    activationTarget ? activationTarget.position : transform.position);
                LogSwan(
                    $"confirm-rejected source={source} stage=Selection " +
                    $"exhibit={exhibit} level={level} reason=commit-not-started");
                return false;
            }
            selectionPreviewGate?.NotifyCommitStarted();
            RuntimeAudioRouter.PlayCue(
                RuntimeAudioCue.CarouselSwipe,
                activationTarget ? activationTarget.position : transform.position);
            QueueStageAdvance(Mathf.Max(0.70f, selection.commitSeconds * 0.90f));
            LogSwan(
                $"confirm-accepted source={source} stage=Selection " +
                $"exhibit={exhibit} level={level}");
            return true;
        }

        if (stage == RuntimePuzzleStage.Completion)
        {
            int nextLevel = bootstrap.CurrentLevelIndex + 1;
            queuedLevel =
                nextLevel <= 4 &&
                bootstrap.Save != null &&
                nextLevel <= bootstrap.Save.highestUnlockedLevel
                    ? nextLevel
                    : -1;
            RuntimeAudioRouter.PlayCue(
                RuntimeAudioCue.Touch,
                activationTarget ? activationTarget.position : transform.position);
            QueueStageAdvance(0.72f);
            LogSwan(
                $"confirm-accepted source={source} stage=Completion " +
                (queuedLevel >= 0
                    ? $"nextLevel={queuedLevel}"
                    : "target=Collection"));
            if (resilientRealHandPinchV47)
                Debug.Log(
                    "[PPVR V47 Portal] event=accepted stage=Completion " +
                    "source=" + source +
                    " next=" +
                    (queuedLevel >= 0
                        ? queuedLevel.ToString()
                        : "Collection"));
            return true;
        }
        return false;
    }

    /// <summary>
    /// V31's direct exhibit intent calls the same authoritative Selection
    /// commit path after a side exhibit finishes moving to center. Keeping
    /// level mapping, lock checks, audio, comfort transition and save flow in
    /// this portal prevents a hand pinch from becoming a second navigation
    /// implementation.
    /// </summary>
    public bool TryConfirmCurrentSelectionForV31(string source)
    {
        if (stage != RuntimePuzzleStage.Selection)
            return false;
        return HandleConfirmedAction(
            PersistentPuzzleBootstrap.Instance,
            string.IsNullOrEmpty(source)
                ? "v31-direct-exhibit"
                : source);
    }

    public bool TriggerConfirmedActionForSwanEmulator()
    {
        PersistentPuzzleBootstrap bootstrap =
            PersistentPuzzleBootstrap.Instance;
        if (!bootstrap ||
            !bootstrap.SwanEmulatorDiagnosticsEnabled ||
            Time.unscaledTime - enteredAt < minimumStageSeconds)
            return false;
        return HandleConfirmedAction(bootstrap, "swan-adb-single-step");
    }

#if UNITY_EDITOR
    public bool TriggerConfirmedActionForSmoke()
    {
        return HandleConfirmedAction(
            PersistentPuzzleBootstrap.Instance,
            "editor-smoke");
    }

    public bool IsStageAdvanceQueuedForSmoke => stageAdvanceQueued;
#endif

    void QueueStageAdvance(float delay)
    {
        if (stageAdvanceQueued)
            return;
        stageAdvanceQueued = true;
        stageAdvanceAt = Time.unscaledTime + Mathf.Max(0f, delay);
        LogSwan(
            $"advance-queued stage={stage} delay={Mathf.Max(0f, delay):F2} " +
            $"level={queuedLevel}");
    }

    int ResolveSelectionLevel(int exhibit)
    {
        if (selectionLevelByExhibit != null &&
            exhibit >= 0 &&
            exhibit < selectionLevelByExhibit.Length)
            return selectionLevelByExhibit[exhibit];
        return -1;
    }

    bool ReadDesktopConfirm()
    {
        return Keyboard.current != null &&
            (Keyboard.current.enterKey.wasPressedThisFrame ||
             Keyboard.current.spaceKey.wasPressedThisFrame);
    }

    bool ReadScreenPointerTarget(out string source)
    {
        source = null;
        if (!activationTarget || !InteractionCamera())
            return false;
        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame &&
            ScreenPointerTargetsActivation(Mouse.current.position.ReadValue()))
        {
            source = "mouse-target";
            return true;
        }
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            if (touch.press.wasPressedThisFrame)
            {
                touchPointerTracking = true;
                touchPointerStart = touch.position.ReadValue();
            }
            if (touchPointerTracking && touch.press.wasReleasedThisFrame)
            {
                touchPointerTracking = false;
                Vector2 position = touch.position.ReadValue();
                float tapTravel = Mathf.Max(32f, Screen.width * 0.035f);
                if (Vector2.Distance(touchPointerStart, position) <= tapTravel &&
                    ScreenPointerTargetsActivation(position))
                {
                    source = "touch-target";
                    return true;
                }
            }
            else if (touchPointerTracking && !touch.press.isPressed)
            {
                touchPointerTracking = false;
            }
        }
        return false;
    }

    bool ScreenPointerTargetsActivation(Vector2 screenPosition)
    {
        Camera camera = InteractionCamera();
        if (!camera || !activationTarget)
            return false;
        Ray ray = camera.ScreenPointToRay(screenPosition);
        foreach (RaycastHit hit in Physics.RaycastAll(
            ray,
            20f,
            ~0,
            QueryTriggerInteraction.Collide))
        {
            if (WithinHierarchy(hit.transform, activationTarget) ||
                (selection && WithinHierarchy(hit.transform, selection.CurrentExhibit)))
                return true;
        }

        // The authored C-position anchor is intentionally collider-free so it
        // cannot become a hidden virtual button.  Project Swan touch input is
        // therefore evaluated against its visible screen-space exhibit area.
        Vector3 target = camera.WorldToScreenPoint(activationTarget.position);
        if (target.z <= 0f)
            return false;
        float radiusX = Mathf.Max(
            96f, Screen.width * activationScreenRadiusX);
        float radiusY = Mathf.Max(
            96f, Screen.height * activationScreenRadiusY);
        Vector2 normalizedDelta = new Vector2(
            (screenPosition.x - target.x) / radiusX,
            (screenPosition.y - target.y) / radiusY);
        return normalizedDelta.sqrMagnitude <= 1f;
    }

    Camera InteractionCamera()
    {
        if (Camera.main)
            return Camera.main;
        if (selection && selection.sceneCamera)
            return selection.sceneCamera;
        if (splash && splash.sceneCamera)
            return splash.sceneCamera;
        if (completion && completion.sceneCamera)
            return completion.sceneCamera;
        if (collectionV06 && collectionV06.sceneCamera)
            return collectionV06.sceneCamera;
        return FindSceneCamera();
    }

    Camera FindSceneCamera()
    {
        Camera fallback = null;
        foreach (Camera candidate in FindObjectsByType<Camera>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (!candidate || candidate.gameObject.scene != gameObject.scene)
                continue;
            if (candidate.enabled && candidate.gameObject.activeInHierarchy)
                return candidate;
            if (!fallback)
                fallback = candidate;
        }
        return fallback;
    }

#if UNITY_EDITOR
    public Camera InteractionCameraForAutomation =>
        InteractionCamera();

    public bool ScreenPointerTargetsActivationForAutomation(
        Vector2 screenPosition)
    {
        return ScreenPointerTargetsActivation(screenPosition);
    }
#endif

    bool ReadConfirm(out string source)
    {
        source = null;
#if UNITY_EDITOR
        bool providerAutomationV47 =
            resilientRealHandPinchV47 &&
            RuntimeRealHandPinchV47.Instance &&
            RuntimeRealHandPinchV47.Instance.AutomationActiveV47;
#else
        const bool providerAutomationV47 = false;
#endif
        if (RuntimeDesktopHeadNavigatorV01.DesktopEditorPreviewActive &&
            !providerAutomationV47)
            return false;
        bool nearRequired = activationTarget &&
            stage != RuntimePuzzleStage.Collection;
        devices.Clear();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.TrackedDevice,
            devices);
        foreach (XRInputDevice device in devices)
        {
            bool button =
                (device.TryGetFeatureValue(
                    XRCommonUsages.primaryButton,
                    out bool primary) && primary) ||
                (device.TryGetFeatureValue(
                    XRCommonUsages.triggerButton,
                    out bool trigger) && trigger);
            if (!button)
                continue;
            if (swanDiagnostics && !swanButtonDiagnosticLogged)
            {
                swanButtonDiagnosticLogged = true;
                bool diagnosticHasPosition = device.TryGetFeatureValue(
                    XRCommonUsages.devicePosition,
                    out Vector3 diagnosticLocalPosition);
                Transform diagnosticTracking =
                    Camera.main && Camera.main.transform.parent
                        ? Camera.main.transform.parent
                        : null;
                Vector3 diagnosticWorldPosition = diagnosticHasPosition
                    ? diagnosticTracking
                        ? diagnosticTracking.TransformPoint(diagnosticLocalPosition)
                        : diagnosticLocalPosition
                    : Vector3.zero;
                float diagnosticAngle =
                    Camera.main && activationTarget
                        ? Vector3.Angle(
                            Camera.main.transform.forward,
                            activationTarget.position -
                            Camera.main.transform.position)
                        : -1f;
                LogSwan(
                    $"xr-button-observed stage={stage} device={device.name} " +
                    $"characteristics={device.characteristics} " +
                    $"hasPosition={diagnosticHasPosition} " +
                    $"world={diagnosticWorldPosition:F2} " +
                    $"target={(activationTarget ? activationTarget.position.ToString("F2") : "none")} " +
                    $"gazeAngle={diagnosticAngle:F2}");
            }
            if (!nearRequired)
            {
                source = "xr-button";
                return true;
            }
            if (device.TryGetFeatureValue(
                XRCommonUsages.devicePosition,
                out Vector3 local))
            {
                Transform tracking =
                    Camera.main && Camera.main.transform.parent
                        ? Camera.main.transform.parent
                        : null;
                Vector3 world = tracking
                    ? tracking.TransformPoint(local)
                    : local;
                if (Vector3.Distance(world, activationTarget.position) <=
                    activationRadius)
                {
                    source = "xr-near-button";
                    return true;
                }
                if (stage == RuntimePuzzleStage.Selection &&
                    device.TryGetFeatureValue(
                        XRCommonUsages.deviceRotation,
                        out Quaternion localRotation))
                {
                    Vector3 direction = tracking
                        ? tracking.TransformDirection(localRotation * Vector3.forward)
                        : localRotation * Vector3.forward;
                    if (RayPointsAtActivation(world, direction, 11f))
                    {
                        source = "xr-selection-ray";
                        return true;
                    }
                }
                // Project Swan 6.1 currently reports the semantic trigger but
                // can retain the simulated controller at the head origin.
                // Accept a deliberate trigger only while the authored target
                // is inside the render-camera gaze cone.  This adapter is
                // emulator-only; real PICO hardware still requires the near
                // direct-touch or tracked ray paths above.
                if (swanDiagnostics &&
                    Camera.main &&
                    RayPointsAtActivation(
                        Camera.main.transform.position,
                        Camera.main.transform.forward,
                        stage == RuntimePuzzleStage.Splash ? 20f : 12f))
                {
                    source = "swan-visible-gaze-trigger";
                    return true;
                }
            }
        }

        swanButtonDiagnosticLogged = false;

        if (resilientRealHandPinchV47 &&
            RuntimeRealHandPinchV47.Instance)
        {
            if (RuntimeRealHandPinchV47.Instance.TryGetHand(
                    true,
                    out RuntimeRealHandPinchV47.HandState leftState) &&
                HandConfirmV47(leftState, nearRequired))
            {
                source = "v47-left-hand-pinch";
                return true;
            }
            if (RuntimeRealHandPinchV47.Instance.TryGetHand(
                    false,
                    out RuntimeRealHandPinchV47.HandState rightState) &&
                HandConfirmV47(rightState, nearRequired))
            {
                source = "v47-right-hand-pinch";
                return true;
            }
            return false;
        }

        ScanHands();
        if (hands == null || !hands.running)
            return false;
        if (HandConfirm(hands.leftHand, nearRequired))
        {
            source = "left-hand-pinch";
            return true;
        }
        if (HandConfirm(hands.rightHand, nearRequired))
        {
            source = "right-hand-pinch";
            return true;
        }
        return false;
    }

    bool HandConfirmV47(
        RuntimeRealHandPinchV47.HandState hand,
        bool nearRequired)
    {
        if (!hand.available || !hand.pressed)
            return false;
        if (!nearRequired)
            return true;
        if (!hand.poseValid || !activationTarget)
            return false;
        Transform tracking =
            Camera.main && Camera.main.transform.parent
                ? Camera.main.transform.parent
                : null;
        Vector3 world = tracking
            ? tracking.TransformPoint(hand.localPinchPose.position)
            : hand.localPinchPose.position;
        if (Vector3.Distance(world, activationTarget.position) <=
            activationRadius)
            return true;
        return stage == RuntimePuzzleStage.Selection &&
            Camera.main &&
            RayPointsAtActivation(
                Camera.main.transform.position,
                Camera.main.transform.forward,
                12f);
    }

    bool HandConfirm(XRHand hand, bool nearRequired)
    {
        float pinchThreshold =
            PersistentPuzzleBootstrap.Instance &&
            PersistentPuzzleBootstrap.Instance
                .NativeOpenXrTrackingOnlyV24
                ? 0.048f
                : 0.034f;
        if (!hand.isTracked ||
            !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose index) ||
            !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumb) ||
            Vector3.Distance(index.position, thumb.position) >
                pinchThreshold)
            return false;
        if (!nearRequired)
            return true;
        Transform tracking =
            Camera.main && Camera.main.transform.parent
                ? Camera.main.transform.parent
                : null;
        Vector3 localPinch =
            (index.position + thumb.position) * 0.5f;
        Vector3 world = tracking
            ? tracking.TransformPoint(localPinch)
            : localPinch;
        if (Vector3.Distance(world, activationTarget.position) <=
            activationRadius)
            return true;
        return stage == RuntimePuzzleStage.Selection &&
            Camera.main &&
            RayPointsAtActivation(
                Camera.main.transform.position,
                Camera.main.transform.forward,
                12f);
    }

    bool RayPointsAtActivation(
        Vector3 origin,
        Vector3 direction,
        float maximumAngle)
    {
        if (!activationTarget || direction.sqrMagnitude < 0.0001f)
            return false;
        Vector3 toTarget = activationTarget.position - origin;
        return toTarget.sqrMagnitude > 0.0001f &&
            Vector3.Angle(direction, toTarget) <= maximumAngle;
    }

    static bool WithinHierarchy(Transform value, Transform root)
    {
        return value && root &&
            (value == root || value.IsChildOf(root) || root.IsChildOf(value));
    }

    void ScanHands()
    {
        if (Time.unscaledTime < nextScan && hands != null)
            return;
        nextScan = Time.unscaledTime + 1f;
        handSubsystems.Clear();
        SubsystemManager.GetSubsystems(handSubsystems);
        hands = handSubsystems.Find(value => value != null && value.running);
    }

    bool ValidateBinding()
    {
        bool valid = stage switch
        {
            RuntimePuzzleStage.Splash =>
                splash &&
                activationTarget,
            RuntimePuzzleStage.Selection =>
                selection &&
                activationTarget &&
                selectionLevelByExhibit != null &&
                selectionLevelByExhibit.Length ==
                (selection.exhibits?.Length ?? 0),
            RuntimePuzzleStage.Completion =>
                completion &&
                activationTarget,
            RuntimePuzzleStage.Collection =>
                collectionV06,
            _ => false,
        };
        if (!valid)
            Debug.LogError(
                "[Polished Full Game V02] Physical stage portal binding is incomplete: " +
                stage,
                this);
        return valid;
    }

    string SelectionState(PersistentPuzzleBootstrap bootstrap)
    {
        if (stage != RuntimePuzzleStage.Selection || !selection)
            return string.Empty;
        int exhibit = selection.exhibits != null && selection.exhibits.Length > 0
            ? Mathf.Clamp(selection.currentIndex, 0, selection.exhibits.Length - 1)
            : -1;
        int level = ResolveSelectionLevel(exhibit);
        int unlocked = bootstrap && bootstrap.Save != null
            ? bootstrap.Save.highestUnlockedLevel
            : -1;
        return
            $"selectionIndex={exhibit} level={level} highestUnlocked={unlocked} " +
            $"swapping={selection.IsTransitioning} committing={selection.IsCommitting}";
    }

    void LogSwan(string message)
    {
        if (swanDiagnostics)
            Debug.Log("[Personally Puzzled Swan Portal] " + message, this);
    }
}
