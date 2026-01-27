namespace SCStreamDeck.Models;

/// <summary>
///     Metadata describing the behavior of an activation mode based on Star Citizen's defaultProfile.xml definitions.
///     These flags determine when and how a keybinding should trigger.
/// </summary>
public sealed class ActivationModeMetadata
{
    /// <summary>
    ///     Trigger action when button is initially pressed.
    /// </summary>
    public bool OnPress { get; init; }

    /// <summary>
    ///     Trigger action continuously while button is held down (for continuous actions like firing).
    /// </summary>
    public bool OnHold { get; init; }

    /// <summary>
    ///     Trigger action when button is released.
    /// </summary>
    public bool OnRelease { get; init; }

    /// <summary>
    ///     Delay in seconds before press trigger fires (-1 = instant/no delay).
    /// </summary>
    public float PressTriggerThreshold { get; init; }

    /// <summary>
    ///     Delay in seconds before release trigger fires (-1 = instant/no delay).
    /// </summary>
    public float ReleaseTriggerThreshold { get; init; }

    /// <summary>
    ///     Whether the action can be retriggered while held.
    /// </summary>
    public bool Retriggerable { get; init; }

    /// <summary>
    ///     Number of taps required for activation (1 = single tap, 2 = double tap).
    /// </summary>
    public int MultiTap { get; init; }

    /// <summary>
    ///     Delay in seconds after key release before action triggers (used by smart_toggle).
    /// </summary>
    public float ReleaseTriggerDelay { get; init; }

    /// <summary>
    ///     Whether multi-tap detection blocks other inputs during the tap window.
    ///     0 = non-blocking (double_tap_nonblocking), 1 = blocking (double_tap).
    /// </summary>
    public int MultiTapBlock { get; init; }

    /// <summary>
    ///     Returns an empty/activation mode metadata with default values.
    /// </summary>
    public static ActivationModeMetadata Empty() => new()
    {
        OnPress = true,
        OnHold = false,
        OnRelease = false,
        PressTriggerThreshold = -1.0f,
        ReleaseTriggerThreshold = -1.0f,
        Retriggerable = false,
        MultiTap = 1,
        ReleaseTriggerDelay = 0.0f,
        MultiTapBlock = 1
    };
}
