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
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when any path parameter is invalid.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to the file is denied.</exception>
    /// <exception cref="JsonSerializationException">Thrown when JSON serialization fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task WriteKeybindingsJsonAsync(
        SCInstallCandidate installation,
        string? actionMapsPath,
        string language,
        string outputJsonPath,
        List<KeybindingActionData> actions,
        Dictionary<string, ActivationModeMetadata> activationModes,
        CancellationToken cancellationToken = default);
}
