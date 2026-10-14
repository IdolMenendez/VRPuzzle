using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Reads the official PICO hand-interaction devices without binding the game
/// assembly to one SDK compile symbol. Swan exposes either Pico Aim Hand
/// (indexPressed/pinchStrengthIndex) or Pico Hand Interaction
/// (pointerActivated/pinchTouched). Joint distance remains a caller-owned
/// fallback only when neither official device exists.
/// </summary>
public static class PicoSwanPinchSignalV40
{
    const float OfficialPressThreshold = 0.80f;

    const float StrictPressThresholdV42 = 0.92f;
    const float ResponsivePressThresholdV44 = 0.74f;

    /// <summary>
    /// V44 responsive intent read. It still ignores contact-only
    /// `pinchTouched`, while accepting the official analogue intent before it
    /// reaches V42's near-maximum threshold. Callers retain release, hold and
    /// single-consumption gates.
    /// </summary>
    public static bool TryReadIntentV44(
        out bool anyTracked,
        out bool anyPressed)
    {
        bool available = false;
        anyTracked = false;
        anyPressed = false;

        foreach (InputDevice device in
                 InputSystem.devices)
        {
            if (device == null ||
                !device.added ||
                !device.enabled)
                continue;
            string product =
                device.description.product ??
                string.Empty;
            bool aimHand =
                product.IndexOf(
                    "Pico Aim Hand",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            bool handInteraction =
                product.IndexOf(
                    "Pico Hand Interaction",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            if (!aimHand && !handInteraction)
                continue;

            available = true;
            ButtonControl tracked =
                Control<ButtonControl>(
                    device,
                    "isTracked");
            bool deviceTracked =
                tracked == null ||
                tracked.isPressed;
            anyTracked |= deviceTracked;
            if (!deviceTracked)
                continue;

            if (aimHand)
            {
                AxisControl strength =
                    Control<AxisControl>(
                        device,
                        "pinchStrengthIndex");
                ButtonControl pressed =
                    Control<ButtonControl>(
                        device,
                        "indexPressed");
                anyPressed |=
                    (strength != null &&
                     strength.ReadValue() >=
                        ResponsivePressThresholdV44) ||
                    (pressed != null &&
                     pressed.isPressed);
            }
            else
            {
                AxisControl pinchValue =
                    Control<AxisControl>(
                        device,
                        "pinchValue");
                AxisControl pointerValue =
                    Control<AxisControl>(
                        device,
                        "pointerActivateValue");
                ButtonControl pointer =
                    Control<ButtonControl>(
                        device,
                        "pointerActivated");
                anyPressed |=
                    (pinchValue != null &&
                     pinchValue.ReadValue() >=
                        ResponsivePressThresholdV44) ||
                    (pointerValue != null &&
                     pointerValue.ReadValue() >=
                        ResponsivePressThresholdV44) ||
                    (pointer != null &&
                     pointer.isPressed);
            }
        }
        return available;
    }

    /// <summary>
    /// V42 strict read. `pinchTouched` reports thumb/index contact, not a
    /// deliberate pinch, so it is ignored here and the analogue envelopes must
    /// reach a near-full value. This is what stops one hand pose from firing
    /// repeated carousel actions.
    /// </summary>
    public static bool TryReadStrictV42(
        out bool anyTracked,
        out bool anyPressed)
    {
        bool available = false;
        anyTracked = false;
        anyPressed = false;

        foreach (InputDevice device in
                 InputSystem.devices)
        {
            if (device == null ||
                !device.added ||
                !device.enabled)
                continue;
            string product =
                device.description.product ??
                string.Empty;
            bool aimHand =
                product.IndexOf(
                    "Pico Aim Hand",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            bool handInteraction =
                product.IndexOf(
                    "Pico Hand Interaction",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            if (!aimHand &&
                !handInteraction)
                continue;

            available = true;
            ButtonControl tracked =
                Control<ButtonControl>(
                    device,
                    "isTracked");
            bool deviceTracked =
                tracked == null ||
                tracked.isPressed;
            anyTracked |= deviceTracked;
            if (!deviceTracked)
                continue;

            if (aimHand)
            {
                AxisControl strength =
                    Control<AxisControl>(
                        device,
                        "pinchStrengthIndex");
                ButtonControl pressed =
                    Control<ButtonControl>(
                        device,
                        "indexPressed");
                anyPressed |=
                    strength != null
                        ? strength.ReadValue() >=
                            StrictPressThresholdV42
                        : pressed != null &&
                            pressed.isPressed;
            }
            else
            {
                AxisControl pinchValue =
                    Control<AxisControl>(
                        device,
                        "pinchValue");
                AxisControl pointerValue =
                    Control<AxisControl>(
                        device,
                        "pointerActivateValue");
                ButtonControl pointer =
                    Control<ButtonControl>(
                        device,
                        "pointerActivated");
                if (pinchValue != null)
                    anyPressed |=
                        pinchValue.ReadValue() >=
                        StrictPressThresholdV42;
                else if (pointerValue != null)
                    anyPressed |=
                        pointerValue.ReadValue() >=
                        StrictPressThresholdV42;
                else if (pointer != null)
                    anyPressed |= pointer.isPressed;
            }
        }

        return available;
    }

    public static bool TryRead(
        out bool anyTracked,
        out bool anyPressed)
    {
        bool available = false;
        anyTracked = false;
        anyPressed = false;

        foreach (InputDevice device in
                 InputSystem.devices)
        {
            if (device == null ||
                !device.added ||
                !device.enabled)
                continue;
            string product =
                device.description.product ??
                string.Empty;
            bool aimHand =
                product.IndexOf(
                    "Pico Aim Hand",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            bool handInteraction =
                product.IndexOf(
                    "Pico Hand Interaction",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            if (!aimHand &&
                !handInteraction)
                continue;

            available = true;
            ButtonControl tracked =
                Control<ButtonControl>(
                    device,
                    "isTracked");
            bool deviceTracked =
                tracked == null ||
                tracked.isPressed;
            anyTracked |=
                deviceTracked;
            if (!deviceTracked)
                continue;

            if (aimHand)
            {
                ButtonControl pressed =
                    Control<ButtonControl>(
                        device,
                        "indexPressed");
                AxisControl strength =
                    Control<AxisControl>(
                        device,
                        "pinchStrengthIndex");
                anyPressed |=
                    (pressed != null &&
                     pressed.isPressed) ||
                    (strength != null &&
                     strength.ReadValue() >=
                     OfficialPressThreshold);
            }
            else
            {
                ButtonControl pointer =
                    Control<ButtonControl>(
                        device,
                        "pointerActivated");
                ButtonControl pinch =
                    Control<ButtonControl>(
                        device,
                        "pinchTouched");
                AxisControl pointerValue =
                    Control<AxisControl>(
                        device,
                        "pointerActivateValue");
                AxisControl pinchValue =
                    Control<AxisControl>(
                        device,
                        "pinchValue");
                anyPressed |=
                    (pointer != null &&
                     pointer.isPressed) ||
                    (pinch != null &&
                     pinch.isPressed) ||
                    (pointerValue != null &&
                     pointerValue.ReadValue() >=
                     OfficialPressThreshold) ||
                    (pinchValue != null &&
                     pinchValue.ReadValue() >=
                     OfficialPressThreshold);
            }
        }

        return available;
    }

    static T Control<T>(
        InputDevice device,
        string name)
        where T : InputControl
    {
        foreach (InputControl control in
                 device.allControls)
            if (control is T typed &&
                string.Equals(
                    control.name,
                    name,
                    StringComparison.Ordinal))
                return typed;
        return null;
    }
}
