using System.Runtime.InteropServices;
using WindowsInput.Native;

namespace SCStreamDeck.Common;

/// <summary>
///     Maps DirectInput key codes to user-friendly display strings.
/// </summary>
internal static class DirectInputDisplayMapper
{
    #region Constants and Imports

    private const int BufferSize = 256;
    private const int LParamShift = 16;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetKeyNameText(long lParam, [Out] char[] lpString, int cchSize);

    #endregion

    #region Public Interface

    /// <summary>
    ///     Converts SC keyboard binding to display string.
    /// </summary>
    /// <param name="scKeyboardBind">SC binding (e.g. "lshift+apostrophe")</param>
    /// <param name="hkl">Keyboard layout handle</param>
    /// <returns>Formatted display (e.g. "L-Shift + Ä")</returns>
    public static string ToDisplay(string? scKeyboardBind, nint hkl)
    {
        if (string.IsNullOrWhiteSpace(scKeyboardBind))
        {
            return string.Empty;
        }

        string[] parts = ParseBindingParts(scKeyboardBind);
        return parts.Length == 0 ? string.Empty : FormatDisplayString(parts, hkl);
    }

    #endregion

    #region Binding Parsing

    /// <summary>
    ///     Parses binding into token parts.
    /// </summary>
    private static string[] ParseBindingParts(string scKeyboardBind)
    {
        return scKeyboardBind.Split(['+'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    ///     Formats display by converting tokens and joining.
    /// </summary>
    private static string FormatDisplayString(string[] parts, nint hkl)
    {
        return string.Join(" + ", parts.Select(p => TokenToDisplay(p, hkl)));
    }

    #endregion

    #region Token Processing

    private static string TokenToDisplay(string token, nint hkl)
    {
        token = token.Trim().ToLowerInvariant();

        return SCKeyToDirectInputMapper.TryGetDirectInputKeyCode(token, out DirectInputKeyCode dik)
            ? ToDisplay(dik, hkl) : token;
    }

    #endregion

    #region Key Resolution

    /// <summary>
    ///     Converts DIK to display string for given layout.
    ///     1. Try fixed names for special/modifier keys.
    ///     2. For typeable keys: DIK → VK → layout-aware char.
    /// </summary>
    /// <param name="dik">DirectInput key code</param>
    /// <param name="hkl">Keyboard layout handle</param>
    /// <returns>User-friendly display string</returns>
    private static string ToDisplay(DirectInputKeyCode dik, nint hkl)
    {
        if (TryGetFixedDisplay(dik, out string fixedDisplay))
        {
            return fixedDisplay;
        }

        // Typeable keys: Use Windows API for layout-aware detection
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

        string result = ToTitleCase(ch.Length == 1 ? ch.ToUpperInvariant() : ch);
        return result;
    }

    /// <summary>
    ///     Checks if DIK is a modifier key requiring L/R distinction.
    /// </summary>
    internal static bool IsModifierKey(DirectInputKeyCode dik)
    {
        return dik is DirectInputKeyCode.DikLshift or DirectInputKeyCode.DikRshift or
               DirectInputKeyCode.DikLcontrol or DirectInputKeyCode.DikRcontrol or
               DirectInputKeyCode.DikLalt or DirectInputKeyCode.DikRalt;
    }

    #endregion

    #region Fixed Displays

    private static bool TryGetFixedDisplay(DirectInputKeyCode dik, out string display)
    {
        // First, check explicit mappings for consistency (e.g., Numpad, Modifiers)
        display = dik switch
        {
            // Legacy specials - now via GetKeyNameText
            /*DirectInputKeyCode.DikEscape => "Esc",
            DirectInputKeyCode.DikSpace => "Space",
            DirectInputKeyCode.DikReturn => "Enter",
            DirectInputKeyCode.DikTab => "Tab",
            DirectInputKeyCode.DikBackspace => "Backspace",
            DirectInputKeyCode.DikCapital => "CapsLock",
            DirectInputKeyCode.DikNumlock => "NumLock",
            DirectInputKeyCode.DikScroll => "ScrollLock",*/

            // Modifiers - explicit for L/R distinction
            DirectInputKeyCode.DikLshift => "L-Shift",
            DirectInputKeyCode.DikRshift => "R-Shift",
            DirectInputKeyCode.DikLcontrol => "L-Ctrl",
            DirectInputKeyCode.DikRcontrol => "R-Ctrl",
            DirectInputKeyCode.DikLalt => "L-Alt",
            DirectInputKeyCode.DikRalt => "R-Alt",

            // Navigation - now handled by GetKeyNameText with RemoveDikPrefix
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

            // Numpad - explicit for consistency
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

        if (!string.IsNullOrEmpty(display))
        {
            return true;
        }

        // Fallback to GetKeyNameText for other keys
        string? keyName = TryGetKeyNameTextFromDik(dik);

        if (!string.IsNullOrWhiteSpace(keyName) && !IsModifierKey(dik))
        {
            display = ToTitleCase(RemoveDikPrefix(keyName));
            return true;
        }

        if (IsModifierKey(dik))
        {
            return false;
        }

        display = ToTitleCase(RemoveDikPrefix(dik.ToString()));
        return true;
    }

    #endregion

    #region Windows API Helpers

    /// <summary>
    ///     Gets localized key name for DIK using GetKeyNameText.
    /// </summary>
    internal static string? TryGetKeyNameTextFromDik(DirectInputKeyCode dik)
    {
        uint scanCode = (uint)dik;
        return GetKeyNameTextFromScanCode(scanCode);
    }

    /// <summary>
    ///     Gets key name via Windows API.
    /// </summary>
    private static string? GetKeyNameTextFromScanCode(uint scanCode)
    {
        char[] buffer = new char[BufferSize];
        long lParam = scanCode << LParamShift; // lParam format for GetKeyNameText
        int result = GetKeyNameText(lParam, buffer, BufferSize);
        return result > 0 ? new string(buffer, 0, result) : null;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    ///     Removes 'Dik' or 'dik' prefix from key name if present.
    /// </summary>
    private static string RemoveDikPrefix(string keyName)
    {
        return keyName.StartsWith("Dik", StringComparison.OrdinalIgnoreCase) ? keyName[3..] : keyName;
    }

    /// <summary>
    ///     Converts string to title case (first letter uppercase, rest lowercase).
    /// </summary>
    private static string ToTitleCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return char.ToUpperInvariant(input[0]) + input[1..].ToLowerInvariant();
    }

    #endregion
}
