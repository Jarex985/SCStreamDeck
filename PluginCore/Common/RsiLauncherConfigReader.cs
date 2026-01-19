using System.Text.RegularExpressions;
using BarRaider.SdTools;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;

namespace SCStreamDeck.Common;

/// <summary>
///     Reads and parses RSI Launcher configuration files and logs to extract Star Citizen installation paths.
///     Uses channel-based detection to support any custom installation path.
/// </summary>
internal sealed partial class RsiLauncherConfigReader
{
    private string? _rsiLauncherDirectory;

    // HIGH CONFIDENCE: Regex for parsing validateDirectories entries (comma-separated paths)
    [GeneratedRegex(@"\[LauncherSupport::validateDirectories\]\s*-\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex ValidateDirectoriesRegex();

    // HIGH CONFIDENCE: Regex for "Launching Star Citizen..." entries
    [GeneratedRegex(@"Launching Star Citizen \w+ from \(([^)]+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex LaunchPathRegex();

    // MEDIUM CONFIDENCE: Regex for "[Installer] - Installing Star Citizen... at PATH" entries
    [GeneratedRegex(
        @"\[Installer\]\s*-\s*(?:Installing|Starting|Delta update applied).*?(?:at|in)\s+([A-Z]:[^""<>\r\n]+?)(?=""|$)",
        RegexOptions.IgnoreCase)]
    private static partial Regex InstallerPathRegex();

    /// <summary>
    ///     Gets the RSI Launcher directory path, or null if not found.
    /// </summary>
    private string? GetRsiLauncherDirectory()
    {
        if (_rsiLauncherDirectory != null)
        {
            return _rsiLauncherDirectory;
        }

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string launcherPath = Path.Combine(appData, "rsilauncher");

        if (!Directory.Exists(launcherPath))
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(RsiLauncherConfigReader)}] {ErrorMessages.RsiLauncherDirNotFound}");
            return null;
        }

        if (!SecurePathValidator.TryNormalizePath(launcherPath, out string normalized))
        {
            return _rsiLauncherDirectory;
        }

        _rsiLauncherDirectory = normalized;

        return _rsiLauncherDirectory;
    }

    /// <summary>
    ///     Finds RSI Launcher log files, ordered by most recent.
    /// </summary>
    /// <param name="maxCount">Maximum number of log files to return.</param>
    public IEnumerable<string> FindLogFiles(int maxCount = 3)
    {
        string? launcherDir = GetRsiLauncherDirectory();
        if (launcherDir == null)
        {
            yield break;
        }

        string logsDir = Path.Combine(launcherDir, "logs");
        if (!Directory.Exists(logsDir))
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(RsiLauncherConfigReader)}] {ErrorMessages.RsiLauncherLogsNotFound}");
            yield break;
        }

        IEnumerable<string> logFiles =
            Directory.GetFiles(logsDir, "*.log").OrderByDescending(File.GetLastWriteTime).Take(maxCount);
        foreach (string logFile in logFiles)
        {
            yield return logFile;
        }
    }

    /// <summary>
    ///     Returns common default Star Citizen installation paths to check as fallback.
    ///     Used when RSI Launcher logs contain no installation data.
    /// </summary>
    private static IEnumerable<string> GetDefaultInstallationPaths()
    {
        // Common default paths
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Roberts Space Industries",
            "StarCitizen"
        );

        // Check all fixed drives for common installation patterns
        foreach (DriveInfo drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            yield return Path.Combine(drive.Name, "Roberts Space Industries", "StarCitizen");

            yield return Path.Combine(drive.Name, "StarCitizen");

            yield return Path.Combine(drive.Name, "Games", "StarCitizen");

            yield return Path.Combine(drive.Name, "SC");
        }
    }

    /// <summary>
    ///     Extracts Star Citizen installation root paths from a log file by detecting channel folders.
    ///     Returns only unique root paths without duplicates.
    /// </summary>
    public static async Task<HashSet<string>> ExtractPathsFromLogAsync(string logFilePath,
        CancellationToken cancellationToken = default)
    {
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(logFilePath))
        {
            return paths;
        }

        try
        {
            string content = await File.ReadAllTextAsync(logFilePath, cancellationToken).ConfigureAwait(false);
            ExtractPathsFromContent(content, paths);

            // If no paths found in log, try fallback detection
            if (paths.Count == 0)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN,
                    $"[{nameof(RsiLauncherConfigReader)}] No installation paths found in log file '{Path.GetFileName(logFilePath)}', trying fallback detection");

                foreach (string defaultPath in GetDefaultInstallationPaths())
                {
                    if (Directory.Exists(defaultPath) && IsValidGameRootCandidate(defaultPath))
                    {
                        paths.Add(defaultPath);
                        Logger.Instance.LogMessage(TracingLevel.INFO,
                            $"[{nameof(RsiLauncherConfigReader)}] Found installation at common path: '{defaultPath}'");
                    }
                }
            }
        }

        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(RsiLauncherConfigReader)}]: '{Path.GetFileName(logFilePath)}': {ex.Message}");
        }

        return paths;
    }

    /// <summary>
    ///     Extracts all potential game paths from log content by detecting channel folders.
    ///     Uses prioritized strategies: validateDirectories > Launch entries > Installer entries.
    /// </summary>
    private static void ExtractPathsFromContent(string content, HashSet<string> paths)
    {
        int strategy1Count = 0;
        int strategy2Count = 0;
        int strategy3Count = 0;

        // Strategy 1 (HIGHEST PRIORITY): Extract comma-separated paths from validateDirectories entries
        // This is the gold standard - RSI Launcher's own path validation
        foreach (Match match in ValidateDirectoriesRegex().Matches(content))
        {
            string pathList = match.Groups[1].Value;
            foreach (string path in pathList.Split(','))
            {
                string trimmedPath = path.Trim().Replace("\\\\", "\\"); // Handle escaped backslashes from JSON
                if (IsValidGameRootCandidate(trimmedPath))
                {
                    int beforeCount = paths.Count;
                    TryExtractGameRootFromPath(trimmedPath, paths, true);
                    if (paths.Count > beforeCount)
                    {
                        strategy1Count++;
                    }
                }
            }
        }

        // Strategy 2 (HIGH PRIORITY): Extract paths from "Launching Star Citizen" entries
        // These entries always contain complete, accurate paths
        foreach (Match match in LaunchPathRegex().Matches(content))
        {
            string path = match.Groups[1].Value.Trim();
            if (IsValidGameRootCandidate(path))
            {
                int beforeCount = paths.Count;
                TryExtractGameRootFromPath(path, paths, true);
                if (paths.Count > beforeCount)
                {
                    strategy2Count++;
                }
            }
        }

        // Strategy 3 (MEDIUM PRIORITY): Extract paths from Installer operations
        // Installer paths are root directories where the installer will create channel folders
        foreach (Match match in InstallerPathRegex().Matches(content))
        {
            string path = match.Groups[1].Value.Trim().Replace("\\\\", "\\");
            if (IsValidGameRootCandidate(path))
            {
                int beforeCount = paths.Count;
                TryExtractGameRootFromPath(path, paths, false);
                if (paths.Count > beforeCount)
                {
                    strategy3Count++;
                }
            }
        }

        // Warn if all strategies failed - possible RSI log format change
        if (strategy1Count == 0 && strategy2Count == 0 && strategy3Count == 0 && paths.Count == 0)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"[{nameof(RsiLauncherConfigReader)}] No installation paths found using any detection strategy. RSI Launcher log format may have changed, or no installations have been launched yet.");
        }

