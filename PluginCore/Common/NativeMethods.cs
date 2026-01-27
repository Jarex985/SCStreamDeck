using System.Runtime.InteropServices;

namespace SCStreamDeck.Common;

/// <summary>
///     Centralized Windows API P/Invoke declarations.
/// </summary>
internal static class NativeMethods
{
    /// <summary>
    ///     Maps a scan code to a virtual-key code.
    /// </summary>
    /// <param name="uCode">The scan code or virtual-key code to be translated</param>
    /// <param name="uMapType">
    ///     The translation to be performed:
    ///     0 = MAPVK_VK_TO_VSC (virtual key to scan code)
    ///     1 = MAPVK_VSC_TO_VK (scan code to virtual key)
    ///     2 = MAPVK_VK_TO_CHAR (virtual key to character)
    ///     3 = MAPVK_VSC_TO_VK_EX (scan code to virtual key, distinguishes left/right keys)
    /// </param>
    /// <param name="dwhkl">Handle to the keyboard layout</param>
    /// <returns>The resulting virtual key code or scan code</returns>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern uint MapVirtualKeyEx(uint uCode, uint uMapType, nint dwhkl);

    /// <summary>
    ///     Translates the specified virtual-key code and keyboard state to the corresponding Unicode character or characters.
    /// </summary>
    /// <param name="wVirtKey">The virtual-key code to be translated</param>
    /// <param name="wScanCode">The hardware scan code of the key</param>
    /// <param name="lpKeyState">
    ///     A 256-byte array that contains the current keyboard state.
    ///     Each element (byte) in the array contains the state of one key.
    ///     If the high-order bit of a byte is set, the key is down.
    /// </param>
    /// <param name="pwszBuff">
    ///     Buffer that receives the translated Unicode character or characters.
    /// </param>
    /// <param name="cchBuff">The size, in characters, of the buffer</param>
    /// <param name="wFlags">
    ///     Behavior of the function. If bit 0 is set, a menu is in the active state.
    /// </param>
    /// <param name="dwhkl">Handle to the keyboard layout</param>
    /// <returns>
    ///     Return value is the number of Unicode characters in the buffer.
    ///     If the key is a dead key, the return value is negative.
    /// </returns>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int ToUnicodeEx(
        uint wVirtKey,
        uint wScanCode,
        byte[] lpKeyState,
        char[] pwszBuff,
        int cchBuff,
        uint wFlags,
        nint dwhkl);
}
