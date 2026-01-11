using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Keybinding.ActivationHandlers;

/// <summary>
///     Strategy interface for handling different activation modes.
///     Each activation mode (press, hold, delayed_press, etc.) has its own handler.
/// </summary>
internal interface IActivationModeHandler
{
    /// <summary>
    ///     Gets the activation modes this handler supports.
    /// </summary>
    IEnumerable<string> SupportedModes { get; }

    /// <summary>
    ///     Executes the activation mode logic.
    /// </summary>
    /// <param name="context">The execution context containing action info and key state.</param>
    /// <param name="metadata">The activation mode metadata from the game config.</param>
    /// <param name="executor">The executor for performing input actions.</param>
    /// <returns>True if execution was successful.</returns>
    bool Execute(ActivationExecutionContext context, ActivationModeMetadata metadata, IInputExecutor executor);
}

/// <summary>
///     Context for activation mode execution.
/// </summary>
internal sealed class ActivationExecutionContext
{
    public required string ActionName { get; init; }
    public required ParsedInput Input { get; init; }
    public required bool IsKeyDown { get; init; }
    public required ActivationMode Mode { get; init; }
}

/// <summary>
///     Represents parsed input data ready for execution.
///     Note: Only Keyboard, MouseButton, and MouseWheel are executed by ActivationHandlers.
///     Other types (Joystick, Gamepad, MouseAxis) are used for display/metadata only.
/// </summary>
internal sealed class ParsedInput
{
    public required InputType Type { get; init; }
    public required object Value { get; init; }
}

/// <summary>
///     Interface for executing input actions (keyboard, mouse).
///     Abstraction over the actual input simulation.
///     Only handles Keyboard, MouseButton, and MouseWheel inputs.
/// </summary>
internal interface IInputExecutor
{
    /// <summary>
    ///     Executes a press action (key/button down then up).
    /// </summary>
    bool ExecutePress(ParsedInput input);

    /// <summary>
    ///     Holds a key/button down.
    /// </summary>
    bool ExecuteDown(ParsedInput input, string actionKey);

    /// <summary>
    ///     Releases a held key/button.
    /// </summary>
    bool ExecuteUp(ParsedInput input, string actionKey);

    /// <summary>
    ///     Schedules a delayed press execution.
    /// </summary>
    bool ScheduleDelayedPress(ParsedInput input, string actionKey, float delaySeconds);

    /// <summary>
    ///     Cancels a scheduled delayed press.
    /// </summary>
    void CancelDelayedPress(string actionKey);

    /// <summary>
    ///     Schedules a delayed hold execution (starts holding after delay).
    /// </summary>
    bool ScheduleDelayedHold(ParsedInput input, string actionKey, float delaySeconds);

    /// <summary>
    ///     Cancels a scheduled delayed hold.
    /// </summary>
    void CancelDelayedHold(string actionKey);
}
