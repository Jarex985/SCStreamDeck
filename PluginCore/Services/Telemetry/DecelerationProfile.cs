using Newtonsoft.Json;

namespace SCStreamDeck.Services.Telemetry;

/// <summary>One telemetry sample of a recorded deceleration run.</summary>
public readonly record struct DecelerationSample(DateTime At, double Speed, double Density);

/// <summary>
///     What a ship needs to come to a stop from a speed at an atmospheric density. <paramref name="Approximate" />
///     is set when the recorded runs do not cover the speed or the density well.
/// </summary>
public readonly record struct ProfileEstimate(double Units, double TimeSeconds, bool Approximate);

/// <summary>
///     How one ship (in one master mode) decelerates when the throttle is released, learned from recorded runs.
///     The deceleration rate is kept per speed bin and atmospheric density bin as speed lost divided by time spent,
///     so noisy samples average out. A new run weighs more than the older ones (<see cref="RunDecay" />), which
///     lets the profile follow changes such as cargo or loadout. The stopping distance and time are the integral
///     of the rate over the speed range, blended between the two nearest recorded densities.
/// </summary>
public sealed class DecelerationProfile
{
    public const double SpeedBinWidth = 10;
    public const double DensityBinWidth = 0.1;

    /// <summary>Weight kept of the existing data of a bin when a new run touches it.</summary>
    public const double RunDecay = 0.75;

    /// <summary>The densities of a run and of the estimate may differ this much before the estimate is approximate.</summary>
    public const double DensityTolerance = 0.15;

    private const double MinRate = 0.05;
    private const double MinCellSeconds = 1e-3;

    private readonly object _gate = new();
    private readonly Dictionary<(int Speed, int Density), Cell> _cells = [];

    [JsonProperty("runs")] public int Runs { get; private set; }

