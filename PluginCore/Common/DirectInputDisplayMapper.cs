using System.Runtime.InteropServices;
using WindowsInput.Native;

namespace SCStreamDeck.SCCore.Common;

/// <summary>
///     Maps DirectInput key codes to user-friendly display strings.
///     Uses layout-aware character mapping for typeable keys and fixed labels for special keys.
/// </summary>
internal static partial class DirectInputDisplayMapper
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetKeyNameText(long lParam, [Out] char[] lpString, int cchSize);

    /// <summary>
    ///     Gets the key name using Windows API (for testing GetKeyNameText).
    /// </summary>
    private static string? GetKeyNameFromScanCode(uint scanCode)
    {
        const int bufferSize = 256;
        char[] buffer = new char[bufferSize];
        long lParam = scanCode << 16; // lParam format for GetKeyNameText
        int result = GetKeyNameText(lParam, buffer, bufferSize);
        return result > 0 ? new string(buffer, 0, result) : null;
    }

    /// <summary>
    ///     Test method to check if GetKeyNameText works for a DIK.
    /// </summary>
    public static string? TestGetKeyName(DirectInputKeyCode dik)
    {
        // Convert DIK to scan code (approximate, as DIK is not directly scan code)
        uint scanCode = (uint)dik;
        return GetKeyNameFromScanCode(scanCode);
    }

    /// <summary>
    ///     Converts a Star Citizen keyboard binding to a user-friendly display string.
    /// </summary>
    /// <param name="scKeyboardBind">The SC keyboard binding (e.g. "lshift+apostrophe")</param>
    /// <param name="hkl">The keyboard layout handle</param>
    /// <returns>Formatted display string (e.g. "L-Shift + Ä")</returns>
    public static string ToDisplay(string? scKeyboardBind, nint hkl)
    {
        if (string.IsNullOrWhiteSpace(scKeyboardBind))
        {
            return string.Empty;
        }

        string[] parts = scKeyboardBind.Split(['+'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            return string.Empty;
        }

        return string.Join(" + ", parts.Select(p => TokenToDisplay(p, hkl)));
    }

    private static string TokenToDisplay(string token, nint hkl)
    {
        token = token.Trim().ToLowerInvariant();

        if (SCKeyToDirectInputMapper.TryGetDirectInputKeyCode(token, out DirectInputKeyCode dik))
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
        if (TryGetFixedDisplay(dik, out string fixedDisplay))
        {
            return fixedDisplay;
        }

        // Character keys: Nutze Windows API für layout-aware Zeichenermittlung
        uint scanCode = (uint)dik;
        uint vk = NativeMethods.MapVirtualKeyEx(scanCode, 3, hkl);

        if (vk == 0)
        {
            return dik.ToString();
        }

        VirtualKeyCode virtualKey = (VirtualKeyCode)vk;
        string? ch = WindowsKeyLayoutCharMapper.TryGetChar(hkl, virtualKey, scanCode, false, false);

        if (string.IsNullOrWhiteSpace(ch))
        {
            return dik.ToString();
        }

        string result = ch.Length == 1 ? ch.ToUpperInvariant() : ch;


        return result;
    }

    private static bool TryGetFixedDisplay(DirectInputKeyCode dik, out string display)
    {
        // Try GetKeyNameText first for most keys
        string? keyName = TestGetKeyName(dik);
        if (!string.IsNullOrWhiteSpace(keyName))
        {
            // Use GetKeyNameText for all keys except modifiers (to ensure L/R distinction)
            if (!IsModifierKey(dik))
            {
                display = keyName.ToUpperInvariant();
                return true;
            }
        }

        // Fallback to switch only for modifier keys that need specific L/R names
        display = dik switch
        {
            // Special keys
            /*DirectInputKeyCode.DikEscape => "Esc",
            DirectInputKeyCode.DikSpace => "Space",
            DirectInputKeyCode.DikReturn => "Enter",
            DirectInputKeyCode.DikTab => "Tab",
            DirectInputKeyCode.DikBackspace => "Backspace",
            DirectInputKeyCode.DikCapital => "CapsLock",
            DirectInputKeyCode.DikNumlock => "NumLock",
            DirectInputKeyCode.DikScroll => "ScrollLock",*/

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
            /*DirectInputKeyCode.DikNumpad0 => "Num0",
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
            DirectInputKeyCode.DikNumpadenter => "NumEnter",*/

            _ => string.Empty
        };

        return !string.IsNullOrEmpty(display);
    }

    private static bool IsModifierKey(DirectInputKeyCode dik)
    {
        return dik is DirectInputKeyCode.DikLshift or DirectInputKeyCode.DikRshift or
               DirectInputKeyCode.DikLcontrol or DirectInputKeyCode.DikRcontrol or
               DirectInputKeyCode.DikLalt or DirectInputKeyCode.DikRalt;
    }
}
