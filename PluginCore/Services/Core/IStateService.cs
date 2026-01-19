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
    /// <exception cref="IOException">Thrown when the state file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to the state file is denied.</exception>
    /// <exception cref="JsonException">Thrown when the state file contains invalid JSON.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task<PluginState?> LoadStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves plugin state to disk asynchronously.
    /// </summary>
    /// <param name="state">The plugin state to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentNullException">Thrown when state is null.</exception>
    /// <exception cref="IOException">Thrown when the state file cannot be written.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to the state file is denied.</exception>
    /// <exception cref="JsonException">Thrown when the state cannot be serialized to JSON.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task SaveStateAsync(PluginState state, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets cached installation candidates from state if valid.
    ///     Returns null if no valid cached state exists.
    /// </summary>
    /// <exception cref="IOException">Thrown when the state file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to the state file is denied.</exception>
    /// <exception cref="JsonException">Thrown when the state file contains invalid JSON.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task<IReadOnlyList<SCInstallCandidate>?> GetCachedCandidatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates the state with a new installation for the specified channel.
    /// </summary>
    /// <param name="channel">The channel to update.</param>
    /// <param name="installation">The installation state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentNullException">Thrown when installation is null.</exception>
    /// <exception cref="IOException">Thrown when the state file cannot be written.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to the state file is denied.</exception>
    /// <exception cref="JsonException">Thrown when the state cannot be serialized to JSON.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task UpdateInstallationAsync(SCChannel channel, InstallationState installation,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes the installation for the specified channel from state.
    /// </summary>
    /// <param name="channel">The channel to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="IOException">Thrown when the state file cannot be written.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to the state file is denied.</exception>
    /// <exception cref="JsonException">Thrown when the state cannot be serialized to JSON.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task RemoveInstallationAsync(SCChannel channel, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates the selected channel in state.
    /// </summary>
    /// <param name="channel">The channel to set as selected.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="IOException">Thrown when the state file cannot be written.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to the state file is denied.</exception>
    /// <exception cref="JsonException">Thrown when the state cannot be serialized to JSON.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task UpdateSelectedChannelAsync(SCChannel channel, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Invalidates the cached state, forcing fresh initialization on next load.
    /// </summary>
    void InvalidateState();
}
