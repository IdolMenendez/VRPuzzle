using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

/// <summary>
/// Runtime direction for the splash: staggered mechanical assembly, subtle
/// workshop motion, desktop/XR touch detection and a tactile wake-up response.
/// No virtual button is required.
/// </summary>
public sealed class SplashWorkshopController : MonoBehaviour
{
    [Header("Assembly")]
    public Transform logoRoot;
    public SplashAssemblyPiece[] assemblyPieces;
    public Transform[] continuouslyRotating;
    public float assemblyLeadIn = 0.2f;

    [Header("Touch")]
    public Transform touchCore;
    public Renderer[] touchRenderers;
    public Light touchLight;
    public TextMesh prompt;
    public string nextSceneName;
    public float transitionDelay = 1.15f;
    [Range(0.35f, 0.60f)] public float xrReachDistance = 0.56f;
    [Range(0.06f, 0.14f)] public float directTouchRadius = 0.105f;
    public Transform previewHands;

    [Header("V35 immersive gaze + pinch")]
    [Tooltip(
        "V35 opt-in. Keep the workshop at human scale, ignore physical touch, " +
        "and enter only when the player looks at the floating model and starts " +
        "a new native-hand pinch.")]
    public bool immersiveGazePinchEntryV35;
    [Min(0.65f)] public float immersiveModelHeightV35 = 0.92f;
    [Min(0.85f)] public float immersiveModelDistanceV35 = 1.40f;
    [Range(1f, 1.6f)] public float immersiveEnvironmentScaleV35 = 1.28f;
    [Range(5f, 24f)] public float gazeMaximumAngleV35 = 14f;
    [Range(0.025f, 0.075f)] public float pinchDistanceV35 = 0.048f;

    [Header("V37 hero-only gaze + pinch")]
    [Tooltip(
        "V37 opt-in. Never transforms the workshop spatial root. Only the " +
        "floating hero model is resized and placed relative to the tracked eye.")]
    public bool heroOnlyGazePinchEntryV37;
    [Min(0.85f)] public float heroModelHeightV37 = 1.28f;
    [Min(1.1f)] public float heroModelDistanceV37 = 1.48f;

    [Header("V38 deliberate tracked pinch gate")]
    [Tooltip(
        "Require a continuously tracked open hand before accepting a later " +
        "pinch. This rejects the first noisy frame when hand tracking starts.")]
    public bool requireTrackedReleaseBeforePinchV38;
    [Min(0.08f)] public float trackedReleaseSecondsV38 = 0.22f;
    [Min(0.04f)] public float pinchHoldSecondsV38 = 0.075f;
    [Min(0.05f)] public float gazeHoldSecondsV38 = 0.10f;
    [Tooltip(
        "V39 opt-in. Once a tracked released hand has armed the gate, any " +
        "fresh pinch enters. The player does not need to gaze at or reach " +
        "the hero.")]
    public bool pinchAnywhereEntryV39;
    [Tooltip(
        "V40 opt-in. Prefer PICO's official remote pinch/select device over " +
        "locally calculated thumb/index distance. Joint distance is used only " +
        "when no official PICO hand device exists.")]
    public bool officialPicoRemotePinchV40;
    [Tooltip(
        "V44 opt-in. Require the shared long head ray to remain on the hero. " +
        "The ray component owns the temporary debug line and pinch pulse.")]
    public RuntimeHeadRayDebugV44 headRayTargetingV44;
    public bool requireHeadRayTargetV44;
    [Tooltip(
        "V44 opt-in. Use the strict official PICO pinch envelope so contact " +
        "noise cannot enter the next scene.")]
    public bool strictOfficialPicoPinchV44;

    [Header("V45 coherent interaction ownership")]
    [Tooltip(
        "V45 opt-in. A dedicated whole-MODEL-BUILDER gaze/pinch component " +
        "owns XR entry. This controller still animates and transitions, but " +
        "does not run any legacy XR touch/gaze/pinch path.")]
    public bool externalXrEntryOwnerV45;

    [Header("Camera")]
    public Camera sceneCamera;
    public Transform parallaxRoot;
    public float desktopParallaxDegrees = 1.25f;

    Vector3 logoBasePosition;
    Vector3 logoBaseScale;
    Quaternion logoBaseRotation;
    Vector3 touchCoreBaseScale;
    Quaternion parallaxBaseRotation;
    float elapsed;
    float activatedAt = -1f;
    float nextXrScan;
    bool hovering;
    bool activated;
    bool xrLayoutApplied;
    readonly List<UnityEngine.XR.InputDevice> xrDevices = new List<UnityEngine.XR.InputDevice>();
    readonly List<XRHandSubsystem> handSubsystems = new List<XRHandSubsystem>();
    XRHandSubsystem handSubsystem;
    float nextHandSubsystemScan;
    MaterialPropertyBlock propertyBlock;
    Renderer transitionFadeRenderer;
    Material transitionFadeMaterial;
    float touchLightBaseRange;
    float touchLightIntensityScale = 1f;
    bool pinchArmedV35;
    bool wasPinchingV35;
    float trackedReleaseStartedV38 = -1f;
    float pinchStartedV38 = -1f;
    float gazeStartedV38 = -1f;

