using System.Globalization;
using System.Text.RegularExpressions;

namespace SCStreamDeck.Services.Telemetry;

public static partial class TelemetryTextFormatter
{
    public const string NoDataText = "—";
    public const string DefaultTemplate = "{label}\n{value}";

    private const int MaxDecimals = 4;

    public static string FormatValue(TelemetryValue? value, int? decimals)
    {
        if (value is not { } v)
        {
            return NoDataText;
        }

        if (v.Kind != TelemetryValueKind.Number)
        {
            return v.DisplayText;
        }

        int digits = decimals is { } d ? Math.Clamp(d, 0, MaxDecimals) : v.IsInteger ? 0 : 1;
        return v.Number.ToString($"N{digits}", CultureInfo.CurrentCulture);
    }

    /// <summary>
    ///     Accounting style without currency symbol: 999 and (999) for negatives, abbreviated from 1,000 on
    ///     (see <see cref="FormatCompact" />).
    /// </summary>
    public static string FormatAccounting(long amount) =>
        amount < 0 ? $"({FormatCompact(-(decimal)amount)})" : FormatCompact(amount);

    /// <summary>
    ///     Keeps big amounts short for the small key: 999, 1.5k (up to one decimal), 1.25m and 1.25b (up to two
    ///     decimals). Trailing zeros are dropped and rounding that reaches the next unit moves up (999,999 is 1m).
    /// </summary>
    public static string FormatCompact(decimal amount)
    {
        decimal magnitude = Math.Abs(amount);
        string sign = amount < 0 ? "-" : string.Empty;
        if (magnitude < 1_000m)
        {
            return sign + magnitude.ToString("N0", CultureInfo.CurrentCulture);
        }

        (decimal Divisor, string Suffix, int Decimals)[] units = [(1_000m, "k", 1), (1_000_000m, "m", 2), (1_000_000_000m, "b", 2)];
        int index = 0;
        while (index < units.Length - 1 && magnitude >= units[index + 1].Divisor)
        {
            index++;
        }

        decimal scaled = Math.Round(magnitude / units[index].Divisor, units[index].Decimals, MidpointRounding.AwayFromZero);
        if (scaled >= 1_000m && index < units.Length - 1)
        {
            index++;
            scaled = Math.Round(magnitude / units[index].Divisor, units[index].Decimals, MidpointRounding.AwayFromZero);
        }

        string digits = scaled.ToString(units[index].Decimals == 1 ? "0.#" : "0.##", CultureInfo.CurrentCulture);
        return sign + digits + units[index].Suffix;
    }

    /// <summary>
    ///     Replaces {label} and {value}; with <paramref name="fieldValue" /> also {value:attribute.path} tokens,
    ///     so advanced keys can print several attributes.
    /// </summary>
    public static string Apply(
        string? template, string label, string valueText, Func<string, string>? fieldValue = null,
        IReadOnlyDictionary<string, string>? tokens = null)
    {
        string source = string.IsNullOrEmpty(template) ? DefaultTemplate : template;
        string text = source
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("{label}", label, StringComparison.OrdinalIgnoreCase)
            .Replace("{value}", valueText, StringComparison.OrdinalIgnoreCase);

        if (tokens != null)
        {
            foreach ((string name, string replacement) in tokens)
            {
                text = text.Replace($"{{{name}}}", replacement, StringComparison.OrdinalIgnoreCase);
            }
        }

        return fieldValue == null
            ? text
            : FieldToken().Replace(text, m => fieldValue(m.Groups[1].Value));
    }

    /// <summary>
    ///     Rounds to the nearest multiple of <paramref name="step" /> (no rounding step: one decimal) and shows only
    ///     as many decimals as the step needs, e.g. step 50 gives "1,250", step 0.5 gives "12.5".
    /// </summary>
    public static string FormatRounded(double value, double? step)
    {
        if (step is not { } s || s <= 0 || !double.IsFinite(s))
        {
            return value.ToString("N1", CultureInfo.CurrentCulture);
        }

        double rounded = Math.Round(value / s, MidpointRounding.AwayFromZero) * s;
        string stepText = s.ToString("0.#######", CultureInfo.InvariantCulture);
        int dot = stepText.IndexOf('.', StringComparison.Ordinal);
        int decimals = dot < 0 ? 0 : Math.Min(MaxDecimals, stepText.Length - dot - 1);
        return rounded.ToString($"N{decimals}", CultureInfo.CurrentCulture);
    }

    [GeneratedRegex(@"\{value:([A-Za-z0-9_.]+)\}", RegexOptions.IgnoreCase)]
    private static partial Regex FieldToken();
}
