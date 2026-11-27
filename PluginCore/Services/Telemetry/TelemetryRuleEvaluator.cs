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
    public static TelemetryRuleMatch Evaluate(TelemetryDisplaySettings settings, TelemetryValue? value) =>
        EvaluateCore(settings, value, _ => null);

    /// <summary>
    ///     Like <see cref="EvaluateWithResolver" /> but the primary value (tested by rules without conditions) is given
    ///     explicitly, so it can be a derived value that has no attribute path (for example a deceleration estimate).
    /// </summary>
    public static TelemetryRuleMatch EvaluateWithPrimary(
        TelemetryDisplaySettings settings, TelemetryValue? primary, Func<string, TelemetryValue?> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);

        return EvaluateCore(settings, primary, path => string.IsNullOrWhiteSpace(path) ? null : resolve(path));
    }

    /// <summary>
    ///     Like <see cref="Evaluate" /> but values are looked up by attribute path, so rules can use several
    ///     attributes. Rules without conditions test the settings' primary attribute (<c>FieldPath</c>); rules with
    ///     conditions test their own attributes and match when all (or any) conditions hold. A condition on an
    ///     attribute without data is false.
    /// </summary>
    public static TelemetryRuleMatch EvaluateWithResolver(
        TelemetryDisplaySettings settings, Func<string, TelemetryValue?> resolve)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(resolve);

        Dictionary<string, TelemetryValue?> cache = new(StringComparer.Ordinal);

        TelemetryValue? Get(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (!cache.TryGetValue(path, out TelemetryValue? value))
            {
                value = resolve(path);
                cache[path] = value;
            }

            return value;
        }

        return EvaluateCore(settings, Get(settings.FieldPath), Get);
    }

    private static TelemetryRuleMatch EvaluateCore(
        TelemetryDisplaySettings settings, TelemetryValue? primary, Func<string?, TelemetryValue?> get)
    {
        ArgumentNullException.ThrowIfNull(settings);

        TelemetryRule fallback = settings.DefaultRule ?? new TelemetryRule();

        for (int i = 0; i < settings.Rules.Count; i++)
        {
            TelemetryRule rule = settings.Rules[i];
            if (rule == null)
            {
                continue;
            }

            bool matched = rule.Conditions is { Count: > 0 }
                ? MatchesConditions(rule, get)
                : primary is { } p && Matches(rule, p);

            if (matched)
            {
                return new TelemetryRuleMatch(rule, i, true);
            }
        }

        // Flags always have a value, so they say nothing about whether telemetry arrives.
        bool anyData = primary.HasValue ||
                       settings.Rules.Any(r => r?.Conditions != null && r.Conditions.Any(c =>
                           c?.FieldPath != null && !c.FieldPath.StartsWith(TelemetryFlagService.PathPrefix, StringComparison.Ordinal) &&
                           get(c.FieldPath).HasValue));
        return new TelemetryRuleMatch(fallback, -1, anyData);
    }

    public static bool Matches(TelemetryRule rule, TelemetryValue value)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return Matches(rule.Op, rule.Value, rule.Value2, value);
    }

    public static bool Matches(TelemetryCondition condition, TelemetryValue value)
    {
        ArgumentNullException.ThrowIfNull(condition);

        return Matches(condition.Op, condition.Value, condition.Value2, value);
    }

    public static bool TryParseNumber(string? text, out double number) =>
        double.TryParse(text?.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out number) &&
        double.IsFinite(number);

    private static bool MatchesConditions(TelemetryRule rule, Func<string?, TelemetryValue?> get)
    {
        bool any = string.Equals(rule.Match, TelemetryRule.MatchAny, StringComparison.OrdinalIgnoreCase);

        bool Holds(TelemetryCondition? condition) =>
            condition != null && get(condition.FieldPath) is { } value && Matches(condition, value);

        return any ? rule.Conditions!.Any(Holds) : rule.Conditions!.All(Holds);
    }

    private static bool Matches(string op, string? value, string? value2, TelemetryValue actual)
    {
        if (op == TelemetryRule.OpAlways)
        {
            return true;
        }

        return actual.Kind switch
        {
            TelemetryValueKind.Number => MatchesNumber(op, value, value2, actual.Number),
            TelemetryValueKind.Boolean => MatchesBoolean(op, value, actual.Boolean),
            _ => MatchesText(op, value, actual.Text)
        };
    }

    private static bool MatchesNumber(string op, string? value, string? value2, double actual)
    {
        if (!TryParseNumber(value, out double threshold))
        {
            return false;
        }

        switch (op)
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
                if (!TryParseNumber(value2, out double upper))
                {
                    return false;
                }

                (double low, double high) = threshold <= upper ? (threshold, upper) : (upper, threshold);
                return actual >= low && actual <= high;
            default:
                return false;
        }
    }

    private static bool MatchesBoolean(string op, string? value, bool actual)
    {
        if (!bool.TryParse(value?.Trim(), out bool expected))
        {
            return false;
        }

        return op switch
        {
            TelemetryRule.OpEquals => actual == expected,
            TelemetryRule.OpNotEquals => actual != expected,
            _ => false
        };
    }

    private static bool MatchesText(string op, string? value, string actual)
    {
        string expected = value?.Trim() ?? string.Empty;
        bool equal = string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

        return op switch
        {
            TelemetryRule.OpEquals => equal,
            TelemetryRule.OpNotEquals => !equal,
            _ => false
        };
    }
}
