namespace SCStreamDeck.Common;

/// <summary>
///     Attempts to locate the user profile folder containing actionmaps.xml.
///     Assumes path structure: {channelPath}\user\client\{instanceId}\Profiles\default\actionmaps.xml
/// </summary>
public static class KeybindingProfilePathResolver
{
    /// <summary>
    ///     Tries to find the actionmaps.xml file in the user profile directory.
    /// </summary>
    /// <param name="channelPath">The Star Citizen installation channel path (e.g., LIVE or PTU folder).</param>
    /// <returns>The normalized path to actionmaps.xml, or null if not found.</returns>
    public static string? TryFindActionMapsXml(string? channelPath)
    {
        if (string.IsNullOrWhiteSpace(channelPath))
        {
            return null;
        }

        try
        {
            string userDir = Path.Combine(channelPath, "user");
            string clientDir = Path.Combine(userDir, "client");
            if (!Directory.Exists(userDir) || !Directory.Exists(clientDir))
            {
                return null;
            }

            // Iterate through instance directories (typically "0", but could be others in the future)
            foreach (string instanceDir in Directory.GetDirectories(clientDir))
            {
                string candidate = Path.Combine(instanceDir, "Profiles", "default", SCConstants.Files.ActionMapsFileName);

                if (!File.Exists(candidate))
                {
                    continue;
                }

                return SecurePathValidator.TryNormalizePath(candidate, out string normalized) ? normalized : null;
            }
        }
        catch
        {
            // Ignore errors; return null if path resolution fails
        }

        return null;
    }
}
