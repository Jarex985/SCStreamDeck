using System.Text.Json;
using SCStreamDeck.Common;

namespace SCStreamDeck.Services.Installation;

/// <summary>
///     Provides plugin version information from manifest.json.
/// </summary>
public sealed class VersionProviderService : IVersionProvider
{
    private readonly Lazy<string> _cachedVersion;
    private readonly IPathProvider _pathProvider;

    public VersionProviderService(IPathProvider pathProvider)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _cachedVersion = new Lazy<string>(LoadVersionSync);
    }

    /// <summary>
    ///     Gets the plugin version synchronously (uses cached value after first load).
    /// </summary>
    public string GetPluginVersion() => _cachedVersion.Value;

    /// <summary>
    ///     Gets the plugin version asynchronously.
    /// </summary>
    public async Task<string> GetPluginVersionAsync(CancellationToken cancellationToken = default)
    {
        string manifestPath = Path.Combine(_pathProvider.BaseDirectory, "manifest.json");

        if (!SecurePathValidator.TryNormalizePath(manifestPath, out string normalizedPath))
        {
            throw new InvalidOperationException("Failed to resolve manifest.json path.");
        }

        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException("manifest.json not found.", normalizedPath);
        }

        try
        {
            await using FileStream stream = File.OpenRead(normalizedPath);
            JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (doc.RootElement.TryGetProperty("Version", out JsonElement versionElement))
            {
                string? version = versionElement.GetString();
                if (!string.IsNullOrWhiteSpace(version))
                {
                    return version;
                }
            }

            throw new InvalidOperationException("Version property not found or empty in manifest.json.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to parse manifest.json.", ex);
        }
    }

    private string LoadVersionSync()
    {
        try
        {
            return Task.Run(() => GetPluginVersionAsync(CancellationToken.None)).GetAwaiter().GetResult();
        }

        catch (AggregateException ex) when (ex.InnerException != null)
        {
            throw new InvalidOperationException("Failed to load plugin version.", ex.InnerException);
        }
        catch (FileNotFoundException)
        {
            throw;
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException("Failed to load plugin version.", ex);
        }
    }
}
