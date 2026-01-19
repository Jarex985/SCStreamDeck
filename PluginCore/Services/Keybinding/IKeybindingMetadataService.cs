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
    /// <returns>Detected language code (e.g., "EN", "DE")</returns>
    /// <exception cref="ArgumentException">Thrown when channelPath is null or whitespace.</exception>
    string DetectLanguage(string channelPath);

    /// <summary>
    ///     Checks if keybinding data needs to be regenerated based on metadata.
    /// </summary>
    /// <param name="jsonPath">Path to the existing JSON file</param>
    /// <param name="installation">Star Citizen installation candidate</param>
    /// <returns>True if regeneration is needed, false otherwise</returns>
    /// <exception cref="ArgumentNullException">Thrown when installation is null.</exception>
    bool NeedsRegeneration(string jsonPath, SCInstallCandidate installation);
}
