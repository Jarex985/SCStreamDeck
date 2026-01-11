using BarRaider.SdTools;
using Newtonsoft.Json;
using SCStreamDeck.SCCore.Common;
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
            string userCfgPath = Path.Combine(channelPath, P4KConstants.UserConfigFileName);
            if (!File.Exists(userCfgPath))
            {
                return LocalizationConstants.DefaultLanguage;
            }

            string[] lines = File.ReadAllLines(userCfgPath);
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith(LocalizationConstants.LanguageConfigKey, StringComparison.OrdinalIgnoreCase) &&
                    trimmed.Contains('='))
                {
                    string[] parts = trimmed.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        string lang = parts[1].Trim().Trim('"').ToUpperInvariant();
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

            // Check Data.p4k timestamp
            FileInfo p4kInfo = new(installation.DataP4kPath);
            if (data.Metadata.DataP4kSize != p4kInfo.Length || data.Metadata.DataP4kLastWrite != p4kInfo.LastWriteTime)
            {
                return true;
            }

            // Check actionmaps.xml if it exists
            if (!string.IsNullOrWhiteSpace(data.Metadata.ActionMapsPath) && File.Exists(data.Metadata.ActionMapsPath))
            {
                FileInfo actionMapsInfo = new(data.Metadata.ActionMapsPath);
                if (data.Metadata.ActionMapsSize != actionMapsInfo.Length ||
                    data.Metadata.ActionMapsLastWrite != actionMapsInfo.LastWriteTime)
                {
                    return true;
                }
            }

            // Check language change
            string currentLanguage = DetectLanguage(installation.ChannelPath);
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
