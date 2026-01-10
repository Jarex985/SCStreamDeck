using SCStreamDeck.SCCore.Models;

namespace SCStreamDeck.SCCore.Common;

/// <summary>
///     Constants for input binding strings used in Star Citizen keybindings.
/// </summary>
public static class InputConstants
{
    /// <summary>
    ///     Keyboard key constants.
    ///     Note: Star Citizen XML files use lowercase, but we use UPPERCASE for comparisons
    ///     to avoid Turkish 'i' (I/İ) issues with string comparisons.
    /// </summary>
    public static class Keyboard
    {
        // Modifiers
        public const string LAlt = "LALT";
        public const string RAlt = "RALT";
        public const string LShift = "LSHIFT";
        public const string RShift = "RSHIFT";
        public const string LCtrl = "LCTRL";
        public const string RCtrl = "RCTRL";

        // Special keys (layout-independent)
        public const string Escape = "ESCAPE";
        public const string Space = "SPACE";
        public const string Enter = "ENTER";
        public const string Tab = "TAB";
        public const string Backspace = "BACKSPACE";
        public const string CapsLock = "CAPSLOCK";
        public const string NumLock = "NUMLOCK";
        public const string ScrollLock = "SCROLLLOCK";

        // Navigation
        public const string Up = "UP";
        public const string Down = "DOWN";
        public const string Left = "LEFT";
        public const string Right = "RIGHT";
        public const string Home = "HOME";
        public const string End = "END";
        public const string PgUp = "PGUP";
        public const string PgDown = "PGDOWN";
        public const string Insert = "INSERT";
        public const string Delete = "DELETE";

        // Function keys
        public const string F1 = "F1";
        public const string F2 = "F2";
        public const string F3 = "F3";
        public const string F4 = "F4";
        public const string F5 = "F5";
        public const string F6 = "F6";
        public const string F7 = "F7";
        public const string F8 = "F8";
        public const string F9 = "F9";
        public const string F10 = "F10";
        public const string F11 = "F11";
        public const string F12 = "F12";

        // Other
        public const string HmdPrefix = "HMD_";
    }

    /// <summary>
    ///     Mouse input constants.
    ///     Note: Star Citizen XML files use lowercase, but we use UPPERCASE for comparisons
    ///     to avoid Turkish 'i' (I/İ) issues with string comparisons.
    ///     Mouse Wheel:
    ///     - Discrete Events (Buttons): mwheel_up, mwheel_down (used for Stream Deck buttons)
    ///     Windows VerticalScroll Semantik:
    ///     - Positive value (+1) = content scrolls DOWN (page moves up)
    ///     - Negative value (-1) = content scrolls UP (page moves down)
    ///     (counter-intuitive but follows Windows API convention)
    /// </summary>
    public static class Mouse
    {
        // Mouse Wheel Bindings
        // WheelPrefix is used internally by IsMouseWheel() extension method
        public const string WheelPrefix = "MWHEEL";
        public const string WheelUp = "MWHEEL_UP";
        public const string WheelDown = "MWHEEL_DOWN";

        // Mouse Buttons
        public const string Button1 = "MOUSE1";
        public const string Button2 = "MOUSE2";
        public const string Button3 = "MOUSE3";
        public const string Button4 = "MOUSE4";
        public const string Button5 = "MOUSE5";

        public const string LeftButton = "LMB";
        public const string RightButton = "RMB";
        public const string MiddleButton = "MMB";
    }

    /// <summary>
    ///     Mouse axis constants for Star Citizen axis bindings (e.g., maxis_x).
    ///     Note: Star Citizen XML files use lowercase, but we use UPPERCASE for comparisons
    ///     to avoid Turkish 'i' (I/İ) issues with string comparisons.
    /// </summary>
    public static class MouseAxis
    {
        public const string Prefix = "MAXIS_";
    }
}

/// <summary>
///     Extension methods for input string validation and classification.
/// </summary>
public static class InputStringExtensions
{
    /// <summary>
    ///     Determines if the input string is a keyboard modifier key only.
    /// </summary>
    public static bool IsModifierOnly(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string upper = input.ToUpperInvariant();
        return upper.Equals(InputConstants.Keyboard.LAlt, StringComparison.Ordinal) ||
               upper.Equals(InputConstants.Keyboard.RAlt, StringComparison.Ordinal) ||
               upper.Equals(InputConstants.Keyboard.LShift, StringComparison.Ordinal) ||
               upper.Equals(InputConstants.Keyboard.RShift, StringComparison.Ordinal) ||
               upper.Equals(InputConstants.Keyboard.LCtrl, StringComparison.Ordinal) ||
               upper.Equals(InputConstants.Keyboard.RCtrl, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Determines if the input string is a mouse wheel binding.
    /// </summary>
    public static bool IsMouseWheel(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        // Don't treat mouse wheel bindings with modifiers as pure mouse wheel
        if (input.Contains('+', StringComparison.Ordinal))
        {
            return false;
        }

        string upper = input.ToUpperInvariant();
        return upper.Contains(InputConstants.Mouse.WheelPrefix, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Determines input string is a mouse button (mouse1, mouse2, etc.).
    /// </summary>
    public static bool IsMouseButton(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        // Don't treat mouse buttons with modifiers as pure mouse buttons
        if (input.Contains('+', StringComparison.Ordinal))
        {
            return false;
        }

        string upper = input.ToUpperInvariant();
        return upper.Contains(InputConstants.Mouse.Button1, StringComparison.Ordinal) ||
               upper.Contains(InputConstants.Mouse.Button2, StringComparison.Ordinal) ||
               upper.Contains(InputConstants.Mouse.Button3, StringComparison.Ordinal) ||
               upper.Contains(InputConstants.Mouse.Button4, StringComparison.Ordinal) ||
               upper.Contains(InputConstants.Mouse.Button5, StringComparison.Ordinal) ||
               upper.Equals(InputConstants.Mouse.LeftButton, StringComparison.Ordinal) ||
               upper.Equals(InputConstants.Mouse.RightButton, StringComparison.Ordinal) ||
               upper.Equals(InputConstants.Mouse.MiddleButton, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Determines the input type from a binding string.
    /// </summary>
    public static InputType GetInputType(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return InputType.Unknown;
        }

        string normalized = input.ToUpperInvariant();

        // Check mouse buttons
        if (normalized.Contains(InputConstants.Mouse.Button1) ||
            normalized.Contains(InputConstants.Mouse.Button2) ||
            normalized.Contains(InputConstants.Mouse.Button3) ||
            normalized.Contains(InputConstants.Mouse.Button4) ||
            normalized.Contains(InputConstants.Mouse.Button5) ||
            normalized == InputConstants.Mouse.LeftButton ||
            normalized == InputConstants.Mouse.RightButton ||
            normalized == InputConstants.Mouse.MiddleButton)
        {
            return InputType.MouseButton;
        }

        // Check mouse wheel (discrete events: mwheel_up/down)
        if (normalized.Contains(InputConstants.Mouse.WheelPrefix))
        {
            return InputType.MouseWheel;
        }

        // Check mouse axis
        if (normalized.Contains(InputConstants.MouseAxis.Prefix))
        {
            return InputType.MouseAxis;
        }

        // Default to keyboard for any other input
        return InputType.Keyboard;
    }
}
