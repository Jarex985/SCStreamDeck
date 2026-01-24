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
                Array.Empty<SCInstallCandidate>(),
                Array.Empty<SCInstallCandidate>(),
                true);
        }

        List<SCInstallCandidate> valid = new();
        List<SCInstallCandidate> invalid = new();

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
