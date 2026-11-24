namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     Outcome of evaluating a key: the rule that applies and the text to draw.
/// </summary>
public readonly record struct TelemetryEvaluation(TelemetryRuleMatch Match, string Text);

/// <summary>
///     Turns the current telemetry and a key's settings into the active rule and key text. One instance per key
///     (implementations may keep state, e.g. a sample history).
/// </summary>
public interface ITelemetryKeyEvaluator
{
    TelemetryEvaluation Evaluate(
        TelemetryDisplaySettings display, TelemetrySnapshot? snapshot, TelemetrySchema schema, DateTime nowUtc);
}

/// <summary>
///     Deceleration key: estimates cumulative units and time until a numeric attribute (for example speed) reaches
///     zero from its reduction over a window. Rules compare the estimate (time when only time is shown, otherwise
///     units); the Default section applies while there is no estimate (not decelerating, no data, window not covered).
/// </summary>
public sealed class DecelerationEvaluator : ITelemetryKeyEvaluator
{
    public const string Placeholder = "—";

    private readonly DecelerationEstimator _estimator = new();

    public TelemetryEvaluation Evaluate(
        TelemetryDisplaySettings display, TelemetrySnapshot? snapshot, TelemetrySchema schema, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(schema);

        TelemetryDecelerationSettings options = display.Deceleration ?? new TelemetryDecelerationSettings();
        TelemetryField? field = schema.Find(display.FieldPath);
        if (field is not { Kind: TelemetryValueKind.Number })
        {
            _estimator.Reset();
            return new TelemetryEvaluation(TelemetryRuleEvaluator.Evaluate(display, null), "Select\nnumeric\nattribute");
        }

        double? current = snapshot != null && snapshot.TryGetValue(field.Path, out TelemetryValue read) &&
                          read.Kind == TelemetryValueKind.Number
            ? read.Number
            : null;

        DecelerationEstimate? estimate = _estimator.Update(nowUtc, current, options.Window);

        string output = options.EffectiveOutput;
        TelemetryValue? ruleValue = estimate is { } e
            ? new TelemetryValue(TelemetryValueKind.Number, output == TelemetryDecelerationSettings.OutputTime ? e.TimeSeconds : e.Units)
            : null;
        TelemetryRuleMatch match = TelemetryRuleEvaluator.Evaluate(display, ruleValue);

        string units = estimate is { } u ? UnitsText(u.Units, options) : Placeholder;
        string time = estimate is { } t ? $"{TelemetryTextFormatter.FormatRounded(t.TimeSeconds, options.TimeRounding)} s" : Placeholder;
        string value = output == TelemetryDecelerationSettings.OutputTime ? time : units;
        string label = string.IsNullOrWhiteSpace(display.Label) ? field.Label : display.Label;

        string? template = string.IsNullOrEmpty(match.Rule.Text)
            ? output == TelemetryDecelerationSettings.OutputBoth ? "{units}\n{time}" : null
            : match.Rule.Text;

        Dictionary<string, string> tokens = new(StringComparer.OrdinalIgnoreCase) { ["units"] = units, ["time"] = time };
        return new TelemetryEvaluation(match, TelemetryTextFormatter.Apply(template, label, value, null, tokens));
    }

    private static string UnitsText(double units, TelemetryDecelerationSettings options)
    {
        string text = TelemetryTextFormatter.FormatRounded(units, options.UnitsRounding);
        return string.IsNullOrWhiteSpace(options.Unit) ? text : $"{text} {options.Unit.Trim()}";
    }
}
/// <summary>
///     Telemetry Display (one attribute) and Advanced Telemetry Display (conditions on several attributes).
/// </summary>
public sealed class RuleKeyEvaluator(bool advanced) : ITelemetryKeyEvaluator
{
    public TelemetryEvaluation Evaluate(
        TelemetryDisplaySettings display, TelemetrySnapshot? snapshot, TelemetrySchema schema, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(schema);

        TelemetryValue? Resolve(string path) =>
            snapshot != null && snapshot.TryGetValue(path, out TelemetryValue read) ? read : null;

        TelemetryField? field = schema.Find(display.FieldPath);
        TelemetryValue? value = Resolve(display.FieldPath ?? string.Empty);

        TelemetryRuleMatch match = advanced
            ? TelemetryRuleEvaluator.EvaluateWithResolver(display, Resolve)
            : TelemetryRuleEvaluator.Evaluate(display, value);

        if (field == null && !advanced)
        {
            return new TelemetryEvaluation(match, "Select\nattribute");
        }

        if (field == null && display.Rules.Count == 0)
        {
            return new TelemetryEvaluation(match, "Add\nrules");
        }

        string label = string.IsNullOrWhiteSpace(display.Label) ? field?.Label ?? string.Empty : display.Label;
        string valueText = field == null ? string.Empty : TelemetryTextFormatter.FormatValue(value, display.Decimals);
        // Advanced keys without a primary attribute only show the label unless the rule's text says otherwise.
        string? template = string.IsNullOrEmpty(match.Rule.Text) && field == null ? "{label}" : match.Rule.Text;

        string text = TelemetryTextFormatter.Apply(template, label, valueText, advanced ? FormatField : null);
        return new TelemetryEvaluation(match, text);

        string FormatField(string path) => TelemetryTextFormatter.FormatValue(Resolve(path), display.Decimals);
    }
}
