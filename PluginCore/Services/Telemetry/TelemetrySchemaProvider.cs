using Google.Protobuf;
using Google.Protobuf.Reflection;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Core;
using SCStreamDeck.Services.Installation;

namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     Supplies the telemetry schema in use. Starts with the bundled proto and switches to the definition read from
///     the installed game once available.
/// </summary>
public interface ITelemetrySchemaProvider
{
    TelemetrySchema Current { get; }

    /// <summary>Raised when <see cref="Current" /> was replaced.</summary>
    event Action? Changed;

    /// <summary>Starts the (one-time, background) read of the game's definition; safe to call repeatedly.</summary>
    void EnsureRefreshStarted();
}

public sealed class TelemetrySchemaProvider(StateService stateService, PathProviderService pathProvider)
    : ITelemetrySchemaProvider
{
    private const string ExeRelativePath = @"Bin64\StarCitizen.exe";
    private const string CachePrefix = "telemetry-schema-";

    private readonly PathProviderService _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
    private readonly StateService _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));

    private volatile TelemetrySchema _current = TelemetrySchema.Bundled;
    private int _started;

    public TelemetrySchema Current => _current;

    public event Action? Changed;

    public void EnsureRefreshStarted()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
        {
            _ = Task.Run(RefreshAsync);
        }
    }

    private async Task RefreshAsync()
    {
        try
        {
            string? exePath = await FindExecutableAsync().ConfigureAwait(false);
            if (exePath == null)
            {
                Log.Info($"[{nameof(TelemetrySchemaProvider)}] No StarCitizen.exe found; using the bundled telemetry proto.");
                return;
            }

            TelemetrySchema? schema = LoadFromCacheOrExecutable(exePath);
            if (schema == null)
            {
                Log.Warn($"[{nameof(TelemetrySchemaProvider)}] Could not read the telemetry protocol from '{exePath}'; using the bundled proto.");
                return;
            }

            _current = schema;
            Log.Info($"[{nameof(TelemetrySchemaProvider)}] Using telemetry protocol from {schema.Source} ({schema.Fields.Count} attributes).");
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Err($"[{nameof(TelemetrySchemaProvider)}] Telemetry schema refresh failed: {ex.Message}", ex);
        }
    }

    private async Task<string?> FindExecutableAsync()
    {
        PluginState? state = await _stateService.LoadStateAsync().ConfigureAwait(false);
        if (state == null)
        {
            return null;
        }

        // Prefer the channel the user plays, then any other known installation.
        IEnumerable<InstallationState?> candidates =
        [
            state.GetInstallation(state.SelectedChannel),
            state.LiveInstallation, state.HotfixInstallation, state.PtuInstallation, state.EptuInstallation
        ];

        foreach (InstallationState? installation in candidates)
        {
            if (installation == null)
            {
                continue;
            }

            string exe = Path.Combine(installation.ChannelPath, ExeRelativePath);
            if (File.Exists(exe))
            {
                return exe;
            }
        }

        return null;
    }

    private TelemetrySchema? LoadFromCacheOrExecutable(string exePath)
    {
        FileInfo exe = new(exePath);
        string cacheName = $"{CachePrefix}{exe.Length}-{exe.LastWriteTimeUtc.Ticks}.bin";
        string cachePath = _pathProvider.GetSecureCachePath(cacheName);

        if (File.Exists(cachePath) && TryReadCache(cachePath, exePath, out TelemetrySchema? cached))
        {
            return cached;
        }

        IReadOnlyList<FileDescriptorProto> protos = ProtoDescriptorExtractor.ExtractFromFile(exePath);
        if (!TryBuild(protos, exePath, out TelemetrySchema? schema))
        {
            return null;
        }

        WriteCache(cachePath, protos);
        return schema;
    }

    private static bool TryReadCache(string cachePath, string exePath, out TelemetrySchema? schema)
    {
        schema = null;
        try
        {
            return TryBuild(FileDescriptorSet.Parser.ParseFrom(File.ReadAllBytes(cachePath)).File,
                $"cache of {exePath}", out schema);
        }
        catch (Exception ex) when (ex is InvalidProtocolBufferException or IOException or UnauthorizedAccessException)
        {
            Log.Warn($"[{nameof(TelemetrySchemaProvider)}] Ignoring unreadable schema cache: {ex.Message}");
            return false;
        }
    }

    private static bool TryBuild(IEnumerable<FileDescriptorProto> protos, string source, out TelemetrySchema? schema)
    {
        schema = null;
        List<FileDescriptorProto> list = protos.ToList();
        if (list.Count == 0)
        {
            return false;
        }

        try
        {
            return TelemetrySchema.TryCreate(ProtoDescriptorExtractor.Build(list), source, out schema);
        }
        catch (Exception ex) when (ex is InvalidProtocolBufferException or ArgumentException or InvalidOperationException
                                       or DescriptorValidationException)
        {
            Log.Warn($"[{nameof(TelemetrySchemaProvider)}] Extracted definitions are unusable: {ex.Message}");
            return false;
        }
    }

    private void WriteCache(string cachePath, IReadOnlyList<FileDescriptorProto> protos)
    {
        try
        {
            _pathProvider.EnsureDirectoriesExist();
            FileDescriptorSet set = new();
            set.File.AddRange(protos);
            File.WriteAllBytes(cachePath, set.ToByteArray());

            // One cache per exe version is enough; drop stale ones after a patch.
            foreach (string old in Directory.EnumerateFiles(_pathProvider.CacheDirectory, $"{CachePrefix}*.bin"))
            {
                if (!string.Equals(old, cachePath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(old);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"[{nameof(TelemetrySchemaProvider)}] Could not cache the telemetry schema: {ex.Message}");
        }
    }
}
