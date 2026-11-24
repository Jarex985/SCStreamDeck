using Newtonsoft.Json;

namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     One step of a press-action sequence.
/// </summary>
public sealed class TelemetryPressAction
{
    public const int MaxDelayMs = 60_000;

    /// <summary>Star Citizen action name (as used in defaultProfile.xml).</summary>
    [JsonProperty("function")] public string? Function { get; set; }

    /// <summary>Wait this many milliseconds before running this action.</summary>
    [JsonProperty("delayMs")] public int? DelayMs { get; set; }

    public int EffectiveDelayMs => Math.Clamp(DelayMs ?? 0, 0, MaxDelayMs);
}

/// <summary>
///     One test on one attribute, used by advanced rules.
/// </summary>
public sealed class TelemetryCondition
{
    [JsonProperty("fieldPath")] public string? FieldPath { get; set; }

    [JsonProperty("op")] public string Op { get; set; } = TelemetryRule.OpEquals;

    [JsonProperty("value")] public string? Value { get; set; }

    [JsonProperty("value2")] public string? Value2 { get; set; }
}

/// <summary>
///     Appearance (and optional press action) applied while a rule is the active one.
/// </summary>
public sealed class TelemetryRule
{
    public const string OpAlways = "always";
    public const string OpLessThan = "lt";
    public const string OpLessOrEqual = "lte";
    public const string OpGreaterThan = "gt";
    public const string OpGreaterOrEqual = "gte";
    public const string OpEquals = "eq";
    public const string OpNotEquals = "neq";
    public const string OpBetween = "between";
    public const string MatchAll = "all";
    public const string MatchAny = "any";

    [JsonProperty("op")] public string Op { get; set; } = OpAlways;

    [JsonProperty("value")] public string? Value { get; set; }

    /// <summary>Upper bound (inclusive) for <see cref="OpBetween" />; <see cref="Value" /> is the lower bound.</summary>
    [JsonProperty("value2")] public string? Value2 { get; set; }

    /// <summary>
    ///     Advanced rules: conditions on several attributes. When there are any, they replace
    ///     <see cref="Op" />/<see cref="Value" /> and the rule matches according to <see cref="Match" />.
    /// </summary>
    [JsonProperty("conditions")] public List<TelemetryCondition>? Conditions { get; set; }

    /// <summary><see cref="MatchAll" /> (AND, default) or <see cref="MatchAny" /> (OR) for <see cref="Conditions" />.</summary>
    [JsonProperty("match")] public string? Match { get; set; }

    /// <summary>Text template, supports {value} and {label} and line breaks. Empty uses the default template.</summary>
    [JsonProperty("text")] public string? Text { get; set; }

    [JsonProperty("imagePath")] public string? ImagePath { get; set; }

    [JsonProperty("textColor")] public string? TextColor { get; set; }

    [JsonProperty("backgroundColor")] public string? BackgroundColor { get; set; }

    /// <summary>
    ///     Id of a shared format template (<see cref="TelemetryFormatTemplate" />) supplying colors, image and flash
    ///     for whatever this rule does not set itself.
    /// </summary>
    [JsonProperty("templateId")] public string? TemplateId { get; set; }

    /// <summary>Star Citizen action name executed on key press while this rule is active.</summary>
    [JsonProperty("function")] public string? Function { get; set; }

    /// <summary>
    ///     Advanced rules: several actions run in order on key press (each optionally after a delay). When present
    ///     they replace <see cref="Function" />.
    /// </summary>
    [JsonProperty("actions")] public List<TelemetryPressAction>? Actions { get; set; }

    /// <summary>
    ///     The actions to run on key press: <see cref="Actions" /> if any, otherwise the single <see cref="Function" />.
    /// </summary>
    public IReadOnlyList<TelemetryPressAction> GetPressActions()
    {
        List<TelemetryPressAction> configured = (Actions ?? [])
            .Where(a => a != null && !string.IsNullOrWhiteSpace(a.Function))
            .ToList();

        if (configured.Count > 0)
        {
            return configured;
        }

        return string.IsNullOrWhiteSpace(Function) ? [] : [new TelemetryPressAction { Function = Function }];
    }

    /// <summary>Optional sound file (.wav/.mp3) played once when this rule becomes the active one.</summary>
    [JsonProperty("soundPath")] public string? SoundPath { get; set; }

    /// <summary>
    ///     When set, text and image alternate between shown and hidden, each for this many milliseconds,
    ///     while this rule is active. Empty or 0 disables flashing.
    /// </summary>
    [JsonProperty("flashMs")] public int? FlashMs { get; set; }
}

/// <summary>
///     Settings of the Deceleration key: how the estimate is derived and shown.
/// </summary>
public sealed class TelemetryDecelerationSettings
{
    public const string OutputUnits = "units";
    public const string OutputTime = "time";
    public const string OutputBoth = "both";

    public const int DefaultWindowMs = 1000;
    public const int MinWindowMs = 200;
    public const int MaxWindowMs = 30_000;

    /// <summary>Observation period over which the reduction is measured.</summary>
    [JsonProperty("windowMs")] public int? WindowMs { get; set; }

    /// <summary><see cref="OutputUnits" />, <see cref="OutputTime" /> or <see cref="OutputBoth" /> (default).</summary>
    [JsonProperty("output")] public string? Output { get; set; }

    /// <summary>Round the cumulative units to a multiple of this value (for example 50 m). Empty = one decimal.</summary>
    [JsonProperty("unitsRounding")] public double? UnitsRounding { get; set; }

    /// <summary>
    ///     The cumulative units are divided by this before rounding and display, to change the order of magnitude
    ///     (1000: meters become kilometers). Empty or invalid = 1.
    /// </summary>
    [JsonProperty("unitsDivisor")] public double? UnitsDivisor { get; set; }

    [JsonIgnore]
    public double EffectiveUnitsDivisor =>
        UnitsDivisor is { } d && d > 0 && double.IsFinite(d) ? d : 1;

    /// <summary>Round the time (seconds) to a multiple of this value. Empty = one decimal.</summary>
    [JsonProperty("timeRounding")] public double? TimeRounding { get; set; }

    /// <summary>Suffix shown after the cumulative units, for example "m".</summary>
    [JsonProperty("unit")] public string? Unit { get; set; }

    [JsonIgnore]
    public TimeSpan Window =>
        TimeSpan.FromMilliseconds(Math.Clamp(WindowMs ?? DefaultWindowMs, MinWindowMs, MaxWindowMs));

    [JsonIgnore]
    public string EffectiveOutput =>
        string.Equals(Output, OutputUnits, StringComparison.OrdinalIgnoreCase) ? OutputUnits
        : string.Equals(Output, OutputTime, StringComparison.OrdinalIgnoreCase) ? OutputTime
        : OutputBoth;
}

public sealed class TelemetryDisplaySettings
{
    [JsonProperty("fieldPath")] public string? FieldPath { get; set; }

    /// <summary>Deceleration key only.</summary>
    [JsonProperty("deceleration")] public TelemetryDecelerationSettings Deceleration { get; set; } = new();

    /// <summary>Text used for {label}. Empty uses the field's friendly name.</summary>
    [JsonProperty("label")] public string? Label { get; set; }

    [JsonProperty("decimals")] public int? Decimals { get; set; }

    /// <summary>Evaluated in order; the first matching rule wins.</summary>
    [JsonProperty("rules")] public List<TelemetryRule> Rules { get; set; } = [];

    /// <summary>Used when no rule matches (and while there is no data).</summary>
    [JsonProperty("defaultRule")] public TelemetryRule DefaultRule { get; set; } = new();
}
