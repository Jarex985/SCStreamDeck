namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     The UEC balance and how much it changed since the session started (or was last reset).
/// </summary>
public readonly record struct UecReading(long Balance, long Change);

/// <summary>
///     Remembers the UEC balance a session started with, so keys can show the amount earned or lost. Shared by all
///     UEC keys, so changing pages or profiles does not lose the session. When the balance was unavailable for
///     longer than <see cref="SessionGap" /> (the game was closed or restarted) the next balance starts a new session.
/// </summary>
public sealed class UecSessionTracker
{
    public static readonly TimeSpan SessionGap = TimeSpan.FromSeconds(60);

    private readonly object _gate = new();
    private long? _baseline;
    private long? _last;
    private DateTime _lastSeenUtc;

    public UecReading? Observe(long? balance, DateTime nowUtc)
    {
        if (balance is not { } current)
        {
            return null;
        }

        lock (_gate)
        {
            if (_last != null && nowUtc - _lastSeenUtc > SessionGap)
            {
                _baseline = null;
            }

            _baseline ??= current;
            _last = current;
            _lastSeenUtc = nowUtc;
            return new UecReading(current, current - _baseline.Value);
        }
    }

    /// <summary>
    ///     Starts counting from the latest known balance.
    /// </summary>
    public void Reset()
    {
        lock (_gate)
        {
            _baseline = _last;
        }
    }
}
