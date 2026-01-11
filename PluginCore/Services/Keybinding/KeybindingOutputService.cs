using System.Text;
using Newtonsoft.Json;
using SCStreamDeck.Common;
using SCStreamDeck.Models;
using Formatting = Newtonsoft.Json.Formatting;

namespace SCStreamDeck.Services.Keybinding;

/// <summary>
///     Service for writing keybinding data to JSON files.
/// </summary>
public sealed class KeybindingOutputService : IKeybindingOutputService
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
    public Task WriteKeybindingsJsonAsync(
        SCInstallCandidate installation,
        string? actionMapsPath,
        string language,
        string outputJsonPath,
        List<KeybindingActionData> actions,
        Dictionary<string, ActivationModeMetadata> activationModes,
        CancellationToken cancellationToken = default)
    {
        KeybindingMetadata metadata = BuildMetadata(
            installation,
            actionMapsPath,
            language,
            activationModes);

        KeybindingDataFile dataFile = new() { Metadata = metadata, Actions = actions };

        Directory.CreateDirectory(Path.GetDirectoryName(outputJsonPath)!);

        string json = JsonConvert.SerializeObject(dataFile, Formatting.Indented);
        return File.WriteAllTextAsync(outputJsonPath, json, Encoding.UTF8, cancellationToken);
    }

    /// <summary>
    ///     Builds the metadata object for the keybinding data file.
    /// </summary>
    private static KeybindingMetadata BuildMetadata(
        SCInstallCandidate installation,
        string? actionMapsPath,
        string language,
        Dictionary<string, ActivationModeMetadata> activationModes)
    {
        FileInfo p4KInfo = new(installation.DataP4KPath);
        KeybindingMetadata metadata = new()
        {
            ExtractedAt = DateTime.UtcNow,
            Language = language,
            DataP4KPath = NormalizePath(installation.DataP4KPath),
            DataP4KSize = p4KInfo.Length,
            DataP4KLastWrite = p4KInfo.LastWriteTime,
            ActivationModes = activationModes
        };

        if (!string.IsNullOrWhiteSpace(actionMapsPath) && File.Exists(actionMapsPath))
        {
            FileInfo actionMapsInfo = new(actionMapsPath);
            metadata.ActionMapsPath = NormalizePath(actionMapsPath);
            metadata.ActionMapsSize = actionMapsInfo.Length;
            metadata.ActionMapsLastWrite = actionMapsInfo.LastWriteTime;
        }

        return metadata;
    }

    /// <summary>
    ///     Normalizes a file path by converting it to a full path and using forward slashes.
    /// </summary>
    /// <param name="path">The path to normalize</param>
    /// <returns>Normalized path with forward slashes</returns>
    private static string NormalizePath(string path) => Path.GetFullPath(path).Replace('\\', '/');
}
