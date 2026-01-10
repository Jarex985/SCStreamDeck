namespace SCStreamDeck.SCCore.Services.Installation;

/// <summary>
///     Provides plugin version information.
///     Replaces the legacy static PluginVersionProvider class with a DI-ready service.
/// </summary>
public interface IVersionProvider
{
    /// <summary>
    ///     Gets the plugin version from the manifest.json file.
    /// </summary>
    /// <returns>The plugin version string.</returns>
    string GetPluginVersion();

    /// <summary>
    ///     Asynchronously gets the plugin version from the manifest.json file.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The plugin version string.</returns>
    Task<string> GetPluginVersionAsync(CancellationToken cancellationToken = default);
}