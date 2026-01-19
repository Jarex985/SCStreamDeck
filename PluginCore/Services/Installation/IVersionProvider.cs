namespace SCStreamDeck.Services.Installation;

/// <summary>
///     Provides plugin version information.
/// </summary>
public interface IVersionProvider
{
    /// <summary>
    ///     Gets the plugin version from the manifest.json file.
    /// </summary>
    /// <returns>The plugin version string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when version cannot be loaded or parsed.</exception>
    /// <exception cref="FileNotFoundException">Thrown when manifest.json is not found.</exception>
    /// <exception cref="IOException">Thrown when manifest.json cannot be read.</exception>
    string GetPluginVersion();

    /// <summary>
    ///     Asynchronously gets the plugin version from the manifest.json file.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The plugin version string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when version cannot be loaded or parsed.</exception>
    /// <exception cref="FileNotFoundException">Thrown when manifest.json is not found.</exception>
    /// <exception cref="IOException">Thrown when manifest.json cannot be read.</exception>
    /// <exception cref="JsonException">Thrown when manifest.json contains invalid JSON.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task<string> GetPluginVersionAsync(CancellationToken cancellationToken = default);
}
