using System.Text;
using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;
using SCStreamDeck.SCCore.Common;
using SCStreamDeck.SCCore.Models;

namespace SCStreamDeck.SCCore.Services.Keybinding;

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
    /// <param name="keyboardLayout">Keyboard layout information</param>
    /// <param name="language">Detected language code</param>
    /// <param name="outputJsonPath">Path where the JSON file should be written</param>
    /// <param name="actions">List of keybinding actions</param>
    /// <param name="activationModes">Dictionary of activation mode metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public Task WriteKeybindingsJsonAsync(
        SCInstallCandidate installation,
        string? actionMapsPath,
        KeyboardLayoutInfo keyboardLayout,
        string language,
        string outputJsonPath,
        List<KeybindingActionData> actions,
        Dictionary<string, ActivationModeMetadata> activationModes,
        CancellationToken cancellationToken = default)
    {
        var metadata = BuildMetadata(
            installation,
            actionMapsPath,
            keyboardLayout,
            language,
            activationModes);

        var dataFile = new KeybindingDataFile
        {
            Metadata = metadata,
            Actions = actions
        };

        Directory.CreateDirectory(Path.GetDirectoryName(outputJsonPath)!);

        var json = JsonConvert.SerializeObject(dataFile, Formatting.Indented);
        return File.WriteAllTextAsync(outputJsonPath, json, Encoding.UTF8, cancellationToken);
    }

    /// <summary>
    ///     Builds the metadata object for the keybinding data file.
    /// </summary>
    private static KeybindingMetadata BuildMetadata(
        SCInstallCandidate installation,
        string? actionMapsPath,
        KeyboardLayoutInfo keyboardLayout,
        string language,
        Dictionary<string, ActivationModeMetadata> activationModes)
    {
        var p4kInfo = new FileInfo(installation.DataP4kPath);
        var metadata = new KeybindingMetadata
        {
            Version = "1.0",
            ExtractedAt = DateTime.UtcNow,
            KeyboardHkl = (long)keyboardLayout.Hkl,
            Language = language,
            DataP4kPath = NormalizePath(installation.DataP4kPath),
            DataP4kSize = p4kInfo.Length,
            DataP4kLastWrite = p4kInfo.LastWriteTime,
            ActivationModes = activationModes
        };

        if (!string.IsNullOrWhiteSpace(actionMapsPath) && File.Exists(actionMapsPath))
        {
            var actionMapsInfo = new FileInfo(actionMapsPath);
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
        return Path.GetFullPath(path).Replace('\\', '/');
    }
}
