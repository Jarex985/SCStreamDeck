namespace SCStreamDeck.Models;

/// <summary>
///     Represents a Star Citizen action with its bindings and metadata.
/// </summary>
public sealed class KeybindingAction
{
    public required string ActionName { get; init; }
    public required string MapName { get; init; }
    public string MapLabel { get; init; } = string.Empty;
    public string UiLabel { get; init; } = string.Empty;
    public string UiDescription { get; init; } = string.Empty;
    public string UiCategory { get; init; } = string.Empty;
    public string KeyboardBinding { get; init; } = string.Empty;
    public string MouseBinding { get; init; } = string.Empty;
    public string JoystickBinding { get; init; } = string.Empty;
    public string GamepadBinding { get; init; } = string.Empty;
    public ActivationMode ActivationMode { get; init; } = ActivationMode.press;
}
