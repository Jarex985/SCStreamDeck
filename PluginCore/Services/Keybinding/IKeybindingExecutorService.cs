using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Keybinding;

/// <summary>
///     Service for executing keybinding actions.
/// </summary>
public interface IKeybindingExecutorService
{
    Task<bool> ExecuteAsync(KeybindingExecutionContext context, CancellationToken cancellationToken = default);
}
