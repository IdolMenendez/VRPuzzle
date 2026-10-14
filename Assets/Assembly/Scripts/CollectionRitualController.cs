using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

/// <summary>
/// Physical collection ritual. A gentle forward palm push sends the completed
/// miniature along a readable quadratic arc while cabinet stars illuminate in
/// sequence. Controllers and desktop input preserve the complete interaction.
/// </summary>
public sealed class CollectionRitualController : MonoBehaviour
{
    public Camera sceneCamera;
    public Transform heroModel;
    public Transform flightStart;
    public Transform flightControl;
    public Transform cabinetTarget;
    public LineRenderer goldenTrail;
    public Light[] starLights;
    public Renderer[] starRenderers;
    public Transform previewHand;
    public float flightDuration = 1.35f;
    public bool freezeReferencePose;
    [Range(0f, 1f)] public float referenceFlightProgress = 0.58f;

    readonly List<XRHandSubsystem> handSubsystems = new List<XRHandSubsystem>();
    readonly List<UnityEngine.XR.InputDevice> devices = new List<UnityEngine.XR.InputDevice>();
    XRHandSubsystem hands;
    Vector3 previousPalm;
    float previousPalmTime;
    float nextScan;
    float progress;
    bool hasPalm;
    bool ritualActive;
    bool completed;
    int lastLitStars;
    AudioSource music;
    AudioSource cue;

    public float Progress => progress;
    public bool Completed => completed;

    void Awake()
    {
        BuildTrail();
        CreateOriginalCollectionAudio();
        if (freezeReferencePose)
            ApplyReferencePose();
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
        if (!ritualActive && !completed && (ReadPhysicalPush() || ReadControllerPush() ||
            (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)))
            BeginRitual();
        if (ritualActive)
        {
            progress = Mathf.Clamp01(progress + Time.unscaledDeltaTime / Mathf.Max(0.25f, flightDuration));
            ApplyProgress(progress);
            if (progress >= 1f)
            {
                ritualActive = false;
                completed = true;
                cue.Play();
            }
        }
    }

    public void ApplyReferencePose()
    {
        progress = Mathf.Clamp01(referenceFlightProgress);
        ApplyProgress(progress);
    }

    public void BeginRitualForSmoke()
    {
        freezeReferencePose = false;
        BeginRitual();
    }

    public void AdvanceRitualForSmoke(float normalizedProgress)
    {
        progress = Mathf.Clamp01(normalizedProgress);
        ApplyProgress(progress);
        if (progress >= 1f)
        {
            ritualActive = false;
            completed = true;
        }
    }

    void BeginRitual()
    {
        progress = 0f;
        ritualActive = true;
        completed = false;
        lastLitStars = 0;
        RuntimeAudioRouter.PlayCue(RuntimeAudioCue.Archive,
            heroModel ? heroModel.position : transform.position);
        ApplyProgress(0f);
    }

    void ApplyProgress(float t)
    {
        if (!heroModel || !flightStart || !flightControl || !cabinetTarget)
            return;
        float u = 1f - t;
        Vector3 position = u * u * flightStart.position + 2f * u * t * flightControl.position + t * t * cabinetTarget.position;
        heroModel.position = position;
        heroModel.rotation = Quaternion.Slerp(flightStart.rotation, cabinetTarget.rotation, Mathf.SmoothStep(0f, 1f, t));
        heroModel.localScale = Vector3.Lerp(flightStart.localScale, cabinetTarget.localScale, Mathf.SmoothStep(0f, 1f, t));
        UpdateTrail(t);
        int lit = Mathf.Clamp(Mathf.FloorToInt(t * (starLights != null ? starLights.Length + 1 : 1)), 0,
            starLights != null ? starLights.Length : 0);
        if (starLights != null)
        {
            for (int i = 0; i < starLights.Length; i++)
                if (starLights[i]) starLights[i].intensity = i < lit ? 0.32f : 0.02f;
        }
        if (starRenderers != null)
        {
            for (int i = 0; i < starRenderers.Length; i++)
                if (starRenderers[i]) starRenderers[i].enabled = i < lit;
        }
        if (Application.isPlaying && lit > lastLitStars)
        {
            for (int i = lastLitStars; i < lit; i++)
            {
                Vector3 cuePosition = starLights != null && i < starLights.Length && starLights[i]
                    ? starLights[i].transform.position
                    : cabinetTarget ? cabinetTarget.position : transform.position;
                RuntimeAudioRouter.PlayCue(RuntimeAudioCue.StarLight, cuePosition);
            }
        }
        lastLitStars = lit;
    }

