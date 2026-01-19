using SCStreamDeck.Common;

namespace SCStreamDeck.Services.Installation;

/// <summary>
///     Provides centralized path management for the plugin with security validation.
/// </summary>
public sealed class PathProviderService : IPathProvider
{
    public PathProviderService()
    {
        BaseDirectory = AppContext.BaseDirectory;
        CacheDirectory = Path.Combine(BaseDirectory, "cache");
    }

    public string BaseDirectory { get; }
    public string CacheDirectory { get; }

    public string GetKeybindingJsonPath(string channel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);

        string fileName = $"{channel.ToUpperInvariant()}-keybindings.json";
        return Path.Combine(CacheDirectory, fileName);
    }

    public void EnsureDirectoriesExist()
    {
        if (Directory.Exists(CacheDirectory))
        {
            return;
        }

        Directory.CreateDirectory(CacheDirectory);
    }

    public string GetSecureCachePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        string fullPath = Path.Combine(CacheDirectory, relativePath);
        return SecurePathValidator.GetSecurePath(fullPath, CacheDirectory);
    }
}
