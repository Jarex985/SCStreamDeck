using System;
using System.Runtime.InteropServices;

namespace WindowsInput
{
    /// <summary>
    ///     Runtime check for whether the process is running on top of Wine (a
    ///     Linux/macOS Win32 translation layer). The standard way is to look up
    ///     the <c>wine_get_version</c> export of <c>ntdll.dll</c> — present
    ///     under Wine, absent on native Windows.
    /// </summary>
    internal static class WineDetector
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        private static readonly Lazy<bool> _isWine = new Lazy<bool>(() =>
        {
            try
            {
                IntPtr ntdll = LoadLibrary("ntdll.dll");
                return ntdll != IntPtr.Zero
                    && GetProcAddress(ntdll, "wine_get_version") != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        });

        public static bool IsWine => _isWine.Value;
    }
}
