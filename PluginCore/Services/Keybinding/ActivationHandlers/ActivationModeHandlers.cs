using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Keybinding.ActivationHandlers;

/// <summary>
///     Handler for immediate press activation modes.
///     Triggers immediately on button press for modes like press, tap, double_tap, toggle, all.
/// </summary>
internal sealed class ImmediatePressHandler : IActivationModeHandler
{
    public IEnumerable<string> SupportedModes =>
    [
        "press", "press_quicker", "tap", "tap_quicker", "double_tap", "double_tap_nonblocking", "hold_toggle", "smart_toggle",
        "all"
    ];

    public bool Execute(ActivationExecutionContext context, ActivationModeMetadata metadata, IInputExecutor executor)
    {
        switch (context.Mode)
        {
            case ActivationMode.press:
            case ActivationMode.press_quicker:
                // Press modes: KeyDown starts holding, KeyUp releases
                if (context.IsKeyDown)
                {
                    return executor.ExecuteDown(context.Input, context.ActionName);
                }

                return executor.ExecuteUp(context.Input, context.ActionName);

            case ActivationMode.tap:
            case ActivationMode.tap_quicker:
                // Tap modes trigger on key UP (release), not down
                if (context.IsKeyDown)
                {
                    return true; // Ignore press, wait for release
                }

                return executor.ExecutePress(context.Input);

            case ActivationMode.double_tap:
            case ActivationMode.double_tap_nonblocking:
                // Treat as simple press for StreamDeck use
                if (!context.IsKeyDown)
                {
                    return true;
                }

                return executor.ExecutePress(context.Input);

            case ActivationMode.hold_toggle:
            case ActivationMode.smart_toggle:
                // Toggle modes send a press on each button press
                if (!context.IsKeyDown)
                {
                    return true;
                }

                return executor.ExecutePress(context.Input);

            case ActivationMode.all:
                // Hold while pressed - Star Citizen decides based on duration
                if (context.IsKeyDown)
                {
                    return executor.ExecuteDown(context.Input, context.ActionName);
                }

                return executor.ExecuteUp(context.Input, context.ActionName);

            default:
                return false;
        }
    }
}

/// <summary>
///     Handler for delayed press activation modes.
///     Starts holding the key after a delay threshold, continues until key is released.
/// </summary>
internal sealed class DelayedPressHandler : IActivationModeHandler
{
    public IEnumerable<string> SupportedModes =>
        ["delayed_press", "delayed_press_quicker", "delayed_press_medium", "delayed_press_long"];

    public bool Execute(ActivationExecutionContext context, ActivationModeMetadata metadata, IInputExecutor executor)
    {
        if (context.IsKeyDown)
        {
            // Schedule delayed hold start
            float delay = metadata.PressTriggerThreshold > 0
                ? metadata.PressTriggerThreshold
                : GetDefaultDelay(context.Mode);

            return executor.ScheduleDelayedHold(context.Input, context.ActionName, delay);
        }

        // KeyUp: Cancel delayed hold if not started yet, or release if already holding
        executor.CancelDelayedHold(context.ActionName);
        return executor.ExecuteUp(context.Input, context.ActionName);
    }

    private static float GetDefaultDelay(ActivationMode mode) =>
        mode switch
        {
            ActivationMode.delayed_press_quicker => 0.15f,
            ActivationMode.delayed_press => 0.25f,
            ActivationMode.delayed_press_medium => 0.5f,
            ActivationMode.delayed_press_long => 1.5f,
            _ => 0.25f
        };
}

/// <summary>
///     Handler for hold activation modes.
///     Key/button stays pressed while StreamDeck button is held.
/// </summary>
internal sealed class HoldHandler : IActivationModeHandler
{
    public IEnumerable<string> SupportedModes =>
    [
        "hold", "hold_no_retrigger", "delayed_hold", "delayed_hold_long", "delayed_hold_no_retrigger"
    ];

    public bool Execute(ActivationExecutionContext context, ActivationModeMetadata metadata, IInputExecutor executor)
    {
        switch (context.Mode)
        {
            case ActivationMode.hold:
            case ActivationMode.hold_no_retrigger:
                if (context.IsKeyDown)
                {
                    return executor.ExecuteDown(context.Input, context.ActionName);
                }

                return executor.ExecuteUp(context.Input, context.ActionName);

            case ActivationMode.delayed_hold:
            case ActivationMode.delayed_hold_long:
            case ActivationMode.delayed_hold_no_retrigger:
                if (context.IsKeyDown)
                {
                    // Schedule delayed hold
                    float delay = metadata.PressTriggerThreshold > 0
                        ? metadata.PressTriggerThreshold
                        : GetDefaultDelay(context.Mode);

                    return executor.ScheduleDelayedHold(context.Input, context.ActionName, delay);
                }

                // Release: Cancel delayed hold if not started yet, or release if already holding
                executor.CancelDelayedHold(context.ActionName);
                return executor.ExecuteUp(context.Input, context.ActionName);

            default:
                return false;
        }
    }

    private static float GetDefaultDelay(ActivationMode mode) =>
        mode switch
        {
            ActivationMode.delayed_hold_no_retrigger => 0.15f,
            ActivationMode.delayed_hold => 0.25f,
            ActivationMode.delayed_hold_long => 1.5f,
            _ => 0.25f
        };
}