    [JsonProperty("cells", ObjectCreationHandling = ObjectCreationHandling.Replace)]
    private List<CellDto> CellsForJson
    {
        get
        {
            lock (_gate)
            {
                return _cells.Select(c => new CellDto
                {
                    Speed = c.Key.Speed, Density = c.Key.Density, SpeedLost = c.Value.SpeedLost, Seconds = c.Value.Seconds
                }).ToList();
            }
        }
        set
        {
            lock (_gate)
            {
                _cells.Clear();
                foreach (CellDto cell in value ?? [])
                {
                    if (cell.SpeedLost > 0 && cell.Seconds > MinCellSeconds && double.IsFinite(cell.SpeedLost) &&
                        double.IsFinite(cell.Seconds))
                    {
                        _cells[(cell.Speed, cell.Density)] = new Cell(cell.SpeedLost, cell.Seconds);
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Adds a run (samples in time order, speed falling). Intervals where the speed does not fall are ignored.
    ///     Returns false when the run held nothing usable.
    /// </summary>
    public bool AddRun(IReadOnlyList<DecelerationSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        Dictionary<(int Speed, int Density), Cell> added = [];
        for (int i = 1; i < samples.Count; i++)
        {
            DecelerationSample from = samples[i - 1];
            DecelerationSample to = samples[i];
            double seconds = (to.At - from.At).TotalSeconds;
            double lost = from.Speed - to.Speed;
            if (seconds <= 0 || lost <= 0 || !double.IsFinite(lost) || !double.IsFinite(seconds))
            {
                continue;
            }

            int density = DensityBin((from.Density + to.Density) / 2);
            int first = (int)Math.Floor(to.Speed / SpeedBinWidth);
            int last = (int)Math.Floor(from.Speed / SpeedBinWidth);
            for (int bin = Math.Max(0, first); bin <= last; bin++)
            {
                double overlap = Math.Min(from.Speed, (bin + 1) * SpeedBinWidth) - Math.Max(to.Speed, bin * SpeedBinWidth);
                if (overlap <= 0)
                {
                    continue;
                }

                (int Speed, int Density) key = (bin, density);
                added.TryGetValue(key, out Cell sum);
                added[key] = new Cell(sum.SpeedLost + overlap, sum.Seconds + (seconds * overlap / lost));
            }
        }

        if (added.Count == 0)
        {
            return false;
        }

        lock (_gate)
        {
            foreach ((( int Speed, int Density) key, Cell cell) in added)
            {
                _cells.TryGetValue(key, out Cell existing);
                _cells[key] = new Cell(
                    (existing.SpeedLost * RunDecay) + cell.SpeedLost, (existing.Seconds * RunDecay) + cell.Seconds);
            }

            Runs++;
        }

        return true;
    }

    /// <summary>Distance and time to stop from <paramref name="speed" />, or null without usable data.</summary>
    public ProfileEstimate? Estimate(double speed, double density)
    {
        if (!double.IsFinite(speed) || speed <= 0 || !double.IsFinite(density))
        {
            return null;
        }

        density = Math.Max(0, density);
        Dictionary<int, SortedDictionary<int, double>> curves;
        lock (_gate)
        {
            curves = [];
            foreach (((int speedBin, int densityBin), Cell cell) in _cells)
            {
                if (cell.Seconds <= MinCellSeconds)
                {
                    continue;
                }

                if (!curves.TryGetValue(densityBin, out SortedDictionary<int, double>? curve))
                {
                    curves[densityBin] = curve = [];
                }

                curve[speedBin] = Math.Max(MinRate, cell.SpeedLost / cell.Seconds);
            }
        }

        if (curves.Count == 0)
        {
            return null;
        }

        int? below = null;
        int? above = null;
        foreach (int bin in curves.Keys)
        {
            double center = bin * DensityBinWidth;
            if (center <= density && (below is null || bin > below))
            {
                below = bin;
            }

            if (center >= density && (above is null || bin < above))
            {
                above = bin;
            }
        }

        if (below is { } lo && above is { } hi && lo != hi)
        {
            double weight = (density - (lo * DensityBinWidth)) / ((hi - lo) * DensityBinWidth);
            (double t0, double u0, bool a0) = Integrate(curves[lo], speed);
            (double t1, double u1, bool a1) = Integrate(curves[hi], speed);
            return new ProfileEstimate(
                (u0 * (1 - weight)) + (u1 * weight), (t0 * (1 - weight)) + (t1 * weight), a0 || a1);
        }

        int nearest = below ?? above!.Value;
        (double time, double units, bool approximate) = Integrate(curves[nearest], speed);
        return new ProfileEstimate(units, time, approximate || Math.Abs(density - (nearest * DensityBinWidth)) > DensityTolerance);
    }

    private static (double Time, double Units, bool Approximate) Integrate(SortedDictionary<int, double> curve, double speed)
    {
        int highestRecorded = curve.Keys.Max();
        int top = (int)Math.Ceiling(speed / SpeedBinWidth) - 1;
        double time = 0;
        double units = 0;
        for (int bin = 0; bin <= top; bin++)
        {
            double low = bin * SpeedBinWidth;
            double high = Math.Min(speed, (bin + 1) * SpeedBinWidth);
            if (high <= low)
            {
                continue;
            }

            double rate = RateAt(curve, bin);
            time += (high - low) / rate;
            units += ((high * high) - (low * low)) / (2 * rate);
        }

        return (time, units, top > highestRecorded);
    }

    // A bin nobody recorded takes the rate of the nearest recorded one (the slower side wins a tie).
    private static double RateAt(SortedDictionary<int, double> curve, int bin)
    {
        if (curve.TryGetValue(bin, out double rate))
        {
            return rate;
        }

        int best = curve.Keys.MinBy(k => (Math.Abs(k - bin), k));
        return curve[best];
    }

    private static int DensityBin(double density) =>
        (int)Math.Round(Math.Max(0, density) / DensityBinWidth, MidpointRounding.AwayFromZero);

    private readonly record struct Cell(double SpeedLost, double Seconds);

    private sealed class CellDto
    {
        [JsonProperty("s")] public int Speed { get; set; }

        [JsonProperty("d")] public int Density { get; set; }

        [JsonProperty("dv")] public double SpeedLost { get; set; }

        [JsonProperty("dt")] public double Seconds { get; set; }
    }
}
