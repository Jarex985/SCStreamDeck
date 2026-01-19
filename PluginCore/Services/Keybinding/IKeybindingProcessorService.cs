using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Keybinding;

/// <summary>
///     Service for processing Star Citizen keybindings.
/// </summary>
public interface IKeybindingProcessorService
{
    /// <summary>
    ///     Processes keybindings from defaultProfile.xml and generates JSON.
    /// </summary>
    /// <param name="installation">SC installation to process</param>
    /// <param name="actionMapsPath">Path to user's actionmaps.xml (optional overrides)</param>
    /// <param name="outputJsonPath">Path where JSON should be saved</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detected language and success status</returns>
    /// <exception cref="ArgumentNullException">Thrown when installation is null.</exception>
    /// <exception cref="ArgumentException">Thrown when outputJsonPath is null or whitespace.</exception>
    /// <exception cref="IOException">Thrown when file I/O fails.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when file access is denied.</exception>
    /// <exception cref="ZipException">Thrown when P4K archive operations fail.</exception>
    /// <exception cref="JsonException">Thrown when JSON serialization fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task<KeybindingProcessResult> ProcessKeybindingsAsync(
        SCInstallCandidate installation,
        string? actionMapsPath,
        string outputJsonPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if keybindings JSON needs regeneration.
    /// </summary>
    /// <param name="jsonPath">Path to the existing JSON file.</param>
    /// <param name="installation">Star Citizen installation candidate.</param>
    /// <returns>True if regeneration is needed, false otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when installation is null.</exception>
    bool NeedsRegeneration(string jsonPath, SCInstallCandidate installation);
}

/// <summary>
///     Result of keybinding processing.
/// </summary>
public sealed class KeybindingProcessResult
{
    public bool IsSuccess { get; init; }
    public string? DetectedLanguage { get; init; }
    public string? ErrorMessage { get; private init; }

    public static KeybindingProcessResult Success(string detectedLanguage) =>
        new() { IsSuccess = true, DetectedLanguage = detectedLanguage };

    public static KeybindingProcessResult Failure(string errorMessage) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage };
}
