namespace SCStreamDeck.Services.Core;

/// <summary>
///     Provides localization services for Star Citizen UI strings.
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    ///     Loads and caches global.ini localization dictionary.
    ///     Uses 3-tier priority: Override folder → P4K → English fallback.
    /// </summary>
    /// <param name="channelPath">Path to SC channel folder.</param>
    /// <param name="language">Language identifier (e.g., "german_(germany)").</param>
    /// <param name="dataP4KPath">Path to Data.p4k file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of localized strings.</returns>
    /// <exception cref="ArgumentException">Thrown when any path parameter is null or whitespace.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task<IReadOnlyDictionary<string, string>> LoadGlobalIniAsync(
        string channelPath,
        string language,
        string dataP4KPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reads language setting from user.cfg file.
    /// </summary>
    /// <param name="channelPath">Path to SC channel folder.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detected language or "english" as fallback.</returns>
    /// <exception cref="ArgumentException">Thrown when channelPath is null or whitespace.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to the file is denied.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task<string> ReadLanguageSettingAsync(
        string channelPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Clears the cache for a specific channel and language.
    /// </summary>
    /// <param name="channelPath">Path to SC channel folder.</param>
    /// <param name="language">Language identifier.</param>
    /// <exception cref="ArgumentException">Thrown when language is null or whitespace.</exception>
    void ClearCache(string channelPath, string language);
}
