using SCStreamDeck.SCCore.Common;

namespace SCStreamDeck.SCCore.Services.Installation;

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
        if (string.IsNullOrWhiteSpace(channel))
            throw new ArgumentException("Channel cannot be null or whitespace.", nameof(channel));

        var fileName = $"{channel.ToUpperInvariant()}-keybindings.json";
        return Path.Combine(CacheDirectory, fileName);
    }

    public void EnsureDirectoriesExist()
    {
        if (!Directory.Exists(CacheDirectory)) Directory.CreateDirectory(CacheDirectory);
    }

    public string GetSecureCachePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Relative path cannot be null or whitespace.", nameof(relativePath));

        var fullPath = Path.Combine(CacheDirectory, relativePath);
        return SecurePathValidator.GetSecurePath(fullPath, CacheDirectory);
    }
}