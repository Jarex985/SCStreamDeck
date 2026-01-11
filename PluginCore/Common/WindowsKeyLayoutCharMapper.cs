using BarRaider.SdTools;
using WindowsInput.Native;

namespace SCStreamDeck.Common;

/// <summary>
///     Windows helper to translate a physical key (virtual key + scan code) into a character
///     produced on the user's current keyboard layout.
///     Uses ToUnicodeEx and the HKL provided by KeyboardLayoutDetector.
/// </summary>
internal static class WindowsKeyLayoutCharMapper
{
    /// <summary>
    ///     Size of Windows keyboard state array (256 bytes).
    /// </summary>
    private const int KeyboardStateArraySize = 256;

    /// <summary>
    ///     Maximum buffer size for character output from ToUnicodeEx.
    /// </summary>
    private const int CharacterBufferSize = 16;

    /// <summary>
    ///     State indicating a key is pressed (high bit set).
    /// </summary>
    private const byte KeyPressedState = 0x80;

    /// <summary>
    ///     Attempts to get a character representation of a virtual key for the current keyboard layout.
    /// </summary>
    /// <param name="hkl">The keyboard layout handle</param>
    /// <param name="virtualKey">The virtual key code (using VirtualKeyCode enum)</param>
    /// <param name="scanCode">The scan code</param>
    /// <param name="shift">Whether Shift key is pressed</param>
    /// <param name="altGr">Whether AltGr key is pressed</param>
    /// <returns>The character string, or null if conversion failed</returns>
    public static string? TryGetChar(nint hkl, VirtualKeyCode virtualKey, uint scanCode, bool shift, bool altGr)
    {
        try
        {
            byte[] keyState = new byte[KeyboardStateArraySize];
            if (shift)
            {
                keyState[(int)VirtualKeyCode.SHIFT] = KeyPressedState;
            }

            // AltGr is represented as RightAlt (Alt+Ctrl) on Windows.
            if (altGr)
            {
                keyState[(int)VirtualKeyCode.CONTROL] = KeyPressedState;
                keyState[(int)VirtualKeyCode.MENU] = KeyPressedState; // Alt
            }

            char[] buffer = new char[CharacterBufferSize];
            int characterCount = NativeMethods.ToUnicodeEx(
                (uint)virtualKey, scanCode, keyState, buffer, buffer.Length, 0, hkl);


            if (characterCount <= 0)
            {
                return null;
            }

            string result = new(buffer, 0, characterCount);
            return result;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[WindowsKeyLayoutCharMapper] Exception: {ex.Message}");
            return null;
        }
    }
}
