using SCStreamDeck.Models;

// ReSharper disable UnusedMember.Global

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
    /// <param name="context">The execution context containing action info, key state, and metadata.</param>
    /// <param name="executor">The executor for performing input actions.</param>
    /// <returns>True if execution was successful.</returns>
    /// <exception cref="ArgumentNullException">Thrown when context or executor is null.</exception>
    bool Execute(ActivationExecutionContext context, IInputExecutor executor);
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
    public required ActivationModeMetadata Metadata { get; init; }
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
    ///     Important: For toggle actions, this should NOT repeat while held.
    /// </summary>
    /// <param name="input">The parsed input to execute.</param>
    /// <returns>True if execution succeeded, false otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
    bool ExecutePress(ParsedInput input);

    /// <summary>
    ///     Executes a press action without repetition (for toggle modes).
    ///     Does NOT start repeat timers even with modifier keys.
    /// </summary>
    /// <param name="input">The parsed input to execute.</param>
    /// <returns>True if execution succeeded, false otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
    bool ExecutePressNoRepeat(ParsedInput input);

    /// <summary>
    ///     Holds a key/button down.
    /// </summary>
    /// <param name="input">The parsed input to execute.</param>
    /// <param name="actionKey">The action key for tracking.</param>
    /// <returns>True if execution succeeded, false otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
    /// <exception cref="ArgumentException">Thrown when actionKey is null or whitespace.</exception>
    bool ExecuteDown(ParsedInput input, string actionKey);

    /// <summary>
    ///     Releases a held key/button.
    /// </summary>
    /// <param name="input">The parsed input to execute.</param>
    /// <param name="actionKey">The action key for tracking.</param>
    /// <returns>True if execution succeeded, false otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
    /// <exception cref="ArgumentException">Thrown when actionKey is null or whitespace.</exception>
    bool ExecuteUp(ParsedInput input, string actionKey);

    /// <summary>
    ///     Schedules a delayed press execution.
    /// </summary>
    /// <param name="input">The parsed input to execute.</param>
    /// <param name="actionKey">The action key for tracking.</param>
    /// <param name="delaySeconds">Delay in seconds before execution.</param>
    /// <returns>True if scheduling succeeded, false otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
    /// <exception cref="ArgumentException">Thrown when actionKey is null or whitespace.</exception>
    bool ScheduleDelayedPress(ParsedInput input, string actionKey, float delaySeconds);

    /// <summary>
    ///     Cancels a scheduled delayed press.
    /// </summary>
    /// <param name="actionKey">The action key for tracking.</param>
    /// <exception cref="ArgumentException">Thrown when actionKey is null or whitespace.</exception>
    void CancelDelayedPress(string actionKey);

    /// <summary>
    ///     Schedules a delayed hold execution (starts holding after delay).
    /// </summary>
    /// <param name="input">The parsed input to execute.</param>
    /// <param name="actionKey">The action key for tracking.</param>
    /// <param name="delaySeconds">Delay in seconds before execution.</param>
    /// <returns>True if scheduling succeeded, false otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
    /// <exception cref="ArgumentException">Thrown when actionKey is null or whitespace.</exception>
    bool ScheduleDelayedHold(ParsedInput input, string actionKey, float delaySeconds);

    /// <summary>
    ///     Cancels a scheduled delayed hold.
    /// </summary>
    /// <param name="actionKey">The action key for tracking.</param>
    /// <exception cref="ArgumentException">Thrown when actionKey is null or whitespace.</exception>
    void CancelDelayedHold(string actionKey);
}
