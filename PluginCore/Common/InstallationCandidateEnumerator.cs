using SCStreamDeck.Logging;
using SCStreamDeck.Models;

namespace SCStreamDeck.Common;

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
            Log.Err($"[{nameof(InstallationCandidateEnumerator)}] Failed to process root '{root}': {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Enumerates all valid installation candidates from a root path by detecting channel folders.
    ///     Does not assume any specific folder structure - directly checks for channel folders.
    /// </summary>
    private static IEnumerable<SCInstallCandidate> EnumerateCandidates(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            yield break;
        }

        bool foundAny = false;

        // Direct enumeration: Check if root contains channel folders
        foreach (SCChannel channel in Enum.GetValues<SCChannel>())
        {
            string channelPath = Path.Combine(root, channel.GetFolderName());
            string dataP4K = Path.Combine(channelPath, SCConstants.Files.DataP4KFileName);

            if (Directory.Exists(channelPath) && File.Exists(dataP4K))
            {
                foundAny = true;
                Log.Debug($"[{nameof(InstallationCandidateEnumerator)}] Found {channel} at: {channelPath}");

                yield return new SCInstallCandidate(
                    NormalizePath(root),
                    channel,
                    NormalizePath(channelPath),
                    dataP4K);
            }
        }

        if (!foundAny)
        {
            Log.Debug($"[{nameof(InstallationCandidateEnumerator)}] No valid channels found under root: {root}");
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
