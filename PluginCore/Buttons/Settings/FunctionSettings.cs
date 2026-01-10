using Newtonsoft.Json;

namespace SCStreamDeck.SCCore.Buttons.Settings;

/// <summary>
///     Shared settings schema for buttons that execute a single Star Citizen function.
/// </summary>
public sealed class FunctionSettings
{
    /// <summary>
    ///     Star Citizen action name, as used in defaultProfile.xml (e.g. "v_vehicle_enter").
    /// </summary>
    [JsonProperty(PropertyName = "function")]
    public string? Function { get; set; }
}