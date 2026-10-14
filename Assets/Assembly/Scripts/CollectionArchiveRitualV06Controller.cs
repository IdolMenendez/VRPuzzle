using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

/// <summary>
/// Stage 8 V06 physical archive ritual.
/// A gentle palm push sends the same assembled complete-module hero along a
/// smooth gold arc into its proportional cabinet. Stars ignite one-by-one near
/// arrival. Tracked hands are primary; controllers and desktop remain complete
/// fallbacks.
/// </summary>
public sealed class CollectionArchiveRitualV06Controller : MonoBehaviour
{
    public Camera sceneCamera;
    public Transform heroModel;
    public Transform flightStart;
    public Transform flightControl;
    public Transform cabinetTarget;
    public LineRenderer goldenTrail;
    public Renderer goldenRibbonRenderer;
    public Renderer goldDustRenderer;
    [Tooltip("Optional V07 living particle wake. Unlike the authored dust mesh, these motes visibly travel with the model during the archive flight.")]
    public ParticleSystem flightParticles;
    public Light archiveGlow;
    public Light[] starLights;
    public Renderer[] starRenderers;
    public Transform previewHand;
    public float flightDuration = 1.65f;
    public float physicalPushRadius = 0.24f;
    public float minimumPushVelocity = 0.18f;
    [Header("Museum-grade presentation")]
    [Min(0.02f)] public float archiveGlowPeakIntensity = 0.32f;
    [Min(0.01f)] public float starLitIntensity = 0.24f;
    public bool freezeReferencePose;
    [Range(0f, 1f)] public float referenceFlightProgress = 0.58f;
    [Header("V47 shared fresh-palm provider")]
    public bool resilientRealHandInputV47;

    readonly List<XRHandSubsystem> handSubsystems = new List<XRHandSubsystem>();
    readonly List<UnityEngine.XR.InputDevice> devices = new List<UnityEngine.XR.InputDevice>();
    XRHandSubsystem hands;
    Vector3 previousPalm;
    float previousPalmTime;
    float nextScan;
    float progress;
    bool hasPalm;
    Vector3 previousLeftPalmV47;
    Vector3 previousRightPalmV47;
    float previousLeftPalmTimeV47;
    float previousRightPalmTimeV47;
    bool hasLeftPalmV47;
    bool hasRightPalmV47;
    bool ritualActive;
    bool completed;
    bool completionCuePlayed;
    int lastLitStars;

    public float Progress => progress;
    public bool RitualActive => ritualActive;
    public bool Completed => completed;
    public int LitStarCount => CountLitStars(progress);
    public bool HasCompleteBindings =>
        string.IsNullOrEmpty(BindingError());

    void Awake()
    {
        if (!sceneCamera)
            sceneCamera = Camera.main;
        if (!ValidateBindings())
        {
            enabled = false;
            return;
        }
        BuildTrail();
        if (freezeReferencePose) ApplyReferencePose();
        else ResetRitual();
    }

    public bool ValidateBindings(bool logErrors = true)
    {
        string error = BindingError();
        if (string.IsNullOrEmpty(error))
            return true;
        if (logErrors)
            Debug.LogError(
                "[Stage 8 V06] Archive ritual refused invalid runtime bindings: " +
                error,
                this);
        return false;
    }

    string BindingError()
    {
        if (!sceneCamera)
            return "scene camera is missing";
        if (!heroModel || !flightStart || !flightControl || !cabinetTarget)
            return "hero/start/control/cabinet flight transforms are incomplete";
        if (!goldenTrail || !goldenRibbonRenderer ||
            !goldDustRenderer || !archiveGlow)
            return "gold trail/dust/archive glow effects are incomplete";
        if (starLights == null || starLights.Length != 3)
            return "exactly three sequential star lights are required";
        for (int i = 0; i < starLights.Length; i++)
            if (!starLights[i])
                return "sequential star light " + (i + 1) + " is missing";
        if (starRenderers == null || starRenderers.Length != 3)
            return "exactly three sequential star renderers are required";
        for (int i = 0; i < starRenderers.Length; i++)
            if (!starRenderers[i])
                return "sequential star renderer " + (i + 1) + " is missing";
        if (!previewHand)
            return "embodied gentle-push preview hand is missing";
        if (flightDuration < 1.40f || flightDuration > 2.20f)
            return "flight duration must remain in the ceremonial 1.40–2.20 second range";
        if (physicalPushRadius < 0.18f || physicalPushRadius > 0.30f)
            return "physical push radius must remain in the near-field 0.18–0.30 meter range";
        if (minimumPushVelocity < 0.12f || minimumPushVelocity > 0.22f)
            return "push threshold must remain a gentle 0.12–0.22 meters/second";
        return null;
    }

