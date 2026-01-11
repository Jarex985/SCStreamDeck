namespace SCStreamDeck.Services.Installation;

/// <summary>
///     Provides centralized path management for the plugin with security validation.
/// </summary>
public interface IPathProvider
{
    /// <summary>
    ///     Gets the plugin's base directory.
    /// </summary>
    string BaseDirectory { get; }

    /// <summary>
    ///     Gets the cache directory path.
    /// </summary>
    string CacheDirectory { get; }

    /// <summary>
    ///     Gets the path to the processed keybindings JSON file for the specified channel.
    /// </summary>
    /// <param name="channel">The Star Citizen channel (e.g., "LIVE", "PTU").</param>
    /// <returns>Full path to the keybindings JSON file.</returns>
    string GetKeybindingJsonPath(string channel);

    /// <summary>
    ///     Ensures that all required directories exist, creating them if necessary.
    /// </summary>
    void EnsureDirectoriesExist();

    /// <summary>
    ///     Gets a secure path within the cache directory.
    /// </summary>
    /// <param name="relativePath">Relative path within the cache directory.</param>
    /// <returns>Secure full path within cache directory.</returns>
    /// <exception cref="System.Security.SecurityException">Thrown if path traversal is detected.</exception>
    string GetSecureCachePath(string relativePath);
}
