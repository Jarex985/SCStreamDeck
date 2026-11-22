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
    private readonly ITelemetryStreamSource _source = source ?? throw new ArgumentNullException(nameof(source));

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private int _subscribers;
    private volatile TelemetryStreamResponse? _snapshot;
    private volatile TelemetryConnectionState _state;

    public TimeSpan MinRetryDelay { get; init; } = s_defaultMinRetry;
    public TimeSpan MaxRetryDelay { get; init; } = s_defaultMaxRetry;

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

        while (!token.IsCancellationRequested)
        {
            SetState(TelemetryConnectionState.Connecting);
            try
            {
                await foreach (TelemetryStreamResponse frame in _source.StreamAsync(token).ConfigureAwait(false))
                {
                    delay = MinRetryDelay;
                    _snapshot = Merge(_snapshot, frame);
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
    ///     A frame may only carry some of its parts; keep the last known value of the others.
    /// </summary>
    internal static TelemetryStreamResponse Merge(TelemetryStreamResponse? previous, TelemetryStreamResponse frame) =>
        new()
        {
            Vehicle = frame.Vehicle ?? previous?.Vehicle,
            Weapons = frame.Weapons ?? previous?.Weapons,
            Events = frame.Events ?? previous?.Events,
            Actor = frame.Actor ?? previous?.Actor,
            ForceFeedback = frame.ForceFeedback ?? previous?.ForceFeedback,
            Turret = frame.Turret ?? previous?.Turret
        };

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
