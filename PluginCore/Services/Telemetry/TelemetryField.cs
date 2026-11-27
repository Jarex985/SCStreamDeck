using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SCStreamDeck.Services.Telemetry;

public enum TelemetryValueKind
{
    Number,
    Boolean,
    Enum,
    Text
}

/// <summary>
///     One selectable enum member (<see cref="Name" /> is the proto name, <see cref="Label" /> the friendly text).
/// </summary>
public sealed record TelemetryEnumOption(
    [property: JsonProperty("name")] string Name,
    [property: JsonProperty("label")] string Label);

/// <summary>
///     A scalar attribute of <c>TelemetryStreamResponse</c> that can be displayed on a key.
/// </summary>
public sealed record TelemetryField(
    [property: JsonProperty("path")] string Path,
    [property: JsonProperty("label")] string Label,
    [property: JsonProperty("group")] string Group,
    [property: JsonProperty("kind"), JsonConverter(typeof(StringEnumConverter))] TelemetryValueKind Kind,
    [property: JsonProperty("isInteger")] bool IsInteger,
    [property: JsonProperty("enumValues")] IReadOnlyList<TelemetryEnumOption>? EnumValues);

/// <summary>
///     A value read from the telemetry stream. Enums use the proto member name in <see cref="Text" />.
/// </summary>
public readonly record struct TelemetryValue(
    TelemetryValueKind Kind,
    double Number = 0,
    bool Boolean = false,
    string Text = "",
    string DisplayText = "",
    bool IsInteger = false);
