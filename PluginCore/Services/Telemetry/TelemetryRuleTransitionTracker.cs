namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     Detects when a different explicit rule becomes active so its sound can be played once.
///     The first evaluation after construction or <see cref="Reset" /> only records the state (no sound), so
///     page switches and settings edits do not replay sounds.
/// </summary>
public sealed class TelemetryRuleTransitionTracker
{
    private readonly object _gate = new();
    private bool _hasBaseline;
    private int _activeIndex = -1;

    public void Reset()
    {
        lock (_gate)
        {
            _hasBaseline = false;
        }
    }

    /// <summary>
    ///     Returns the sound to play when an explicit rule with data just became active; otherwise null.
    /// </summary>
    public string? OnEvaluated(TelemetryRuleMatch match)
    {
        lock (_gate)
        {
            int previous = _activeIndex;
            bool hadBaseline = _hasBaseline;
            _activeIndex = match.Index;
            _hasBaseline = true;

            if (!hadBaseline || previous == match.Index || match.Index < 0 || !match.HasData)
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(match.Rule.SoundPath) ? null : match.Rule.SoundPath;
        }
    }
}
