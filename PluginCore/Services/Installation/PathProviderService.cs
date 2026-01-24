using SCStreamDeck.Common;

namespace SCStreamDeck.Services.Installation;

/// <summary>
///     Provides centralized path management for the plugin with security validation.
/// </summary>
public class PathProviderService
{
    public PathProviderService()
    {
        BaseDirectory = AppContext.BaseDirectory;
        CacheDirectory = Path.Combine(BaseDirectory, "cache");
    }

    /// <summary>
    ///     Protected constructor for testing with custom directories.
    /// </summary>
    protected PathProviderService(string baseDirectory, string cacheDirectory)
    {
        BaseDirectory = baseDirectory;
        CacheDirectory = cacheDirectory;
    }

    public virtual string BaseDirectory { get; }
    public virtual string CacheDirectory { get; }

    public virtual string GetKeybindingJsonPath(string channel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);

        string fileName = $"{channel.ToUpperInvariant()}-keybindings.json";
        return Path.Combine(CacheDirectory, fileName);
    }

    public virtual void EnsureDirectoriesExist()
    {
        if (Directory.Exists(CacheDirectory))
        {
            return;
        }

        Directory.CreateDirectory(CacheDirectory);
    }

    public virtual string GetSecureCachePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        string fullPath = Path.Combine(CacheDirectory, relativePath);
        return SecurePathValidator.GetSecurePath(fullPath, CacheDirectory);
    }
}
