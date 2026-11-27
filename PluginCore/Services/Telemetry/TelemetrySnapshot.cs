namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     Decoded telemetry: for each top-level part of a frame (actor, vehicle, ...) a flat map of attribute path to value.
///     Parts the game did not send are absent, which is how "no data" is represented.
/// </summary>
public sealed class TelemetrySnapshot(IReadOnlyDictionary<string, IReadOnlyDictionary<string, TelemetryValue>> parts)
{
    public static TelemetrySnapshot Empty { get; } =
        new(new Dictionary<string, IReadOnlyDictionary<string, TelemetryValue>>());

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, TelemetryValue>> Parts { get; } =
        parts ?? throw new ArgumentNullException(nameof(parts));

    public bool HasPart(string name) => Parts.ContainsKey(name);

    /// <summary>
    ///     A copy with one more (or replaced) part.
    /// </summary>
    public TelemetrySnapshot WithPart(string name, IReadOnlyDictionary<string, TelemetryValue> values)
    {
        Dictionary<string, IReadOnlyDictionary<string, TelemetryValue>> copy = new(Parts, StringComparer.Ordinal)
        {
            [name] = values
        };
        return new TelemetrySnapshot(copy);
    }

    /// <summary>
    ///     Reads the value of <paramref name="path" /> (for example <c>actor.vitals.pressure</c>). False when the part
    ///     or the attribute is absent.
    /// </summary>
    public bool TryGetValue(string? path, out TelemetryValue value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        int dot = path.IndexOf('.', StringComparison.Ordinal);
        return dot > 0 && Parts.TryGetValue(path[..dot], out IReadOnlyDictionary<string, TelemetryValue>? part) &&
               part.TryGetValue(path, out value);
    }
}
