using BarRaider.SdTools;
using SCStreamDeck.Common;
using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Installation;

/// <summary>
///     Modern installation locator service with async operations and caching.
///     Detects Star Citizen installations from RSI Launcher config files and logs.
/// </summary>
public sealed class InstallLocatorService : IInstallLocatorService
{
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
    private readonly RsiLauncherConfigReader _configReader = new();
    private readonly object _lock = new();

    private List<SCInstallCandidate>? _cachedInstallations;
    private DateTime? _cacheTimestamp;
    private SCInstallCandidate? _selectedInstallation;

    // Region Public API

    #region Public API

    public async Task<IReadOnlyList<SCInstallCandidate>> FindInstallationsAsync(
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        lock (_lock)
        {
            if (_cachedInstallations != null && _cacheTimestamp.HasValue)
            {
                TimeSpan age = DateTime.UtcNow - _cacheTimestamp.Value;
                if (age < _cacheExpiration)
                {
                    return _cachedInstallations;
                }
            }
        }

        // Find installations async - stop early if we find valid installations
        List<SCInstallCandidate> candidates = await FindInstallationsFromSourcesAsync(cancellationToken).ConfigureAwait(false);

        // TODO: Never happened in any tests, eventually remove
        // De-duplicate based on Data.p4k path
        int beforeDedup = candidates.Count;
        candidates = candidates
            .DistinctBy(c => NormalizePath(c.DataP4KPath))
            .OrderBy(c => c.Channel)
            .ToList();

        if (beforeDedup > candidates.Count)
        {
            Logger.Instance.LogMessage(TracingLevel.DEBUG,
                $"[InstallLocator] Removed {beforeDedup - candidates.Count} duplicate(s)");
        }

        // Update cache
        lock (_lock)
        {
            _cachedInstallations = candidates;
            _cacheTimestamp = DateTime.UtcNow;
        }

        return candidates;
    }

    public void InvalidateCache()
    {
        lock (_lock)
        {
            _cachedInstallations = null;
            _cacheTimestamp = null;
        }

        Logger.Instance.LogMessage(TracingLevel.INFO, "[InstallLocator] Cache invalidated");
    }

    public IReadOnlyList<SCInstallCandidate>? GetCachedInstallations()
    {
        lock (_lock)
        {
            return _cachedInstallations;
        }
    }

    public SCInstallCandidate? GetSelectedInstallation()
    {
        lock (_lock)
        {
            return _selectedInstallation;
        }
    }

    public void SetSelectedInstallation(SCInstallCandidate installation)
    {
        ArgumentNullException.ThrowIfNull(installation);

        lock (_lock)
        {
            SCChannel? previousChannel = _selectedInstallation?.Channel;
            _selectedInstallation = installation;

            if (previousChannel != installation.Channel && previousChannel != null)
            {
                Logger.Instance.LogMessage(TracingLevel.INFO,
                    $"[InstallLocator] Channel changed from {previousChannel} to {installation.Channel}");
            }
        }
    }

    #endregion

    // End Region Public API

    // Region Private Methods

    #region Private Methods

    private async Task<List<SCInstallCandidate>> FindInstallationsFromSourcesAsync(CancellationToken cancellationToken)
    {
        List<SCInstallCandidate> candidates = new();

        // Step 1: Load custom paths from INI file (takes priority)
        string pluginDirectory = AppDomain.CurrentDomain.BaseDirectory;
        CustomPathsConfig? customPaths = CustomPathsConfig.LoadFromIni(pluginDirectory);

        if (customPaths is not null)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "[InstallLocator] Custom paths configuration found");

            foreach (SCChannel channel in customPaths.GetConfiguredChannels())
            {
                string? customPath = customPaths.GetPath(channel);
                if (customPath is null)
                {
                    continue;
                }

                // Validate the custom path points to a valid Data.p4k
                if (!File.Exists(customPath))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN,
                        $"[InstallLocator] Custom path for {channel} is invalid (file not found): {customPath}");
                    continue;
                }

                if (!customPath.EndsWith(SCConstants.Files.DataP4KFileName, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN,
                        $"[InstallLocator] Custom path for {channel} does not point to Data.p4k: {customPath}");
                    continue;
                }

                // Extract paths from the Data.p4k location
                string dataP4KPath = NormalizePath(customPath);
                string channelPath = NormalizePath(Path.GetDirectoryName(dataP4KPath) ?? string.Empty);
                string rootPath = NormalizePath(Path.GetDirectoryName(channelPath) ?? string.Empty);

                if (string.IsNullOrEmpty(channelPath) || string.IsNullOrEmpty(rootPath))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN,
                        $"[InstallLocator] Custom path for {channel} has invalid directory structure: {customPath}");
                    continue;
                }

                Logger.Instance.LogMessage(TracingLevel.INFO,
                    $"[InstallLocator] Custom path configured for {channel}: {dataP4KPath}");

                // Create candidate with UserProvided source
                candidates.Add(new SCInstallCandidate(
                    rootPath,
                    channel,
                    channelPath,
                    dataP4KPath,
                    InstallSource.UserProvided
                ));
            }
        }

        // Step 2: Continue with auto-detection from RSI Launcher logs
        HashSet<string> allRootPaths = new(StringComparer.OrdinalIgnoreCase);

        // RSI Launcher logs - collect ALL unique root paths from all log files
        // This handles cases where user has installations on different drives (C:\, D:\, etc.)
        foreach (string logFile in _configReader.FindLogFiles())
        {
            HashSet<string> paths = await RsiLauncherConfigReader.ExtractPathsFromLogAsync(logFile, cancellationToken)
                .ConfigureAwait(false);
            foreach (string path in paths)
            {
                // Additional cleanup: Trim and remove trailing separators
                string cleanPath = path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                allRootPaths.Add(cleanPath);
            }
        }

        if (allRootPaths.Count == 0)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, "[InstallLocator] No root paths found in launcher logs");
            
            // If we have custom paths, that's okay - return them
            if (candidates.Count > 0)
            {
                return candidates;
            }
            
            return new List<SCInstallCandidate>();
        }

        // Log with better formatting
        List<string> sortedPaths = allRootPaths.OrderBy(p => p).ToList();

#if DEBUG
        if (sortedPaths.Count == 1)
        {
            Logger.Instance.LogMessage(TracingLevel.DEBUG,
                $"[InstallLocator] Found 1 unique root path: {sortedPaths[0]}");
        }
        else
        {
            Logger.Instance.LogMessage(TracingLevel.DEBUG,
                $"[InstallLocator] Found {sortedPaths.Count} unique root paths:");

            foreach (string path in sortedPaths)
            {
                Logger.Instance.LogMessage(TracingLevel.DEBUG, $"  - {path}");
            }
        }
#endif

        // Now enumerate candidates from all unique root paths
        foreach (string rootPath in allRootPaths)
        {
            InstallationCandidateEnumerator.AddCandidatesFromRoot(candidates, rootPath);
        }

        return candidates;
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return SecurePathValidator.TryNormalizePath(path, out string normalized) ? normalized : path.Trim();
    }

    #endregion

    // End Region Private Methods
}
