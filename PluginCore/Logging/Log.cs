using System.Diagnostics;

namespace SCStreamDeck.Logging;

/// <summary>
///     Global logging facade.
///     Defaults to no-op until <see cref="Initialize" /> is called.
/// </summary>
public static class Log
{
    public enum Level
    {
        Debug,
        Info,
        Warn,
        Error
    }

    private static readonly object s_lock = new();

    private static volatile Action<Level, string> s_sink = static (_, _) => { };

    public static void Initialize(Action<Level, string> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        lock (s_lock)
        {
            s_sink = sink;
        }
    }

    [Conditional("DEBUG")]
    public static void Debug(string message) => Write(Level.Debug, message);

    public static void Info(string message) => Write(Level.Info, message);

    public static void Warn(string message) => Write(Level.Warn, message);

    public static void Warn(string message, Exception ex) => Write(Level.Warn, message, ex);

    public static void Err(string message) => Write(Level.Error, message);

    public static void Err(Exception ex) => Write(Level.Error, string.Empty, ex);

    public static void Err(string message, Exception ex) => Write(Level.Error, message, ex);

    private static void Write(Level level, string? message)
    {
        string safe = message ?? string.Empty;
        s_sink(level, safe);
    }

    private static void Write(Level level, string? message, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        string safe = string.IsNullOrWhiteSpace(message)
            ? ex.ToString()
            : $"{message}{Environment.NewLine}{ex}";
        s_sink(level, safe);
    }
}
