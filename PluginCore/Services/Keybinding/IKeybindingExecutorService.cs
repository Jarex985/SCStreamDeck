using SCStreamDeck.SCCore.Models;

namespace SCStreamDeck.SCCore.Services.Keybinding;

/// <summary>
///     Service for executing keybinding actions.
/// </summary>
public interface IKeybindingExecutorService
{
    /// <summary>
    ///     Executes a keybinding asynchronously.
    /// </summary>
    Task<bool> ExecuteAsync(KeybindingExecutionContext context, CancellationToken cancellationToken = default);
}
