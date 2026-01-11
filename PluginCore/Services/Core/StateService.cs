using System.Text.Json;
using BarRaider.SdTools;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Installation;

namespace SCStreamDeck.Services.Core;

/// <summary>
///     Handles persistent storage and validation of plugin state.
/// </summary>
public sealed class StateService(IPathProvider pathProvider, IVersionProvider versionProvider) : IStateService
{
    private readonly IPathProvider
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));

    private readonly IVersionProvider _versionProvider =
        versionProvider ?? throw new ArgumentNullException(nameof(versionProvider));

    public async Task<PluginState?> LoadStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            PluginState? state = await PluginState.LoadAsync(_pathProvider.CacheDirectory, cancellationToken)
                .ConfigureAwait(false);
            return state;
        }

        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(StateService)}] {ErrorMessages.StateLoadFailed}: {ex.Message}");
            return null;
        }
    }

    public async Task SaveStateAsync(PluginState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            await state.SaveAsync(_pathProvider.CacheDirectory, cancellationToken).ConfigureAwait(false);
        }

        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(StateService)}] {ErrorMessages.StateSaveFailed}: {ex.Message}");
            throw;
        }
    }

    public async Task<IReadOnlyList<SCInstallCandidate>?> GetCachedCandidatesAsync(
        CancellationToken cancellationToken = default)
    {
        PluginState? state = await LoadStateAsync(cancellationToken).ConfigureAwait(false);

        if (state == null || !state.IsValid())
        {
            return null;
        }

        IReadOnlyList<SCInstallCandidate> candidates = state.GetCachedCandidates();
        return candidates;
    }

    public async Task UpdateInstallationAsync(SCChannel channel, InstallationState installation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installation);

        PluginState? currentState = await LoadStateAsync(cancellationToken).ConfigureAwait(false);

        if (currentState == null)
        {
            currentState = new PluginState(
                await _versionProvider.GetPluginVersionAsync(cancellationToken).ConfigureAwait(false),
                DateTime.UtcNow,
                channel,
                null,
                null,
                null,
                null
            );
        }

        PluginState updatedState = currentState
            .WithInstallation(channel, installation)
            .WithLastInitialized(DateTime.UtcNow);

        await SaveStateAsync(updatedState, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveInstallationAsync(SCChannel channel, CancellationToken cancellationToken = default)
    {
        PluginState? currentState = await LoadStateAsync(cancellationToken).ConfigureAwait(false);
        if (currentState == null)
        {
            return;
        }

        PluginState updatedState = currentState.WithoutInstallation(channel);
        await SaveStateAsync(updatedState, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateSelectedChannelAsync(SCChannel channel, CancellationToken cancellationToken = default)
    {
        PluginState? currentState = await LoadStateAsync(cancellationToken).ConfigureAwait(false);

        if (currentState == null)
        {
            // Create minimal state if none exists
            PluginState newState = new(
                await _versionProvider.GetPluginVersionAsync(cancellationToken).ConfigureAwait(false),
                DateTime.UtcNow,
                channel,
                null,
                null,
                null,
                null
            );

            await SaveStateAsync(newState, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            PluginState updatedState = currentState.WithSelectedChannel(channel);
            await SaveStateAsync(updatedState, cancellationToken).ConfigureAwait(false);
        }
    }

    public void InvalidateState()
    {
        try
        {
            string stateFile = Path.Combine(_pathProvider.CacheDirectory, ".plugin-state.json");
            if (File.Exists(stateFile))
            {
                File.Delete(stateFile);
            }
        }

        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(StateService)}] {ErrorMessages.StateInvalidateFailed} {ex.Message}");
        }
    }
}
