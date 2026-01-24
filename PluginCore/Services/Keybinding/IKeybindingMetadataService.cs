using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Keybinding;

/// <summary>
///     Service interface for keybinding metadata operations.
/// </summary>
public interface IKeybindingMetadataService
{
    /// <summary>
    ///     Detects the Star Citizen language from the user configuration file.
    /// </summary>
    /// <param name="channelPath">The path to the Star Citizen channel directory</param>
    /// <returns>
    ///     Detected language identifier from user.cfg (uppercased), or the default language ("english") if missing.
    /// </returns>
    string DetectLanguage(string channelPath);

    /// <summary>
    ///     Checks if keybinding data needs to be regenerated based on metadata.
    /// </summary>
    /// <param name="jsonPath">Path to the existing JSON file</param>
    /// <param name="installation">Star Citizen installation candidate</param>
    /// <returns>True if regeneration is needed, false otherwise</returns>
    bool NeedsRegeneration(string jsonPath, SCInstallCandidate installation);
}
