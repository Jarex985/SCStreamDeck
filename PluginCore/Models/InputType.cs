namespace SCStreamDeck.Models;

/// <summary>
///     Represents the type of input device for a keybinding.
/// </summary>
public enum InputType
{
    Unknown,
    Keyboard,
    Mouse,           // Generic type for UI display (Button or Axis)
    MouseButton,     // Specific for execution (mouse1, mouse2, etc.)
    MouseWheel,      // Specific for execution (scroll up/down)
    MouseAxis,       // Specific for UI/execution (mouse axes)
    Joystick,        // For UI display
    Gamepad          // For UI display
}