    void Update()
    {
        if (previewHand && XRSettings.isDeviceActive)
            previewHand.gameObject.SetActive(false);
        if (freezeReferencePose)
        {
            ApplyReferencePose();
            return;
        }
        bool desktopPush =
            Keyboard.current != null &&
            Keyboard.current.pKey.wasPressedThisFrame;
#if UNITY_EDITOR
        bool providerAutomationV47 =
            resilientRealHandInputV47 &&
            RuntimeRealHandPinchV47.Instance &&
            RuntimeRealHandPinchV47.Instance.AutomationActiveV47;
#else
        const bool providerAutomationV47 = false;
#endif
        bool physicalPush =
            (!RuntimeDesktopHeadNavigatorV01.DesktopEditorPreviewActive ||
             providerAutomationV47) &&
            (ReadPhysicalPush() || ReadControllerPush());
        if (!ritualActive && !completed &&
            (physicalPush || desktopPush))
            BeginRitual();

        if (!ritualActive) return;
        progress = Mathf.Clamp01(progress +
            Time.unscaledDeltaTime / Mathf.Max(0.35f, flightDuration));
        ApplyProgress(progress, true);
        if (progress < 1f) return;
        ritualActive = false;
        completed = true;
        if (!completionCuePlayed)
        {
            completionCuePlayed = true;
            RuntimeHaptics.PulseAll(0.34f, 0.095f);
            RuntimeAudioRouter.PlayCue(
                RuntimeAudioCue.Complete,
                cabinetTarget ? cabinetTarget.position : transform.position);
        }
    }

    public void ApplyReferencePose()
    {
        progress = Mathf.Clamp01(referenceFlightProgress);
        completed = progress >= 1f;
        ritualActive = false;
        ApplyProgress(progress, false);
    }

    public void ApplyReferenceProgressForEditor(float normalizedProgress)
    {
        freezeReferencePose = true;
        referenceFlightProgress = Mathf.Clamp01(normalizedProgress);
        ApplyReferencePose();
    }

    public void ResetForEditor()
    {
        // Save a clean but fully interactive runtime state. Capture methods
        // temporarily freeze specific progress poses, then return here.
        freezeReferencePose = false;
        referenceFlightProgress = 0f;
        ResetRitual();
    }

    public void BeginRitualForSmoke()
    {
        freezeReferencePose = false;
        BeginRitual();
    }

    public bool BeginRitualForSwanEmulator()
    {
        PersistentPuzzleBootstrap bootstrap =
            PersistentPuzzleBootstrap.Instance;
        if (!bootstrap ||
            !bootstrap.SwanEmulatorDiagnosticsEnabled ||
            ritualActive || completed || !HasCompleteBindings)
            return false;
        freezeReferencePose = false;
        BeginRitual();
        return ritualActive;
    }

    public void AdvanceRitualForSmoke(float normalizedProgress)
    {
        progress = Mathf.Clamp01(normalizedProgress);
        ritualActive = progress < 1f;
        completed = progress >= 1f;
        ApplyProgress(progress, false);
    }

    public void ResetRitual()
    {
        progress = 0f;
        ritualActive = false;
        completed = false;
        completionCuePlayed = false;
        lastLitStars = 0;
        hasPalm = false;
        previousPalm = Vector3.zero;
        previousPalmTime = 0f;
        hasLeftPalmV47 = false;
        hasRightPalmV47 = false;
        previousLeftPalmV47 = Vector3.zero;
        previousRightPalmV47 = Vector3.zero;
        previousLeftPalmTimeV47 = 0f;
        previousRightPalmTimeV47 = 0f;
        if (flightParticles)
            flightParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        ApplyProgress(0f, false);
    }

    void BeginRitual()
    {
        if (!HasCompleteBindings)
            return;
        progress = 0f;
        ritualActive = true;
        completed = false;
        completionCuePlayed = false;
        lastLitStars = 0;
        Vector3 cuePosition = heroModel ? heroModel.position : transform.position;
        RuntimeHaptics.PulseAll(0.13f, 0.045f);
        RuntimeAudioRouter.PlayCue(RuntimeAudioCue.Touch, cuePosition);
        RuntimeAudioRouter.PlayCue(RuntimeAudioCue.Archive, cuePosition);
        ApplyProgress(0f, true);
    }

