using SCStreamDeck.SCCore.Models;

namespace SCStreamDeck.SCCore.Services.Keybinding;

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
    Dictionary<string, ActivationModeMetadata> ParseActivationModes(string xmlText);

    /// <summary>
    ///     Parses keybinding actions from XML text.
    /// </summary>
    /// <param name="xmlText">The XML text to parse</param>
    /// <returns>List of parsed keybinding actions</returns>
    List<KeybindingActionData> ParseXmlToActions(string xmlText);
}
