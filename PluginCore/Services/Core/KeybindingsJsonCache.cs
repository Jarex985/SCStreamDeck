using SCStreamDeck.Common;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Installation;

namespace SCStreamDeck.Services.Core;

public interface IKeybindingsJsonCache
{
    bool Exists(SCChannel channel);
    bool TryDelete(SCChannel channel);
    bool TryDeleteAll();
}

/// <summary>
///     File-system access for generated keybindings JSON files.
/// </summary>
public sealed class KeybindingsJsonCache(PathProviderService pathProvider, IFileSystem fileSystem) : IKeybindingsJsonCache
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly PathProviderService _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));

    public bool Exists(SCChannel channel)
    {
        string keybindingJson = _pathProvider.GetKeybindingJsonPath(channel.ToString());
        return _fileSystem.FileExists(keybindingJson);
    }

    public bool TryDeleteAll()
    {
        bool anyDeleted = false;
        foreach (SCChannel channel in Enum.GetValues<SCChannel>())
        {
            anyDeleted |= TryDelete(channel);
        }

        return anyDeleted;
    }

    public bool TryDelete(SCChannel channel)
    {
        try
        {
            string keybindingJson = _pathProvider.GetKeybindingJsonPath(channel.ToString());
            if (!_fileSystem.FileExists(keybindingJson))
            {
                return false;
            }

            _fileSystem.DeleteFile(keybindingJson);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"[{nameof(KeybindingsJsonCache)}] Failed to delete keybindings JSON", ex);
            return false;
        }
    }
}
