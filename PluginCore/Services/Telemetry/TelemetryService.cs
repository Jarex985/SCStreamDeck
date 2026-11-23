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
    private readonly Dictionary<string, DateTime> _partStamps = new(StringComparer.Ordinal);
    private readonly ITelemetryStreamSource _source = source ?? throw new ArgumentNullException(nameof(source));

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private int _subscribers;
    private volatile TelemetrySnapshot? _snapshot;
    private volatile TelemetryConnectionState _state;

    public TimeSpan MinRetryDelay { get; init; } = s_defaultMinRetry;
    public TimeSpan MaxRetryDelay { get; init; } = s_defaultMaxRetry;

    /// <summary>A frame part not refreshed for this long is dropped (the game stops sending parts that no longer apply).</summary>
    public TimeSpan StaleAfter { get; init; } = TimeSpan.FromSeconds(3);

    public TimeSpan StaleCheckInterval { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Latest value of each frame part; null while nothing is being received.</summary>
    public TelemetrySnapshot? Snapshot => _snapshot;

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
                await foreach (TelemetrySnapshot frame in _source.StreamAsync(token).ConfigureAwait(false))
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
    internal void ApplyFrame(TelemetrySnapshot frame, DateTime now)
    {
        lock (_stateGate)
        {
            Dictionary<string, IReadOnlyDictionary<string, TelemetryValue>> parts =
                new(_snapshot?.Parts ?? TelemetrySnapshot.Empty.Parts, StringComparer.Ordinal);

            foreach ((string name, IReadOnlyDictionary<string, TelemetryValue> values) in frame.Parts)
            {
                parts[name] = values;
                _partStamps[name] = now;
            }

            _snapshot = new TelemetrySnapshot(parts);
        }
    }

    /// <summary>
    ///     Drops parts that have not been received for <see cref="StaleAfter" />. Returns true if anything changed.
    /// </summary>
    internal bool ExpireStale(DateTime now)
    {
        lock (_stateGate)
        {
            TelemetrySnapshot? current = _snapshot;
            if (current == null)
            {
                return false;
            }

            List<string> stale = current.Parts.Keys
                .Where(name => !_partStamps.TryGetValue(name, out DateTime stamp) || now - stamp >= StaleAfter)
                .ToList();
            if (stale.Count == 0)
            {
                return false;
            }

            Dictionary<string, IReadOnlyDictionary<string, TelemetryValue>> parts = new(current.Parts, StringComparer.Ordinal);
            foreach (string name in stale)
            {
                parts.Remove(name);
                _partStamps.Remove(name);
            }

            _snapshot = new TelemetrySnapshot(parts);
            return true;
        }
    }

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
