using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Keybinding;

/// <summary>
///     Service interface for parsing Star Citizen keybinding XML data.
/// </summary>
public interface IKeybindingXmlParserService
{
    /// <summary>
    ///     Parses activation mode metadata from XML text.
    /// </summary>
    /// <param name="xmlText">The XML text to parse</param>
    /// <returns>Dictionary of activation mode names and their metadata</returns>
    /// <exception cref="ArgumentNullException">Thrown when xmlText is null.</exception>
    Dictionary<string, ActivationModeMetadata> ParseActivationModes(string xmlText);

    /// <summary>
    ///     Parses keybinding actions from XML text.
    /// </summary>
    /// <param name="xmlText">The XML text to parse</param>
    /// <returns>List of parsed keybinding actions</returns>
    /// <exception cref="ArgumentNullException">Thrown when xmlText is null.</exception>
    List<KeybindingActionData> ParseXmlToActions(string xmlText);
}
