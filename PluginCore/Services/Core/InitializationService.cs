using SCStreamDeck.Common;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Installation;
using SCStreamDeck.Services.Keybinding;

namespace SCStreamDeck.Services.Core;

/// <summary>
///     Handles plugin startup, installation detection, and channel management.
/// </summary>
public sealed class InitializationService : IDisposable
{
    private readonly ICachedInstallationsCleanupService _cachedInstallationsCleanupService;
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);
    private readonly IInstallLocatorService _installLocator;
    private readonly KeybindingProcessorService _keybindingProcessor;
    private readonly KeybindingService _keybindingService;
    private readonly IKeybindingsJsonCache _keybindingsJsonCache;
    private readonly object _lock = new();
    private readonly PathProviderService _pathProvider;
    private readonly StateService _stateService;

    private SCChannel _currentChannel = SCChannel.Live;
    private Task<InitializationResult>? _initializationTask;
    private bool _initialized;

    public InitializationService(
        KeybindingService keybindingService,
        IInstallLocatorService installLocator,
        KeybindingProcessorService keybindingProcessor,
        PathProviderService pathProvider,
        StateService stateService,
        IKeybindingsJsonCache keybindingsJsonCache,
        ICachedInstallationsCleanupService cachedInstallationsCleanupService)
    {
        _keybindingService = keybindingService ?? throw new ArgumentNullException(nameof(keybindingService));
        _installLocator = installLocator ?? throw new ArgumentNullException(nameof(installLocator));
        _keybindingProcessor = keybindingProcessor ?? throw new ArgumentNullException(nameof(keybindingProcessor));
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
        _keybindingsJsonCache = keybindingsJsonCache ?? throw new ArgumentNullException(nameof(keybindingsJsonCache));
        _cachedInstallationsCleanupService = cachedInstallationsCleanupService ??
                                             throw new ArgumentNullException(nameof(cachedInstallationsCleanupService));
        _pathProvider.EnsureDirectoriesExist();
    }

    public SCChannel CurrentChannel
    {
        get
        {
            lock (_lock)
            {
                return _currentChannel;
            }
        }
    }

    public bool IsInitialized
    {
        get
        {
            lock (_lock)
            {
                return _initialized;
            }
        }
    }

    public void Dispose() => _initSemaphore.Dispose();

    public event Action? KeybindingsStateChanged;

    public async Task<InitializationResult> EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return InitializationResult.Success(_currentChannel, 0);
        }

        await _initSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return InitializationResult.Success(_currentChannel, 0);
            }

            if (_initializationTask != null)
            {
                return await _initializationTask.ConfigureAwait(false);
            }

            _initializationTask = InitializeInternalAsync(cancellationToken);
            try
            {
                return await _initializationTask.ConfigureAwait(false);
            }
            finally
            {
                _initializationTask = null;
            }
        }
        finally
        {
            _initSemaphore.Release();
        }
    }


    public async Task<bool> SwitchChannelAsync(SCChannel channel, CancellationToken cancellationToken = default)
    {
        try
        {
            string jsonPath = _pathProvider.GetKeybindingJsonPath(channel.ToString());
            bool success = await _keybindingService.LoadKeybindingsAsync(jsonPath, cancellationToken)
                .ConfigureAwait(false);

            if (!success)
            {
                return false;
            }

            lock (_lock)
            {
                _currentChannel = channel;
            }

            Log.Debug($"[{nameof(InitializationService)}] Switched to {channel}");

            KeybindingsStateChanged?.Invoke();
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Err($"[{nameof(InitializationService)}] Failed to switch channel to {channel}", ex);
            return false;
        }
    }

    public async Task<InitializationResult> FactoryResetAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _initialized = false;
            _initializationTask = null;
            _currentChannel = SCChannel.Live;
        }

        // Full reset: delete state (including custom overrides and theme selection) and generated keybindings.
        _stateService.DeleteStateFile();
        _installLocator.InvalidateCache();

        bool anyDeleted = _keybindingsJsonCache.TryDeleteAll();
        if (anyDeleted)
        {
            KeybindingsStateChanged?.Invoke();
        }

        return await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<InitializationResult> ForceRedetectionAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _initialized = false;
            _initializationTask = null;
        }

        _installLocator.InvalidateCache();

        // Force regeneration: re-detection should pick up new/changed actionmaps.xml overrides and in-game keybind updates.
        // Do not raise KeybindingsStateChanged here; it would make open Property Inspectors show a false "No installation" error.
        _keybindingsJsonCache.TryDeleteAll();

        InitializationResult result = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        // Refresh open Property Inspectors so updated bindings/categories are shown immediately.
        KeybindingsStateChanged?.Invoke();

        return result;
    }

    /// <summary>
    ///     Sets or clears a custom Data.p4k override for a specific channel.
    ///     Intended for Control Panel file pickers.
    /// </summary>
    public async Task<bool> ApplyCustomDataP4KOverrideAsync(
        SCChannel channel,
        string? dataP4KPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return string.IsNullOrWhiteSpace(dataP4KPath)
                ? await ClearCustomDataP4KOverrideAsync(channel, cancellationToken).ConfigureAwait(false)
                : await SetCustomDataP4KOverrideAsync(channel, dataP4KPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Err($"[{nameof(InitializationService)}] Failed to apply custom Data.p4k override", ex);
            return false;
        }
    }

    private async Task<bool> ClearCustomDataP4KOverrideAsync(SCChannel channel, CancellationToken cancellationToken)
    {
        bool wasActive = IsActiveChannel(channel);

        await _stateService.RemoveInstallationAsync(channel, cancellationToken).ConfigureAwait(false);

        DeleteKeybindingsJsonAndNotify(channel);

        // Prefer minimal re-detection to avoid disrupting other channels.
        await RedetectChannelInstallationAsync(channel, cancellationToken).ConfigureAwait(false);

        if (!wasActive || KeybindingsJsonExists())
        {
            return true;
        }

        // Avoid getting stuck on a channel without a keybindings JSON.
        bool healed = await TrySwitchToAvailableCachedChannelAsync(channel, cancellationToken).ConfigureAwait(false);
        if (healed)
        {
            return true;
        }

        _ = await ForceRedetectionAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<bool> SetCustomDataP4KOverrideAsync(
        SCChannel channel,
        string dataP4KPath,
        CancellationToken cancellationToken)
    {
        if (!InstallationState.TryCreateFromDataP4KPath(channel, dataP4KPath, out InstallationState? installation) ||
            installation == null)
        {
            return false;
        }

        PluginState? before = await _stateService.LoadStateAsync(cancellationToken).ConfigureAwait(false);
        await _stateService.UpdateInstallationAsync(channel, installation, cancellationToken).ConfigureAwait(false);

        DeleteKeybindingsJsonAndNotify(channel);

        bool generated = await GenerateKeybindingsForChannelAsync(installation, cancellationToken).ConfigureAwait(false);
        if (!generated)
        {
            return false;
        }

        // Preserve an explicit user preference; only pick a default when no state exists.
        if (before == null)
        {
            await _stateService.UpdateSelectedChannelAsync(channel, cancellationToken).ConfigureAwait(false);
        }

        if (IsActiveOrUninitialized(channel))
        {
            await ReloadChannelKeybindingsIfPossibleAsync(channel, cancellationToken).ConfigureAwait(false);
        }

        _installLocator.SetSelectedInstallation(installation.ToCandidate());
        return true;
    }

    private bool IsActiveChannel(SCChannel channel)
    {
        lock (_lock)
        {
            return _currentChannel == channel;
        }
    }

    private bool IsActiveOrUninitialized(SCChannel channel)
    {
        lock (_lock)
        {
            return !_initialized || _currentChannel == channel;
        }
    }

    private void DeleteKeybindingsJsonAndNotify(SCChannel channel)
    {
        bool deleted = _keybindingsJsonCache.TryDelete(channel);
        if (deleted)
        {
            KeybindingsStateChanged?.Invoke();
        }
    }

    private async Task ReloadChannelKeybindingsIfPossibleAsync(SCChannel channel, CancellationToken cancellationToken)
    {
        bool switched = await SwitchChannelAsync(channel, cancellationToken).ConfigureAwait(false);
        if (!switched)
        {
            return;
        }

        lock (_lock)
        {
            _initialized = true;
        }
    }

    private async Task RedetectChannelInstallationAsync(SCChannel channel, CancellationToken cancellationToken)
    {
        try
        {
            _installLocator.InvalidateCache();

            SCInstallCandidate? candidate = (await _installLocator.FindInstallationsAsync(cancellationToken)
                    .ConfigureAwait(false))
                .FirstOrDefault(c => c.Channel == channel);

            if (candidate == null)
            {
                return;
            }

            InstallationState installation = InstallationState.FromCandidate(candidate);
            await _stateService.UpdateInstallationAsync(channel, installation, cancellationToken).ConfigureAwait(false);

            // Best-effort regeneration for this channel only.
            bool generated = await GenerateKeybindingsForChannelAsync(installation, cancellationToken).ConfigureAwait(false);
            if (!generated)
            {
                return;
            }

            bool shouldReload;
            lock (_lock)
            {
                shouldReload = !_initialized || _currentChannel == channel;
            }

            if (shouldReload)
            {
                bool switched = await SwitchChannelAsync(channel, cancellationToken).ConfigureAwait(false);
                if (switched)
                {
                    lock (_lock)
                    {
                        _initialized = true;
                    }
                }
            }

            _installLocator.SetSelectedInstallation(candidate);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warn($"[{nameof(InitializationService)}] Failed to re-detect channel {channel}", ex);
        }
    }

    private async Task<bool> TrySwitchToAvailableCachedChannelAsync(
        SCChannel excludedChannel,
        CancellationToken cancellationToken)
    {
        PluginState? state = await _stateService.LoadStateAsync(cancellationToken).ConfigureAwait(false);
        if (state == null)
        {
            return false;
        }

        SCChannel[] priority = [SCChannel.Live, SCChannel.Hotfix, SCChannel.Ptu, SCChannel.Eptu];
        foreach (SCChannel channel in priority)
        {
            if (channel == excludedChannel)
            {
                continue;
            }

            InstallationState? installation = state.GetInstallation(channel);
            if (installation == null || !installation.Validate())
            {
                continue;
            }

            if (!_keybindingsJsonCache.Exists(channel))
            {
                continue;
            }

            bool switched = await SwitchChannelAsync(channel, cancellationToken).ConfigureAwait(false);
            if (!switched)
            {
                continue;
            }

            await _stateService.UpdateSelectedChannelAsync(channel, cancellationToken).ConfigureAwait(false);
            _installLocator.SetSelectedInstallation(installation.ToCandidate());

            lock (_lock)
            {
                _initialized = true;
            }

            return true;
        }

        return false;
    }

    private string GetKeybindingsJsonPath()
    {
        SCChannel channel;
        lock (_lock)
        {
            channel = _currentChannel;
        }

        return _pathProvider.GetKeybindingJsonPath(channel.ToString());
    }

    public bool KeybindingsJsonExists()
    {
        SCChannel channel;
        lock (_lock)
        {
            channel = _currentChannel;
        }

        return _keybindingsJsonCache.Exists(channel);
    }


    /// <summary>
    ///     Detects Star Citizen installations by scanning RSI logs AND checking cached root paths.
    ///     Always scans RSI logs to detect new installation locations (e.g., moved to different drive).
    ///     Always checks cached paths to detect new channels in existing locations.
    /// </summary>
    private async Task<List<SCInstallCandidate>> DetectInstallationsAsync(CachedInstallationsCleanupResult cleanupResult,
        CancellationToken cancellationToken)
    {
        List<SCInstallCandidate> rsiLogCandidates =
            (await _installLocator.FindInstallationsAsync(cancellationToken).ConfigureAwait(false)).ToList();

        (Dictionary<string, SCInstallCandidate> candidateMap, Dictionary<string, string> detectionSources) =
            InstallationDetectionMerger.Merge(
                rsiLogCandidates,
                cleanupResult.ValidCandidates,
                cleanupResult.NeedsFullDetection);

        List<SCInstallCandidate> finalCandidates = candidateMap.Values.ToList();
        if (finalCandidates.Count == 0)
        {
            Log.Warn($"[{nameof(InitializationService)}] No installations detected");
            return [];
        }

        await PersistCandidatesAsync(finalCandidates, cancellationToken).ConfigureAwait(false);

        List<string> rsiRootPaths = rsiLogCandidates
            .Select(c => c.RootPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p)
            .ToList();

        LogDetectionSummary(finalCandidates, detectionSources, rsiRootPaths);

        return finalCandidates;
    }


    /// <summary>
    ///     Logs a detailed summary of detected installations with their sources.
    /// </summary>
    private static void LogDetectionSummary(
        List<SCInstallCandidate> candidates,
        Dictionary<string, string> sources,
        List<string> rsiRootPaths)
    {
        if (rsiRootPaths.Count > 0)
        {
            // ReSharper disable once RedundantAssignment
            string pathsList = string.Join(", ", rsiRootPaths);
            Log.Debug(
                $"[{nameof(InitializationService)}] Scanned {rsiRootPaths.Count} root path(s) from RSI logs: {pathsList}");
        }

        if (candidates.Count == 0)
        {
            Log.Warn($"[{nameof(InitializationService)}] No installations detected");
            return;
        }

        Log.Info($"[{nameof(InitializationService)}] Auto-detected {candidates.Count} installation(s):");

        foreach (SCInstallCandidate candidate in candidates.OrderBy(c => c.Channel))
        {
            string source = sources.GetValueOrDefault(candidate.Channel.ToString(), "Unknown");
            Log.Info($"[{nameof(InitializationService)}] {candidate.Channel} at {candidate.RootPath} (Source: {source})");
        }
    }


    private SCInstallCandidate SelectPreferredOrFallbackChannel(
        List<SCInstallCandidate> candidates,
        SCChannel preferred,
        out bool usedFallback)
    {
        SCInstallCandidate selectedCandidate =
            ChannelSelector.SelectPreferredOrFallback(candidates, preferred, out usedFallback);

        lock (_lock)
        {
            _currentChannel = selectedCandidate.Channel;
        }

        _installLocator.SetSelectedInstallation(selectedCandidate);

        return selectedCandidate;
    }

    private async Task<bool> GenerateKeybindingsForChannelAsync(InstallationState installation,
        CancellationToken cancellationToken)
    {
        SCInstallCandidate candidate = installation.ToCandidate();
        string channelJsonPath = _pathProvider.GetKeybindingJsonPath(candidate.Channel.ToString());
        string? actionMapsPath = KeybindingProfilePathResolver.TryFindActionMapsXml(candidate.ChannelPath);
        KeybindingProcessResult processResult = await _keybindingProcessor.ProcessKeybindingsAsync(
                candidate,
                actionMapsPath,
                channelJsonPath,
                cancellationToken)
            .ConfigureAwait(false);

        if (!processResult.IsSuccess)
        {
            Log.Warn(
                $"[{nameof(InitializationService)}] Failed to generate keybindings for {candidate.Channel}: {processResult.ErrorMessage}");
            return false;
        }

        return true;
    }


    /// <summary>
    ///     Generates keybindings for all detected channels. Returns true if selected channel succeeded.
    /// </summary>
    private async Task<bool> GenerateKeybindingsForChannelsAsync(
        List<SCInstallCandidate> candidates,
        SCInstallCandidate selectedCandidate,
        CancellationToken cancellationToken)
    {
        foreach (SCInstallCandidate candidate in candidates)
        {
            string channelJsonPath = _pathProvider.GetKeybindingJsonPath(candidate.Channel.ToString());
            if (_keybindingsJsonCache.Exists(candidate.Channel) &&
                !_keybindingProcessor.NeedsRegeneration(channelJsonPath, candidate))
            {
                continue;
            }

            string? actionMapsPath = KeybindingProfilePathResolver.TryFindActionMapsXml(candidate.ChannelPath);
            KeybindingProcessResult processResult = await _keybindingProcessor.ProcessKeybindingsAsync(
                    candidate,
                    actionMapsPath,
                    channelJsonPath,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!processResult.IsSuccess)
            {
                Log.Warn(
                    $"[{nameof(InitializationService)}] Failed to generate keybindings for {candidate.Channel}: {processResult.ErrorMessage}");

                if (candidate.Channel == selectedCandidate.Channel)
                {
                    Log.Err(
                        $"[{nameof(InitializationService)}] Failed to process keybindings for selected channel {selectedCandidate.Channel}: {processResult.ErrorMessage}");
                    return false;
                }
            }
        }

        return true;
    }


    private async Task PersistCandidatesAsync(List<SCInstallCandidate> finalCandidates, CancellationToken cancellationToken)
    {
        foreach (SCInstallCandidate candidate in finalCandidates)
        {
            InstallationState installationState = InstallationState.FromCandidate(candidate);
            await _stateService.UpdateInstallationAsync(candidate.Channel, installationState, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<CachedInstallationsCleanupResult> ValidateCacheAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<SCInstallCandidate>? cachedCandidates =
            await _stateService.GetCachedCandidatesAsync(cancellationToken).ConfigureAwait(false);

        CachedInstallationsCleanupResult result =
            await _cachedInstallationsCleanupService.CleanupAsync(cachedCandidates, cancellationToken).ConfigureAwait(false);

        if (result.AnyKeybindingsDeleted)
        {
            KeybindingsStateChanged?.Invoke();
        }

        return result;
    }

    private async Task<List<SCInstallCandidate>> DetectCandidatesAsync(
        CachedInstallationsCleanupResult cleanupResult,
        CancellationToken cancellationToken)
    {
        try
        {
            return await DetectInstallationsAsync(cleanupResult, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Err($"[{nameof(InitializationService)}] Failed to detect installations", ex);
            return [];
        }
    }

    /// <summary>
    ///     Internal initialization logic - orchestrates the complete initialization process.
    ///     Refactored into smaller methods to reduce complexity.
    /// </summary>
    private async Task<InitializationResult> InitializeInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            PluginState? existingState = await _stateService.LoadStateAsync(cancellationToken).ConfigureAwait(false);
            SCChannel preferredChannel = existingState?.SelectedChannel ?? SCChannel.Live;

            CachedInstallationsCleanupResult cleanupResult = await ValidateCacheAsync(cancellationToken).ConfigureAwait(false);
            List<SCInstallCandidate> candidates = await DetectCandidatesAsync(cleanupResult, cancellationToken)
                .ConfigureAwait(false);
            if (candidates.Count == 0)
            {
                return InitializationResult.Failure("No installation detected. Set custom path.");
            }

            SCInstallCandidate selectedCandidate =
                SelectPreferredOrFallbackChannel(candidates, preferredChannel, out bool usedFallback);

            if (usedFallback)
            {
                if (existingState?.GetInstallation(preferredChannel) == null)
                {
                    await _stateService.UpdateSelectedChannelAsync(selectedCandidate.Channel, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    Log.Warn(
                        $"[{nameof(InitializationService)}] Preferred channel {preferredChannel} not available, using {selectedCandidate.Channel}");
                }
            }

            bool keybindingSuccess = await GenerateKeybindingsForChannelsAsync(candidates, selectedCandidate, cancellationToken)
                .ConfigureAwait(false);
            if (!keybindingSuccess)
            {
                return InitializationResult.Failure("Failed to generate keybindings. See logs.");
            }

            await _keybindingService.LoadKeybindingsAsync(GetKeybindingsJsonPath(), cancellationToken)
                .ConfigureAwait(false);

            lock (_lock)
            {
                _initialized = true;
            }

            // First run: persist the actually selected channel so subsequent startups don't warn about
            // the default (Live) preference when only another channel exists.
            if (existingState == null)
            {
                await _stateService.UpdateSelectedChannelAsync(selectedCandidate.Channel, cancellationToken)
                    .ConfigureAwait(false);
            }

            return InitializationResult.Success(_currentChannel, candidates.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Err($"[{nameof(InitializationService)}] Initialization failed", ex);
            lock (_lock)
            {
                _initialized = false;
            }

            return InitializationResult.Failure($"Initialization failed: {ex.Message}");
        }
    }
}

/// <summary>
///     Result of plugin initialization.
/// </summary>
public sealed class InitializationResult
{
    public bool IsSuccess { get; private init; }
    public string? ErrorMessage { get; private init; }
    public SCChannel SelectedChannel { get; private init; }
    public int DetectedInstallations { get; private init; }

    public static InitializationResult Success(SCChannel channel, int installCount) =>
        new() { IsSuccess = true, SelectedChannel = channel, DetectedInstallations = installCount };

    public static InitializationResult Failure(string errorMessage) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage };
}
