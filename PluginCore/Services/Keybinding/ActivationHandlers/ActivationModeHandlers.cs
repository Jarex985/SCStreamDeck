using System.Collections.Concurrent;
using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Keybinding.ActivationHandlers;

/// <summary>
///     Handler for immediate press activation modes.
///     Triggers based on activation mode metadata (OnPress, OnRelease, Retriggerable, MultiTapBlock).
///     Implements KeyDown/KeyUp behavior using metadata flags.
/// </summary>
internal sealed class ImmediatePressHandler : IActivationModeHandler
{
    /// <summary>
    ///     Tracks which actions have been activated during the current key press cycle.
    ///     Used for MultiTapBlock logic to prevent OnRelease from firing after OnPress.
    /// </summary>
    private readonly ConcurrentDictionary<string, bool> _activationBlocks = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ActivationMode> SupportedModes =>
    [
        ActivationMode.press,
        ActivationMode.press_quicker,
        ActivationMode.tap,
        ActivationMode.tap_quicker,
        ActivationMode.double_tap,
        ActivationMode.double_tap_nonblocking,
        ActivationMode.hold_toggle,
        ActivationMode.all
    ];

    public bool Execute(ActivationExecutionContext context, IInputExecutor executor) =>
        context.IsKeyDown ? HandleKeyDown(context, executor) : HandleKeyUp(context, executor);

    private bool HandleKeyDown(ActivationExecutionContext context, IInputExecutor executor)
    {
        // Clear any previous activation block for this action (new key press cycle)
        _activationBlocks.TryRemove(context.ActionName, out _);

        if (!context.Metadata.OnPress)
        {
            return true; // Ignore KeyDown, wait for KeyUp
        }

        bool result = context.Metadata.Retriggerable
            ? executor.ExecuteDown(context.Input, context.ActionName)
            : executor.ExecutePressNoRepeat(context.Input);

        // If MultiTapBlock is set, block subsequent OnRelease execution
        if (context.Metadata.MultiTapBlock > 0)
        {
            _activationBlocks[context.ActionName] = true;
        }

        return result;
    }

    private bool HandleKeyUp(ActivationExecutionContext context, IInputExecutor executor)
    {
        // Check if OnPress was executed with MultiTapBlock
        bool wasBlocked = _activationBlocks.TryRemove(context.ActionName, out _);

        // If OnRelease is true AND not blocked by MultiTapBlock, execute additional release press
        if (context.Metadata.OnRelease && !wasBlocked)
        {
            // CRITICAL: Check ReleaseTriggerDelay FIRST (used by smart_toggle)
            if (context.Metadata.ReleaseTriggerDelay > 0)
            {
                return executor.ScheduleDelayedPress(context.Input, context.ActionName, context.Metadata.ReleaseTriggerDelay);
            }

            // Then check ReleaseTriggerThreshold (used by tap modes)
            if (context.Metadata.ReleaseTriggerThreshold > 0)
            {
                return executor.ScheduleDelayedPress(context.Input, context.ActionName, context.Metadata.ReleaseTriggerThreshold);
            }

            // No delay - execute immediately
            return executor.ExecutePressNoRepeat(context.Input);
        }

        // ALWAYS release hold if Retriggerable was true (even if MultiTapBlock blocked OnRelease)
        // This prevents "stuck" hold states for modes like "all" (Retriggerable=true)
        if (context.Metadata.Retriggerable)
        {
            return executor.ExecuteUp(context.Input, context.ActionName);
        }

        // For non-retriggerable modes (e.g., hold_toggle with MultiTapBlock),
        // OnRelease was already blocked above, so just return success
        return true;
    }
}

/// <summary>
///     Handler for delayed press activation modes.
///     Starts holding the key after a delay threshold, continues until key is released.
///     Uses PressTriggerThreshold metadata (or mode default delay).
/// </summary>
internal sealed class DelayedPressHandler : IActivationModeHandler
{
    public IReadOnlyCollection<ActivationMode> SupportedModes =>
    [
        ActivationMode.delayed_press,
        ActivationMode.delayed_press_quicker,
        ActivationMode.delayed_press_medium,
        ActivationMode.delayed_press_long
    ];

    public bool Execute(ActivationExecutionContext context, IInputExecutor executor)
    {
        if (context.IsKeyDown)
        {
            // Schedule delayed hold start
            float delay = context.Metadata.PressTriggerThreshold > 0
                ? context.Metadata.PressTriggerThreshold
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
///     For delayed holds, uses PressTriggerThreshold metadata (or a mode default delay).
/// </summary>
internal sealed class HoldHandler : IActivationModeHandler
{
    public IReadOnlyCollection<ActivationMode> SupportedModes =>
    [
        ActivationMode.hold,
        ActivationMode.hold_no_retrigger,
        ActivationMode.delayed_hold,
        ActivationMode.delayed_hold_long,
        ActivationMode.delayed_hold_no_retrigger
    ];

    public bool Execute(ActivationExecutionContext context, IInputExecutor executor)
    {
        switch (context.Mode)
        {
            case ActivationMode.hold:
            case ActivationMode.hold_no_retrigger:
                if (context.IsKeyDown)
                {
                    // Use ExecuteDown/ExecuteUp; the executor determines any repeat behavior.
                    return executor.ExecuteDown(context.Input, context.ActionName);
                }

                return executor.ExecuteUp(context.Input, context.ActionName);

            case ActivationMode.delayed_hold:
            case ActivationMode.delayed_hold_long:
            case ActivationMode.delayed_hold_no_retrigger:
                if (context.IsKeyDown)
                {
                    // Schedule delayed hold using PressTriggerThreshold from metadata
                    float delay = context.Metadata.PressTriggerThreshold > 0
                        ? context.Metadata.PressTriggerThreshold
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
