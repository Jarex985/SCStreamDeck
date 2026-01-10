namespace SCStreamDeck.SCCore.Services.Core;

/// <summary>
///     Provides localization services for Star Citizen UI strings.
///     Replaces legacy static GlobalIniManager and UserConfigReader.
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    ///     Loads and caches global.ini localization dictionary.
    ///     Uses 3-tier priority: Override folder → P4K → English fallback.
    /// </summary>
    /// <param name="channelPath">Path to SC channel folder.</param>
    /// <param name="language">Language identifier (e.g., "german_(germany)").</param>
    /// <param name="dataP4kPath">Path to Data.p4k file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of localized strings.</returns>
    Task<IReadOnlyDictionary<string, string>> LoadGlobalIniAsync(
        string channelPath,
        string language,
        string dataP4kPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reads language setting from user.cfg file.
    /// </summary>
    /// <param name="channelPath">Path to SC channel folder.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detected language or "english" as fallback.</returns>
    Task<string> ReadLanguageSettingAsync(
        string channelPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Clears the cache for a specific channel and language.
    /// </summary>
    void ClearCache(string channelPath, string language);
}