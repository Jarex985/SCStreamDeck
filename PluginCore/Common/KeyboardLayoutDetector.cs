using System.Runtime.InteropServices;

namespace SCStreamDeck.Common;

/// <summary>
///     Detects the current Windows keyboard layout.
///     Thread-safe with caching to avoid repeated Win32 API calls.
/// </summary>
public static partial class KeyboardLayoutDetector
{
    /// <summary>
    ///     Default US English keyboard layout HKL (0x04090409).
    ///     Used as fallback if GetKeyboardLayout fails.
    /// </summary>
    private static readonly nint s_defaultUsEnglishHkl = new(0x04090409);

    private static readonly object s_lock = new();
    private static KeyboardLayoutInfo? s_cached;

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint GetKeyboardLayout(uint idThread);

    /// <summary>
    ///     Detects the current keyboard layout.
    ///     Cached after the first call to avoid repeated Win32 calls.
    ///     Call <see cref="InvalidateCache" /> when keyboard layout changes are detected.
    /// </summary>
    public static KeyboardLayoutInfo DetectCurrent()
    {
        lock (s_lock)
        {
            if (s_cached is not null)
            {
                return s_cached;
            }

            IntPtr hklPtr = GetKeyboardLayout(0);

            // GetKeyboardLayout returns 0 (IntPtr.Zero) on failure
            // Fall back to US English layout if detection fails, although this is unlikely
            IntPtr hkl = hklPtr == IntPtr.Zero ? s_defaultUsEnglishHkl : hklPtr;

            s_cached = new KeyboardLayoutInfo(hkl);
            return s_cached;
        }
    }


    // TODO: Implement a way to listen for keyboard layout changes in the application - if possible.
    /// <summary>
    ///     Clears the cached keyboard layout, forcing the next call to <see cref="DetectCurrent" />
    ///     to re-detect the current layout. Call this when a keyboard layout change is detected.
    /// </summary>
    public static void InvalidateCache()
    {
        lock (s_lock)
        {
            s_cached = null;
        }
    }
}

/// <summary>
///     Represents Windows keyboard layout information.
/// </summary>
/// <param name="Hkl">The keyboard layout handle (HKL) from Windows.</param>
public sealed record KeyboardLayoutInfo(nint Hkl);
