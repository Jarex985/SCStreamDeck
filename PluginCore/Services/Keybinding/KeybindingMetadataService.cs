using Newtonsoft.Json;
using SCStreamDeck.Common;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Keybinding;

/// <summary>
///     Service for keybinding metadata operations.
/// </summary>
public sealed class KeybindingMetadataService(IFileSystem fileSystem) : IKeybindingMetadataService
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    /// <summary>
    ///     Detects the Star Citizen language from the user configuration file.
    /// </summary>
    /// <param name="channelPath">The path to the Star Citizen channel directory</param>
    /// <returns>
    ///     Detected language identifier from user.cfg (uppercased), or the default language ("english") if missing.
    /// </returns>
    public string DetectLanguage(string channelPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelPath);

        try
        {
            string userCfgPath = Path.Combine(channelPath, SCConstants.Files.UserConfigFileName);
            if (!_fileSystem.FileExists(userCfgPath))
            {
                return SCConstants.Localization.DefaultLanguage;
            }

            string cfg = _fileSystem.ReadAllText(userCfgPath);
            using StringReader reader = new(cfg);
            string? detected = null;
            while (reader.ReadLine() is { } line)
            {
                detected = TryExtractLanguage(line.Trim());
                if (!string.IsNullOrWhiteSpace(detected))
                {
                    break;
                }
            }

            return detected ?? SCConstants.Localization.DefaultLanguage;
        }
        catch (Exception ex)
        {
            Log.Err($"[{nameof(KeybindingMetadataService)}] Failed to detect language", ex);
            return SCConstants.Localization.DefaultLanguage;
        }
    }


    /// <summary>
    ///     Checks if keybinding data needs to be regenerated based on metadata.
    /// </summary>
    /// <param name="jsonPath">Path to the existing JSON file</param>
    /// <param name="installation">Star Citizen installation candidate</param>
    /// <returns>True if regeneration is needed, false otherwise</returns>
    public bool NeedsRegeneration(string jsonPath, SCInstallCandidate installation)
    {
        ArgumentNullException.ThrowIfNull(installation);

        if (!_fileSystem.FileExists(jsonPath))
        {
            return true;
        }

        try
        {
            string json = _fileSystem.ReadAllText(jsonPath);
            KeybindingDataFile? data = JsonConvert.DeserializeObject<KeybindingDataFile>(json);

            if (data?.Metadata == null)
            {
                return true;
            }

            FileInfo p4KInfo = new(installation.DataP4KPath);
            if (HasFileChanged(data.Metadata.DataP4KSize, data.Metadata.DataP4KLastWrite, p4KInfo))
            {
                return true;
            }

            if (HasActionMapsChanged(data.Metadata, installation.ChannelPath))
            {
                return true;
            }

            return HasLanguageChanged(data.Metadata.Language, installation.ChannelPath);
        }
        catch (Exception ex)
        {
            Log.Err($"[{nameof(KeybindingMetadataService)}] Failed to check JSON metadata: {ex.Message}", ex);
            return true;
        }
    }

    private static string? TryExtractLanguage(string line)
    {
        if (!line.StartsWith(SCConstants.Localization.LanguageConfigKey, StringComparison.OrdinalIgnoreCase) ||
            !line.Contains('=', StringComparison.Ordinal))
        {
            return null;
        }

        string[] parts = line.Split('=', 2);
        if (parts.Length != 2)
        {
            return null;
        }

        string lang = parts[1].Trim().Trim('"').ToUpperInvariant();
        Log.Debug($"[{nameof(KeybindingMetadataService)}] Detected language from {SCConstants.Files.UserConfigFileName}: {lang}");
        return lang;
    }

    private bool HasLanguageChanged(string previousLanguage, string channelPath)
    {
        string currentLanguage = DetectLanguage(channelPath);
        if (!string.Equals(previousLanguage, currentLanguage, StringComparison.OrdinalIgnoreCase))
        {
            Log.Debug($"[{nameof(KeybindingMetadataService)}] Language changed from '{previousLanguage}' to '{currentLanguage}'");
            return true;
        }

        return false;
    }

    private bool HasActionMapsChanged(KeybindingMetadata metadata, string channelPath)
    {
        string? detectedActionMapsPath = KeybindingProfilePathResolver.TryFindActionMapsXml(channelPath);

        string? previousPath = string.IsNullOrWhiteSpace(metadata.ActionMapsPath)
            ? null
            : metadata.ActionMapsPath;

        string? currentPath = string.IsNullOrWhiteSpace(detectedActionMapsPath)
            ? null
            : detectedActionMapsPath;

        // If the presence of actionmaps.xml changed (appeared/disappeared), overrides likely changed.
        if (previousPath == null && currentPath == null)
        {
            return false;
        }

        if (previousPath == null || currentPath == null)
        {
            return true;
        }

        // If the path changed, treat as changed even if file timestamps happen to match.
        string previousNormalized = previousPath.Replace('\\', '/');
        string currentNormalized = currentPath.Replace('\\', '/');

        if (!string.Equals(previousNormalized, currentNormalized, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Same file: compare size + last write time.
        if (!_fileSystem.FileExists(currentPath))
        {
            return true;
        }

        FileInfo actionMapsInfo = new(currentPath);
        return HasFileChanged(metadata.ActionMapsSize, metadata.ActionMapsLastWrite, actionMapsInfo);
    }

    private static bool HasFileChanged(long? expectedSize, DateTime? expectedWrite, FileInfo fileInfo) =>
        expectedSize != fileInfo.Length || expectedWrite != fileInfo.LastWriteTime;
}
