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
