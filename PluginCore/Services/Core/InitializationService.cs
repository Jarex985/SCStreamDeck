using BarRaider.SdTools;
using SCStreamDeck.Common;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Installation;
using SCStreamDeck.Services.Keybinding;

namespace SCStreamDeck.Services.Core;

/// <summary>
///     Handles plugin startup, installation detection, and channel management.
/// </summary>
public sealed class InitializationService : IInitializationService, IDisposable
{
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);
    private readonly IInstallLocatorService _installLocator;
    private readonly IKeybindingProcessorService _keybindingProcessor;
    private readonly IKeybindingService _keybindingService;
    private readonly object _lock = new();
    private readonly IPathProvider _pathProvider;
    private readonly IStateService _stateService;

    private SCChannel _currentChannel = SCChannel.Live;
    private Task<InitializationResult>? _initializationTask;
    private bool _initialized;

    public InitializationService(
        IKeybindingService keybindingService,
        IInstallLocatorService installLocator,
        IKeybindingProcessorService keybindingProcessor,
        IPathProvider pathProvider,
        IStateService stateService)
    {
        _keybindingService = keybindingService ?? throw new ArgumentNullException(nameof(keybindingService));
        _installLocator = installLocator ?? throw new ArgumentNullException(nameof(installLocator));
        _keybindingProcessor = keybindingProcessor ?? throw new ArgumentNullException(nameof(keybindingProcessor));
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
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

    public void Dispose() => _initSemaphore.Dispose();

    //public event EventHandler<InitializationResult>? InitializationCompleted;

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
            InitializationResult result = await _initializationTask.ConfigureAwait(false);
            _initializationTask = null;
            return result;
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
            lock (_lock)
            {
                _currentChannel = channel;
            }

            string jsonPath = GetKeybindingsJsonPath();
            bool success = await _keybindingService.LoadKeybindingsAsync(jsonPath, cancellationToken)
                .ConfigureAwait(false);

            if (!success)
            {
                return false;
            }

            Logger.Instance.LogMessage(TracingLevel.INFO,
                $"[{nameof(InitializationService)}] Switched to {channel}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[InitializationService] {ErrorMessages.ChannelSwitchFailed}: {ex.Message}");
            return false;
        }
    }


    public void InvalidateCache()
    {
        lock (_lock)
        {
            _initialized = false;
            _initializationTask = null;
        }

        _stateService.InvalidateState();
        _installLocator.InvalidateCache();
        Logger.Instance.LogMessage(TracingLevel.INFO,
            $"[{nameof(InitializationService)}] Cache invalidated");
    }

    public string GetKeybindingsJsonPath()
    {
        SCChannel channel;
        lock (_lock)
        {
            channel = _currentChannel;
        }

        return _pathProvider.GetKeybindingJsonPath(channel.ToString());
    }

    /// <summary>
    ///     Validates cached installations and performs cleanup for removed installations.
    /// </summary>
    private async Task<CacheValidationResult> ValidateAndCleanupCachedInstallationsAsync(
        IReadOnlyList<SCInstallCandidate>? cachedCandidates, CancellationToken cancellationToken)
    {
        if (cachedCandidates == null || cachedCandidates.Count == 0)
        {
            return new CacheValidationResult { ValidCandidates = new List<SCInstallCandidate>(), NeedsFullDetection = true };
        }

        List<SCInstallCandidate> validCandidates = new();
        foreach (SCInstallCandidate cached in cachedCandidates)
        {
            if (File.Exists(cached.DataP4KPath) && Directory.Exists(cached.ChannelPath))
            {
                validCandidates.Add(cached);
                continue;
            }

            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"[{nameof(InitializationService)}] {cached.Channel} installation no longer exists, cleaning up");

            string keybindingJson = _pathProvider.GetKeybindingJsonPath(cached.Channel.ToString());
            if (File.Exists(keybindingJson))
            {
                try
                {
                    File.Delete(keybindingJson);
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN,
                        $"[{nameof(InitializationService)}] {ErrorMessages.KeybindingsDeleteFailed}: {ex.Message}");
                }
            }

            await _stateService.RemoveInstallationAsync(cached.Channel, cancellationToken).ConfigureAwait(false);
        }

        return new CacheValidationResult { ValidCandidates = validCandidates, NeedsFullDetection = validCandidates.Count == 0 };
    }


    /// <summary>
    ///     Detects Star Citizen installations by scanning RSI logs AND checking cached root paths.
    ///     Always scans RSI logs to detect new installation locations (e.g., moved to different drive).
    ///     Always checks cached paths to detect new channels in existing locations.
    /// </summary>
    private async Task<List<SCInstallCandidate>> DetectInstallationsAsync(CacheValidationResult validationResult,
        CancellationToken cancellationToken)
    {
        Dictionary<string, SCInstallCandidate> candidateMap = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> detectionSources = new();

        List<SCInstallCandidate> rsiLogCandidates =
            (await _installLocator.FindInstallationsAsync(cancellationToken).ConfigureAwait(false)).ToList();

        foreach (SCInstallCandidate candidate in rsiLogCandidates)
        {
            string key = BuildCandidateKey(candidate);
            candidateMap[key] = candidate;
            detectionSources[candidate.Channel.ToString()] = candidate.Source == InstallSource.UserProvided
                ? "User Config"
                : "RSI Logs";
        }

        if (!validationResult.NeedsFullDetection && validationResult.ValidCandidates.Count > 0)
        {
            MergeCachedCandidates(validationResult, candidateMap, detectionSources);
        }

        List<SCInstallCandidate> finalCandidates = candidateMap.Values.ToList();
        if (finalCandidates.Count == 0)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[InitializationService] {ErrorMessages.InstallDetectionFailed}");
            throw new InvalidOperationException(ErrorMessages.InstallDetectionFailed);
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
            string pathsList = string.Join(", ", rsiRootPaths);
            Logger.Instance.LogMessage(TracingLevel.INFO,
                $"[InitializationService] Scanned {rsiRootPaths.Count} root path(s) from RSI logs: {pathsList}");
        }

        if (candidates.Count == 0)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"[{nameof(InitializationService)}] No installations detected");
            return;
        }

        Logger.Instance.LogMessage(TracingLevel.INFO,
            $"[{nameof(InitializationService)}] Detected {candidates.Count} installation(s):");


        foreach (SCInstallCandidate candidate in candidates.OrderBy(c => c.Channel))
        {
            string source = sources.TryGetValue(candidate.Channel.ToString(), out string? value) ? value : "Unknown";
            Logger.Instance.LogMessage(TracingLevel.INFO,
                $"[{nameof(InitializationService)}] {candidate.Channel} at {candidate.RootPath} (Source: {source})");
        }
    }


    /// <summary>
    ///     Selects the optimal channel from available installations with priority: LIVE > HOTFIX > PTU > EPTU.
    ///     Only selects channels that actually exist in the candidates list.
    /// </summary>
    private SCInstallCandidate SelectOptimalChannel(List<SCInstallCandidate> candidates)
    {
        SCInstallCandidate selectedCandidate = candidates.FirstOrDefault(c => c.Channel == SCChannel.Live)
                                               ?? candidates.FirstOrDefault(c => c.Channel == SCChannel.Hotfix)
                                               ?? candidates.FirstOrDefault(c => c.Channel == SCChannel.Ptu)
                                               ?? candidates.FirstOrDefault(c => c.Channel == SCChannel.Eptu)
                                               ?? candidates[0];

        lock (_lock)
        {
            _currentChannel = selectedCandidate.Channel;
        }

        _installLocator.SetSelectedInstallation(selectedCandidate);
        Logger.Instance.LogMessage(TracingLevel.INFO,
            $"[InitializationService] Selected {selectedCandidate.Channel} at '{selectedCandidate.DataP4KPath}'");

        return selectedCandidate;
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
            if (File.Exists(channelJsonPath) && !_keybindingProcessor.NeedsRegeneration(channelJsonPath, candidate))
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
                Logger.Instance.LogMessage(TracingLevel.WARN,
                    $"[InitializationService] Failed to generate keybindings for {candidate.Channel}: {processResult.ErrorMessage}");

                if (candidate.Channel == selectedCandidate.Channel)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR,
                        $"[{nameof(InitializationService)}] {ErrorMessages.KeybindingProcessingFailed} {processResult.ErrorMessage}");
                    return false;
                }
            }
        }

        return true;
    }


    private static string BuildCandidateKey(SCInstallCandidate candidate) => $"{candidate.RootPath}|{candidate.Channel}";

    private static void MergeCachedCandidates(
        CacheValidationResult validationResult,
        Dictionary<string, SCInstallCandidate> candidateMap,
        Dictionary<string, string> detectionSources)
    {
        HashSet<string> cachedRootPaths = validationResult.ValidCandidates
            .Select(c => c.RootPath)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        HashSet<SCChannel> cachedChannels = validationResult.ValidCandidates
            .Select(c => c.Channel)
            .ToHashSet();

        List<SCInstallCandidate> cachedPathCandidates = new();
        foreach (string rootPath in cachedRootPaths)
        {
            InstallationCandidateEnumerator.AddCandidatesFromRoot(cachedPathCandidates, rootPath);
        }

        foreach (SCInstallCandidate candidate in cachedPathCandidates)
        {
            string key = BuildCandidateKey(candidate);

            if (!candidateMap.ContainsKey(key))
            {
                candidateMap[key] = candidate;
                detectionSources[candidate.Channel.ToString()] = "Cache (New Channel)";
            }
            else if (cachedChannels.Contains(candidate.Channel))
            {
                detectionSources[candidate.Channel.ToString()] = "Cache";
            }
        }
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

    private async Task<CacheValidationResult> ValidateCacheAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<SCInstallCandidate>? cachedCandidates =
            await _stateService.GetCachedCandidatesAsync(cancellationToken).ConfigureAwait(false);
        return await ValidateAndCleanupCachedInstallationsAsync(cachedCandidates, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<SCInstallCandidate>> DetectCandidatesAsync(
        CacheValidationResult validationResult,
        CancellationToken cancellationToken)
    {
        try
        {
            return await DetectInstallationsAsync(validationResult, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex.Message == ErrorMessages.InstallDetectionFailed)
        {
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
            CacheValidationResult validationResult = await ValidateCacheAsync(cancellationToken).ConfigureAwait(false);
            List<SCInstallCandidate> candidates = await DetectCandidatesAsync(validationResult, cancellationToken)
                .ConfigureAwait(false);
            if (candidates.Count == 0)
            {
                return InitializationResult.Failure(ErrorMessages.InstallDetectionFailed);
            }

            SCInstallCandidate selectedCandidate = SelectOptimalChannel(candidates);

            bool keybindingSuccess = await GenerateKeybindingsForChannelsAsync(candidates, selectedCandidate, cancellationToken)
                .ConfigureAwait(false);
            if (!keybindingSuccess)
            {
                return InitializationResult.Failure(ErrorMessages.KeybindingProcessingFailed);
            }

            await _keybindingService.LoadKeybindingsAsync(GetKeybindingsJsonPath(), cancellationToken)
                .ConfigureAwait(false);

            lock (_lock)
            {
                _initialized = true;
            }

            return InitializationResult.Success(_currentChannel, candidates.Count);
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[InitializationService] Initialization failed: {ex.Message}");
            lock (_lock)
            {
                _initialized = false;
            }

            return InitializationResult.Failure($"Initialization failed: {ex.Message}");
        }
    }


    private sealed class CacheValidationResult
    {
        public List<SCInstallCandidate> ValidCandidates { get; init; } = [];
        public bool NeedsFullDetection { get; init; }
    }
}