    public bool IsActivated => activated;
    /// <summary>
    /// V42: true once a hero layout has resolved, so the authored showcase
    /// can be registered to the final hero pose exactly once.
    /// </summary>
    public bool HeroLayoutAppliedV42 => xrLayoutApplied;
    /// <summary>V42: current world bounds of the hero model.</summary>
    public Bounds HeroWorldBoundsV42 => LogoTargetBoundsV35();
    public Vector3 CachedLogoBasePositionV39 =>
        logoBasePosition;
    public Vector3 CachedLogoBaseScaleV39 =>
        logoBaseScale;
    public float ActivationProgress => activated
        ? Mathf.Clamp01(
            (Time.unscaledTime - activatedAt) /
            Mathf.Max(0.1f, transitionDelay))
        : 0f;

    void Awake()
    {
        if (!sceneCamera)
            sceneCamera = Camera.main;
        if (!logoRoot)
            logoRoot = transform;
        if (!parallaxRoot)
            parallaxRoot = logoRoot;
        logoBasePosition = logoRoot.localPosition;
        logoBaseScale = logoRoot.localScale;
        logoBaseRotation = logoRoot.localRotation;
        touchCoreBaseScale = touchCore ? touchCore.localScale : Vector3.one;
        touchLightBaseRange = touchLight ? touchLight.range : 0f;
        parallaxBaseRotation = parallaxRoot.localRotation;
        propertyBlock = new MaterialPropertyBlock();

        if (assemblyPieces == null || assemblyPieces.Length == 0)
            assemblyPieces = logoRoot.GetComponentsInChildren<SplashAssemblyPiece>(true);
        foreach (var piece in assemblyPieces)
            if (piece)
                piece.CacheAndScatter();

        CreateProceduralAmbience();
        // The integrated flow owns its transition on the persistent bootstrap.
        // Keep this stage-local fallback only for opening the authored Splash
        // scene by itself in the Editor. A stage object must never be parented
        // under the persistent XR camera or it can survive scene unload.
        if (!PersistentPuzzleBootstrap.Instance)
            CreateStageOwnedTransitionFade();
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        elapsed += dt;
        if (!RuntimeDesktopHeadNavigatorV01.DesktopEditorPreviewActive &&
            !xrLayoutApplied && XRSettings.isDeviceActive && elapsed > 0.25f)
        {
            if (heroOnlyGazePinchEntryV37)
                ApplyHeroOnlyXrLayoutV37();
            else if (immersiveGazePinchEntryV35)
                ApplyImmersiveXrLayoutV35();
            else
                ApplyArmReachXrLayout();
        }
        float assemblyTime = Mathf.Max(0f, elapsed - assemblyLeadIn);
        foreach (var piece in assemblyPieces)
            if (piece)
                piece.Evaluate(assemblyTime);

        AnimateWorkshop(dt);
        ReadDesktopInput();
        if (!RuntimeDesktopHeadNavigatorV01.DesktopEditorPreviewActive)
            ReadXrInput();

        if (activated && !string.IsNullOrWhiteSpace(nextSceneName) && Time.unscaledTime - activatedAt >= transitionDelay)
            SceneManager.LoadScene(nextSceneName);
    }

    void LateUpdate()
    {
        UpdateStageOwnedTransitionFadePose();
    }

