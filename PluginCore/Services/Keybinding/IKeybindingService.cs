using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Keybinding;

/// <summary>
///     Service for loading, managing, and executing Star Citizen keybindings.
/// </summary>
public interface IKeybindingService
{
    /// <summary>
    ///     Indicates whether keybindings are currently loaded.
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    ///     Loads keybindings asynchronously from specified JSON path.
    /// </summary>
    /// <param name="jsonPath">Path to processed keybindings JSON file</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>True if loading succeeded, false otherwise</returns>
    /// <exception cref="ArgumentException">Thrown when jsonPath is invalid.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to the file is denied.</exception>
    /// <exception cref="JsonException">Thrown when the JSON is malformed.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task<bool> LoadKeybindingsAsync(string jsonPath, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Executes a keybinding with activation mode awareness.
    ///     Non-blocking, fire-and-forget for UI responsiveness.
    /// </summary>
    /// <param name="context">Execution context with action, binding, and mode</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>True if execution started successfully, false otherwise</returns>
    /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when execution fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task<bool> ExecuteAsync(KeybindingExecutionContext context, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Tries to get a keybinding action by name.
    /// </summary>
    /// <param name="actionName">The action name to lookup (case-insensitive)</param>
    /// <param name="action">The found action, or null if not found</param>
    /// <returns>True if action exists, false otherwise</returns>
    bool TryGetAction(string? actionName, out KeybindingAction? action);

    /// <summary>
    ///     Gets all loaded keybinding actions.
    /// </summary>
    /// <returns>Read-only collection of all actions</returns>
    IReadOnlyList<KeybindingAction> GetAllActions();

    /// <summary>
    ///     Gets the current keyboard layout identifier (HKL).
    /// </summary>
    /// <returns>Keyboard layout identifier</returns>
    IntPtr GetKeyboardLayoutId();
}
