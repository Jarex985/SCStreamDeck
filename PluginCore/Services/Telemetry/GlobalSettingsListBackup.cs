using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SCStreamDeck.Logging;

namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     Keeps a copy of one list stored in the plugin's global settings (format templates, flags) outside the plugin
///     folder, which Stream Deck replaces when the plugin is installed again, and restores it when the list is gone.
/// </summary>
internal sealed class GlobalSettingsListBackup(string key, string description, Func<string> backupPath)
{
    private int _restoreAttempted;

    /// <summary>
    ///     Hands the (possibly restored) global settings to <paramref name="apply" />. The list is copied to the backup
    ///     file; when the settings contain no such list at all (a fresh or reinstalled plugin) the copy is restored
    ///     instead. Returns the global settings to write back to Stream Deck after a restore, otherwise null.
    /// </summary>
    public JObject? Apply(JObject? globalSettings, Action<JObject?> apply)
    {
        ArgumentNullException.ThrowIfNull(apply);

        if (globalSettings?[key] is JArray current)
        {
            apply(globalSettings);
            Write(current);
            return null;
        }

        if (Interlocked.Exchange(ref _restoreAttempted, 1) == 0 && Read() is { Count: > 0 } saved)
        {
            JObject restored = globalSettings != null ? (JObject)globalSettings.DeepClone() : new JObject();
            restored[key] = saved;
            apply(restored);
            Log.Info($"[{nameof(GlobalSettingsListBackup)}] Restored {saved.Count} {description} from {backupPath()}");
            return restored;
        }

        apply(globalSettings);
        return null;
    }

    private JArray? Read()
    {
        try
        {
            string path = backupPath();
            return File.Exists(path) ? JArray.Parse(File.ReadAllText(path)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Warn($"[{nameof(GlobalSettingsListBackup)}] Cannot read the {description} backup: {ex.Message}");
            return null;
        }
    }

    private void Write(JArray list)
    {
        try
        {
            string path = backupPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp = path + ".tmp";
            File.WriteAllText(temp, list.ToString(Formatting.Indented));
            File.Move(temp, path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Log.Warn($"[{nameof(GlobalSettingsListBackup)}] Cannot write the {description} backup: {ex.Message}");
        }
    }
}
