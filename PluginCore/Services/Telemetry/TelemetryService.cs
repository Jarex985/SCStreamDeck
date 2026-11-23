using Sc.Game.Telemetry.V1;
using SCStreamDeck.Logging;

namespace SCStreamDeck.Services.Telemetry;

public enum TelemetryConnectionState
{
    Disconnected,
    Connecting,
    Streaming
}

/// <summary>
///     Keeps one telemetry stream open while at least one key is subscribed and exposes the latest merged frame.
///     Connects lazily and reconnects with backoff, because the game server is off until the game runs.
/// </summary>
public sealed class TelemetryService(ITelemetryStreamSource source) : IDisposable
{
    private static readonly TimeSpan s_defaultMinRetry = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan s_defaultMaxRetry = TimeSpan.FromSeconds(5);

    private readonly object _gate = new();
    private readonly object _stateGate = new();
    private readonly DateTime[] _partStamps = new DateTime[PartCount];
    private readonly ITelemetryStreamSource _source = source ?? throw new ArgumentNullException(nameof(source));

    private const int PartVehicle = 0;
    private const int PartWeapons = 1;
    private const int PartEvents = 2;
    private const int PartActor = 3;
    private const int PartForceFeedback = 4;
    private const int PartTurret = 5;
    private const int PartCount = 6;

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private int _subscribers;
    private volatile TelemetryStreamResponse? _snapshot;
    private volatile TelemetryConnectionState _state;

    public TimeSpan MinRetryDelay { get; init; } = s_defaultMinRetry;
    public TimeSpan MaxRetryDelay { get; init; } = s_defaultMaxRetry;

    /// <summary>A frame part not refreshed for this long is dropped (the game stops sending parts that no longer apply).</summary>
    public TimeSpan StaleAfter { get; init; } = TimeSpan.FromSeconds(3);

