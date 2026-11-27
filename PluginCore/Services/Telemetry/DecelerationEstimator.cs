namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     Estimated remaining distance (cumulative units) and time until the value reaches zero.
/// </summary>
public readonly record struct DecelerationEstimate(double Units, double TimeSeconds, bool Approximate = false);

/// <summary>
///     Estimates, from the reduction of a value over an observation window, how many cumulative units
///     (for a speed in m/s: meters) and how much time remain until it reaches zero, assuming the reduction
///     rate stays constant: with rate a = reduction per second, time = v / a and units = v² / (2a).
/// </summary>
public sealed class DecelerationEstimator
{
    private const double MinRatePerSecond = 1e-6;
    private const double MaxSeconds = 24 * 3600;
    private static readonly TimeSpan s_duplicateSampleSpacing = TimeSpan.FromMilliseconds(5);

    private readonly List<(DateTime At, double Value)> _samples = [];
    private readonly object _gate = new();

    public void Reset()
    {
        lock (_gate)
        {
            _samples.Clear();
        }
    }

    /// <summary>
    ///     Records the current value (null = no data, which clears the history) and returns the estimate, or null while
    ///     the window is not yet covered, the value is not decreasing, or it is already zero.
    /// </summary>
    public DecelerationEstimate? Update(DateTime now, double? value, TimeSpan window)
    {
        lock (_gate)
        {
            if (value is not { } current || !double.IsFinite(current))
            {
                _samples.Clear();
                return null;
            }

            // Time must move forward; a clock jump invalidates the history.
            if (_samples.Count > 0 && now < _samples[^1].At)
            {
                _samples.Clear();
            }

            if (_samples.Count > 0 && now - _samples[^1].At < s_duplicateSampleSpacing)
            {
                _samples[^1] = (now, current);
            }
            else
            {
                _samples.Add((now, current));
            }

            // Keep only the newest sample that is at least one window old, plus everything after it.
            DateTime reference = now - window;
            int keepFrom = 0;
            for (int i = 0; i < _samples.Count; i++)
            {
                if (_samples[i].At <= reference)
                {
                    keepFrom = i;
                }
            }

            if (keepFrom > 0)
            {
                _samples.RemoveRange(0, keepFrom);
            }

            (DateTime At, double Value) oldest = _samples[0];
            if (oldest.At > reference)
            {
                return null; // history does not span the window yet
            }

            double elapsed = (now - oldest.At).TotalSeconds;
            double rate = (oldest.Value - current) / elapsed;

            if (current <= 0 || rate < MinRatePerSecond)
            {
                return null;
            }

            double time = current / rate;
            return time > MaxSeconds ? null : new DecelerationEstimate(current * current / (2 * rate), time);
        }
    }
}
