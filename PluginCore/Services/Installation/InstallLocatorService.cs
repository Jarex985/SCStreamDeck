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

        candidates = DeduplicateCandidates(candidates);

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

        Logger.Instance.LogMessage(TracingLevel.INFO,
            $"[{nameof(InstallLocatorService)}] Cache invalidated");
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
                    $"[{nameof(InstallLocatorService)}] Channel changed from {previousChannel} to {installation.Channel}");
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

        AddCustomPathCandidates(candidates);

        HashSet<string> rootPaths = await CollectRootPathsAsync(cancellationToken).ConfigureAwait(false);
        if (rootPaths.Count == 0)
        {
            return candidates;
        }

        foreach (string rootPath in rootPaths)
        {
            InstallationCandidateEnumerator.AddCandidatesFromRoot(candidates, rootPath);
        }

        return candidates;
    }

    private static void AddCustomPathCandidates(List<SCInstallCandidate> candidates)
    {
        CustomPathsConfig? customPaths = CustomPathsConfig.LoadFromIni(AppDomain.CurrentDomain.BaseDirectory);
        if (customPaths is null)
        {
            return;
        }

        Logger.Instance.LogMessage(TracingLevel.INFO,
            $"[{nameof(InstallLocatorService)}] Custom paths configuration found");


        foreach (SCChannel channel in customPaths.GetConfiguredChannels())
        {
            string? customPath = customPaths.GetPath(channel);
            if (!TryCreateCustomCandidate(channel, customPath, out SCInstallCandidate? candidate))
            {
                continue;
            }

            if (candidate is not null)
            {
                candidates.Add(candidate);
            }
        }
    }

    private static bool TryCreateCustomCandidate(SCChannel channel, string? customPath, out SCInstallCandidate? candidate)
    {
        candidate = null;
        if (string.IsNullOrWhiteSpace(customPath))
        {
            return false;
        }

        if (!File.Exists(customPath))
        {
            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"[{nameof(InstallLocatorService)}] Custom path for {channel} is invalid (file not found): {customPath}");

            return false;
        }

        if (!customPath.EndsWith(SCConstants.Files.DataP4KFileName, StringComparison.OrdinalIgnoreCase))
        {
            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"[{nameof(InstallLocatorService)}] Custom path for {channel} does not point to Data.p4k: {customPath}");

            return false;
        }

        string dataP4KPath = NormalizeOrTrim(customPath);
        string channelPath = NormalizeOrTrim(Path.GetDirectoryName(dataP4KPath));
        string rootPath = NormalizeOrTrim(Path.GetDirectoryName(channelPath));

        if (string.IsNullOrEmpty(channelPath) || string.IsNullOrEmpty(rootPath))
        {
            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"[{nameof(InstallLocatorService)}] Custom path for {channel} has invalid directory structure: {customPath}");

            return false;
        }

        Logger.Instance.LogMessage(TracingLevel.INFO,
            $"[{nameof(InstallLocatorService)}] Custom path configured for {channel}: {dataP4KPath}");


        candidate = new SCInstallCandidate(
            rootPath,
            channel,
            channelPath,
            dataP4KPath,
            InstallSource.UserProvided);
        return true;
    }

    private async Task<HashSet<string>> CollectRootPathsAsync(CancellationToken cancellationToken)
    {
        HashSet<string> allRootPaths = new(StringComparer.OrdinalIgnoreCase);

        foreach (string logFile in _configReader.FindLogFiles())
        {
            HashSet<string> paths = await RsiLauncherConfigReader.ExtractPathsFromLogAsync(logFile, cancellationToken)
                .ConfigureAwait(false);

            foreach (string path in paths)
            {
                string cleanPath = path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!string.IsNullOrWhiteSpace(cleanPath))
                {
                    allRootPaths.Add(cleanPath);
                }
            }
        }

        if (allRootPaths.Count == 0)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"[{nameof(InstallLocatorService)}] No root paths found in launcher logs");

            return allRootPaths;
        }

        List<string> sortedPaths = allRootPaths.OrderBy(p => p).ToList();

#if DEBUG
        if (sortedPaths.Count == 1)
        {
            Logger.Instance.LogMessage(TracingLevel.DEBUG,
                $"[{nameof(InstallLocatorService)}] Found 1 unique root path: {sortedPaths[0]}");
        }
        else
        {
            Logger.Instance.LogMessage(TracingLevel.DEBUG,
                $"[{nameof(InstallLocatorService)}] Found {sortedPaths.Count} unique root paths:");


            foreach (string path in sortedPaths)
            {
                Logger.Instance.LogMessage(TracingLevel.DEBUG, $"  - {path}");
            }
        }
#endif

        return allRootPaths;
    }

    private static List<SCInstallCandidate> DeduplicateCandidates(List<SCInstallCandidate> candidates)
    {
        int beforeDedup = candidates.Count;

        List<SCInstallCandidate> distinct = candidates
            .DistinctBy(c => NormalizeOrTrim(c.DataP4KPath))
            .OrderBy(c => c.Channel)
            .ToList();

        if (beforeDedup > distinct.Count)
        {
            Logger.Instance.LogMessage(TracingLevel.DEBUG,
                $"[{nameof(InstallLocatorService)}] Removed {beforeDedup - distinct.Count} duplicate(s)");
        }

        return distinct;
    }

    private static string NormalizeOrTrim(string? path)
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
