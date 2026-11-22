namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     Flash timing: shown for N ms, hidden for N ms, repeating from the moment the rule became active.
/// </summary>
public static class TelemetryFlash
{
    /// <summary>Stream Deck keys cannot redraw faster than this reliably.</summary>
    public const int MinIntervalMs = 250;

    public const int MaxIntervalMs = 60_000;

    /// <summary>Returns the effective interval in ms, or 0 when flashing is off.</summary>
    public static int Normalize(int? flashMs) =>
        flashMs is > 0 ? Math.Clamp(flashMs.Value, MinIntervalMs, MaxIntervalMs) : 0;

    /// <summary>
    ///     Whether the content is hidden after <paramref name="elapsed" />, and how long until the next switch.
    /// </summary>
    public static bool IsHidden(TimeSpan elapsed, int intervalMs, out TimeSpan untilNextSwitch)
    {
        long elapsedMs = Math.Max(0, (long)elapsed.TotalMilliseconds);
        untilNextSwitch = TimeSpan.FromMilliseconds(intervalMs - (elapsedMs % intervalMs));
        return (elapsedMs / intervalMs) % 2 == 1;
    }
}
