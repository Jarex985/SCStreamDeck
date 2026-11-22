using System.Globalization;

namespace SCStreamDeck.Services.Telemetry;

public static class TelemetryTextFormatter
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

    public static string Apply(string? template, string label, string valueText)
    {
        string source = string.IsNullOrEmpty(template) ? DefaultTemplate : template;
        return source
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("{label}", label, StringComparison.OrdinalIgnoreCase)
            .Replace("{value}", valueText, StringComparison.OrdinalIgnoreCase);
    }
}