    void AnimateWorkshop(float dt)
    {
        float activeBoost = activated ? 2.1f : hovering ? 1.35f : 1f;
        for (int i = 0; i < continuouslyRotating.Length; i++)
        {
            var item = continuouslyRotating[i];
            if (item)
                item.Rotate(0f, 0f, (i % 2 == 0 ? 12f : -9f) * activeBoost * dt, Space.Self);
        }

        if (logoRoot)
        {
            logoRoot.localPosition = logoBasePosition + Vector3.up * Mathf.Sin(Time.unscaledTime * 0.82f) * 0.018f;
            if (activated)
            {
                float transition = Mathf.Clamp01((Time.unscaledTime - activatedAt) / Mathf.Max(0.1f, transitionDelay));
                float settle = transition * transition * (3f - 2f * transition);
                logoRoot.localScale = Vector3.LerpUnclamped(logoBaseScale, logoBaseScale * 0.88f, settle);
                logoRoot.localRotation = logoBaseRotation * Quaternion.Euler(0f, settle * 6f, settle * 1.5f);
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.56f, 0.96f, transition));
                SetTransitionFade(fade);
            }
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (hovering ? 6.2f : 3.4f));
        float activationWave = activated ? Mathf.Clamp01(1f - (Time.unscaledTime - activatedAt) * 0.7f) : 0f;
        if (touchCore)
            touchCore.localScale = touchCoreBaseScale * (1f + pulse * 0.08f + activationWave * 0.32f);
        if (touchLight)
            touchLight.intensity =
                (2.6f + pulse * 1.8f + activationWave * 5f) *
                touchLightIntensityScale;

        Color glow = Color.Lerp(new Color(0.03f, 0.52f, 0.72f), new Color(0.18f, 0.95f, 1f), pulse);
        foreach (var renderer in touchRenderers)
        {
            if (!renderer)
                continue;
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_EmissionColor", glow * (1.15f + activationWave * 2.2f));
            renderer.SetPropertyBlock(propertyBlock);
        }

        if (sceneCamera && Mouse.current != null && !XRSettings.isDeviceActive)
        {
            Vector2 size = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
            Vector2 normalized = Mouse.current.position.ReadValue() / size * 2f - Vector2.one;
            Quaternion target = parallaxBaseRotation * Quaternion.Euler(-normalized.y * desktopParallaxDegrees, normalized.x * desktopParallaxDegrees, 0f);
            parallaxRoot.localRotation = Quaternion.Slerp(parallaxRoot.localRotation, target, 1f - Mathf.Exp(-dt * 3.5f));
        }
    }

    void ReadDesktopInput()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            ReplayAssembly();

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame || !sceneCamera)
            return;
        Ray ray = sceneCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Collide) &&
            hit.collider.GetComponentInParent<SplashTouchTarget>())
            ActivateWorkshop();
    }

    void ReadXrInput()
    {
        if (externalXrEntryOwnerV45)
            return;

        if (heroOnlyGazePinchEntryV37 ||
            immersiveGazePinchEntryV35)
        {
            ReadGazePinchEntryV35();
            return;
        }

        ReadTrackedHandTouch();
        if (activated)
            return;
        if (Time.unscaledTime < nextXrScan)
            return;
        nextXrScan = Time.unscaledTime + 0.08f;
        xrDevices.Clear();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.TrackedDevice | InputDeviceCharacteristics.Controller, xrDevices);
        foreach (var device in xrDevices)
        {
            bool pressed = false;
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool triggerButton))
                pressed |= triggerButton;
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool primaryButton))
                pressed |= primaryButton;
            if (!pressed || !device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 localPosition))
                continue;

            Vector3 worldPosition = sceneCamera && sceneCamera.transform.parent
                ? sceneCamera.transform.parent.TransformPoint(localPosition)
                : localPosition;
            if (touchCore && Vector3.Distance(worldPosition, touchCore.position) < 0.28f)
            {
                ActivateWorkshop();
                return;
            }
        }
    }

    void ReadGazePinchEntryV35()
    {
        if (activated || !sceneCamera || !logoRoot)
            return;

        ScanHandSubsystem();
        XRHand leftHand =
            handSubsystem != null
                ? handSubsystem.leftHand
                : default;
        XRHand rightHand =
            handSubsystem != null
                ? handSubsystem.rightHand
                : default;
        bool hasTrackedHand = false;
        bool pinching = false;
        bool officialAvailable =
            officialPicoRemotePinchV40 &&
            (strictOfficialPicoPinchV44
                ? PicoSwanPinchSignalV40.TryReadIntentV44(
                    out hasTrackedHand,
                    out pinching)
                : PicoSwanPinchSignalV40.TryRead(
                    out hasTrackedHand,
                    out pinching));
        if (!officialAvailable)
        {
            hasTrackedHand =
                leftHand.isTracked ||
                rightHand.isTracked;
            pinching =
                IsPinchingV35(leftHand) ||
                IsPinchingV35(rightHand);
        }

        Bounds targetBounds = LogoTargetBoundsV35();
        Vector3 toTarget =
            targetBounds.center -
            sceneCamera.transform.position;
        bool gazing =
            requireHeadRayTargetV44
                ? headRayTargetingV44 &&
                    headRayTargetingV44.SplashTargetedV44
                : toTarget.sqrMagnitude > 0.01f &&
                    Vector3.Angle(
                        sceneCamera.transform.forward,
                        toTarget) <= gazeMaximumAngleV35;
        SetTouchHover(
            gazing ||
            pinchAnywhereEntryV39);

        bool pinchStarted =
            pinching &&
            !wasPinchingV35;
        bool acceptedPinch;
        if (requireTrackedReleaseBeforePinchV38)
        {
            if (!hasTrackedHand)
            {
                pinchArmedV35 = false;
                trackedReleaseStartedV38 = -1f;
                pinchStartedV38 = -1f;
                gazeStartedV38 = -1f;
                wasPinchingV35 = false;
                return;
            }

            if (gazing ||
                pinchAnywhereEntryV39)
            {
                if (gazeStartedV38 < 0f)
                    gazeStartedV38 = Time.unscaledTime;
            }
            else
            {
                gazeStartedV38 = -1f;
            }

            if (!pinching)
            {
                if (trackedReleaseStartedV38 < 0f)
                    trackedReleaseStartedV38 = Time.unscaledTime;
                if (Time.unscaledTime - trackedReleaseStartedV38 >=
                    Mathf.Max(0.08f, trackedReleaseSecondsV38))
                    pinchArmedV35 = true;
                pinchStartedV38 = -1f;
            }
            else
            {
                trackedReleaseStartedV38 = -1f;
                if (pinchStarted)
                    pinchStartedV38 = Time.unscaledTime;
            }

            acceptedPinch =
                pinchArmedV35 &&
                pinchStartedV38 >= 0f &&
                Time.unscaledTime - pinchStartedV38 >=
                    Mathf.Max(0.04f, pinchHoldSecondsV38) &&
                (pinchAnywhereEntryV39 ||
                 (gazeStartedV38 >= 0f &&
                  Time.unscaledTime - gazeStartedV38 >=
                    Mathf.Max(0.05f, gazeHoldSecondsV38)));
        }
        else
        {
            if (!pinching)
                pinchArmedV35 = true;
            acceptedPinch =
                heroOnlyGazePinchEntryV37
                    ? pinchStarted
                    : pinchArmedV35 && pinchStarted;
        }
        wasPinchingV35 = pinching;
        if ((gazing ||
             pinchAnywhereEntryV39) &&
            acceptedPinch)
        {
            pinchArmedV35 = false;
            pinchStartedV38 = -1f;
            ActivateWorkshop();
        }
    }

    void ScanHandSubsystem()
    {
        if (handSubsystem != null &&
            handSubsystem.running)
        {
            return;
        }
        if (Time.unscaledTime <
            nextHandSubsystemScan)
        {
            return;
        }

        nextHandSubsystemScan =
            Time.unscaledTime + 1f;
        handSubsystems.Clear();
        SubsystemManager.GetSubsystems(
            handSubsystems);
        handSubsystem = null;
        for (int index = 0;
             index < handSubsystems.Count;
             index++)
        {
            XRHandSubsystem candidate =
                handSubsystems[index];
            if (candidate != null &&
                candidate.running)
            {
                handSubsystem = candidate;
                break;
            }
        }
    }

    bool IsPinchingV35(XRHand hand)
    {
        if (!hand.isTracked ||
            !hand.GetJoint(
                XRHandJointID.IndexTip)
                .TryGetPose(out Pose index) ||
            !hand.GetJoint(
                XRHandJointID.ThumbTip)
                .TryGetPose(out Pose thumb))
        {
            return false;
        }
        return Vector3.Distance(
            index.position,
            thumb.position) <=
            pinchDistanceV35;
    }

    void ReadTrackedHandTouch()
    {
        if ((handSubsystem == null || !handSubsystem.running) && Time.unscaledTime >= nextHandSubsystemScan)
        {
            nextHandSubsystemScan = Time.unscaledTime + 1f;
            handSubsystems.Clear();
            SubsystemManager.GetSubsystems(handSubsystems);
            handSubsystem = null;
            for (int i = 0; i < handSubsystems.Count; i++)
            {
                if (handSubsystems[i] != null && handSubsystems[i].running)
                {
                    handSubsystem = handSubsystems[i];
                    break;
                }
            }
        }
        if (handSubsystem == null || !handSubsystem.running || !touchCore)
            return;

        bool near = TryTrackedIndex(handSubsystem.leftHand, out bool leftTouch) |
                    TryTrackedIndex(handSubsystem.rightHand, out bool rightTouch);
        SetTouchHover(near);
        if (leftTouch || rightTouch)
            ActivateWorkshop();
    }

    bool TryTrackedIndex(XRHand hand, out bool touching)
    {
        touching = false;
        if (!hand.isTracked || !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose tipPose))
            return false;
        Transform trackingSpace = sceneCamera && sceneCamera.transform.parent ? sceneCamera.transform.parent : null;
        Vector3 worldPosition = trackingSpace ? trackingSpace.TransformPoint(tipPose.position) : tipPose.position;
        float distance = Vector3.Distance(worldPosition, touchCore.position);
        touching = distance <= directTouchRadius;
        return distance <= 0.19f;
    }

    public void SetTouchHover(bool value)
    {
        hovering = value;
    }

    public void ActivateWorkshop()
    {
        if (activated)
            return;
        activated = true;
        activatedAt = Time.unscaledTime;
        if (prompt)
            prompt.text = "WORKSHOP AWAKE  ·  工坊已唤醒";
        PlayWakeChime();
    }

    public void ReplayAssembly()
    {
        activated = false;
        activatedAt = -1f;
        elapsed = 0f;
        if (logoRoot)
        {
            logoRoot.localScale = logoBaseScale;
            logoRoot.localRotation = logoBaseRotation;
        }
        SetTransitionFade(0f);
        foreach (var piece in assemblyPieces)
            if (piece)
                piece.CacheAndScatter();
        if (prompt)
            prompt.text = "REACH OUT  ·  伸手触碰唤醒工坊";
    }

    void ApplyArmReachXrLayout()
    {
        xrLayoutApplied = true;
        if (!sceneCamera || !logoRoot)
            return;
        if (previewHands)
            previewHands.gameObject.SetActive(false);

        Transform trackingSpace = sceneCamera.transform.parent
            ? sceneCamera.transform.parent
            : sceneCamera.transform;
        Vector3 eyePosition = sceneCamera.transform.position;
        // Use the render camera orientation for placement.  On the Project
        // Swan emulator the head device pose reports a floor-space position,
        // but its device rotation is not guaranteed to match Unity's render
        // camera transform during the first OpenXR frames.  Applying that
        // raw rotation can place the reachable logo behind the rendered eye.
        Quaternion eyeRotation = sceneCamera.transform.rotation;
        UnityEngine.XR.InputDevice head = InputDevices.GetDeviceAtXRNode(
            XRNode.Head);
        if (head.isValid &&
            head.TryGetFeatureValue(
                UnityEngine.XR.CommonUsages.devicePosition,
                out Vector3 localHeadPosition))
        {
            eyePosition = trackingSpace.TransformPoint(localHeadPosition);
        }
        // Preserve the accepted front-view composition *and* its spatial
        // relationships.  The reference scene was authored at roughly ten
        // times tabletop scale so a flat capture camera could sit several
        // metres away.  Scaling only the logo into arm's reach made it float
        // in front of (and below) the workbench.  Instead, convert the whole
        // registered art set to real tabletop scale around the tracked eye.
        // The logo, table, workshop props and lighting therefore retain their
        // authored relative positions while the touch core becomes reachable.
        Vector3 eyeForward = eyeRotation * Vector3.forward;
        Vector3 cameraToLogo = logoRoot.position - eyePosition;
        float referenceDistance = Mathf.Max(
            0.05f,
            Vector3.Dot(cameraToLogo, eyeForward));
        float reachScale = xrReachDistance / referenceDistance;
        Transform spatialRoot = FindSpatialArtRoot();
        if (spatialRoot && !sceneCamera.transform.IsChildOf(spatialRoot))
        {
            spatialRoot.position =
                eyePosition + (spatialRoot.position - eyePosition) * reachScale;
            spatialRoot.localScale *= reachScale;

            // Unity light/audio distance properties are world-unit values and
            // do not inherit Transform scale.  Convert them alongside the art
            // set so a miniature point light cannot wash an entire room and
            // spatial audio retains the authored falloff.
            foreach (Light light in spatialRoot.GetComponentsInChildren<Light>(true))
            {
                if (light && light.type != LightType.Directional)
                {
                    light.range = Mathf.Max(0.025f, light.range * reachScale);
                    // The complete set is uniformly scaled about the eye, so
                    // emitter-to-surface distances shrink by the same factor.
                    // Preserve authored illuminance using inverse-square
                    // compensation; range scaling alone causes severe bloom.
                    light.intensity *= reachScale * reachScale;
                }
            }
            foreach (AudioSource source in spatialRoot.GetComponentsInChildren<AudioSource>(true))
            {
                if (!source)
                    continue;
                source.minDistance = Mathf.Max(0.02f, source.minDistance * reachScale);
                source.maxDistance = Mathf.Max(source.minDistance + 0.05f, source.maxDistance * reachScale);
            }
        }
        else
        {
            Debug.LogWarning(
                "[Personally Puzzled Swan] Splash spatial root could not be " +
                "scaled safely; retaining authored layout.");
            reachScale = 1f;
        }
        if (touchCore && reachScale < 0.999f)
        {
            // The flat-reference source placed the cyan contact glint beyond
            // the narrow single-eye crop.  After the whole workshop has been
            // converted to real tabletop scale, register that physical glint
            // onto the visible right face of the logo instead of leaving an
            // unreachable/off-screen interaction proxy.  This is a one-time
            // world-space placement: it remains part of the workshop and is
            // never parented to the player's head.
            Vector3 eyeRight = eyeRotation * Vector3.right;
            Vector3 eyeUp = eyeRotation * Vector3.up;
            touchCore.position =
                eyePosition +
                eyeForward * (xrReachDistance * 0.91f) +
                eyeRight * (xrReachDistance * 0.25f) +
                eyeUp * (xrReachDistance * 0.035f);
        }
        if (touchLight)
        {
            // Light ranges are world-space and do not inherit Transform scale.
            // Without scaling this authored 0.78 m cyan fingertip light covers
            // the entire arm-reach logo in PICO/Swan.  Preserve the reference
            // image's localized touch glint when the logo is miniaturized.
            touchLight.range = Mathf.Max(
                0.055f,
                touchLightBaseRange * reachScale);
            touchLightIntensityScale = Mathf.Clamp(
                reachScale * reachScale * 3f,
                0.015f,
                1f);
        }

        string touchPosition = touchCore
            ? touchCore.position.ToString()
            : "missing";
        string touchViewport = touchCore && sceneCamera
            ? sceneCamera.WorldToViewportPoint(touchCore.position).ToString()
            : "missing";
        float touchDistance = touchCore
            ? Vector3.Distance(eyePosition, touchCore.position)
            : -1f;
        Debug.Log(
            "[Personally Puzzled Swan] Splash XR layout registered: " +
            $"referenceDistance={referenceDistance:F3}m, reach={xrReachDistance:F3}m, " +
            $"scale={reachScale:F4}, camera={sceneCamera.transform.position}, " +
            $"head={eyePosition}, " +
            $"spatialRoot={(spatialRoot ? spatialRoot.name : "missing")}, " +
            $"renderRotation={eyeRotation.eulerAngles}, " +
            $"touchLightRange={(touchLight ? touchLight.range : 0f):F3}m, " +
            $"touch={touchPosition}, touchViewport={touchViewport}, " +
            $"touchDistance={touchDistance:F3}m");
    }

    void ApplyImmersiveXrLayoutV35()
    {
        xrLayoutApplied = true;
        if (!sceneCamera || !logoRoot)
            return;
        if (previewHands)
            previewHands.gameObject.SetActive(false);

        Transform trackingSpace =
            sceneCamera.transform.parent
                ? sceneCamera.transform.parent
                : sceneCamera.transform;
        Vector3 eyePosition =
            sceneCamera.transform.position;
        Quaternion eyeRotation =
            sceneCamera.transform.rotation;
        UnityEngine.XR.InputDevice head =
            InputDevices.GetDeviceAtXRNode(
                XRNode.Head);
        if (head.isValid &&
            head.TryGetFeatureValue(
                UnityEngine.XR.CommonUsages.devicePosition,
                out Vector3 localHeadPosition))
        {
            eyePosition =
                trackingSpace.TransformPoint(
                    localHeadPosition);
        }

        ApplyImmersiveLayoutV35(
            eyePosition,
            eyeRotation);
    }

    void ApplyHeroOnlyXrLayoutV37()
    {
        xrLayoutApplied = true;
        if (!sceneCamera || !logoRoot)
            return;
        if (previewHands)
            previewHands.gameObject.SetActive(false);

        Bounds before = LogoTargetBoundsV35();
        float scale = heroModelHeightV37 /
            Mathf.Max(0.01f, before.size.y);
        logoRoot.localScale *= scale;
        Physics.SyncTransforms();

        Bounds after = LogoTargetBoundsV35();
        Vector3 targetCenter =
            sceneCamera.transform.position +
            sceneCamera.transform.forward * heroModelDistanceV37 -
            sceneCamera.transform.up * 0.04f;
        logoRoot.position += targetCenter - after.center;
        Physics.SyncTransforms();

        // V39 fixes the runtime snap regression: animation must use the
        // tracked-eye layout as its base pose, not the tiny authored preview
        // pose cached before XR layout.
        logoBasePosition =
            logoRoot.localPosition;
        logoBaseScale =
            logoRoot.localScale;
        logoBaseRotation =
            logoRoot.localRotation;
        if (parallaxRoot == logoRoot)
            parallaxBaseRotation =
                logoRoot.localRotation;

        pinchArmedV35 = !requireTrackedReleaseBeforePinchV38;
        trackedReleaseStartedV38 = -1f;
        pinchStartedV38 = -1f;
        gazeStartedV38 = -1f;
        wasPinchingV35 = false;
        if (prompt)
            prompt.text =
                "LOOK AT THE MODEL · PINCH TO ENTER\n" +
                "看向悬浮模型 · 捏合进入";
        Bounds finalBounds = LogoTargetBoundsV35();
        Debug.Log(
            "[Personally Puzzled V37] hero-only Splash layout: " +
            $"modelHeight={finalBounds.size.y:F3}m " +
            $"distance={Vector3.Distance(sceneCamera.transform.position, finalBounds.center):F3}m " +
            $"heroScale={scale:F4} workshopRootTransform=UNCHANGED " +
            "interaction=gaze-pinch");
    }

    public void ApplyHeroOnlyLayoutForValidationV37(
        Camera validationCamera)
    {
        if (validationCamera)
            sceneCamera = validationCamera;
        ApplyHeroOnlyXrLayoutV37();
    }

    public void ApplyImmersiveLayoutForValidationV35(
        Camera validationCamera)
    {
        if (validationCamera)
            sceneCamera = validationCamera;
        if (!sceneCamera)
            return;
        ApplyImmersiveLayoutV35(
            sceneCamera.transform.position,
            sceneCamera.transform.rotation);
    }

    void ApplyImmersiveLayoutV35(
        Vector3 eyePosition,
        Quaternion eyeRotation)
    {
        Transform spatialRoot =
            FindSpatialArtRoot();
        if (!spatialRoot ||
            sceneCamera.transform.IsChildOf(
                spatialRoot))
        {
            Debug.LogError(
                "[Personally Puzzled V35] Splash spatial root is unsafe.");
            return;
        }

        Bounds before =
            LogoTargetBoundsV35();
        float modelScale =
            immersiveModelHeightV35 /
            Mathf.Max(0.01f, before.size.y);
        float environmentScale =
            modelScale *
            immersiveEnvironmentScaleV35;
        spatialRoot.localScale *=
            environmentScale;
        logoRoot.localScale /=
            immersiveEnvironmentScaleV35;
        Physics.SyncTransforms();

        Bounds after =
            LogoTargetBoundsV35();
        Vector3 targetCenter =
            eyePosition +
            eyeRotation * Vector3.forward *
            immersiveModelDistanceV35 -
            eyeRotation * Vector3.up * 0.03f;
        spatialRoot.position +=
            targetCenter - after.center;
        Physics.SyncTransforms();

        foreach (Light light in
                 spatialRoot.GetComponentsInChildren<
                     Light>(true))
        {
            if (light &&
                light.type !=
                    LightType.Directional)
            {
                light.range =
                    Mathf.Max(
                        0.15f,
                        light.range *
                        environmentScale);
                light.intensity *=
                    environmentScale *
                    environmentScale;
            }
        }
        foreach (AudioSource source in
                 spatialRoot.GetComponentsInChildren<
                     AudioSource>(true))
        {
            if (!source)
                continue;
            source.minDistance =
                Mathf.Max(
                    0.08f,
                    source.minDistance *
                    environmentScale);
            source.maxDistance =
                Mathf.Max(
                    source.minDistance + 0.2f,
                    source.maxDistance *
                    environmentScale);
        }

        if (prompt)
        {
            prompt.text =
                "LOOK AT THE MODEL · PINCH TO ENTER\n" +
                "看向悬浮模型 · 捏合进入";
        }

        Bounds finalBounds =
            LogoTargetBoundsV35();
        Debug.Log(
            "[Personally Puzzled V35] immersive Splash layout: " +
            $"modelHeight={finalBounds.size.y:F3}m " +
            $"distance={Vector3.Distance(eyePosition, finalBounds.center):F3}m " +
            $"modelScale={modelScale:F4} " +
            $"environmentScale={environmentScale:F4} " +
            $"gazeAngle={gazeMaximumAngleV35:F1}deg " +
            "interaction=gaze-new-pinch-only");
    }

    Bounds LogoTargetBoundsV35()
    {
        Collider targetCollider =
            logoRoot
                ? logoRoot.GetComponent<Collider>()
                : null;
        if (targetCollider)
            return targetCollider.bounds;

        Renderer[] renderers =
            logoRoot
                ? logoRoot.GetComponentsInChildren<
                    Renderer>(true)
                : new Renderer[0];
        Bounds bounds =
            new Bounds(
                logoRoot
                    ? logoRoot.position
                    : transform.position,
                Vector3.one * 0.01f);
        bool initialized = false;
        foreach (Renderer renderer in renderers)
        {
            if (!renderer)
                continue;
            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(
                    renderer.bounds);
            }
        }
        return bounds;
    }

    Transform FindSpatialArtRoot()
    {
        Transform candidate = logoRoot;
        Scene scene = logoRoot.gameObject.scene;
        while (candidate.parent && candidate.parent.gameObject.scene == scene)
            candidate = candidate.parent;
        return candidate;
    }

    void CreateStageOwnedTransitionFade()
    {
        if (!sceneCamera)
            return;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (!shader)
            return;
        GameObject fade = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fade.name = "Stage-owned comfort fade · standalone preview only";
        SceneManager.MoveGameObjectToScene(fade, gameObject.scene);
        Collider collider = fade.GetComponent<Collider>();
        if (collider)
            Destroy(collider);
        transitionFadeRenderer = fade.GetComponent<Renderer>();
        transitionFadeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        transitionFadeRenderer.receiveShadows = false;
        transitionFadeMaterial = new Material(shader) { name = "Runtime comfort fade" };
        if (transitionFadeMaterial.HasProperty("_Surface")) transitionFadeMaterial.SetFloat("_Surface", 1f);
        if (transitionFadeMaterial.HasProperty("_Blend")) transitionFadeMaterial.SetFloat("_Blend", 0f);
        if (transitionFadeMaterial.HasProperty("_SrcBlend")) transitionFadeMaterial.SetFloat("_SrcBlend", 5f);
        if (transitionFadeMaterial.HasProperty("_DstBlend")) transitionFadeMaterial.SetFloat("_DstBlend", 10f);
        if (transitionFadeMaterial.HasProperty("_ZWrite")) transitionFadeMaterial.SetFloat("_ZWrite", 0f);
        transitionFadeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        transitionFadeMaterial.renderQueue = 3100;
        transitionFadeRenderer.sharedMaterial = transitionFadeMaterial;
        UpdateStageOwnedTransitionFadePose();
        SetTransitionFade(0f);
    }

    void UpdateStageOwnedTransitionFadePose()
    {
        if (!transitionFadeRenderer || !sceneCamera)
            return;
        float distance = Mathf.Max(
            sceneCamera.nearClipPlane + 0.035f,
            0.16f);
        float height =
            2f * distance * Mathf.Tan(
                sceneCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        Transform fade = transitionFadeRenderer.transform;
        fade.SetPositionAndRotation(
            sceneCamera.transform.position +
            sceneCamera.transform.forward * distance,
            sceneCamera.transform.rotation);
        fade.localScale = new Vector3(
            height * Mathf.Max(2.35f, sceneCamera.aspect * 1.30f),
            height * 1.22f,
            1f);
    }

    void SetTransitionFade(float alpha)
    {
        if (!transitionFadeRenderer || !transitionFadeMaterial)
            return;
        transitionFadeRenderer.enabled = alpha > 0.001f;
        Color color = new Color(0.012f, 0.007f, 0.004f, Mathf.Clamp01(alpha));
        if (transitionFadeMaterial.HasProperty("_BaseColor")) transitionFadeMaterial.SetColor("_BaseColor", color);
        if (transitionFadeMaterial.HasProperty("_Color")) transitionFadeMaterial.SetColor("_Color", color);
    }

    void CreateProceduralAmbience()
    {
        if (GetComponent<AudioSource>())
            return;
        const int rate = 22050;
        const int seconds = 6;
        float[] data = new float[rate * seconds];
        uint seed = 38191u;
        float filteredNoise = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)rate;
            seed = seed * 1664525u + 1013904223u;
            float noise = ((seed >> 8) / 16777216f) * 2f - 1f;
            filteredNoise = Mathf.Lerp(filteredNoise, noise, 0.008f);
            float room = Mathf.Sin(t * Mathf.PI * 2f * 48f) * 0.006f + Mathf.Sin(t * Mathf.PI * 2f * 73f) * 0.003f;
            data[i] = filteredNoise * 0.014f + room;
        }
        AudioClip clip = AudioClip.Create("Warm workshop room tone", data.Length, 1, rate, false);
        clip.SetData(data, 0);
        var source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.spatialBlend = 0.35f;
        source.volume = 0.55f;
        source.Play();
    }

    void PlayWakeChime()
    {
        const int rate = 22050;
        const float length = 0.9f;
        int count = Mathf.CeilToInt(rate * length);
        float[] data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            float envelope = Mathf.Exp(-t * 4.1f) * Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 50f));
            data[i] = (Mathf.Sin(t * Mathf.PI * 2f * 523.25f) * 0.34f +
                       Mathf.Sin(t * Mathf.PI * 2f * 784.88f) * 0.18f +
                       Mathf.Sin(t * Mathf.PI * 2f * 1046.5f) * 0.08f) * envelope;
        }
        AudioClip clip = AudioClip.Create("Workshop wake chime", count, 1, rate, false);
        clip.SetData(data, 0);
        AudioSource.PlayClipAtPoint(clip, touchCore ? touchCore.position : transform.position, 0.55f);
    }
}
