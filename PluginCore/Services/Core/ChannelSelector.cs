using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Core;

internal static class ChannelSelector
{
    internal static SCInstallCandidate SelectPreferredOrFallback(
        IReadOnlyList<SCInstallCandidate> candidates,
        SCChannel preferred,
        out bool usedFallback)
    {
        if (candidates == null || candidates.Count == 0)
        {
            throw new ArgumentException("Candidates cannot be empty", nameof(candidates));
        }

        SCInstallCandidate? preferredCandidate = candidates.FirstOrDefault(c => c.Channel == preferred);
        if (preferredCandidate != null)
        {
            usedFallback = false;
            return preferredCandidate;
        }

        SCChannel[] priority = [SCChannel.Live, SCChannel.Hotfix, SCChannel.Ptu, SCChannel.Eptu];
        foreach (SCChannel channel in priority)
        {
            SCInstallCandidate? candidate = candidates.FirstOrDefault(c => c.Channel == channel);
            if (candidate != null)
            {
                usedFallback = true;
                return candidate;
            }
        }

        usedFallback = true;
        return candidates[0];
    }
}
