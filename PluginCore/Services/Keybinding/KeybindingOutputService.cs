using Newtonsoft.Json;
using SCStreamDeck.Common;
using SCStreamDeck.Models;
using Formatting = Newtonsoft.Json.Formatting;


namespace SCStreamDeck.Services.Keybinding;

/// <summary>
///     Service for writing keybinding data to JSON files.
/// </summary>
public sealed class KeybindingOutputService(IFileSystem fileSystem) : IKeybindingOutputService
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

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
    public async Task WriteKeybindingsJsonAsync(
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

        string? directory = Path.GetDirectoryName(outputJsonPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonConvert.SerializeObject(dataFile, Formatting.Indented);
        await _fileSystem.WriteAllTextAsync(outputJsonPath, json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Builds the metadata object for the keybinding data file.
    /// </summary>
    private KeybindingMetadata BuildMetadata(
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

        if (!string.IsNullOrWhiteSpace(actionMapsPath) && _fileSystem.FileExists(actionMapsPath))
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
    private static string NormalizePath(string path)
    {
        if (SecurePathValidator.TryNormalizePath(path, out string normalized))
        {
            return normalized.Replace('\\', '/');
        }

        return Path.GetFullPath(path).Replace('\\', '/');
    }
}