    void BuildTrail()
    {
        if (!goldenTrail || !flightStart || !flightControl || !cabinetTarget)
            return;
        goldenTrail.positionCount = 40;
        for (int i = 0; i < goldenTrail.positionCount; i++)
        {
            float t = i / (goldenTrail.positionCount - 1f);
            float u = 1f - t;
            goldenTrail.SetPosition(i, u * u * flightStart.position + 2f * u * t * flightControl.position + t * t * cabinetTarget.position);
        }
    }

    void UpdateTrail(float progressValue)
    {
        if (!goldenTrail || !flightStart || !flightControl || !cabinetTarget)
            return;
        goldenTrail.positionCount = 40;
        for (int i = 0; i < goldenTrail.positionCount; i++)
        {
            float t = progressValue * i / (goldenTrail.positionCount - 1f);
            float u = 1f - t;
            goldenTrail.SetPosition(i, u * u * flightStart.position + 2f * u * t * flightControl.position + t * t * cabinetTarget.position);
        }
    }

    bool ReadPhysicalPush()
    {
        ScanHands();
        if (hands == null || !hands.running)
            return false;
        return IsPush(hands.leftHand) || IsPush(hands.rightHand);
    }

    bool IsPush(XRHand hand)
    {
        if (!hand.isTracked || !hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palm))
            return false;
        Transform tracking = sceneCamera && sceneCamera.transform.parent ? sceneCamera.transform.parent : null;
        Vector3 world = tracking ? tracking.TransformPoint(palm.position) : palm.position;
        float now = Time.unscaledTime;
        bool pushed = false;
        if (hasPalm)
        {
            Vector3 velocity = (world - previousPalm) / Mathf.Max(0.001f, now - previousPalmTime);
            Vector3 toCabinet = cabinetTarget ? (cabinetTarget.position - world).normalized : Vector3.forward;
            pushed = heroModel && Vector3.Distance(world, heroModel.position) < 0.22f &&
                Vector3.Dot(velocity, toCabinet) > 0.28f;
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
            InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.TrackedDevice, devices);
        foreach (UnityEngine.XR.InputDevice device in devices)
        {
            bool pressed = (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool primary) && primary) ||
                (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool trigger) && trigger);
            if (!pressed || !device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 local))
                continue;
            Transform tracking = sceneCamera && sceneCamera.transform.parent ? sceneCamera.transform.parent : null;
            Vector3 world = tracking ? tracking.TransformPoint(local) : local;
            if (heroModel && Vector3.Distance(world, heroModel.position) < 0.22f)
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

    void CreateOriginalCollectionAudio()
    {
        music = gameObject.AddComponent<AudioSource>();
        music.loop = true;
        music.spatialBlend = 0f;
        music.volume = 0.17f;
        music.clip = BuildClip("Original gallery of remembered wonders", 18f, false);
        music.Play();
        cue = gameObject.AddComponent<AudioSource>();
        cue.spatialBlend = 0.45f;
        cue.volume = 0.36f;
        cue.clip = BuildClip("Original cabinet star ignition", 2.4f, true);
    }

    static AudioClip BuildClip(string name, float seconds, bool stars)
    {
        const int rate = 22050;
        int count = Mathf.CeilToInt(seconds * rate);
        float[] data = new float[count];
        float[] notes = stars ? new[] { 587.33f, 739.99f, 880f } : new[] { 174.61f, 220f, 261.63f, 329.63f };
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            int note = stars ? Mathf.Min(notes.Length - 1, Mathf.FloorToInt(t / 0.62f)) : Mathf.FloorToInt(t / 4.5f) % notes.Length;
            float envelope = stars ? Mathf.Exp(-2.1f * Mathf.Repeat(t, 0.62f)) : 0.6f + 0.4f * Mathf.Sin(Mathf.PI * Mathf.Repeat(t, 4.5f) / 4.5f);
            data[i] = (Mathf.Sin(Mathf.PI * 2f * notes[note] * t) + 0.25f * Mathf.Sin(Mathf.PI * 4f * notes[note] * t)) * 0.052f * envelope;
        }
        AudioClip clip = AudioClip.Create(name, count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
