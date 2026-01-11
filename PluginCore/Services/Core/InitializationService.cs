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
        // Fast path: Check if already initialized
        if (_initialized)
        {
            return InitializationResult.Success(_currentChannel, 0);
        }

        // Acquire semaphore to prevent concurrent initialization
        await _initSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-checked locking: Check again after acquiring semaphore
            if (_initialized)
            {
                return InitializationResult.Success(_currentChannel, 0);
            }

            // If another task is already initializing, await it
            if (_initializationTask != null)
            {
                return await _initializationTask.ConfigureAwait(false);
            }

            // Start new initialization task
            _initializationTask = InitializeInternalAsync(cancellationToken);
            InitializationResult result = await _initializationTask.ConfigureAwait(false);

            // Clear task reference
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

            Logger.Instance.LogMessage(TracingLevel.INFO, $"[InitializationService] Switched to {channel}");
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
        Logger.Instance.LogMessage(TracingLevel.INFO, "[InitializationService] Cache invalidated");
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
        List<SCInstallCandidate> validCandidates = new();

        if (cachedCandidates == null || cachedCandidates.Count == 0)
        {
            return new CacheValidationResult { ValidCandidates = validCandidates, NeedsFullDetection = true };
        }


        foreach (SCInstallCandidate cached in cachedCandidates)
        {
            if (File.Exists(cached.DataP4KPath) && Directory.Exists(cached.ChannelPath))
            {
                validCandidates.Add(cached);
            }
            else
            {
                Logger.Instance.LogMessage(TracingLevel.WARN,
                    $"[InitializationService] {cached.Channel} installation no longer exists, cleaning up");

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
                            $"[InitializationService] {ErrorMessages.KeybindingsDeleteFailed}: {ex.Message}");
                    }
                }

                await _stateService.RemoveInstallationAsync(cached.Channel, cancellationToken).ConfigureAwait(false);
            }
        }

        return new CacheValidationResult { ValidCandidates = validCandidates, NeedsFullDetection = validCandidates.Count == 0 };
    }

    /// <summary>
    ///     Detects Star Citizen installations - either full detection or delta-detection for new installations.
    /// </summary>
    private async Task<List<SCInstallCandidate>> DetectInstallationsAsync(CacheValidationResult validationResult,
        CancellationToken cancellationToken)
    {
        List<SCInstallCandidate> candidates;

        if (validationResult.NeedsFullDetection)
        {
            // Full detection (no valid cache or all installations removed)
            candidates =
                (await _installLocator.FindInstallationsAsync(cancellationToken).ConfigureAwait(false)).ToList();

            if (candidates.Count == 0)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[InitializationService] {ErrorMessages.InstallDetectionFailed}");
                throw new InvalidOperationException(ErrorMessages.InstallDetectionFailed);
            }

            // Save all newly detected installations to state
            foreach (SCInstallCandidate candidate in candidates)
            {
                InstallationState installationState = InstallationState.FromCandidate(candidate);
                await _stateService.UpdateInstallationAsync(candidate.Channel, installationState, cancellationToken)
                    .ConfigureAwait(false);
            }

            Logger.Instance.LogMessage(TracingLevel.INFO,
                $"[{nameof(InitializationService)}] Detected {candidates.Count} installation(s): {string.Join(", ", candidates.Select(c => c.Channel))}");
        }
        else
        {
            // Cache is valid, but check for new installations
            List<SCInstallCandidate> freshCandidates =
                (await _installLocator.FindInstallationsAsync(cancellationToken).ConfigureAwait(false)).ToList();

            // Find new channels that weren't in cache
            HashSet<SCChannel> cachedChannels = validationResult.ValidCandidates.Select(c => c.Channel).ToHashSet();
            List<SCInstallCandidate> newInstallations = freshCandidates.Where(c => !cachedChannels.Contains(c.Channel)).ToList();

            if (newInstallations.Count > 0)
            {
                Logger.Instance.LogMessage(TracingLevel.INFO,
                    $"[InitializationService] Found {newInstallations.Count} new installation(s): " +
                    $"{string.Join(", ", newInstallations.Select(c => c.Channel))}");

                // Add new installations to state
                foreach (SCInstallCandidate newCandidate in newInstallations)
                {
                    InstallationState installationState = InstallationState.FromCandidate(newCandidate);
                    await _stateService
                        .UpdateInstallationAsync(newCandidate.Channel, installationState, cancellationToken)
                        .ConfigureAwait(false);
                }

                // Combine cached and new installations
                candidates = validationResult.ValidCandidates.Concat(newInstallations).ToList();
            }
            else
            {
                // No new installations, use cache
                candidates = validationResult.ValidCandidates;
            }
        }

        return candidates;
    }

    /// <summary>
    ///     Selects the optimal channel from available installations with priority: LIVE > HOTFIX > PTU > EPTU.
    ///     Only selects channels that actually exist in the candidates list.
    /// </summary>
    private SCInstallCandidate SelectOptimalChannel(List<SCInstallCandidate> candidates)
    {
        // Prefer LIVE, then HOTFIX, then PTU, then EPTU - but only if they actually exist
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
    private async Task<bool> GenerateKeybindingsForChannelsAsync(List<SCInstallCandidate> candidates,
        SCInstallCandidate selectedCandidate, CancellationToken cancellationToken)
    {
        foreach (SCInstallCandidate candidate in candidates)
        {
            string channelJsonPath = _pathProvider.GetKeybindingJsonPath(candidate.Channel.ToString());

            if (!File.Exists(channelJsonPath) || _keybindingProcessor.NeedsRegeneration(channelJsonPath, candidate))
            {
                string? actionMapsPath = KeybindingProfilePathResolver.TryFindActionMapsXml(candidate.ChannelPath);

                KeybindingProcessResult processResult = await _keybindingProcessor.ProcessKeybindingsAsync(
                    candidate,
                    actionMapsPath,
                    channelJsonPath,
                    cancellationToken).ConfigureAwait(false);

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
        }

        return true;
    }

    /// <summary>
    ///     Internal initialization logic - orchestrates the complete initialization process.
    ///     Refactored into smaller methods to reduce complexity.
    /// </summary>
    private async Task<InitializationResult> InitializeInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Step 1: Validate and cleanup cached installations
            IReadOnlyList<SCInstallCandidate>? cachedCandidates =
                await _stateService.GetCachedCandidatesAsync(cancellationToken).ConfigureAwait(false);
            CacheValidationResult validationResult =
                await ValidateAndCleanupCachedInstallationsAsync(cachedCandidates, cancellationToken)
                    .ConfigureAwait(false);

            // Step 2: Detect installations (full or delta)
            List<SCInstallCandidate> candidates;
            try
            {
                candidates = await DetectInstallationsAsync(validationResult, cancellationToken).ConfigureAwait(false);
            }

            catch (InvalidOperationException ex) when (ex.Message == ErrorMessages.InstallDetectionFailed)
            {
                return InitializationResult.Failure(ErrorMessages.InstallDetectionFailed);
            }

            // Step 3: Select optimal channel
            SCInstallCandidate selectedCandidate = SelectOptimalChannel(candidates);

            // Step 4: Generate keybindings for all channels
            bool keybindingSuccess =
                await GenerateKeybindingsForChannelsAsync(candidates, selectedCandidate, cancellationToken)
                    .ConfigureAwait(false);
            if (!keybindingSuccess)
            {
                return InitializationResult.Failure($"{ErrorMessages.KeybindingProcessingFailed}");
            }

            // Step 5: Load keybindings for selected channel
            await _keybindingService.LoadKeybindingsAsync(GetKeybindingsJsonPath(), cancellationToken)
                .ConfigureAwait(false);

            lock (_lock)
            {
                _initialized = true;
            }
            InitializationResult result = InitializationResult.Success(_currentChannel, candidates.Count);

            return result;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[InitializationService] {ErrorMessages.InitializationFailed}: {ex.Message}");
            lock (_lock)
            {
                _initialized = false;
            }

            InitializationResult result = InitializationResult.Failure($"{ErrorMessages.InitializationFailed}: {ex.Message}");


            return result;
        }
    }

    private sealed class CacheValidationResult
    {
        public List<SCInstallCandidate> ValidCandidates { get; init; } = [];
        public bool NeedsFullDetection { get; init; }
    }
}
