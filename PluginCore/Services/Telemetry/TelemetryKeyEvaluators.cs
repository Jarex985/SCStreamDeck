using System.Globalization;

namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     Outcome of evaluating a key: the rule that applies and the text to draw. <paramref name="LineColors" />
///     optionally overrides the text color of individual lines (null entries keep the rule's text color).
///     <paramref name="Disabled" /> marks a key that is currently unusable: pressing it does nothing.
/// </summary>
public readonly record struct TelemetryEvaluation(
    TelemetryRuleMatch Match, string Text, IReadOnlyList<string?>? LineColors = null, bool Disabled = false);

/// <summary>
///     Implemented by evaluators whose key does something of its own when pressed (before any rule action runs).
/// </summary>
public interface ITelemetryKeyPressHandler
{
    void OnKeyPressed();

    /// <summary>True when the last press was fully handled here, so the active rule's press actions do not run.</summary>
    bool ConsumesPress => false;
}

/// <summary>
///     Implemented by evaluators whose text changes with the clock, so the key is refreshed every tick even when no
///     telemetry arrives.
/// </summary>
public interface ITelemetryKeyTimeDriven;

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
///     For ship speed the estimate comes by default from the deceleration recorded for the current ship and the
///     atmospheric density: pressing the key arms a recording, and releasing the throttle until the ship stops saves
///     the run. Pressing while armed or recording cancels it.
/// </summary>
public sealed class DecelerationEvaluator(DecelerationProfileService? profiles = null)
    : ITelemetryKeyEvaluator, ITelemetryKeyPressHandler, ITelemetryKeyTimeDriven
{
    public const string Placeholder = "—";

    /// <summary>How long the result of a recording stays on the key.</summary>
    public static readonly TimeSpan OutcomeDisplay = TimeSpan.FromSeconds(4);

    private volatile bool _recordedMode;
    private volatile bool _consumesPress;

    /// <summary>Pseudo attributes rules can test: the estimate in the displayed unit, and the time in seconds.</summary>
    public const string UnitsPath = "deceleration.units";

    public const string TimePath = "deceleration.time";

    private readonly DecelerationEstimator _estimator = new();

    public bool ConsumesPress => _consumesPress;

    public void OnKeyPressed()
    {
        _consumesPress = false;
        if (profiles == null || !_recordedMode)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        if (profiles.Status(now).Phase == DecelerationRecordingPhase.Idle)
        {
            profiles.Arm(now);
        }
        else
        {
            profiles.Cancel(now);
            _consumesPress = true;
        }
    }

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
            _recordedMode = false;
            return new TelemetryEvaluation(TelemetryRuleEvaluator.Evaluate(display, null), "Select\nnumeric\nattribute");
        }

        double? current = snapshot != null && snapshot.TryGetValue(field.Path, out TelemetryValue read) &&
                          read.Kind == TelemetryValueKind.Number
            ? read.Number
            : null;

        bool recorded = UsesRecordedProfile(options, field);
        _recordedMode = recorded;

        DecelerationEstimate? estimate;
        string? statusText = null;
        if (recorded)
        {
            _estimator.Reset();
            estimate = ProfileEstimateFor(snapshot, current);
            statusText = RecordingText(snapshot, nowUtc);
        }
        else
        {
            estimate = _estimator.Update(nowUtc, current, options.Window);
        }

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

        string units = estimate is { } u ? UnitsText(u.Units / divisor, options, u.Approximate) : Placeholder;
        string time = estimate is { } t
            ? $"{(t.Approximate ? "~" : string.Empty)}{TelemetryTextFormatter.FormatRounded(t.TimeSeconds, options.TimeRounding)} s"
            : Placeholder;
        string value = output == TelemetryDecelerationSettings.OutputTime ? time : units;
        string label = string.IsNullOrWhiteSpace(display.Label) ? field.Label : display.Label;

        string? template = string.IsNullOrEmpty(match.Rule.Text)
            ? output == TelemetryDecelerationSettings.OutputBoth ? "{units}\n{time}" : null
            : match.Rule.Text;

        Dictionary<string, string> tokens = new(StringComparer.OrdinalIgnoreCase) { ["units"] = units, ["time"] = time };
        return new TelemetryEvaluation(
            match,
            statusText ?? TelemetryTextFormatter.Apply(template, label, value, path => TelemetryTextFormatter.FormatValue(Resolve(path), display.Decimals), tokens));
    }

    // Ship speed uses the recorded profile unless the key was set to extrapolate live.
    private bool UsesRecordedProfile(TelemetryDecelerationSettings options, TelemetryField field)
    {
        if (profiles == null || field.Path != DecelerationProfileService.SpeedPath)
        {
            return false;
        }

        return !string.Equals(options.Mode, TelemetryDecelerationSettings.ModeLive, StringComparison.OrdinalIgnoreCase);
    }

    private DecelerationEstimate? ProfileEstimateFor(TelemetrySnapshot? snapshot, double? speed)
    {
        if (speed is not { } current || current < DecelerationRunRecorder.StopSpeed || snapshot == null)
        {
            return null;
        }

        double density = snapshot.TryGetValue(DecelerationProfileService.DensityPath, out TelemetryValue d) &&
                         d.Kind == TelemetryValueKind.Number
            ? d.Number
            : 0;
        return profiles!.Estimate(DecelerationProfileService.ShipKey(snapshot), current, density) is { } estimate
            ? new DecelerationEstimate(estimate.Units, estimate.TimeSeconds, estimate.Approximate)
            : null;
    }

    // While arming or recording, and briefly afterwards, the key reports on the recording instead of the estimate.
    private string? RecordingText(TelemetrySnapshot? snapshot, DateTime nowUtc)
    {
        DecelerationRecordingStatus status = profiles!.Status(nowUtc);
        switch (status.Phase)
        {
            case DecelerationRecordingPhase.Armed:
                return "Armed\nrelease\nthrottle";
            case DecelerationRecordingPhase.Recording:
                return $"REC\n{status.Speed.ToString("0", CultureInfo.InvariantCulture)}";
        }

        if (status.Outcome == DecelerationRunOutcome.None || nowUtc - status.OutcomeAt > OutcomeDisplay)
        {
            return null;
        }

        if (status.Outcome == DecelerationRunOutcome.Discarded)
        {
            return $"Discarded\n{status.Reason}";
        }

        int runs = profiles.RunCount(DecelerationProfileService.ShipKey(snapshot));
        return $"Saved\n{runs} run{(runs == 1 ? string.Empty : "s")}";
    }

    private static string UnitsText(double units, TelemetryDecelerationSettings options, bool approximate)
    {
        string text = (approximate ? "~" : string.Empty) + TelemetryTextFormatter.FormatRounded(units, options.UnitsRounding);
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

        string balanceText = reading is { } rb ? TelemetryTextFormatter.FormatCompact(rb.Balance) : TelemetryTextFormatter.NoDataText;
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
///     Shared by the travel keys: evaluates rules against the measurement and fills the tokens {state}, {elapsed}
///     (minutes and tenths) and {distance} (m, then km). Rules can test the pseudo attributes <see cref="StatePath" />,
///     <see cref="MinutesPath" /> and <see cref="MetersPath" /> besides any telemetry attribute.
/// </summary>
public abstract class TravelEvaluatorBase(TravelTracker tracker) : ITelemetryKeyEvaluator, ITelemetryKeyTimeDriven
{
    public const string StatePath = "travel.state";
    public const string MinutesPath = "travel.minutes";
    public const string MetersPath = "travel.meters";

    public const string StateIdle = "IDLE";
    public const string StateRunning = "RUNNING";
    public const string StateStopped = "STOPPED";

    public const string UnavailableText = "Unavailable";

    protected TravelTracker Tracker { get; } = tracker ?? throw new ArgumentNullException(nameof(tracker));

    protected abstract string DefaultLabel { get; }

    /// <summary>
    ///     The look of the key while it is available: its rule, the offset of its index behind the unavailable rules
    ///     (so the sound of a look plays once when it becomes the active one) and the text used when it has none.
    /// </summary>
    protected abstract (TelemetryRule Rule, int IndexOffset, string DefaultText) AvailableLook(
        TelemetryDisplaySettings display, TravelReading reading);

    private (TelemetryRuleMatch Match, bool Disabled, string DefaultText) ChooseLook(
        TelemetryDisplaySettings display, TravelReading reading, Func<string, TelemetryValue?> resolve)
    {
        // The rules say when the key is unavailable: the first matching one gives its look.
        TelemetryRuleMatch unavailable = TelemetryRuleEvaluator.EvaluateWithPrimary(display, null, resolve);
        if (unavailable.Index >= 0)
        {
            return (unavailable, true, UnavailableText);
        }

        (TelemetryRule rule, int offset, string defaultText) = AvailableLook(display, reading);
        return (new TelemetryRuleMatch(rule ?? new TelemetryRule(), display.Rules.Count + offset, true), false, defaultText);
    }

    public TelemetryEvaluation Evaluate(
        TelemetryDisplaySettings display, TelemetrySnapshot? snapshot, TelemetrySchema schema, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(display);

        TravelReading reading = Tracker.Read(nowUtc);
        bool started = reading.State != TravelState.Idle;

        TelemetryValue stateValue = new(
            TelemetryValueKind.Enum, Text: StateName(reading.State), DisplayText: reading.State.ToString());
        TelemetryValue? minutesValue = started
            ? new TelemetryValue(TelemetryValueKind.Number, reading.Elapsed.TotalMinutes)
            : null;
        TelemetryValue? metersValue = started
            ? new TelemetryValue(TelemetryValueKind.Number, reading.Meters)
            : null;

        TelemetryValue? Resolve(string path) => path switch
        {
            StatePath => stateValue,
            MinutesPath => minutesValue,
            MetersPath => metersValue,
            _ => snapshot != null && snapshot.TryGetValue(path, out TelemetryValue other) ? other : null
        };

        (TelemetryRuleMatch match, bool disabled, string defaultText) = ChooseLook(display, reading, Resolve);

        string elapsed = started ? TelemetryTextFormatter.FormatMinutes(reading.Elapsed) : TelemetryTextFormatter.NoDataText;
        string distance = started ? TelemetryTextFormatter.FormatDistance(reading.Meters) : TelemetryTextFormatter.NoDataText;
        string label = string.IsNullOrWhiteSpace(display.Label) ? DefaultLabel : display.Label;
        string template = string.IsNullOrEmpty(match.Rule.Text) ? defaultText : match.Rule.Text;

        Dictionary<string, string> tokens = new(StringComparer.OrdinalIgnoreCase)
        {
            ["elapsed"] = elapsed, ["distance"] = distance, ["state"] = reading.State.ToString()
        };
        string text = TelemetryTextFormatter.Apply(
            template, label, elapsed,
            path => TelemetryTextFormatter.FormatValue(Resolve(path), display.Decimals),
            tokens);

        return new TelemetryEvaluation(match, text, Disabled: disabled);
    }

    private static string StateName(TravelState state) => state switch
    {
        TravelState.Running => StateRunning,
        TravelState.Stopped => StateStopped,
        _ => StateIdle
    };
}

/// <summary>
///     Travel Timer key with three looks instead of rules: <b>Start</b> (shown while a measurement can be started,
///     also after a stop), <b>Stop</b> (while one is running) and <b>Unavailable</b>, which replaces them while one of
///     the rules matches. An unavailable key does nothing when pressed. Otherwise pressing starts or stops the
///     measurement; after a stop it starts a new one.
/// </summary>
public sealed class TravelToggleEvaluator(TravelTracker tracker) : TravelEvaluatorBase(tracker), ITelemetryKeyPressHandler
{
    protected override string DefaultLabel => "Travel";

    public void OnKeyPressed() => Tracker.Toggle(DateTime.UtcNow);

    protected override (TelemetryRule Rule, int IndexOffset, string DefaultText) AvailableLook(
        TelemetryDisplaySettings display, TravelReading reading) =>
        reading.State == TravelState.Running
            ? (display.StopRule, 1, "Stop")
            : (display.StartRule, 0, "Start");
}

/// <summary>
///     Travel Display key: shows the elapsed minutes and the estimated distance of the measurement with one look
///     and, like the Travel Timer, an <b>Unavailable</b> look chosen by rules. Pressing it only runs the press actions
///     of the look (nothing while unavailable).
/// </summary>
public sealed class TravelDisplayEvaluator(TravelTracker tracker) : TravelEvaluatorBase(tracker)
{
    protected override string DefaultLabel => "Travel";

    protected override (TelemetryRule Rule, int IndexOffset, string DefaultText) AvailableLook(
        TelemetryDisplaySettings display, TravelReading reading) =>
        (display.DefaultRule, 0, "{elapsed}\n{distance}");
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
