using BarRaider.SdTools;

namespace SCStreamDeck.Common;

/// <summary>
///     Attempts to locate the user profile folder containing actionmaps.xml.
///     Assumes path structure: {channelPath}\user\client\{instanceId}\Profiles\default\actionmaps.xml
/// </summary>
public static class KeybindingProfilePathResolver
{
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

            return FindFirstExistingProfile(clientDir);
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"KeybindingProfilePathResolver: {ex.Message}");
        }

        return null;
    }

    private static string? FindFirstExistingProfile(string clientDir)
    {
        foreach (string instanceDir in Directory.GetDirectories(clientDir))
        {
            string candidate = Path.Combine(instanceDir, "Profiles", "default", SCConstants.Files.ActionMapsFileName);

            if (!File.Exists(candidate))
            {
                continue;
            }

            return SecurePathValidator.TryNormalizePath(candidate, out string normalized) ? normalized : null;
        }

        return null;
    }
}