    void ApplyProgress(float normalizedProgress, bool playStarCues)
    {
        if (!heroModel || !flightStart || !flightControl || !cabinetTarget)
            return;
        float eased = Mathf.SmoothStep(0f, 1f, normalizedProgress);
        float u = 1f - eased;
        Vector3 position =
            u * u * flightStart.position +
            2f * u * eased * flightControl.position +
            eased * eased * cabinetTarget.position;
        heroModel.position = position;
        heroModel.rotation = Quaternion.Slerp(
            flightStart.rotation,
            cabinetTarget.rotation,
            eased);
        heroModel.localScale = Vector3.Lerp(
            flightStart.localScale,
            cabinetTarget.localScale,
            eased);
        if (flightParticles)
        {
            flightParticles.transform.position = position;
            if (normalizedProgress > 0.015f && normalizedProgress < 0.995f)
            {
                if (!flightParticles.isPlaying)
                    flightParticles.Play(true);
            }
            else if (flightParticles.isPlaying)
            {
                flightParticles.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmitting);
            }
        }

        UpdateTrail(eased);
        if (goldDustRenderer)
            goldDustRenderer.enabled =
                normalizedProgress > 0.015f &&
                normalizedProgress < 0.965f;
        if (goldenRibbonRenderer)
            goldenRibbonRenderer.enabled =
                normalizedProgress > 0.012f &&
                normalizedProgress < 0.985f;
        if (archiveGlow)
            archiveGlow.intensity = Mathf.Lerp(
                0.045f,
                archiveGlowPeakIntensity,
                Mathf.SmoothStep(0.56f, 1f, normalizedProgress));

