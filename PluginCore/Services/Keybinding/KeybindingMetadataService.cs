using BarRaider.SdTools;
using Newtonsoft.Json;
using SCStreamDeck.SCCore.Logging;
using SCStreamDeck.SCCore.Models;

namespace SCStreamDeck.SCCore.Services.Keybinding;

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
            // user.cfg is directly in the channel folder (e.g., LIVE/user.cfg)
            // If we don't find any g_language entry, use english as default
            var userCfgPath = System.IO.Path.Combine(channelPath, P4KConstants.UserConfigFileName);
            if (!System.IO.File.Exists(userCfgPath)) return LocalizationConstants.DefaultLanguage;

            var lines = System.IO.File.ReadAllLines(userCfgPath);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith(LocalizationConstants.LanguageConfigKey, StringComparison.OrdinalIgnoreCase) &&
                    trimmed.Contains('='))
                {
                    var parts = trimmed.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        var lang = parts[1].Trim().Trim('"').ToUpperInvariant();
                        Logger.Instance.LogMessage(TracingLevel.INFO,
                            $"[KeybindingMetadataService] Detected language from {P4KConstants.UserConfigFileName}: {lang}");
                        return lang;
                    }
                }
            }

            return LocalizationConstants.DefaultLanguage;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(KeybindingMetadataService)}] {ErrorMessages.LanguageDetectionFailed} {ex.Message}");
            return LocalizationConstants.DefaultLanguage;
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
        if (!System.IO.File.Exists(jsonPath)) return true;

        try
        {
            var json = System.IO.File.ReadAllText(jsonPath);
            var data = JsonConvert.DeserializeObject<KeybindingDataFile>(json);

            if (data?.Metadata == null) return true;

            // Check Data.p4k timestamp
            var p4kInfo = new System.IO.FileInfo(installation.DataP4kPath);
            if (data.Metadata.DataP4kSize != p4kInfo.Length || data.Metadata.DataP4kLastWrite != p4kInfo.LastWriteTime)
                return true;

            // Check actionmaps.xml if it exists
            if (!string.IsNullOrWhiteSpace(data.Metadata.ActionMapsPath) && System.IO.File.Exists(data.Metadata.ActionMapsPath))
            {
                var actionMapsInfo = new System.IO.FileInfo(data.Metadata.ActionMapsPath);
                if (data.Metadata.ActionMapsSize != actionMapsInfo.Length ||
                    data.Metadata.ActionMapsLastWrite != actionMapsInfo.LastWriteTime)
                    return true;
            }

            // Check language change
            var currentLanguage = DetectLanguage(installation.ChannelPath);
            if (!string.Equals(data.Metadata.Language, currentLanguage, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Instance.LogMessage(TracingLevel.INFO,
                    $"[{nameof(KeybindingMetadataService)}] Language changed from '{data.Metadata.Language}' to '{currentLanguage}'");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(KeybindingMetadataService)}] {ErrorMessages.JsonMetadataCheckFailed} {ex.Message}");
            return true;
        }
    }
}
