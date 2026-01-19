using System.Collections.Concurrent;
using BarRaider.SdTools;

namespace SCStreamDeck.Services.Keybinding.ActivationHandlers;

/// <summary>
///     Handler for smart_toggle activation mode.
///     Implements temporal toggle behavior: short press = permanent, long press = temporary.
/// </summary>
internal sealed class SmartToggleHandler : IActivationModeHandler
{
    private readonly ConcurrentDictionary<string, KeyPressState> _keyStates = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<string> SupportedModes => ["smart_toggle"];

    public bool Execute(ActivationExecutionContext context, IInputExecutor executor) =>
        context.IsKeyDown ? HandleKeyDown(context, executor) : HandleKeyUp(context, executor);

    private bool HandleKeyDown(ActivationExecutionContext context, IInputExecutor executor)
    {
        string key = context.ActionName;
        float delay = context.Metadata.ReleaseTriggerDelay > 0 ? context.Metadata.ReleaseTriggerDelay : 0.25f;

        // Initialize state for this key press
        KeyPressState state = new() { KeyDownTime = DateTime.UtcNow, AutoToggleExecuted = false };

        // Create timer that will execute auto-toggle after delay
        state.AutoToggleTimer = new Timer(_ =>
        {
            try
            {
                // Execute auto-toggle (first toggle)
                executor.ExecutePressNoRepeat(context.Input);
                state.AutoToggleExecuted = true;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[SmartToggleHandler] Auto-toggle failed for '{key}': {ex.Message}");
            }
        }, null, (int)(delay * 1000), Timeout.Infinite);

        _keyStates[key] = state;

        return true;
    }

    private bool HandleKeyUp(ActivationExecutionContext context, IInputExecutor executor)
    {
        string key = context.ActionName;

        if (!_keyStates.TryRemove(key, out KeyPressState? state))
        {
            return true;
        }

        state.AutoToggleTimer?.Dispose();
        TimeSpan heldDuration = DateTime.UtcNow - state.KeyDownTime;

#if DEBUG
        Logger.Instance.LogMessage(TracingLevel.DEBUG,
            $"[SmartToggleHandler] KeyUp for '{key}' after {heldDuration.TotalSeconds:F3}s, AutoToggleExecuted={state.AutoToggleExecuted}");
#endif
        // Decision based on whether auto-toggle was executed
        if (state.AutoToggleExecuted)
        {
            // Long press → Second toggle (reverts the first one)
            return executor.ExecutePressNoRepeat(context.Input);
        }

        // Short press → First (and only) toggle
        return executor.ExecutePressNoRepeat(context.Input);
    }

    /// <summary>
    ///     State tracking for a single key press.
    /// </summary>
    private sealed class KeyPressState
    {
        public DateTime KeyDownTime { get; init; }
        public Timer? AutoToggleTimer { get; set; }
        public bool AutoToggleExecuted { get; set; }
    }
}
