namespace SCStreamDeck.Services.Telemetry;

public enum DecelerationRecordingPhase
{
    Idle,

    /// <summary>Waiting for the pilot to release the throttle (the speed starts falling).</summary>
    Armed,

    Recording
}

public enum DecelerationRunOutcome
{
    None,
    Saved,
    Discarded
}

/// <summary>What the recorder needs from one telemetry frame.</summary>
public readonly record struct DecelerationObservation(
    string ShipKey, double Speed, double Density, bool Boosting, bool Landed);

/// <summary>Current phase plus the outcome of the last run, for the key to show.</summary>
public readonly record struct DecelerationRecordingStatus(
    DecelerationRecordingPhase Phase, double Speed, DecelerationRunOutcome Outcome, string? Reason, DateTime OutcomeAt);

/// <summary>
///     Turns a stream of observations into one clean deceleration run. Armed: waits until the speed (at least
///     <see cref="MinStartSpeed" />) falls from its peak. Recording: keeps every sample while the speed only falls,
///     and finishes when it reaches <see cref="StopSpeed" />. The run is discarded when the speed rises again, the
///     ship boosts or lands, the ship changes, the speed stops falling for <see cref="StallTimeout" />, or the data
///     disappears: the throttle was not simply released, so the run would teach the wrong curve.
/// </summary>
public sealed class DecelerationRunRecorder
{
    public const double MinStartSpeed = 20;
    public const double StartDrop = 0.5;
    public const double StopSpeed = 1.5;
    public const double RiseTolerance = 2;
    public static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan ArmTimeout = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan MinRunDuration = TimeSpan.FromSeconds(1);

    private readonly object _gate = new();
    private readonly List<DecelerationSample> _samples = [];

    private DecelerationRecordingPhase _phase;
    private DateTime _armedAt;
    private DecelerationSample? _peak;
    private string _shipKey = string.Empty;
    private double _speed;
    private DecelerationRunOutcome _outcome;
    private string? _reason;
    private DateTime _outcomeAt;

    /// <summary>Raised (outside the lock) with the ship and the samples of a completed run.</summary>
    public event Action<string, IReadOnlyList<DecelerationSample>>? RunCompleted;

    public bool IsActive
    {
        get
        {
            lock (_gate)
            {
                return _phase != DecelerationRecordingPhase.Idle;
            }
        }
    }

    public DecelerationRecordingStatus Status(DateTime now)
    {
        lock (_gate)
        {
            ExpireLocked(now);
            return new DecelerationRecordingStatus(_phase, _speed, _outcome, _reason, _outcomeAt);
        }
    }

    public void Arm(DateTime now)
    {
        lock (_gate)
        {
            ResetRunLocked();
            _phase = DecelerationRecordingPhase.Armed;
            _armedAt = now;
            _outcome = DecelerationRunOutcome.None;
            _reason = null;
        }
    }

    public void Cancel(DateTime now)
    {
        lock (_gate)
        {
            if (_phase != DecelerationRecordingPhase.Idle)
            {
                DiscardLocked(now, "cancelled");
            }
        }
    }

    public void Observe(DateTime now, DecelerationObservation? observation)
    {
        (string Ship, IReadOnlyList<DecelerationSample> Samples)? completed = null;
        lock (_gate)
        {
            ExpireLocked(now);
            switch (_phase)
            {
                case DecelerationRecordingPhase.Armed:
                    ObserveArmedLocked(now, observation);
                    break;
                case DecelerationRecordingPhase.Recording:
                    completed = ObserveRecordingLocked(now, observation);
                    break;
            }
        }

        if (completed is { } run)
        {
            RunCompleted?.Invoke(run.Ship, run.Samples);
        }
    }

    private void ExpireLocked(DateTime now)
    {
        if (_phase == DecelerationRecordingPhase.Armed && now - _armedAt > ArmTimeout)
        {
            DiscardLocked(now, "timed out");
        }
        else if (_phase == DecelerationRecordingPhase.Recording && _samples.Count > 0 &&
                 now - _samples[^1].At > StallTimeout)
        {
            DiscardLocked(now, "speed stopped falling");
        }
    }

    private void ObserveArmedLocked(DateTime now, DecelerationObservation? observation)
    {
        if (observation is not { } obs || !IsUsable(obs) || obs.Boosting || obs.Landed)
        {
            _peak = null;
            return;
        }

        _speed = obs.Speed;
        DecelerationSample current = new(now, obs.Speed, obs.Density);
        if (_peak is not { } peak || obs.ShipKey != _shipKey || obs.Speed > peak.Speed)
        {
            _peak = current;
            _shipKey = obs.ShipKey;
            return;
        }

        if (peak.Speed >= MinStartSpeed && obs.Speed <= peak.Speed - StartDrop)
        {
            _samples.Clear();
            _samples.Add(peak);
            _samples.Add(current);
            _phase = DecelerationRecordingPhase.Recording;
        }
    }

    private (string, IReadOnlyList<DecelerationSample>)? ObserveRecordingLocked(
        DateTime now, DecelerationObservation? observation)
    {
        if (observation is not { } obs || !IsUsable(obs))
        {
            DiscardLocked(now, "no telemetry");
            return null;
        }

        _speed = obs.Speed;
        if (obs.Boosting || obs.Landed || obs.ShipKey != _shipKey)
        {
            DiscardLocked(now, "boost, landing or ship change");
            return null;
        }

        DecelerationSample last = _samples[^1];
        if (obs.Speed > last.Speed + RiseTolerance)
        {
            DiscardLocked(now, "speed increased");
            return null;
        }

        if (obs.Speed >= last.Speed)
        {
            return null;
        }

        _samples.Add(new DecelerationSample(now, obs.Speed, obs.Density));
        if (obs.Speed > StopSpeed)
        {
            return null;
        }

        if (now - _samples[0].At < MinRunDuration)
        {
            DiscardLocked(now, "too short");
            return null;
        }

        List<DecelerationSample> samples = [.. _samples];
        string ship = _shipKey;
        _outcome = DecelerationRunOutcome.Saved;
        _reason = null;
        _outcomeAt = now;
        ResetRunLocked();
        return (ship, samples);
    }

    private static bool IsUsable(DecelerationObservation obs) =>
        double.IsFinite(obs.Speed) && obs.Speed >= 0 && double.IsFinite(obs.Density) && !string.IsNullOrEmpty(obs.ShipKey);

    private void DiscardLocked(DateTime now, string reason)
    {
        ResetRunLocked();
        _outcome = DecelerationRunOutcome.Discarded;
        _reason = reason;
        _outcomeAt = now;
    }

    private void ResetRunLocked()
    {
        _phase = DecelerationRecordingPhase.Idle;
        _samples.Clear();
        _peak = null;
        _speed = 0;
    }
}