    public TimeSpan StaleCheckInterval { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Latest value of each frame type; null while nothing is being received.</summary>
    public TelemetryStreamResponse? Snapshot => _snapshot;

    public TelemetryConnectionState State => _state;

    public event Action? Updated;

    public void Dispose()
    {
        CancellationTokenSource? cts;
        lock (_gate)
        {
            _subscribers = 0;
            cts = _loopCts;
            _loopCts = null;
            _loopTask = null;
        }

        cts?.Cancel();
    }

    /// <summary>
    ///     Registers a handler and starts streaming if needed. Dispose the result to unsubscribe.
    /// </summary>
    public IDisposable Subscribe(Action onUpdated)
    {
        ArgumentNullException.ThrowIfNull(onUpdated);

        Updated += onUpdated;
        lock (_gate)
        {
            _subscribers++;
            if (_loopCts == null)
            {
                _loopCts = new CancellationTokenSource();
                _loopTask = Task.Run(() => RunAsync(_loopCts.Token));
            }
        }

        return new Subscription(this, onUpdated);
    }

    /// <summary>
    ///     Completes when the stream loop has stopped. Intended for tests.
    /// </summary>
    internal Task? LoopTask
    {
        get
        {
            lock (_gate)
            {
                return _loopTask;
            }
        }
    }

    private void Unsubscribe(Action onUpdated)
    {
        Updated -= onUpdated;

        CancellationTokenSource? cts = null;
        lock (_gate)
        {
            if (_subscribers > 0)
            {
                _subscribers--;
            }

            if (_subscribers == 0)
            {
                cts = _loopCts;
                _loopCts = null;
            }
        }

        if (cts == null)
        {
            return;
        }

        // Not disposed on purpose: the loop may still be observing its token.
        cts.Cancel();
    }

    private async Task RunAsync(CancellationToken token)
    {
        TimeSpan delay = MinRetryDelay;
        _ = ExpireLoopAsync(token);

        while (!token.IsCancellationRequested)
        {
            SetState(TelemetryConnectionState.Connecting);
            try
            {
                await foreach (TelemetryStreamResponse frame in _source.StreamAsync(token).ConfigureAwait(false))
                {
                    delay = MinRetryDelay;
                    ApplyFrame(frame, DateTime.UtcNow);
                    _state = TelemetryConnectionState.Streaming;
                    RaiseUpdated();
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Debug($"[{nameof(TelemetryService)}] Stream unavailable: {ex.GetType().Name}: {ex.Message}");
            }

            _snapshot = null;
            SetState(TelemetryConnectionState.Disconnected);

            try
            {
                await Task.Delay(delay, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            delay = TimeSpan.FromTicks(Math.Min(MaxRetryDelay.Ticks, delay.Ticks * 2));
        }

        lock (_gate)
        {
            // A newer loop may already own the state after a quick unsubscribe/subscribe.
            if (_loopCts == null || _loopCts.Token == token)
            {
                _snapshot = null;
                _state = TelemetryConnectionState.Disconnected;
            }
        }
    }

    /// <summary>
    ///     A frame may only carry some of its parts and the game stops sending a part when it no longer applies
    ///     (e.g. the vehicle part after leaving a seat). Keep the last value of each part until it goes stale.
    /// </summary>
    internal void ApplyFrame(TelemetryStreamResponse frame, DateTime now)
    {
        lock (_stateGate)
        {
            TelemetryStreamResponse? previous = _snapshot;
            _snapshot = new TelemetryStreamResponse
            {
                Vehicle = Pick(frame.Vehicle, previous?.Vehicle, PartVehicle, now),
                Weapons = Pick(frame.Weapons, previous?.Weapons, PartWeapons, now),
                Events = Pick(frame.Events, previous?.Events, PartEvents, now),
                Actor = Pick(frame.Actor, previous?.Actor, PartActor, now),
                ForceFeedback = Pick(frame.ForceFeedback, previous?.ForceFeedback, PartForceFeedback, now),
                Turret = Pick(frame.Turret, previous?.Turret, PartTurret, now)
            };
        }
    }

    /// <summary>
    ///     Drops parts that have not been received for <see cref="StaleAfter" />. Returns true if anything changed.
    /// </summary>
    internal bool ExpireStale(DateTime now)
    {
        lock (_stateGate)
        {
            TelemetryStreamResponse? current = _snapshot;
            if (current == null)
            {
                return false;
            }

            TelemetryStreamResponse next = new()
            {
                Vehicle = Fresh(current.Vehicle, PartVehicle, now),
                Weapons = Fresh(current.Weapons, PartWeapons, now),
                Events = Fresh(current.Events, PartEvents, now),
                Actor = Fresh(current.Actor, PartActor, now),
                ForceFeedback = Fresh(current.ForceFeedback, PartForceFeedback, now),
                Turret = Fresh(current.Turret, PartTurret, now)
            };

            bool changed = !ReferenceEquals(next.Vehicle, current.Vehicle) || !ReferenceEquals(next.Weapons, current.Weapons) ||
                           !ReferenceEquals(next.Events, current.Events) || !ReferenceEquals(next.Actor, current.Actor) ||
                           !ReferenceEquals(next.ForceFeedback, current.ForceFeedback) ||
                           !ReferenceEquals(next.Turret, current.Turret);
            if (!changed)
            {
                return false;
            }

            _snapshot = next;
            return true;
        }
    }

    private T? Pick<T>(T? fromFrame, T? previous, int part, DateTime now) where T : class
    {
        if (fromFrame == null)
        {
            return previous;
        }

        _partStamps[part] = now;
        return fromFrame;
    }

    private T? Fresh<T>(T? value, int part, DateTime now) where T : class =>
        value != null && now - _partStamps[part] < StaleAfter ? value : null;

    private async Task ExpireLoopAsync(CancellationToken token)
    {
        using PeriodicTimer timer = new(StaleCheckInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                if (ExpireStale(DateTime.UtcNow))
                {
                    RaiseUpdated();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Stream loop stopped.
        }
    }

    private void SetState(TelemetryConnectionState state)
    {
        if (_state == state)
        {
            return;
        }

        _state = state;
        RaiseUpdated();
    }

    private void RaiseUpdated()
    {
        try
        {
            Updated?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Err($"[{nameof(TelemetryService)}] Update handler failed: {ex.Message}", ex);
        }
    }

    private sealed class Subscription(TelemetryService owner, Action handler) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Unsubscribe(handler);
            }
        }
    }
}
