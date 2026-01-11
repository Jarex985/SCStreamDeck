namespace SCStreamDeck.Models;

/// <summary>
///     Represents a Star Citizen action with its bindings and metadata.
/// </summary>
public sealed class KeybindingAction
{
    public required string ActionName { get; init; }
    public required string MapName { get; init; }
    public string UILabel { get; init; } = string.Empty;
    public string UIDescription { get; init; } = string.Empty;
    public string UICategory { get; init; } = string.Empty;
    public string KeyboardBinding { get; init; } = string.Empty;
    public string MouseBinding { get; init; } = string.Empty;
    public string JoystickBinding { get; init; } = string.Empty;
    public string GamepadBinding { get; init; } = string.Empty;
    public ActivationMode ActivationMode { get; init; } = ActivationMode.press;
}
