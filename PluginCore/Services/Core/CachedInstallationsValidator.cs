using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Core;

internal static class CachedInstallationsValidator
{
    internal static Result Validate(
        IReadOnlyList<SCInstallCandidate>? cachedCandidates,
        Func<string, bool> fileExists,
        Func<string, bool> dirExists)
    {
        if (cachedCandidates == null || cachedCandidates.Count == 0)
        {
            return new Result(
                [],
                [],
                true);
        }

        List<SCInstallCandidate> valid = [];
        List<SCInstallCandidate> invalid = [];

        foreach (SCInstallCandidate cached in cachedCandidates)
        {
            if (fileExists(cached.DataP4KPath) && dirExists(cached.ChannelPath))
            {
                valid.Add(cached);
            }
            else
            {
                invalid.Add(cached);
            }
        }

        return new Result(
            valid,
            invalid,
            valid.Count == 0);
    }

    internal sealed record Result(
        IReadOnlyList<SCInstallCandidate> ValidCandidates,
        IReadOnlyList<SCInstallCandidate> InvalidCandidates,
        bool NeedsFullDetection);
}
