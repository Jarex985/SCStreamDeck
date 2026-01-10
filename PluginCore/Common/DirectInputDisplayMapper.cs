using BarRaider.SdTools;
using WindowsInput.Native;

namespace SCStreamDeck.SCCore.Common;

/// <summary>
///     Maps DirectInput key codes to user-friendly display strings.
///     Uses layout-aware character mapping for typeable keys and fixed labels for special keys.
/// </summary>
internal static class DirectInputDisplayMapper
{
    /// <summary>
    ///     Converts a Star Citizen keyboard binding to a user-friendly display string.
    /// </summary>
    /// <param name="scKeyboardBind">The SC keyboard binding (e.g. "lshift+apostrophe")</param>
    /// <param name="hkl">The keyboard layout handle</param>
    /// <returns>Formatted display string (e.g. "L-Shift + Ä")</returns>
    public static string ToDisplay(string? scKeyboardBind, nint hkl)
    {
        if (string.IsNullOrWhiteSpace(scKeyboardBind))
            return string.Empty;

        var parts = scKeyboardBind.Split(['+'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        
        if (parts.Length == 0)
            return string.Empty;

        return string.Join(" + ", parts.Select(p => TokenToDisplay(p, hkl)));
    }

    private static string TokenToDisplay(string token, nint hkl)
    {
        token = token.Trim().ToLowerInvariant();

        if (SCKeyToDirectInputMapper.TryGetDirectInputKeyCode(token, out var dik))
        {
            return ToDisplay(dik, hkl);
        }

        return token.ToUpperInvariant();
    }

    /// <summary>
    ///     Converts a DirectInput key code to a display string for the specified keyboard layout.
    /// </summary>
    /// <param name="dik">The DirectInput key code</param>
    /// <param name="hkl">The keyboard layout handle</param>
    /// <returns>User-friendly display string</returns>
    public static string ToDisplay(DirectInputKeyCode dik, nint hkl)
    {
        if (TryGetFixedDisplay(dik, out var fixedDisplay))
            return fixedDisplay;

        // Character keys: Nutze Windows API für layout-aware Zeichenermittlung
        var scanCode = (uint)dik;
        var vk = NativeMethods.MapVirtualKeyEx(scanCode, 3, hkl);
        
        if (vk == 0)
        {
            return dik.ToString();
        }

        var virtualKey = (VirtualKeyCode)vk;
        var ch = WindowsKeyLayoutCharMapper.TryGetChar(hkl, virtualKey, scanCode, false, false);
        
        if (string.IsNullOrWhiteSpace(ch))
        {
            return dik.ToString();
        }

        var result = ch.Length == 1 ? ch.ToUpperInvariant() : ch;

        
        return result;
    }

    private static bool TryGetFixedDisplay(DirectInputKeyCode dik, out string display)
    {
        display = dik switch
        {
            // Function keys
            DirectInputKeyCode.DikF1 => "F1",
            DirectInputKeyCode.DikF2 => "F2",
            DirectInputKeyCode.DikF3 => "F3",
            DirectInputKeyCode.DikF4 => "F4",
            DirectInputKeyCode.DikF5 => "F5",
            DirectInputKeyCode.DikF6 => "F6",
            DirectInputKeyCode.DikF7 => "F7",
            DirectInputKeyCode.DikF8 => "F8",
            DirectInputKeyCode.DikF9 => "F9",
            DirectInputKeyCode.DikF10 => "F10",
            DirectInputKeyCode.DikF11 => "F11",
            DirectInputKeyCode.DikF12 => "F12",

            // Special keys
            DirectInputKeyCode.DikEscape => "Esc",
            DirectInputKeyCode.DikSpace => "Space",
            DirectInputKeyCode.DikReturn => "Enter",
            DirectInputKeyCode.DikTab => "Tab",
            DirectInputKeyCode.DikBackspace => "Backspace",
            DirectInputKeyCode.DikCapital => "CapsLock",
            DirectInputKeyCode.DikNumlock => "NumLock",
            DirectInputKeyCode.DikScroll => "ScrollLock",

            // Modifiers
            DirectInputKeyCode.DikLshift => "L-Shift",
            DirectInputKeyCode.DikRshift => "R-Shift",
            DirectInputKeyCode.DikLcontrol => "L-Ctrl",
            DirectInputKeyCode.DikRcontrol => "R-Ctrl",
            DirectInputKeyCode.DikLalt => "L-Alt",
            DirectInputKeyCode.DikRalt => "R-Alt",

            // Navigation
            DirectInputKeyCode.DikUp => "Up",
            DirectInputKeyCode.DikDown => "Down",
            DirectInputKeyCode.DikLeft => "Left",
            DirectInputKeyCode.DikRight => "Right",
            DirectInputKeyCode.DikHome => "Home",
            DirectInputKeyCode.DikEnd => "End",
            DirectInputKeyCode.DikPageUp => "PgUp",
            DirectInputKeyCode.DikPageDown => "PgDn",
            DirectInputKeyCode.DikInsert => "Ins",
            DirectInputKeyCode.DikDelete => "Del",

            // Numpad
            DirectInputKeyCode.DikNumpad0 => "Num0",
            DirectInputKeyCode.DikNumpad1 => "Num1",
            DirectInputKeyCode.DikNumpad2 => "Num2",
            DirectInputKeyCode.DikNumpad3 => "Num3",
            DirectInputKeyCode.DikNumpad4 => "Num4",
            DirectInputKeyCode.DikNumpad5 => "Num5",
            DirectInputKeyCode.DikNumpad6 => "Num6",
            DirectInputKeyCode.DikNumpad7 => "Num7",
            DirectInputKeyCode.DikNumpad8 => "Num8",
            DirectInputKeyCode.DikNumpad9 => "Num9",
            DirectInputKeyCode.DikMultiply => "Num*",
            DirectInputKeyCode.DikAdd => "Num+",
            DirectInputKeyCode.DikSubtract => "Num-",
            DirectInputKeyCode.DikDivide => "Num/",
            DirectInputKeyCode.DikDecimal => "Num.",
            DirectInputKeyCode.DikNumpadenter => "NumEnter",

            _ => string.Empty
        };

        return !string.IsNullOrEmpty(display);
    }
}
