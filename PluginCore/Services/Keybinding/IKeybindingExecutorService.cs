using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Keybinding;

/// <summary>
///     Service for executing keybinding actions.
/// </summary>
public interface IKeybindingExecutorService
{
    /// <summary>
    ///     Executes a keybinding action with the given context.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if execution succeeded, false otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when execution fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task<bool> ExecuteAsync(KeybindingExecutionContext context, CancellationToken cancellationToken = default);
}
