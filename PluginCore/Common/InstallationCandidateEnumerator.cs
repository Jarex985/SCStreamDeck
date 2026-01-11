using BarRaider.SdTools;
using SCStreamDeck.SCCore.Models;

namespace SCStreamDeck.SCCore.Common;

/// <summary>
///     Enumerates Star Citizen installation candidates from filesystem paths.
/// </summary>
internal static class InstallationCandidateEnumerator
{
    /// <summary>
    ///     Adds all valid installation candidates from a root path to the provided list.
    /// </summary>
    /// <param name="list">The list to add candidates to.</param>
    /// <param name="root">The root path to search from.</param>
    public static void AddCandidatesFromRoot(List<SCInstallCandidate> list, string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        try
        {
            root = NormalizePath(root);
            if (!Directory.Exists(root))
            {
                return;
            }

            list.AddRange(EnumerateCandidates(root));
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[CandidateEnumerator] Error processing root '{root}': {ex.Message}");
        }
    }

    /// <summary>
    ///     Enumerates all valid installation candidates from a root path.
    /// </summary>
    private static IEnumerable<SCInstallCandidate> EnumerateCandidates(string root)
    {
        bool foundAny = false;

        // RSI style: root + StarCitizen (e.g., "F:\Roberts Space Industries" + "StarCitizen")
        string rsiStarCitizen = Path.Combine(root, P4KConstants.StarCitizenFolderName);
        if (Directory.Exists(rsiStarCitizen))
        {
            foreach (SCInstallCandidate candidate in EnumerateCandidates(root, rsiStarCitizen))
            {
                foundAny = true;
                yield return candidate;
            }
        }

        // Direct style: root already points at StarCitizen folder
        // Only check if we didn't find anything via RSI style to avoid duplicates
        if (!foundAny && root.EndsWith(P4KConstants.StarCitizenFolderName, StringComparison.OrdinalIgnoreCase))
        {
            foreach (SCInstallCandidate candidate in EnumerateCandidates(root, root))
            {
                foundAny = true;
                yield return candidate;
            }
        }

        if (!foundAny)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[CandidateEnumerator] No StarCitizen folder found under root: {root}");
        }
    }

    /// <summary>
    ///     Enumerates installation candidates under a StarCitizen folder (LIVE, PTU, EPTU channels).
    /// </summary>
    private static IEnumerable<SCInstallCandidate> EnumerateCandidates(string root, string starCitizenFolder)
    {
        foreach (SCChannel channel in Enum.GetValues<SCChannel>())
        {
            string folderName = channel.GetFolderName();
            string channelPath = Path.Combine(starCitizenFolder, folderName);
            string dataP4K = Path.Combine(channelPath, P4KConstants.DataP4kFileName);

            if (!Directory.Exists(channelPath) || !File.Exists(dataP4K))
            {
                continue;
            }

            string actualRootPath = starCitizenFolder.EndsWith(P4KConstants.StarCitizenFolderName, StringComparison.OrdinalIgnoreCase)
                ? Path.GetDirectoryName(starCitizenFolder) ?? root
                : root;

            yield return new SCInstallCandidate(
                actualRootPath,
                channel,
                NormalizePath(channelPath),
                dataP4K);
        }
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return SecurePathValidator.TryNormalizePath(path, out string normalized) ? normalized : path.Trim();
    }
}
