using System.Runtime.InteropServices;

namespace SCStreamDeck.Common;

/// <summary>
///     Wraps the modern Win32 Common Item Dialog (<c>IFileOpenDialog</c>)
///     so the plugin can present its own native file picker. Necessary
///     because OpenDeck's Property Inspector runs inside a Tauri WebView
///     that doesn't grant <c>dialog:*</c> capabilities to plugin windows,
///     and plain HTML <c>&lt;input type="file"&gt;</c> in that WebView
///     only reveals the basename, not the full path.
///
///     Under Wine on Linux, <c>comdlg32</c>'s <c>IFileOpenDialog</c>
///     implementation has been mature for years
///     (<c>dlls/comdlg32/itemdlg.c</c>) and renders as a native dialog,
///     so the user sees a familiar Linux file chooser and the returned
///     path is Wine-resolvable (typically <c>Z:\…</c> for Unix files).
///
///     On native Windows the COM dialog is the recommended API since
///     Vista (2007) and gives the modern shell-style dialog.
///
///     The dialog is modal and the COM apartment must be STA, so callers
///     should invoke <see cref="ShowOpenFileDialogAsync"/> which spawns a
///     dedicated STA worker.
/// </summary>
internal static class WineFileDialog
{
    // FILEOPENDIALOGOPTIONS — values from shobjidl_core.h
    private const uint FOS_NOCHANGEDIR    = 0x00000008;
    private const uint FOS_PATHMUSTEXIST  = 0x00000800;
    private const uint FOS_FILEMUSTEXIST  = 0x00001000;
    private const uint FOS_FORCEFILESYSTEM = 0x00000040;

    // SIGDN — SIGDN_FILESYSPATH gives us a normal "X:\foo\bar.ext" path
    // (the only SIGDN guaranteed to map to a real filesystem location).
    private const uint SIGDN_FILESYSPATH = 0x80058000;

    private static readonly Guid CLSID_FileOpenDialog = new("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7");

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(
        in Guid rclsid,
        nint pUnkOuter,
        uint dwClsContext,
        in Guid riid,
        out nint ppv);

    private const uint CLSCTX_INPROC_SERVER = 0x1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct COMDLG_FILTERSPEC
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string pszName;
        [MarshalAs(UnmanagedType.LPWStr)] public string pszSpec;
    }

    [ComImport]
    [Guid("b4db1657-70d7-485e-8e3e-6fcb5a5c1802")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IModalWindow
    {
        [PreserveSig]
        int Show(nint hwndParent);
    }

    [ComImport]
    [Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileDialog
    {
        // IModalWindow
        [PreserveSig] int Show(nint hwndParent);
        // IFileDialog
        void SetFileTypes(uint cFileTypes, [MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise(nint pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(uint fos);
        void GetOptions(out uint pfos);
        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int fdap);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close(int hr);
        void SetClientGuid(in Guid guid);
        void ClearClientData();
        void SetFilter(nint pFilter);
    }

    [ComImport]
    [Guid("d57c7288-d4ad-4768-be02-9d969532d960")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog : IFileDialog
    {
        // (IFileOpenDialog extends IFileDialog; we don't need the
        // multi-select / current-results methods so they are omitted.)
    }

    [ComImport]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(nint pbc, in Guid bhid, in Guid riid, out nint ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

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
        // IFileOpenDialog uses COM internally and requires STA.
        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Name = "WineFileDialog";
        t.Start();
        return tcs.Task;
    }

    private static string? ShowOpenFileDialog(string title, string filter)
    {
        Guid clsid = CLSID_FileOpenDialog;
        Guid iid = typeof(IFileOpenDialog).GUID;

        int hr = CoCreateInstance(in clsid, nint.Zero, CLSCTX_INPROC_SERVER, in iid, out nint pDialog);
        if (hr != 0 || pDialog == nint.Zero)
        {
            return null;
        }

        IFileOpenDialog dialog = (IFileOpenDialog)Marshal.GetObjectForIUnknown(pDialog);
        Marshal.Release(pDialog);

        try
        {
            if (!string.IsNullOrEmpty(title))
            {
                dialog.SetTitle(title);
            }

            COMDLG_FILTERSPEC[] specs = ParseFilter(filter);
            if (specs.Length > 0)
            {
                dialog.SetFileTypes((uint)specs.Length, specs);
                dialog.SetFileTypeIndex(1); // 1-based; default to first filter
            }

            dialog.SetOptions(FOS_FILEMUSTEXIST | FOS_PATHMUSTEXIST | FOS_NOCHANGEDIR | FOS_FORCEFILESYSTEM);

            // Show with no parent window — the dialog manages itself.
            // S_OK = picked, anything else (incl. HRESULT_FROM_WIN32(ERROR_CANCELLED) = 0x800704C7) = cancel/error.
            int showHr = dialog.Show(nint.Zero);
            if (showHr != 0)
            {
                return null;
            }

            dialog.GetResult(out IShellItem item);
            try
            {
                item.GetDisplayName(SIGDN_FILESYSPATH, out string path);
                return path;
            }
            finally
            {
                Marshal.ReleaseComObject(item);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    // Filter input: pipe-separated pairs, e.g. "Data.p4k|*.p4k|All files|*.*".
    // IFileDialog wants COMDLG_FILTERSPEC[]{ {pszName, pszSpec}, ... } with
    // multiple patterns inside a single pszSpec separated by ';'.
    private static COMDLG_FILTERSPEC[] ParseFilter(string filter)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return [new COMDLG_FILTERSPEC { pszName = "All files", pszSpec = "*.*" }];
        }

        string[] parts = filter.Split('|');
        int pairs = parts.Length / 2;
        if (pairs == 0)
        {
            return [new COMDLG_FILTERSPEC { pszName = "All files", pszSpec = "*.*" }];
        }

        var result = new COMDLG_FILTERSPEC[pairs];
        for (int i = 0; i < pairs; i++)
        {
            result[i].pszName = parts[i * 2];
            result[i].pszSpec = parts[i * 2 + 1];
        }
        return result;
    }
}