        int lit = CountLitStars(normalizedProgress);
        if (starLights != null)
        {
            for (int i = 0; i < starLights.Length; i++)
            {
                if (!starLights[i]) continue;
                starLights[i].intensity =
                    i < lit ? starLitIntensity : 0.008f;
                starLights[i].range = i < lit ? 0.15f : 0.04f;
            }
        }
        if (starRenderers != null)
        {
            for (int i = 0; i < starRenderers.Length; i++)
                if (starRenderers[i]) starRenderers[i].enabled = i < lit;
        }
        if (playStarCues && lit > lastLitStars)
        {
            for (int i = lastLitStars; i < lit; i++)
            {
                Vector3 positionForCue =
                    starLights != null && i < starLights.Length && starLights[i]
                        ? starLights[i].transform.position
                        : cabinetTarget.position;
                RuntimeHaptics.PulseAll(0.18f + i * 0.035f, 0.040f);
                RuntimeAudioRouter.PlayCue(RuntimeAudioCue.StarLight, positionForCue);
            }
        }
        lastLitStars = lit;
    }

    int CountLitStars(float normalizedProgress)
    {
        if (normalizedProgress >= 0.965f) return 3;
        if (normalizedProgress >= 0.845f) return 2;
        if (normalizedProgress >= 0.710f) return 1;
        return 0;
    }

    void BuildTrail()
    {
        if (!goldenTrail || !flightStart || !flightControl || !cabinetTarget)
            return;
        // Unity 6 macOS batch rendering can crash in the native LineRenderer
        // path on dense archive scenes. Keep the component as the interaction
        // contract, but render the authored custom-mesh ribbon instead.
        // Bezier() returns world-space positions. Explicitly preserve that
        // contract so the ribbon follower never applies the collection root
        // transform a second time and creates a giant diagonal streak.
        goldenTrail.useWorldSpace = true;
        goldenTrail.forceRenderingOff = true;
        goldenTrail.positionCount = 64;
        for (int i = 0; i < goldenTrail.positionCount; i++)
        {
            float t = i / (goldenTrail.positionCount - 1f);
            goldenTrail.SetPosition(i, Bezier(t));
        }
    }

    void UpdateTrail(float progressValue)
    {
        if (!goldenTrail || !flightStart || !flightControl || !cabinetTarget)
            return;
        goldenTrail.useWorldSpace = true;
        goldenTrail.forceRenderingOff = true;
        goldenTrail.positionCount = 64;
        for (int i = 0; i < goldenTrail.positionCount; i++)
        {
            float t = progressValue * i / (goldenTrail.positionCount - 1f);
            goldenTrail.SetPosition(i, Bezier(t));
        }
        goldenTrail.enabled =
            progressValue > 0.012f &&
            progressValue < 0.985f;
    }

    Vector3 Bezier(float t)
    {
        float u = 1f - t;
        return
            u * u * flightStart.position +
            2f * u * t * flightControl.position +
            t * t * cabinetTarget.position;
    }

    bool ReadPhysicalPush()
    {
        if (resilientRealHandInputV47 && RuntimeRealHandPinchV47.Instance)
        {
            bool leftPush = ReadPhysicalPushV47(true);
            bool rightPush = ReadPhysicalPushV47(false);
            return leftPush || rightPush;
        }
        ScanHands();
        if (hands == null || !hands.running)
            return false;
        return IsPush(hands.leftHand) || IsPush(hands.rightHand);
    }

    bool ReadPhysicalPushV47(bool leftHand)
    {
        if (!RuntimeRealHandPinchV47.Instance.TryGetPalm(
                leftHand,
                out Pose localPalm))
        {
            if (leftHand)
                hasLeftPalmV47 = false;
            else
                hasRightPalmV47 = false;
            return false;
        }

        Transform tracking =
            sceneCamera && sceneCamera.transform.parent
                ? sceneCamera.transform.parent
                : null;
        Vector3 world = tracking
            ? tracking.TransformPoint(localPalm.position)
            : localPalm.position;
        float now = Time.unscaledTime;
        bool hadPrevious = leftHand ? hasLeftPalmV47 : hasRightPalmV47;
        Vector3 previous = leftHand
            ? previousLeftPalmV47
            : previousRightPalmV47;
        float previousTime = leftHand
            ? previousLeftPalmTimeV47
            : previousRightPalmTimeV47;
        bool pushed = false;
        if (hadPrevious)
        {
            Vector3 velocity =
                (world - previous) /
                Mathf.Max(0.001f, now - previousTime);
            Vector3 toCabinet =
                cabinetTarget
                    ? (cabinetTarget.position - world).normalized
                    : sceneCamera
                        ? sceneCamera.transform.forward
                        : Vector3.forward;
            pushed =
                heroModel &&
                Vector3.Distance(world, heroModel.position) <=
                    physicalPushRadius &&
                Vector3.Dot(velocity, toCabinet) >= minimumPushVelocity;
        }

        if (leftHand)
        {
            previousLeftPalmV47 = world;
            previousLeftPalmTimeV47 = now;
            hasLeftPalmV47 = true;
        }
        else
        {
            previousRightPalmV47 = world;
            previousRightPalmTimeV47 = now;
            hasRightPalmV47 = true;
        }
        if (pushed)
            Debug.Log(
                "[PPVR V47 Collection] event=palm-push-accepted hand=" +
                (leftHand ? "left" : "right"));
        return pushed;
    }

    bool IsPush(XRHand hand)
    {
        if (!hand.isTracked ||
            !hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palm))
            return false;
        Transform tracking =
            sceneCamera && sceneCamera.transform.parent
                ? sceneCamera.transform.parent
                : null;
        Vector3 world = tracking ? tracking.TransformPoint(palm.position) : palm.position;
        float now = Time.unscaledTime;
        bool pushed = false;
        if (hasPalm)
        {
            Vector3 velocity =
                (world - previousPalm) /
                Mathf.Max(0.001f, now - previousPalmTime);
            Vector3 toCabinet =
                cabinetTarget
                    ? (cabinetTarget.position - world).normalized
                    : sceneCamera ? sceneCamera.transform.forward : Vector3.forward;
            pushed =
                heroModel &&
                Vector3.Distance(world, heroModel.position) <= physicalPushRadius &&
                Vector3.Dot(velocity, toCabinet) >= minimumPushVelocity;
        }
        previousPalm = world;
        previousPalmTime = now;
        hasPalm = true;
        return pushed;
    }

    bool ReadControllerPush()
    {
        devices.Clear();
        UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.HeldInHand |
            InputDeviceCharacteristics.TrackedDevice,
            devices);
        foreach (UnityEngine.XR.InputDevice device in devices)
        {
            bool pressed =
                (device.TryGetFeatureValue(
                    UnityEngine.XR.CommonUsages.primaryButton,
                    out bool primary) && primary) ||
                (device.TryGetFeatureValue(
                    UnityEngine.XR.CommonUsages.triggerButton,
                    out bool trigger) && trigger);
            if (!pressed ||
                !device.TryGetFeatureValue(
                    UnityEngine.XR.CommonUsages.devicePosition,
                    out Vector3 local))
                continue;
            Transform tracking =
                sceneCamera && sceneCamera.transform.parent
                    ? sceneCamera.transform.parent
                    : null;
            Vector3 world = tracking ? tracking.TransformPoint(local) : local;
            if (heroModel &&
                Vector3.Distance(world, heroModel.position) <= physicalPushRadius)
                return true;
        }
        return false;
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
}
