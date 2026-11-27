using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     How a press action changes a flag.
/// </summary>
public static class TelemetryFlagMode
{
    public const string On = "on";
    public const string Off = "off";
    public const string Toggle = "toggle";
}

/// <summary>
///     A named on/off switch created on the Config key. Rules read it as the attribute <c>flag.&lt;id&gt;</c> and can
///     change it; the plugin keeps its value while it runs.
/// </summary>
public sealed class TelemetryFlag
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;

    [JsonProperty("name")] public string? Name { get; set; }

    /// <summary>Value of the flag when the plugin starts or the flag is created.</summary>
    [JsonProperty("defaultValue")] public bool DefaultValue { get; set; }
}

/// <summary>
///     Part of a rule: set the flag <see cref="Flag" /> (an id) to <see cref="Value" /> when the rule becomes active.
/// </summary>
public sealed class TelemetryFlagAssignment
{
    [JsonProperty("flag")] public string? Flag { get; set; }

    [JsonProperty("value")] public bool Value { get; set; } = true;
}

/// <summary>
///     Holds the flags shared by all telemetry keys: their definitions (stored in the plugin's global settings and
///     edited on the Config key) and their current values. Flags appear to rules as the boolean attributes
///     <c>flag.&lt;id&gt;</c>.
/// </summary>
public sealed class TelemetryFlagService
{
    public const string GlobalSettingsKey = "flags";
    public const string BackupFileName = "telemetry-flags.json";
    public const string PathPrefix = "flag.";
    private const string Part = "flag";

    private readonly object _gate = new();
    private readonly GlobalSettingsListBackup _backup;
    private Dictionary<string, TelemetryFlag> _flags = new(StringComparer.Ordinal);
    private Dictionary<string, bool> _values = new(StringComparer.Ordinal);

    public TelemetryFlagService() : this(DefaultBackupPath)
    {
    }

    internal TelemetryFlagService(Func<string> backupPath)
    {
        ArgumentNullException.ThrowIfNull(backupPath);
        _backup = new GlobalSettingsListBackup(GlobalSettingsKey, "flag(s)", backupPath);
    }

    /// <summary>Raised when a flag value changed or a flag was added or removed.</summary>
    public event Action? Changed;

    public IReadOnlyCollection<TelemetryFlag> Flags
    {
        get
        {
            lock (_gate)
            {
                return _flags.Values.ToList();
            }
        }
    }

    public static string DefaultBackupPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SCStreamDeck", BackupFileName);

    public static string PathFor(string id) => PathPrefix + id;

    /// <summary>
    ///     Applies the plugin's global settings, keeping a copy outside the plugin folder and restoring it when Stream
    ///     Deck lost the list (see <see cref="GlobalSettingsListBackup" />). Returns the global settings to write back
    ///     after a restore, otherwise null.
    /// </summary>
    public JObject? ApplyGlobalSettings(JObject? globalSettings) =>
        _backup.Apply(globalSettings, settings => Update(Parse(settings)));

    public static IReadOnlyList<TelemetryFlag> Parse(JObject? globalSettings)
    {
        if (globalSettings?[GlobalSettingsKey] is not JArray array)
        {
            return [];
        }

        List<TelemetryFlag> result = [];
        foreach (JToken token in array)
        {
            try
            {
                if (token.ToObject<TelemetryFlag>() is { Id.Length: > 0 } flag)
                {
                    result.Add(flag);
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidCastException or FormatException)
            {
                // Skip a malformed entry rather than losing all flags.
            }
        }

        return result;
    }

    /// <summary>
    ///     Replaces the known flags. A new flag starts at its default value, a known one keeps its current value.
    /// </summary>
    public void Update(IEnumerable<TelemetryFlag>? flags)
    {
        bool changed;
        lock (_gate)
        {
            Dictionary<string, TelemetryFlag> next = new(StringComparer.Ordinal);
            foreach (TelemetryFlag flag in flags ?? [])
            {
                next[flag.Id] = flag;
            }

            Dictionary<string, bool> values = new(StringComparer.Ordinal);
            foreach ((string id, TelemetryFlag flag) in next)
            {
                values[id] = _values.TryGetValue(id, out bool current) ? current : flag.DefaultValue;
            }

            changed = _values.Count != values.Count ||
                      values.Any(pair => !_values.TryGetValue(pair.Key, out bool old) || old != pair.Value);
            _flags = next;
            _values = values;
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    public bool IsSet(string? id)
    {
        lock (_gate)
        {
            return id != null && _values.TryGetValue(id, out bool value) && value;
        }
    }

    /// <summary>
    ///     Sets a known flag. Returns whether its value changed.
    /// </summary>
    public bool Set(string? id, bool value)
    {
        lock (_gate)
        {
            if (!ChangeLocked(id, _ => value))
            {
                return false;
            }
        }

        Changed?.Invoke();
        return true;
    }

    public bool Toggle(string? id)
    {
        lock (_gate)
        {
            if (!ChangeLocked(id, current => !current))
            {
                return false;
            }
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>
    ///     Applies the flag assignments of a rule that just became active. Returns whether any value changed.
    /// </summary>
    public bool Apply(IEnumerable<TelemetryFlagAssignment>? assignments)
    {
        bool changed = false;
        lock (_gate)
        {
            foreach (TelemetryFlagAssignment assignment in assignments ?? [])
            {
                changed |= assignment != null && ChangeLocked(assignment.Flag, _ => assignment.Value);
            }
        }

        if (changed)
        {
            Changed?.Invoke();
        }

        return changed;
    }

    /// <summary>
    ///     Runs a press action that targets a flag. Returns whether the value changed.
    /// </summary>
    public bool Apply(TelemetryPressAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return action.FlagMode?.Trim().ToLowerInvariant() switch
        {
            TelemetryFlagMode.On => Set(action.Flag, true),
            TelemetryFlagMode.Off => Set(action.Flag, false),
            _ => Toggle(action.Flag)
        };
    }

    /// <summary>
    ///     The snapshot with the current flag values added as the part <c>flag</c>, so rules read them like any
    ///     attribute. Without telemetry the result only contains the flags (and stays null when there are no flags).
    /// </summary>
    public TelemetrySnapshot? Overlay(TelemetrySnapshot? snapshot)
    {
        Dictionary<string, TelemetryValue> values;
        lock (_gate)
        {
            if (_values.Count == 0)
            {
                return snapshot;
            }

            values = _values.ToDictionary(
                pair => PathFor(pair.Key),
                pair => new TelemetryValue(TelemetryValueKind.Boolean, Boolean: pair.Value),
                StringComparer.Ordinal);
        }

        return (snapshot ?? TelemetrySnapshot.Empty).WithPart(Part, values);
    }

    // Only flags that exist can change, so rules that refer to a deleted flag do nothing.
    private bool ChangeLocked(string? id, Func<bool, bool> next)
    {
        if (id == null || !_values.TryGetValue(id, out bool current))
        {
            return false;
        }

        bool value = next(current);
        if (value == current)
        {
            return false;
        }

        _values[id] = value;
        return true;
    }
}
