using UnityEngine;

/// <summary>
/// V46 Splash entry contract: observe a released official PICO pinch, aim at
/// any part of the complete visible MODEL BUILDER bounds, then begin and hold
/// one fresh pinch. The target is locked at pinch start.
/// </summary>
[DefaultExecutionOrder(-180)]
[DisallowMultipleComponent]
public sealed class SplashModelBuilderEntryV46 : MonoBehaviour
{
    public SplashWorkshopController splash;
    public RuntimeIntentRayV46 intentRay;
    [Min(0.05f)] public float releaseArmSeconds = 0.10f;
    [Min(0.02f)] public float pinchHoldSeconds = 0.045f;
    [Header("V47 tracking-gap recovery")]
    public bool resilientSignalGapV47;
    [Min(0.05f)] public float signalGapArmSecondsV47 = 0.12f;
    [Header("V49 pinch-edge diagnostics")]
    public bool pinchEdgeTelemetryV49 = true;

    float releasedSince = -1f;
    float unavailableSince = -1f;
    float pressedSince = -1f;
    bool armed;
    bool wasPressed;
    bool targetLocked;

    public bool ArmedV46 => armed;
    public bool TargetLockedV46 => targetLocked;

    void Awake()
    {
        if (!splash)
            splash = GetComponent<SplashWorkshopController>();
        if (!intentRay)
            intentRay = GetComponent<RuntimeIntentRayV46>();
    }

    void OnEnable()
    {
        ResetGate();
    }

    void Update()
    {
        if (!PersonallyPuzzledExperienceModeState.IsPlayerManual ||
            !splash ||
            splash.IsActivated ||
            !intentRay)
            return;
        ProcessInput(
            intentRay.PinchTrackedV46,
            intentRay.PinchingV46,
            intentRay.SplashTargetedV46,
            Time.unscaledTime);
    }

    void ProcessInput(
        bool tracked,
        bool pressed,
        bool targeted,
        float now)
    {
        if (!tracked)
        {
            if (!resilientSignalGapV47)
            {
                ResetGate();
                return;
            }
            if (unavailableSince < 0f)
                unavailableSince = now;
            splash.SetTouchHover(targeted);
            if (now - unavailableSince >=
                Mathf.Max(0.05f, signalGapArmSecondsV47))
            {
                if (!armed)
                    Debug.Log(
                        "[PPVR V47 Gate] stage=Splash event=armed " +
                        "reason=tracking-gap gap=" +
                        (now - unavailableSince).ToString("F3"));
                armed = true;
                wasPressed = false;
                pressedSince = -1f;
                targetLocked = false;
            }
            return;
        }
        unavailableSince = -1f;

        if (!pressed)
        {
            if (releasedSince < 0f)
                releasedSince = now;
            if (now - releasedSince >=
                Mathf.Max(0.05f, releaseArmSeconds))
                armed = true;
            wasPressed = false;
            pressedSince = -1f;
            targetLocked = false;
            splash.SetTouchHover(targeted);
            return;
        }

        bool pressEdge = !wasPressed;
        wasPressed = true;
        releasedSince = -1f;
        if (pressEdge)
        {
            targetLocked = armed && targeted;
            pressedSince = targetLocked ? now : -1f;
            if (resilientSignalGapV47 || pinchEdgeTelemetryV49)
                Debug.Log(
                    "[PPVR V49 Gate] stage=Splash event=press-edge " +
                    "armed=" + armed +
                    " targeted=" + targeted +
                    " locked=" + targetLocked);
        }
        splash.SetTouchHover(targetLocked || targeted);
        if (!targetLocked ||
            pressedSince < 0f ||
            now - pressedSince <
                Mathf.Max(0.02f, pinchHoldSeconds))
            return;

        armed = false;
        targetLocked = false;
        pressedSince = -1f;
        splash.ActivateWorkshop();
        if (resilientSignalGapV47 || pinchEdgeTelemetryV49)
            Debug.Log(
                "[PPVR V49 Gate] stage=Splash event=accepted " +
                "activated=" + splash.IsActivated);
    }

    void ResetGate()
    {
        releasedSince = -1f;
        unavailableSince = -1f;
        pressedSince = -1f;
        armed = false;
        wasPressed = false;
        targetLocked = false;
    }

#if UNITY_EDITOR
    public void ProcessForAutomationV47(
        bool tracked,
        bool pressed,
        bool targeted,
        float now)
    {
        ProcessInput(tracked, pressed, targeted, now);
    }

    public void ArmForAutomationV46()
    {
        ResetGate();
        armed = true;
    }

    public bool TryPinchRayForAutomationV46(Ray ray)
    {
        if (!armed ||
            !intentRay ||
            !intentRay.ResolveSplashRayV46(ray, out _))
            return false;
        armed = false;
        targetLocked = false;
        splash.ActivateWorkshop();
        return splash.IsActivated;
    }
#endif
}
