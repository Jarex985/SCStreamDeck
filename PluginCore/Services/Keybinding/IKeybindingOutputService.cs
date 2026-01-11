using SCStreamDeck.Common;
using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Keybinding;

/// <summary>
///     Service interface for writing keybinding data to JSON files.
/// </summary>
public interface IKeybindingOutputService
{
    /// <summary>
    ///     Writes keybinding data to a JSON file.
    /// </summary>
    /// <param name="installation">Star Citizen installation candidate</param>
    /// <param name="actionMapsPath">Path to the actionmaps.xml file (optional)</param>
    /// <param name="language">Detected language code</param>
    /// <param name="outputJsonPath">Path where the JSON file should be written</param>
    /// <param name="actions">List of keybinding actions</param>
    /// <param name="activationModes">Dictionary of activation mode metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task WriteKeybindingsJsonAsync(
        SCInstallCandidate installation,
        string? actionMapsPath,
        string language,
        string outputJsonPath,
        List<KeybindingActionData> actions,
        Dictionary<string, ActivationModeMetadata> activationModes,
        CancellationToken cancellationToken = default);
}
