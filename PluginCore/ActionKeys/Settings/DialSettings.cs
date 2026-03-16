using Newtonsoft.Json;

namespace SCStreamDeck.ActionKeys.Settings;

/// <summary>
///     Settings schema for Stream Deck Plus dial actions.
/// </summary>
public sealed class DialSettings
{
    [JsonProperty(PropertyName = "rotateLeftFunction")]
    public string? RotateLeftFunction { get; set; }

    [JsonProperty(PropertyName = "rotateRightFunction")]
    public string? RotateRightFunction { get; set; }

    [JsonProperty(PropertyName = "pressFunction")]
    public string? PressFunction { get; set; }

    [JsonProperty(PropertyName = "clickSoundPath")]
    public string? ClickSoundPath { get; set; }
}
