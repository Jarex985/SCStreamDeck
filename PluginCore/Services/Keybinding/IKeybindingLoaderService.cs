using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Keybinding;

/// <summary>
///     Service for loading and caching keybinding actions and activation modes.
/// </summary>
public interface IKeybindingLoaderService
{
    /// <summary>
    ///     Indicates whether keybindings are loaded.
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    ///     Loads keybindings from a JSON file asynchronously.
    /// </summary>
    Task<bool> LoadKeybindingsAsync(string jsonPath, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Tries to get a keybinding action by name.
    /// </summary>
    bool TryGetAction(string? actionName, out KeybindingAction? action);

    /// <summary>
    ///     Gets all loaded keybinding actions.
    /// </summary>
    IReadOnlyList<KeybindingAction> GetAllActions();

    /// <summary>
    ///     Gets the current keyboard layout ID.
    /// </summary>
    IntPtr GetKeyboardLayoutId();

    /// <summary>
    ///     Gets the loaded activation modes.
    /// </summary>
    IReadOnlyDictionary<string, ActivationModeMetadata> GetActivationModes();

    /// <summary>
    ///     Gets the activation mode metadata for a specific action.
    /// </summary>
    /// <param name="actionName">The action name to get metadata for.</param>
    /// <returns>The activation mode metadata, or null if not found.</returns>
    ActivationModeMetadata? GetMetadata(string actionName);
}
