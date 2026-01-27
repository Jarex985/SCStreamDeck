using SCStreamDeck.Common;
using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Core;

internal static class InstallationDetectionMerger
{
    private static string BuildCandidateKey(SCInstallCandidate candidate) => $"{candidate.RootPath}|{candidate.Channel}";

    public static (Dictionary<string, SCInstallCandidate> CandidateMap, Dictionary<string, string> DetectionSources) Merge(
        IReadOnlyList<SCInstallCandidate> rsiLogCandidates,
        IReadOnlyList<SCInstallCandidate> validCachedCandidates,
        bool needsFullDetection)
    {
        ArgumentNullException.ThrowIfNull(rsiLogCandidates);
        ArgumentNullException.ThrowIfNull(validCachedCandidates);

        Dictionary<string, SCInstallCandidate> candidateMap = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> detectionSources = [];

        foreach (SCInstallCandidate candidate in rsiLogCandidates)
        {
            string key = BuildCandidateKey(candidate);
            candidateMap[key] = candidate;
            detectionSources[candidate.Channel.ToString()] = "RSI Logs";
        }

        // Always merge cached candidates directly (preserves custom overrides).
        foreach (SCInstallCandidate cached in validCachedCandidates)
        {
            string key = BuildCandidateKey(cached);

            if (cached.Source == InstallSource.UserProvided)
            {
                // Custom overrides always win over any auto-detected candidate.
                candidateMap[key] = cached;
                detectionSources[cached.Channel.ToString()] = "Custom Override";
                continue;
            }

            if (candidateMap.TryAdd(key, cached))
            {
                detectionSources[cached.Channel.ToString()] = "Cache";
            }
        }

        if (!needsFullDetection && validCachedCandidates.Count > 0)
        {
            MergeNewChannelsFromCachedRoots(validCachedCandidates, candidateMap, detectionSources);
        }

        return (candidateMap, detectionSources);
    }

    private static void MergeNewChannelsFromCachedRoots(
        IReadOnlyList<SCInstallCandidate> validCachedCandidates,
        Dictionary<string, SCInstallCandidate> candidateMap,
        Dictionary<string, string> detectionSources)
    {
        HashSet<string> cachedRootPaths = validCachedCandidates
            .Select(c => c.RootPath)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        HashSet<SCChannel> cachedChannels = validCachedCandidates
            .Select(c => c.Channel)
            .ToHashSet();

        List<SCInstallCandidate> cachedPathCandidates = [];
        foreach (string rootPath in cachedRootPaths)
        {
            InstallationCandidateEnumerator.AddCandidatesFromRoot(cachedPathCandidates, rootPath);
        }

        foreach (SCInstallCandidate candidate in cachedPathCandidates)
        {
            string key = BuildCandidateKey(candidate);

            if (candidateMap.TryAdd(key, candidate))
            {
                detectionSources[candidate.Channel.ToString()] = "Cache (New Channel)";
            }
            else if (cachedChannels.Contains(candidate.Channel))
            {
                detectionSources[candidate.Channel.ToString()] = "Cache";
            }
        }
    }
}
