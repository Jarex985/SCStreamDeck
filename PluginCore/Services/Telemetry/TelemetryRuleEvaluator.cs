using System.Globalization;

namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     Result of evaluating the rules: <see cref="Index" /> is -1 when the default rule applies.
/// </summary>
public readonly record struct TelemetryRuleMatch(TelemetryRule Rule, int Index, bool HasData);

public static class TelemetryRuleEvaluator
{
    private const double Epsilon = 1e-6;

    /// <summary>
    ///     Picks the first rule matching <paramref name="value" />; falls back to the default rule.
    ///     Without data only the default rule applies.
    /// </summary>
    public static TelemetryRuleMatch Evaluate(TelemetryDisplaySettings settings, TelemetryValue? value)
    {
        ArgumentNullException.ThrowIfNull(settings);

        TelemetryRule fallback = settings.DefaultRule ?? new TelemetryRule();
        if (value is not { } current)
        {
            return new TelemetryRuleMatch(fallback, -1, false);
        }

        for (int i = 0; i < settings.Rules.Count; i++)
        {
            TelemetryRule rule = settings.Rules[i];
            if (rule != null && Matches(rule, current))
            {
                return new TelemetryRuleMatch(rule, i, true);
            }
        }

        return new TelemetryRuleMatch(fallback, -1, true);
    }

    public static bool Matches(TelemetryRule rule, TelemetryValue value)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (rule.Op == TelemetryRule.OpAlways)
        {
            return true;
        }

        return value.Kind switch
        {
            TelemetryValueKind.Number => MatchesNumber(rule, value.Number),
            TelemetryValueKind.Boolean => MatchesBoolean(rule, value.Boolean),
            _ => MatchesText(rule, value.Text)
        };
    }

    public static bool TryParseNumber(string? text, out double number) =>
        double.TryParse(text?.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out number) &&
        double.IsFinite(number);

    private static bool MatchesNumber(TelemetryRule rule, double actual)
    {
        if (!TryParseNumber(rule.Value, out double threshold))
        {
            return false;
        }

        switch (rule.Op)
        {
            case TelemetryRule.OpLessThan:
                return actual < threshold;
            case TelemetryRule.OpLessOrEqual:
                return actual <= threshold;
            case TelemetryRule.OpGreaterThan:
                return actual > threshold;
            case TelemetryRule.OpGreaterOrEqual:
                return actual >= threshold;
            case TelemetryRule.OpEquals:
                return Math.Abs(actual - threshold) < Epsilon;
            case TelemetryRule.OpNotEquals:
                return Math.Abs(actual - threshold) >= Epsilon;
            case TelemetryRule.OpBetween:
                if (!TryParseNumber(rule.Value2, out double upper))
                {
                    return false;
                }

                (double low, double high) = threshold <= upper ? (threshold, upper) : (upper, threshold);
                return actual >= low && actual <= high;
            default:
                return false;
        }
    }

    private static bool MatchesBoolean(TelemetryRule rule, bool actual)
    {
        if (!bool.TryParse(rule.Value?.Trim(), out bool expected))
        {
            return false;
        }

        return rule.Op switch
        {
            TelemetryRule.OpEquals => actual == expected,
            TelemetryRule.OpNotEquals => actual != expected,
            _ => false
        };
    }

    private static bool MatchesText(TelemetryRule rule, string actual)
    {
        string expected = rule.Value?.Trim() ?? string.Empty;
        bool equal = string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

        return rule.Op switch
        {
            TelemetryRule.OpEquals => equal,
            TelemetryRule.OpNotEquals => !equal,
            _ => false
        };
    }
}
