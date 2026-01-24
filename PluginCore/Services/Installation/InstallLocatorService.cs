using SCStreamDeck.Common;
using SCStreamDeck.Logging;
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

        // Find installations asynchronously from all available sources.
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
            _selectedInstallation = installation;
        }
    }

    #endregion

    #region Private Methods

    private async Task<List<SCInstallCandidate>> FindInstallationsFromSourcesAsync(CancellationToken cancellationToken)
    {
        List<SCInstallCandidate> candidates = new();

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
            Log.Warn($"[{nameof(InstallLocatorService)}] No root paths found in launcher logs");

            return allRootPaths;
        }

        List<string> sortedPaths = allRootPaths.OrderBy(p => p).ToList();

        if (sortedPaths.Count == 1)
        {
            Log.Debug($"[{nameof(InstallLocatorService)}] Found 1 unique root path: {sortedPaths[0]}");
        }
        else
        {
            Log.Debug($"[{nameof(InstallLocatorService)}] Found {sortedPaths.Count} unique root paths:");


            foreach (string path in sortedPaths)
            {
                Log.Debug($"  - {path}");
            }
        }

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
            Log.Debug($"[{nameof(InstallLocatorService)}] Removed {beforeDedup - distinct.Count} duplicate(s)");
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
}
