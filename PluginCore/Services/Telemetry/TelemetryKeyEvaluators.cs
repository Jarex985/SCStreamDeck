using System.Globalization;

namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     Outcome of evaluating a key: the rule that applies and the text to draw. <paramref name="LineColors" />
///     optionally overrides the text color of individual lines (null entries keep the rule's text color).
/// </summary>
public readonly record struct TelemetryEvaluation(
    TelemetryRuleMatch Match, string Text, IReadOnlyList<string?>? LineColors = null);

/// <summary>
///     Implemented by evaluators whose key does something of its own when pressed (before any rule action runs).
/// </summary>
public interface ITelemetryKeyPressHandler
{
    void OnKeyPressed();
}

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

    /// <summary>Pseudo attributes rules can test: the estimate in the displayed unit, and the time in seconds.</summary>
    public const string UnitsPath = "deceleration.units";

    public const string TimePath = "deceleration.time";

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

        // Units are shown (and compared by rules) in the chosen order of magnitude, e.g. km instead of m.
        double divisor = options.EffectiveUnitsDivisor;
        string output = options.EffectiveOutput;
        TelemetryValue? unitsValue = estimate is { } eu ? new TelemetryValue(TelemetryValueKind.Number, eu.Units / divisor) : null;
        TelemetryValue? timeValue = estimate is { } et ? new TelemetryValue(TelemetryValueKind.Number, et.TimeSeconds) : null;
        TelemetryValue? ruleValue = output == TelemetryDecelerationSettings.OutputTime ? timeValue : unitsValue;

        // Rules may test any attribute and the estimate itself ("deceleration.units" / "deceleration.time").
        TelemetryValue? Resolve(string path) => path switch
        {
            UnitsPath => unitsValue,
            TimePath => timeValue,
            _ => snapshot != null && snapshot.TryGetValue(path, out TelemetryValue read) ? read : null
        };

        TelemetryRuleMatch match = TelemetryRuleEvaluator.EvaluateWithPrimary(display, ruleValue, Resolve);

        string units = estimate is { } u ? UnitsText(u.Units / divisor, options) : Placeholder;
        string time = estimate is { } t ? $"{TelemetryTextFormatter.FormatRounded(t.TimeSeconds, options.TimeRounding)} s" : Placeholder;
        string value = output == TelemetryDecelerationSettings.OutputTime ? time : units;
        string label = string.IsNullOrWhiteSpace(display.Label) ? field.Label : display.Label;

        string? template = string.IsNullOrEmpty(match.Rule.Text)
            ? output == TelemetryDecelerationSettings.OutputBoth ? "{units}\n{time}" : null
            : match.Rule.Text;

        Dictionary<string, string> tokens = new(StringComparer.OrdinalIgnoreCase) { ["units"] = units, ["time"] = time };
        return new TelemetryEvaluation(
            match,
            TelemetryTextFormatter.Apply(template, label, value, path => TelemetryTextFormatter.FormatValue(Resolve(path), display.Decimals), tokens));
    }

    private static string UnitsText(double units, TelemetryDecelerationSettings options)
    {
        string text = TelemetryTextFormatter.FormatRounded(units, options.UnitsRounding);
        return string.IsNullOrWhiteSpace(options.Unit) ? text : $"{text} {options.Unit.Trim()}";
    }
}

/// <summary>
///     UEC key: shows the in-game UEC balance and, on a second line, how much it changed since the session started
///     or the key was last pressed (accounting style, green for gains and red for losses). Rules compare the balance;
///     conditions can also test the change as the pseudo attribute <see cref="ChangePath" />.
/// </summary>
public sealed class UecBalanceEvaluator(UecSessionTracker tracker) : ITelemetryKeyEvaluator, ITelemetryKeyPressHandler
{
    public const string BalancePath = "actor.state.uec_balance";

    /// <summary>Pseudo attribute rules can test: the gain (positive) or loss (negative) of the session.</summary>
    public const string ChangePath = "uec.change";

    public const string DefaultLabel = "UEC";
    public const string DefaultTemplate = "{value}\n{change}";
    public const string GainColor = "#2ECC40";
    public const string LossColor = "#FF4136";

    private readonly UecSessionTracker _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));

    public void OnKeyPressed() => _tracker.Reset();

    public TelemetryEvaluation Evaluate(
        TelemetryDisplaySettings display, TelemetrySnapshot? snapshot, TelemetrySchema schema, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(display);

        TelemetryValue? balanceValue =
            snapshot != null && snapshot.TryGetValue(BalancePath, out TelemetryValue read) &&
            read.Kind == TelemetryValueKind.Number
                ? read
                : null;

        UecReading? reading = _tracker.Observe(
            balanceValue is { } b ? (long)Math.Round(b.Number, MidpointRounding.AwayFromZero) : null, nowUtc);
        TelemetryValue? changeValue = reading is { } r
            ? new TelemetryValue(TelemetryValueKind.Number, r.Change, IsInteger: true)
            : null;

        TelemetryValue? Resolve(string path) => path switch
        {
            ChangePath => changeValue,
            _ => snapshot != null && snapshot.TryGetValue(path, out TelemetryValue other) ? other : null
        };

        TelemetryRuleMatch match = TelemetryRuleEvaluator.EvaluateWithPrimary(display, balanceValue, Resolve);

        string balanceText = reading is { } rb ? rb.Balance.ToString("N0", CultureInfo.CurrentCulture) : TelemetryTextFormatter.NoDataText;
        string changeText = reading is { } rc ? TelemetryTextFormatter.FormatAccounting(rc.Change) : TelemetryTextFormatter.NoDataText;
        string label = string.IsNullOrWhiteSpace(display.Label) ? DefaultLabel : display.Label;
        string template = (string.IsNullOrEmpty(match.Rule.Text) ? DefaultTemplate : match.Rule.Text)
            .Replace("\\n", "\n", StringComparison.Ordinal);

        Dictionary<string, string> tokens = new(StringComparer.OrdinalIgnoreCase)
        {
            ["change"] = changeText, ["balance"] = balanceText
        };
        string text = TelemetryTextFormatter.Apply(
            template, label, balanceText,
            path => path.Equals(ChangePath, StringComparison.OrdinalIgnoreCase)
                ? changeText
                : TelemetryTextFormatter.FormatValue(Resolve(path), display.Decimals),
            tokens);

        string? changeColor = reading is { Change: > 0 } ? GainColor : reading is { Change: < 0 } ? LossColor : null;
        string?[]? lineColors = changeColor == null
            ? null
            : template.Split('\n').Select(line => ShowsChange(line) ? changeColor : null).ToArray();

        return new TelemetryEvaluation(match, text, lineColors);
    }

    private static bool ShowsChange(string line) =>
        line.Contains("{change}", StringComparison.OrdinalIgnoreCase) ||
        line.Contains($"{{value:{ChangePath}}}", StringComparison.OrdinalIgnoreCase);
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
