using SCStreamDeck.SCCore.Common;
using SCStreamDeck.SCCore.Models;

namespace SCStreamDeck.SCCore.Services.Keybinding;

/// <summary>
///     Service for processing Star Citizen keybindings.
///     Replaces legacy KeybindingProcessor with async service.
/// </summary>
public interface IKeybindingProcessorService
{
    /// <summary>
    ///     Processes keybindings from defaultProfile.xml and generates JSON.
    /// </summary>
    /// <param name="installation">SC installation to process</param>
    /// <param name="actionMapsPath">Path to user's actionmaps.xml (optional overrides)</param>
    /// <param name="keyboardLayout">Keyboard layout info</param>
    /// <param name="outputJsonPath">Path where JSON should be saved</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detected language and success status</returns>
    Task<KeybindingProcessResult> ProcessKeybindingsAsync(
        SCInstallCandidate installation,
        string? actionMapsPath,
        KeyboardLayoutInfo keyboardLayout,
        string outputJsonPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if keybindings JSON needs regeneration.
    /// </summary>
    bool NeedsRegeneration(string jsonPath, SCInstallCandidate installation);
}

/// <summary>
///     Result of keybinding processing.
/// </summary>
public sealed class KeybindingProcessResult
{
    public bool IsSuccess { get; init; }
    public string? DetectedLanguage { get; init; }
    public string? ErrorMessage { get; init; }

    public static KeybindingProcessResult Success(string detectedLanguage) =>
        new() { IsSuccess = true, DetectedLanguage = detectedLanguage };

    public static KeybindingProcessResult Failure(string errorMessage) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage };
}
