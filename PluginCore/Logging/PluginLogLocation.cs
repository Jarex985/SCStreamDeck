using NLog;
using NLog.Layouts;
using NLog.Targets;

namespace SCStreamDeck.Logging;

/// <summary>
///     Finds the folder the plugin's log file is written to, so related diagnostic files can sit next to it.
/// </summary>
public static class PluginLogLocation
{
    /// <summary>
    ///     Directory of the first file target in the active NLog configuration (the Stream Deck SDK logs through
    ///     NLog); falls back to the plugin's own folder, where the SDK writes <c>pluginlog.log</c> by default.
    /// </summary>
    public static string GetDirectory()
    {
        try
        {
            IEnumerable<Target> targets = LogManager.Configuration?.AllTargets ?? Enumerable.Empty<Target>();
            foreach (Target target in targets)
            {
                if (target is not FileTarget { FileName: { } fileName })
                {
                    continue;
                }

                string rendered = ((Layout)fileName).Render(LogEventInfo.CreateNullEvent());
                string? directory = string.IsNullOrWhiteSpace(rendered)
                    ? null
                    : Path.GetDirectoryName(Path.GetFullPath(rendered));

                if (!string.IsNullOrWhiteSpace(directory))
                {
                    return directory;
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or InvalidOperationException)
        {
            // Fall through to the default location.
        }

        return AppContext.BaseDirectory;
    }
}
