using Newtonsoft.Json;
using SCStreamDeck.Logging;

namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     Learns how each ship decelerates and estimates the stopping distance and time from it. Runs are recorded
///     while armed (by the Deceleration key); the profiles are kept in a file outside the plugin folder, which
///     Stream Deck replaces when the plugin is installed again. While a run is being recorded it listens to the
///     telemetry itself, so it keeps working when the key is not on the visible page.
/// </summary>
public sealed class DecelerationProfileService : IDisposable
{
    public const string SpeedPath = "vehicle.state.flight.instruments.speed";
    public const string DensityPath = "vehicle.state.flight.instruments.atmospheric_density";
    public const string BoostPath = "vehicle.state.flight.instruments.boost.engaged";
    public const string LandedPath = "vehicle.state.flight.state.is_landed";
    public const string ClassPath = "vehicle.state.class_name";
    public const string MasterModePath = "vehicle.state.master_mode.mode";
    public const string BackupFileName = "deceleration-profiles.json";

    private const int FormatVersion = 1;

    private readonly object _gate = new();
    private readonly TelemetryService _telemetry;
    private readonly Func<string> _path;
    private readonly DecelerationRunRecorder _recorder = new();
    private Dictionary<string, DecelerationProfile>? _profiles;
    private IDisposable? _subscription;

    public DecelerationProfileService(TelemetryService telemetry) : this(telemetry, DefaultPath)
    {
    }

    internal DecelerationProfileService(TelemetryService telemetry, Func<string> path)
    {
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _recorder.RunCompleted += OnRunCompleted;
    }

    public static string DefaultPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SCStreamDeck", BackupFileName);

    /// <summary>Raised when a run was saved into a profile.</summary>
    public event Action? ProfilesChanged;

    public void Dispose()
    {
        lock (_gate)
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }

    /// <summary>The profile of a ship is kept per class and master mode (SCM and NAV decelerate differently).</summary>
    public static string ShipKey(TelemetrySnapshot? snapshot)
    {
        if (snapshot == null || !snapshot.TryGetValue(ClassPath, out TelemetryValue ship) || string.IsNullOrWhiteSpace(ship.Text))
        {
            return string.Empty;
        }

        string mode = snapshot.TryGetValue(MasterModePath, out TelemetryValue read) ? read.Text : string.Empty;
        return $"{ship.Text}|{mode}";
    }

    public DecelerationRecordingStatus Status(DateTime nowUtc) => _recorder.Status(nowUtc);

    public int RunCount(string shipKey) => Profile(shipKey, create: false)?.Runs ?? 0;

    public ProfileEstimate? Estimate(string shipKey, double speed, double density) =>
        string.IsNullOrEmpty(shipKey) ? null : Profile(shipKey, create: false)?.Estimate(speed, density);

    /// <summary>Starts waiting for the throttle to be released.</summary>
    public void Arm(DateTime nowUtc)
    {
        lock (_gate)
        {
            _recorder.Arm(nowUtc);
            _subscription ??= _telemetry.Subscribe(OnTelemetryUpdated);
        }
    }

    public void Cancel(DateTime nowUtc)
    {
        lock (_gate)
        {
            _recorder.Cancel(nowUtc);
            StopListeningIfIdleLocked();
        }
    }

    internal void Observe(DateTime nowUtc, DecelerationObservation? observation)
    {
        _recorder.Observe(nowUtc, observation);
        lock (_gate)
        {
            StopListeningIfIdleLocked();
        }
    }

    internal void AddRun(string shipKey, IReadOnlyList<DecelerationSample> samples)
    {
        DecelerationProfile? profile = Profile(shipKey, create: true);
        if (profile != null && profile.AddRun(samples))
        {
            Save();
            ProfilesChanged?.Invoke();
        }
    }

    private void OnRunCompleted(string shipKey, IReadOnlyList<DecelerationSample> samples) => AddRun(shipKey, samples);

    private void OnTelemetryUpdated() => Observe(DateTime.UtcNow, Read(_telemetry.Snapshot));

    private void StopListeningIfIdleLocked()
    {
        if (!_recorder.IsActive)
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }

    private static DecelerationObservation? Read(TelemetrySnapshot? snapshot)
    {
        if (snapshot == null || !snapshot.TryGetValue(SpeedPath, out TelemetryValue speed) ||
            speed.Kind != TelemetryValueKind.Number)
        {
            return null;
        }

        double density = snapshot.TryGetValue(DensityPath, out TelemetryValue d) && d.Kind == TelemetryValueKind.Number
            ? d.Number
            : 0;
        bool boosting = snapshot.TryGetValue(BoostPath, out TelemetryValue b) && b.Boolean;
        bool landed = snapshot.TryGetValue(LandedPath, out TelemetryValue l) && l.Boolean;
        return new DecelerationObservation(ShipKey(snapshot), speed.Number, density, boosting, landed);
    }

    private DecelerationProfile? Profile(string shipKey, bool create)
    {
        if (string.IsNullOrEmpty(shipKey))
        {
            return null;
        }

        lock (_gate)
        {
            _profiles ??= Load();
            if (_profiles.TryGetValue(shipKey, out DecelerationProfile? profile))
            {
                return profile;
            }

            if (!create)
            {
                return null;
            }

            return _profiles[shipKey] = new DecelerationProfile();
        }
    }

    private Dictionary<string, DecelerationProfile> Load()
    {
        try
        {
            string path = _path();
            if (File.Exists(path) &&
                JsonConvert.DeserializeObject<ProfileFile>(File.ReadAllText(path)) is { Ships: { } ships })
            {
                return new Dictionary<string, DecelerationProfile>(ships, StringComparer.Ordinal);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Warn($"[{nameof(DecelerationProfileService)}] Cannot read the deceleration profiles: {ex.Message}");
        }

        return new Dictionary<string, DecelerationProfile>(StringComparer.Ordinal);
    }

    private void Save()
    {
        try
        {
            string json;
            lock (_gate)
            {
                json = JsonConvert.SerializeObject(new ProfileFile { Ships = _profiles ?? [] }, Formatting.Indented);
            }

            string path = _path();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp = path + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Log.Warn($"[{nameof(DecelerationProfileService)}] Cannot save the deceleration profiles: {ex.Message}");
        }
    }

    private sealed class ProfileFile
    {
        [JsonProperty("version")] public int Version { get; set; } = FormatVersion;

        [JsonProperty("ships")] public Dictionary<string, DecelerationProfile>? Ships { get; set; }
    }
}
