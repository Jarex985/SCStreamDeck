using System.Runtime.InteropServices;

namespace SCStreamDeck.Common;

/// <summary>
///     Wraps the Win32 <c>GetOpenFileNameW</c> system dialog so the plugin
///     can present its own native file picker. Necessary because
///     OpenDeck's Property Inspector runs inside a Tauri WebView that
///     doesn't grant <c>dialog:*</c> capabilities to plugin windows, and
///     plain HTML <c>&lt;input type="file"&gt;</c> in that WebView only
///     reveals the basename, not the full path.
///
///     Under Wine on Linux, <c>comdlg32!GetOpenFileNameW</c> renders as a
///     native GTK file chooser, so the user sees a familiar Linux dialog
///     and the returned path is Wine-resolvable (typically <c>Z:\…</c>
///     for Unix files).
///
///     The dialog is modal and requires an STA thread, so callers should
///     invoke <see cref="ShowOpenFileDialogAsync"/> which spawns a
///     dedicated STA worker.
/// </summary>
internal static class WineFileDialog
{
    private const int OfnFileMustExist = 0x1000;
    private const int OfnPathMustExist = 0x0800;
    private const int OfnNoChangeDir = 0x0008;

    private const int MaxPath = 4096;

    // Note on string fields: LPCWSTR fields that may contain embedded
    // NUL bytes (`lpstrFilter`, `lpstrCustomFilter`) MUST be passed as
    // raw pointers — managed-string marshaling truncates at the first
    // NUL. Filter format is e.g. "Name1\0Pattern1\0Name2\0Pattern2\0\0".
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OPENFILENAMEW
    {
        public int lStructSize;
        public nint hwndOwner;
        public nint hInstance;
        public nint lpstrFilter;            // raw pointer (embedded NULs)
        public nint lpstrCustomFilter;      // raw pointer (embedded NULs) or 0
        public int nMaxCustFilter;
        public int nFilterIndex;
        public nint lpstrFile;
        public int nMaxFile;
        public nint lpstrFileTitle;
        public int nMaxFileTitle;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpstrInitialDir;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpstrTitle;
        public int Flags;
        public ushort nFileOffset;
        public ushort nFileExtension;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpstrDefExt;
        public nint lCustData;
        public nint lpfnHook;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpTemplateName;
        public nint pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool GetOpenFileNameW(ref OPENFILENAMEW lpofn);

    /// <summary>
    ///     Show a Win32 open-file dialog and return the picked path.
    ///     Returns <c>null</c> on cancel.
    /// </summary>
    /// <param name="title">Window title.</param>
    /// <param name="filter">
    ///     Filter pattern using the standard Windows format with <c>|</c>
    ///     as the separator, e.g. <c>"Data.p4k|*.p4k|All files|*.*"</c>.
    ///     The method converts to the native NUL-delimited form.
    /// </param>
    public static Task<string?> ShowOpenFileDialogAsync(string title, string filter)
    {
        TaskCompletionSource<string?> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread t = new(() =>
        {
            try
            {
                tcs.SetResult(ShowOpenFileDialog(title, filter));
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        // GetOpenFileNameW uses COM internally — STA is required for the
        // shell file dialog to function correctly.
        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Name = "WineFileDialog";
        t.Start();
        return tcs.Task;
    }

    private static string? ShowOpenFileDialog(string title, string filter)
    {
        nint fileBuffer = Marshal.AllocHGlobal(MaxPath * sizeof(char));
        nint filterBuffer = AllocFilterBuffer(filter);
        try
        {
            // Zero-fill the file buffer — GetOpenFileName uses it as in/out.
            for (int i = 0; i < MaxPath; i++)
            {
                Marshal.WriteInt16(fileBuffer, i * sizeof(char), 0);
            }

            OPENFILENAMEW ofn = new()
            {
                lStructSize = Marshal.SizeOf<OPENFILENAMEW>(),
                lpstrTitle = title,
                lpstrFilter = filterBuffer,
                lpstrFile = fileBuffer,
                nMaxFile = MaxPath,
                Flags = OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir,
            };

            if (!GetOpenFileNameW(ref ofn))
            {
                return null;
            }

            return Marshal.PtrToStringUni(fileBuffer);
        }
        finally
        {
            Marshal.FreeHGlobal(fileBuffer);
            if (filterBuffer != nint.Zero)
            {
                Marshal.FreeHGlobal(filterBuffer);
            }
        }
    }

    // Filter input: pipe-separated pairs, e.g. "Data.p4k|*.p4k|All files|*.*".
    // Allocates a UTF-16 buffer with each segment followed by NUL and a final
    // double-NUL terminator — the format required by GetOpenFileName.
    private static nint AllocFilterBuffer(string filter)
    {
        string[] parts = string.IsNullOrEmpty(filter)
            ? ["All files", "*.*"]
            : filter.Split('|');

        // Total UTF-16 char count = sum(parts.length + 1 NUL each) + 1 final NUL.
        int totalChars = 1; // final terminating NUL
        foreach (string p in parts)
        {
            totalChars += p.Length + 1;
        }

        nint buf = Marshal.AllocHGlobal(totalChars * sizeof(char));
        int offset = 0;
        foreach (string p in parts)
        {
            foreach (char c in p)
            {
                Marshal.WriteInt16(buf, offset, c);
                offset += sizeof(char);
            }
            Marshal.WriteInt16(buf, offset, 0); // NUL after each part
            offset += sizeof(char);
        }
        Marshal.WriteInt16(buf, offset, 0); // final double-NUL
        return buf;
    }
}
