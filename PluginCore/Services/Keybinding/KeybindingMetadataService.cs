using BarRaider.SdTools;
using Newtonsoft.Json;
using SCStreamDeck.Common;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Keybinding;

/// <summary>
///     Service for keybinding metadata operations.
/// </summary>
public sealed class KeybindingMetadataService : IKeybindingMetadataService
{
    /// <summary>
    ///     Detects the Star Citizen language from the user configuration file.
    /// </summary>
    /// <param name="channelPath">The path to the Star Citizen channel directory</param>
    /// <returns>Detected language code (e.g., "EN", "DE")</returns>
    public string DetectLanguage(string channelPath)
    {
        try
        {
            string userCfgPath = Path.Combine(channelPath, SCConstants.Files.UserConfigFileName);
            if (!File.Exists(userCfgPath))
            {
                return SCConstants.Localization.DefaultLanguage;
            }

            string? detected = File.ReadLines(userCfgPath)
                .Select(line => line.Trim())
                .Select(TryExtractLanguage)
                .FirstOrDefault(lang => !string.IsNullOrWhiteSpace(lang));

            return detected ?? SCConstants.Localization.DefaultLanguage;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(KeybindingMetadataService)}] {ErrorMessages.LanguageDetectionFailed} {ex.Message}");
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
        if (!File.Exists(jsonPath))
        {
            return true;
        }

        try
        {
            string json = File.ReadAllText(jsonPath);
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

            if (HasActionMapsChanged(data.Metadata))
            {
                return true;
            }

            return HasLanguageChanged(data.Metadata.Language, installation.ChannelPath);
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(KeybindingMetadataService)}] Failed to check JSON metadata: {ex.Message}");
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
        Logger.Instance.LogMessage(TracingLevel.INFO,
            $"[{nameof(KeybindingMetadataService)}] Detected language from {SCConstants.Files.UserConfigFileName}: {lang}");
        return lang;
    }

    private bool HasLanguageChanged(string previousLanguage, string channelPath)
    {
        string currentLanguage = DetectLanguage(channelPath);
        if (!string.Equals(previousLanguage, currentLanguage, StringComparison.OrdinalIgnoreCase))
        {
            Logger.Instance.LogMessage(TracingLevel.INFO,
                $"[{nameof(KeybindingMetadataService)}] Language changed from '{previousLanguage}' to '{currentLanguage}'");
            return true;
        }

        return false;
    }

    private static bool HasActionMapsChanged(KeybindingMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata.ActionMapsPath) || !File.Exists(metadata.ActionMapsPath))
        {
            return false;
        }

        FileInfo actionMapsInfo = new(metadata.ActionMapsPath);
        return HasFileChanged(metadata.ActionMapsSize, metadata.ActionMapsLastWrite, actionMapsInfo);
    }

    private static bool HasFileChanged(long? expectedSize, DateTime? expectedWrite, FileInfo fileInfo) =>
        expectedSize != fileInfo.Length || expectedWrite != fileInfo.LastWriteTime;
}
