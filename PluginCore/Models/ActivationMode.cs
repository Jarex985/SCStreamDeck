// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

#pragma warning disable CA1707

namespace SCStreamDeck.Models;

/// <summary>
///     Represents Star Citizen activation modes that determine how keybindings trigger.
///     Based on defaultProfile.xml ActivationModes definitions.
/// </summary>
public enum ActivationMode
{
    tap, // Trigger on release with 0.25s release threshold (quick press/release).
    double_tap, // Double-tap detection (multiTap=2).
    double_tap_nonblocking, // Double-tap detection without blocking (multiTap=2, multiTapBlock=0).
    tap_quicker, // Trigger on release with 0.15s release threshold (quicker than tap).
    press, // Trigger immediately on press.
    press_quicker, // Trigger on press and release with 0.15s release threshold.
    delayed_press, // Trigger on press after 0.25s delay.
    delayed_press_quicker, // Trigger on press after 0.15s delay.
    delayed_press_medium, // Trigger on press after 0.5s delay.
    delayed_press_long, // Trigger on press after 1.5s delay.
    hold, // Hold while pressed, retriggerable.
    hold_no_retrigger, // Hold while pressed, not retriggerable.
    all, // Trigger on press, hold, and release.
    delayed_hold, // Delayed hold with 0.25s threshold, retriggerable.
    delayed_hold_long, // Delayed hold with 1.5s threshold, retriggerable.
    delayed_hold_no_retrigger, // Delayed hold with 0.15s threshold, not retriggerable.
    hold_toggle, // Toggle on press and release.
    smart_toggle // Smart toggle with 0.25s release delay.
}
#pragma warning restore CA1707
