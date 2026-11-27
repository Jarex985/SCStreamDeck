namespace SCStreamDeck.Services.Telemetry;

public enum TravelState
{
    Idle,
    Running,
    Stopped
}

/// <summary>
///     Elapsed time and estimated distance (meters) of a travel measurement.
/// </summary>
public readonly record struct TravelReading(TravelState State, TimeSpan Elapsed, double Meters);

/// <summary>
///     Measures how long something takes and how far the ship travelled meanwhile. The distance is the speed
///     integrated over time (trapezoid rule: the average of two consecutive speed samples times the time between
///     them), which is exact for a constant acceleration and follows curves and turns, because the speed is a
///     magnitude. While running it listens to the telemetry itself, so it keeps counting when the keys are not on
///     the visible page. Shared by all travel keys; pressing start again after a stop begins a new measurement.
/// </summary>
public sealed class TravelTracker(TelemetryService telemetry) : IDisposable
{
    public const string SpeedPath = "vehicle.state.flight.instruments.speed";

    /// <summary>Samples further apart than this (no speed received meanwhile) are not bridged.</summary>
    public static readonly TimeSpan MaxSampleGap = TimeSpan.FromSeconds(5);

    private readonly object _gate = new();
    private readonly TelemetryService _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));

    private TravelState _state;
    private DateTime _startUtc;
    private DateTime _stopUtc;
    private double _meters;
    private (DateTime At, double Speed)? _previous;
    private IDisposable? _subscription;

    public void Dispose()
    {
        lock (_gate)
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }

    public TravelReading Read(DateTime nowUtc)
    {
        lock (_gate)
        {
            return ReadLocked(nowUtc);
        }
    }

    /// <summary>
    ///     Starts a new measurement (also after a stop), or stops the running one and freezes its values.
    /// </summary>
    public TravelReading Toggle(DateTime nowUtc)
    {
        return Toggle(nowUtc, CurrentSpeed());
    }

    internal TravelReading Toggle(DateTime nowUtc, double? speed)
    {
        lock (_gate)
        {
            if (_state == TravelState.Running)
            {
                ObserveLocked(speed, nowUtc);
                _stopUtc = nowUtc;
                _state = TravelState.Stopped;
                _previous = null;
                _subscription?.Dispose();
                _subscription = null;
            }
            else
            {
                _state = TravelState.Running;
                _startUtc = nowUtc;
                _meters = 0;
                _previous = null;
                ObserveLocked(speed, nowUtc);
                _subscription ??= _telemetry.Subscribe(OnTelemetryUpdated);
            }

            return ReadLocked(nowUtc);
        }
    }

    /// <summary>
    ///     Records the current speed (null = unknown, which ends the current stretch) and adds the distance covered
    ///     since the previous sample.
    /// </summary>
    internal void Observe(double? speed, DateTime nowUtc)
    {
        lock (_gate)
        {
            ObserveLocked(speed, nowUtc);
        }
    }

    private void OnTelemetryUpdated()
    {
        Observe(CurrentSpeed(), DateTime.UtcNow);
    }

    private double? CurrentSpeed() =>
        _telemetry.Snapshot is { } snapshot &&
        snapshot.TryGetValue(SpeedPath, out TelemetryValue read) &&
        read.Kind == TelemetryValueKind.Number
            ? read.Number
            : null;

    private void ObserveLocked(double? speed, DateTime nowUtc)
    {
        if (_state != TravelState.Running)
        {
            return;
        }

        if (speed is not { } current || !double.IsFinite(current))
        {
            _previous = null;
            return;
        }

        current = Math.Max(0, current);
        if (_previous is { } previous)
        {
            // A zero or negative gap (clock moved back) adds nothing; the sample just becomes the new reference.
            TimeSpan gap = nowUtc - previous.At;
            if (gap > TimeSpan.Zero && gap <= MaxSampleGap)
            {
                _meters += (previous.Speed + current) / 2 * gap.TotalSeconds;
            }
        }

        _previous = (nowUtc, current);
    }

    private TravelReading ReadLocked(DateTime nowUtc)
    {
        if (_state == TravelState.Idle)
        {
            return new TravelReading(TravelState.Idle, TimeSpan.Zero, 0);
        }

        DateTime end = _state == TravelState.Running ? nowUtc : _stopUtc;
        TimeSpan elapsed = end - _startUtc;
        return new TravelReading(_state, elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed, _meters);
    }
}
