namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     Which part of a key blinks while a rule flashes.
/// </summary>
public enum TelemetryFlashTarget
{
    /// <summary>Only the text blinks; background color and image stay.</summary>
    Text,

    /// <summary>Background color and image blink (black while off); the text stays.</summary>
    BackgroundAndImage,

    /// <summary>Text, image and background all blink (black while off).</summary>
    Everything
}

/// <summary>
///     Flash timing: shown for N ms, hidden for N ms, repeating from the moment the rule became active.
/// </summary>
public static class TelemetryFlash
{
    public const string TargetText = "text";
    public const string TargetBackground = "background";
    public const string TargetEverything = "everything";

    /// <summary>Parses a stored flash target; empty or unknown values mean <see cref="TelemetryFlashTarget.Text" />.</summary>
    public static TelemetryFlashTarget ParseTarget(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            TargetBackground => TelemetryFlashTarget.BackgroundAndImage,
            TargetEverything => TelemetryFlashTarget.Everything,
            _ => TelemetryFlashTarget.Text
        };

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