#if DEBUG
        Logger.Instance.LogMessage(TracingLevel.DEBUG,
            $"[{nameof(RsiLauncherConfigReader)}] Extracted {paths.Count} unique root path(s) from log content (Strategy 1: {strategy1Count}, Strategy 2: {strategy2Count}, Strategy 3: {strategy3Count})");
#endif
    }

    /// <summary>
    ///     Attempts to extract game root from a path by detecting channel folders.
    ///     If path contains a channel folder (LIVE, PTU, EPTU, HOTFIX), extracts the parent as game root.
    ///     If requireChannelFolder is false (installer entries), assumes the path itself is the game root.
    /// </summary>
    private static void TryExtractGameRootFromPath(string path, HashSet<string> paths, bool requireChannelFolder)
    {
        if (!SecurePathValidator.TryNormalizePath(path, out string normalizedPath))
        {
            return;
        }

        // Check if path contains any channel folder
        foreach (SCChannel channel in Enum.GetValues<SCChannel>())
        {
            string channelFolder = $"\\{channel.GetFolderName()}";

            int channelIndex = normalizedPath.LastIndexOf(channelFolder, StringComparison.OrdinalIgnoreCase);
            if (channelIndex > 0)
            {
                // Extract root as parent of channel folder
                string gameRoot = normalizedPath.Substring(0, channelIndex).TrimEnd('\\', '/');

                if (!string.IsNullOrWhiteSpace(gameRoot) &&
                    SecurePathValidator.TryNormalizePath(gameRoot, out string normalizedRoot))
                {
                    paths.Add(normalizedRoot);
                    return; // Found valid root, stop checking other channels
                }
            }
        }

        // If no channel folder detected:
        // - For launch/validate entries: reject (prevents false positives from partial paths)
        // - For installer entries: accept as root (installer creates channel folders inside)
        if (!requireChannelFolder)
        {
            paths.Add(normalizedPath);
        }
    }

    /// <summary>
    ///     Validates if a path could be a valid Star Citizen game root.
    ///     Rejects launcher internal paths, updater paths, and system directories.
    /// </summary>
    private static bool IsValidGameRootCandidate(string path)
    {
        // Reject null/empty
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // Reject paths that are too short (must have drive + at least one folder)
        // Minimum valid: "C:\X" (4 chars), but realistically "C:\SC" (5+ chars)
        if (path.Length < 4)
        {
            return false;
        }

        // Blacklist: Reject launcher internal paths and system directories
        string lowerPath = path.ToLowerInvariant();
        string[] blacklist =
        [
            "\\rsilauncher", // Launcher directory
            "\\rsilauncher-updater", // Updater directory
            "\\appdata\\local", // User temp directories
            "\\appdata\\roaming", // User config directories
            "\\windows\\", // Windows system directory
            "\\program files\\roberts space industries\\rsi launcher", // Launcher installation
            ".exe", // Executable files
            ".dll", // Libraries
            ".log", // Log files
            "\\resources\\", // Launcher resources
            "\\pending\\", // Update pending directory
            "installer.exe" // Installer executables
        ];

        foreach (string blocked in blacklist)
        {
            if (lowerPath.Contains(blocked))
            {
                Logger.Instance.LogMessage(TracingLevel.DEBUG,
                    $"[{nameof(RsiLauncherConfigReader)}] Rejected blacklisted path: '{path}'");
                return false;
            }
        }

        return true;
    }
}
