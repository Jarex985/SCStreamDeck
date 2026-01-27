using SCStreamDeck.Common;

namespace SCStreamDeck.Services.Installation;

/// <summary>
///     Provides centralized path management for the plugin with security validation.
/// </summary>
public class PathProviderService
{
    public PathProviderService()
    {
        string baseDirectory = AppContext.BaseDirectory;
        BaseDirectory = baseDirectory;
        CacheDirectory = Path.Combine(baseDirectory, "cache");
    }

    /// <summary>
    ///     Protected constructor for testing with custom directories.
    /// </summary>
    protected PathProviderService(string baseDirectory, string cacheDirectory)
    {
        BaseDirectory = baseDirectory;
        CacheDirectory = cacheDirectory;
    }

    public string BaseDirectory { get; }
    public string CacheDirectory { get; }

    public string GetKeybindingJsonPath(string channel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);

        EnsureDirectoriesExist();

        string fileName = $"{channel.ToUpperInvariant()}-keybindings.json";
        return GetSecureCachePath(fileName);
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
