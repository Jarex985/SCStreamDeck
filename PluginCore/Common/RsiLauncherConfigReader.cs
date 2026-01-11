using System.Text.RegularExpressions;
using BarRaider.SdTools;
using SCStreamDeck.Logging;

namespace SCStreamDeck.Common;

/// <summary>
///     Reads and parses RSI Launcher configuration files and logs to extract Star Citizen installation paths.
/// </summary>
internal sealed partial class RsiLauncherConfigReader
{
    private string? _rsiLauncherDirectory;

    // Regex for parsing log files
    // Excludes commas to prevent matching comma-separated path lists as single path
    [GeneratedRegex(@"([A-Za-z]:\\[^\""<>|,\r\n]+?\\StarCitizen)", RegexOptions.IgnoreCase)]
    private static partial Regex StarCitizenPathRegex();

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
    ///     Extracts Star Citizen installation root paths from a log file (e.g., "F:\Roberts Space Industries").
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
        }

        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(RsiLauncherConfigReader)}]: '{Path.GetFileName(logFilePath)}': {ex.Message}");
        }

        return paths;
    }

    /// <summary>
    ///     Extracts Star Citizen paths from log content.
    /// </summary>
    private static void ExtractPathsFromContent(string content, HashSet<string> paths)
    {
        foreach (string rawPath in StarCitizenPathRegex().Matches(content).Select(m => m.Value))
        {
            if (!SecurePathValidator.TryNormalizePath(rawPath, out string path))
            {
                continue;
            }

            AddNormalizedRootPath(path, paths);
        }
    }

    /// <summary>
    ///     Adds the normalized root path to the collection if valid.
    /// </summary>
    private static void AddNormalizedRootPath(string starCitizenPath, HashSet<string> paths)
    {
        if (!starCitizenPath.EndsWith(SCConstants.Paths.StarCitizenFolderName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string? rootPath = Path.GetDirectoryName(starCitizenPath);
        if (string.IsNullOrWhiteSpace(rootPath) ||
            !SecurePathValidator.TryNormalizePath(rootPath, out string normalizedRoot))
        {
            return;
        }

        normalizedRoot = normalizedRoot.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        paths.Add(normalizedRoot);
    }
}
