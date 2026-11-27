using System.Globalization;
using System.Text;
using SCStreamDeck.Logging;

namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     Troubleshooting aid toggled by the Telemetry Debug key. While on, raw frames of the telemetry stream are
///     written (throttled) to <see cref="FileName" /> next to the plugin log, annotated with the schema in use,
///     followed by a summary of the protocol attributes the game never sent.
/// </summary>
public sealed class TelemetryDebugRecorder
{
    public const string FileName = "telemetry-debug.log";

    private const int MaxFileBytes = 5 * 1024 * 1024;

    private static readonly TimeSpan s_defaultInterval = TimeSpan.FromSeconds(2);

    private readonly object _gate = new();
    private readonly Func<string> _directory;
    private readonly Func<DateTime> _clock;
    private readonly HashSet<string> _sentPaths = new(StringComparer.Ordinal);

    private volatile bool _enabled;
    private int _framesSeen;
    private int _framesWritten;
    private long _bytesWritten;
    private DateTime _lastWrite = DateTime.MinValue;
    private TelemetrySchema? _schema;
    private string? _path;

    public TelemetryDebugRecorder() : this(PluginLogLocation.GetDirectory, () => DateTime.UtcNow)
    {
    }

    internal TelemetryDebugRecorder(Func<string> directory, Func<DateTime> clock)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Minimum time between two frames written to the file.</summary>
    public TimeSpan Interval { get; init; } = s_defaultInterval;

    public bool IsEnabled => _enabled;

    /// <summary>Frames received since debugging was switched on.</summary>
    public int FramesSeen => Volatile.Read(ref _framesSeen);

    public int FramesWritten => Volatile.Read(ref _framesWritten);

    /// <summary>The file of the current or last session; null before the first one.</summary>
    public string? FilePath
    {
        get
        {
            lock (_gate)
            {
                return _path;
            }
        }
    }

    public event Action? Changed;

    /// <summary>Switches debugging on or off; returns the new state.</summary>
    public bool Toggle()
    {
        if (_enabled)
        {
            Disable();
            return false;
        }

        return Enable();
    }

    public bool Enable()
    {
        lock (_gate)
        {
            if (_enabled)
            {
                return true;
            }

            try
            {
                string directory = _directory();
                Directory.CreateDirectory(directory);
                _path = Path.Combine(directory, FileName);

                string header =
                    $"Star Citizen telemetry debug session started {_clock().ToLocalTime().ToString("u", CultureInfo.InvariantCulture)}{Environment.NewLine}" +
                    $"One frame is written at most every {Interval.TotalSeconds:0.#} s. Each line is: field number, schema name, value.{Environment.NewLine}" +
                    $"Proto3 does not send scalars that are 0/false, so such attributes are listed as \"not sent\".{Environment.NewLine}{Environment.NewLine}";
                File.WriteAllText(_path, header);
                _bytesWritten = header.Length;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                Log.Warn($"[{nameof(TelemetryDebugRecorder)}] Cannot start debug capture: {ex.Message}");
                return false;
            }

            _sentPaths.Clear();
            _schema = null;
            _lastWrite = DateTime.MinValue;
            Interlocked.Exchange(ref _framesSeen, 0);
            Interlocked.Exchange(ref _framesWritten, 0);
            _enabled = true;
        }

        Log.Info($"[{nameof(TelemetryDebugRecorder)}] Telemetry debug capture on, writing to {FilePath}");
        Changed?.Invoke();
        return true;
    }

    public void Disable()
    {
        lock (_gate)
        {
            if (!_enabled)
            {
                return;
            }

            _enabled = false;
            Append(BuildSummary());
        }

        Log.Info($"[{nameof(TelemetryDebugRecorder)}] Telemetry debug capture off ({FramesWritten} frames written to {FilePath})");
        Changed?.Invoke();
    }

    /// <summary>
    ///     Called for every received frame; cheap when debugging is off. Never throws.
    /// </summary>
    public void Record(byte[] frame, TelemetrySchema schema)
    {
        if (!_enabled)
        {
            return;
        }

        bool stop = false;
        try
        {
            lock (_gate)
            {
                if (!_enabled)
                {
                    return;
                }

                _schema = schema;
                Interlocked.Increment(ref _framesSeen);
                DateTime now = _clock();

                if (now - _lastWrite < Interval)
                {
                    // Frames in between still count for the "never sent" summary.
                    TelemetryRawFrameDumper.CollectSentPaths(schema.Response, frame, _sentPaths);
                    return;
                }

                _lastWrite = now;
                int index = Interlocked.Increment(ref _framesWritten);
                string dump = TelemetryRawFrameDumper.Dump(schema.Response, frame, _sentPaths);
                Append($"=== frame {index} (#{FramesSeen} received) at {now.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture)}, {frame.Length} bytes ==={Environment.NewLine}{dump}{Environment.NewLine}");

                if (_bytesWritten > MaxFileBytes)
                {
                    _enabled = false;
                    Append($"Stopped: the file reached {MaxFileBytes / 1024 / 1024} MB.{Environment.NewLine}{BuildSummary()}");
                    stop = true;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[{nameof(TelemetryDebugRecorder)}] Debug capture failed: {ex.Message}");
        }

        if (stop)
        {
            Changed?.Invoke();
        }
    }

    // Must be called with _gate held.
    private string BuildSummary()
    {
        StringBuilder text = new();
        text.AppendLine(CultureInfo.InvariantCulture, $"=== summary: {FramesSeen} frames received, {FramesWritten} written ===");
        if (_schema == null)
        {
            text.AppendLine("No frame was received. The game is not running, telemetry is not enabled, or nothing is listening on 127.0.0.1:2942.");
            return text.ToString();
        }

        List<string> never = _schema.Fields.Select(f => f.Path).Where(p => !_sentPaths.Contains(p)).ToList();
        text.AppendLine(CultureInfo.InvariantCulture, $"Schema source: {_schema.Source}");
        text.AppendLine(CultureInfo.InvariantCulture, $"{_schema.Fields.Count - never.Count} of {_schema.Fields.Count} attributes were sent at least once.");
        text.AppendLine(CultureInfo.InvariantCulture, $"Never sent ({never.Count}) - the game omitted them (value 0/false/empty) or does not fill them in:");
        foreach (string path in never)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"  {path}");
        }

        return text.ToString();
    }

    // Must be called with _gate held.
    private void Append(string text)
    {
        if (_path == null)
        {
            return;
        }

        try
        {
            File.AppendAllText(_path, text);
            _bytesWritten += text.Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"[{nameof(TelemetryDebugRecorder)}] Cannot write {_path}: {ex.Message}");
        }
    }
}
