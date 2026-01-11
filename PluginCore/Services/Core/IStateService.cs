using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Core;

/// <summary>
///     Service for managing persistent plugin state.
///     Handles loading, saving, and validation of cached installation data.
/// </summary>
public interface IStateService
{
    /// <summary>
    ///     Loads plugin state from disk asynchronously.
    ///     Returns null if no state exists or loading fails.
    /// </summary>
    Task<PluginState?> LoadStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves plugin state to disk asynchronously.
    /// </summary>
    Task SaveStateAsync(PluginState state, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets cached installation candidates from state if valid.
    ///     Returns null if no valid cached state exists.
    /// </summary>
    Task<IReadOnlyList<SCInstallCandidate>?> GetCachedCandidatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates the state with a new installation for the specified channel.
    /// </summary>
    Task UpdateInstallationAsync(SCChannel channel, InstallationState installation,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes the installation for the specified channel from state.
    /// </summary>
    Task RemoveInstallationAsync(SCChannel channel, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates the selected channel in state.
    /// </summary>
    Task UpdateSelectedChannelAsync(SCChannel channel, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Invalidates the cached state, forcing fresh initialization on next load.
    /// </summary>
    void InvalidateState();
}
