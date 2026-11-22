using Newtonsoft.Json;

namespace SCStreamDeck.Services.Telemetry;

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

    [JsonProperty("op")] public string Op { get; set; } = OpAlways;

    [JsonProperty("value")] public string? Value { get; set; }

    /// <summary>Upper bound (inclusive) for <see cref="OpBetween" />; <see cref="Value" /> is the lower bound.</summary>
    [JsonProperty("value2")] public string? Value2 { get; set; }

    /// <summary>Text template, supports {value} and {label} and line breaks. Empty uses the default template.</summary>
    [JsonProperty("text")] public string? Text { get; set; }

    [JsonProperty("imagePath")] public string? ImagePath { get; set; }

    [JsonProperty("textColor")] public string? TextColor { get; set; }

    [JsonProperty("backgroundColor")] public string? BackgroundColor { get; set; }

    /// <summary>Star Citizen action name executed on key press while this rule is active.</summary>
    [JsonProperty("function")] public string? Function { get; set; }

    /// <summary>
    ///     When set, text and image alternate between shown and hidden, each for this many milliseconds,
    ///     while this rule is active. Empty or 0 disables flashing.
    /// </summary>
    [JsonProperty("flashMs")] public int? FlashMs { get; set; }
}

public sealed class TelemetryDisplaySettings
{
    [JsonProperty("fieldPath")] public string? FieldPath { get; set; }

    /// <summary>Text used for {label}. Empty uses the field's friendly name.</summary>
    [JsonProperty("label")] public string? Label { get; set; }

    [JsonProperty("decimals")] public int? Decimals { get; set; }

    /// <summary>Evaluated in order; the first matching rule wins.</summary>
    [JsonProperty("rules")] public List<TelemetryRule> Rules { get; set; } = [];

    /// <summary>Used when no rule matches (and while there is no data).</summary>
    [JsonProperty("defaultRule")] public TelemetryRule DefaultRule { get; set; } = new();
}
