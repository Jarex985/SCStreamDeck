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
    public static string Apply(string? template, string label, string valueText, Func<string, string>? fieldValue = null)
    {
        string source = string.IsNullOrEmpty(template) ? DefaultTemplate : template;
        string text = source
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("{label}", label, StringComparison.OrdinalIgnoreCase)
            .Replace("{value}", valueText, StringComparison.OrdinalIgnoreCase);

        return fieldValue == null
            ? text
            : FieldToken().Replace(text, m => fieldValue(m.Groups[1].Value));
    }

    [GeneratedRegex(@"\{value:([A-Za-z0-9_.]+)\}", RegexOptions.IgnoreCase)]
    private static partial Regex FieldToken();
}
