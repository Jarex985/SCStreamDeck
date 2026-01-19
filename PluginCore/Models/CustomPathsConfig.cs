using BarRaider.SdTools;

namespace SCStreamDeck.Models;

/// <summary>
///     Represents custom installation paths loaded from custom-paths.ini
/// </summary>
internal sealed class CustomPathsConfig
{
    private readonly Dictionary<SCChannel, string> _paths;

    private CustomPathsConfig(Dictionary<SCChannel, string> paths) => _paths = paths;

    /// <summary>
    ///     Loads custom paths from the INI file in the plugin directory
    /// </summary>
    /// <param name="pluginDirectory">Directory where custom-paths.ini is located</param>
    /// <returns>CustomPathsConfig instance, or null if file doesn't exist or has no valid paths</returns>
    public static CustomPathsConfig? LoadFromIni(string pluginDirectory)
    {
        ArgumentNullException.ThrowIfNull(pluginDirectory);

        string iniPath = Path.Combine(pluginDirectory, "custom-paths.ini");

        if (!File.Exists(iniPath))
        {
            return null;
        }

        try
        {
            Dictionary<SCChannel, string> paths = ParseSimpleIni(iniPath);

            if (paths.Count == 0)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, "Custom paths file exists but contains no valid paths");
                return null;
            }

            Logger.Instance.LogMessage(TracingLevel.INFO, $"Loaded {paths.Count} custom path(s) from configuration");
            return new CustomPathsConfig(paths);
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, $"Failed to load custom paths configuration: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Gets the custom path for a specific channel, if configured
    /// </summary>
    /// <param name="channel">The Star Citizen channel</param>
    /// <returns>Full path to Data.p4k, or null if not configured</returns>
    public string? GetPath(SCChannel channel) => _paths.TryGetValue(channel, out string? path) ? path : null;

    /// <summary>
    ///     Gets all configured channels
    /// </summary>
    public IEnumerable<SCChannel> GetConfiguredChannels() => _paths.Keys;

    /// <summary>
    ///     Simple INI parser that extracts paths from [Paths] section
    /// </summary>
    private static Dictionary<SCChannel, string> ParseSimpleIni(string filePath)
    {
        Dictionary<SCChannel, string> paths = new();
        bool inPathsSection = false;

        foreach (string line in File.ReadLines(filePath))
        {
            string trimmed = line.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
            {
                continue;
            }

            // Check for [Paths] section
            if (trimmed.Equals("[Paths]", StringComparison.OrdinalIgnoreCase))
            {
                inPathsSection = true;
                continue;
            }

            // Check for other sections (exit [Paths] section)
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                inPathsSection = false;
                continue;
            }

            // Parse key=value pairs only within [Paths] section
            if (inPathsSection && trimmed.Contains('='))
            {
                int equalIndex = trimmed.IndexOf('=');
                string key = trimmed.Substring(0, equalIndex).Trim();
                string value = trimmed.Substring(equalIndex + 1).Trim();

                // Skip empty values
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                // Clean the path (remove quotes, normalize slashes)
                string cleanPath = GetCleanPath(value);

                // Try to parse channel name
                if (TryParseChannel(key, out SCChannel channel))
                {
                    paths[channel] = cleanPath;
                }
            }
        }

        return paths;
    }

    /// <summary>
    ///     Removes quotes and normalizes path separators
    /// </summary>
    private static string GetCleanPath(string path)
    {
        string cleaned = path.Trim();

        // Remove surrounding quotes (double or single)
        if ((cleaned.StartsWith('"') && cleaned.EndsWith('"')) ||
            (cleaned.StartsWith('\'') && cleaned.EndsWith('\'')))
        {
            cleaned = cleaned.Substring(1, cleaned.Length - 2);
        }

        // Normalize slashes to backslashes (Windows standard)
        cleaned = cleaned.Replace('/', '\\');

        return cleaned.Trim();
    }

    /// <summary>
    ///     Tries to parse a channel name from the INI key
    /// </summary>
    private static bool TryParseChannel(string key, out SCChannel channel)
    {
        // Case-insensitive matching
        if (key.Equals("Live", StringComparison.OrdinalIgnoreCase))
        {
            channel = SCChannel.Live;
            return true;
        }

        if (key.Equals("Hotfix", StringComparison.OrdinalIgnoreCase))
        {
            channel = SCChannel.Hotfix;
            return true;
        }

        if (key.Equals("Ptu", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("PTU", StringComparison.OrdinalIgnoreCase))
        {
            channel = SCChannel.Ptu;
            return true;
        }

        if (key.Equals("Eptu", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("EPTU", StringComparison.OrdinalIgnoreCase))
        {
            channel = SCChannel.Eptu;
            return true;
        }

        channel = default;
        return false;
    }
}
