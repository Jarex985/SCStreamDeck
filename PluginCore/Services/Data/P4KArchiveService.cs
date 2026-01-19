using System.Reflection;
using System.Text;
using BarRaider.SdTools;
using ICSharpCode.SharpZipLib.Zip;
using SCStreamDeck.Common;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Data;

/// <summary>
///     Modern P4K archive service using SharpZipLib directly.
/// </summary>
public sealed class P4KArchiveService : IP4KArchiveService, IDisposable
{
    private static readonly Lazy<PropertyInfo?> EncryptionKeyProperty = new(() =>
        typeof(ZipFile).GetProperty("Key", BindingFlags.NonPublic | BindingFlags.Instance));

    private readonly object _lock = new();
    private bool _disposed;
    private FileStream? _fileStream;
    private ZipFile? _zipFile;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            CloseArchiveInternal();
            _disposed = true;
        }
    }

    public bool IsArchiveOpen
    {
        get
        {
            lock (_lock)
            {
                return _zipFile != null && !_disposed;
            }
        }
    }

    public async Task<bool> OpenArchiveAsync(string p4KPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(p4KPath))
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"[SCCore.P4K] {ErrorMessages.InvalidPath}");
            return false;
        }

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!SecurePathValidator.TryNormalizePath(p4KPath, out string validatedPath))
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"[SCCore.P4K] {ErrorMessages.InvalidPath}");
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(validatedPath))
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"[SCCore.P4K] P4K archive not found: '{validatedPath}'");
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return OpenArchiveInternal(validatedPath, cancellationToken);
            }
            catch (IOException ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[SCCore.P4K] {ex.Message}");

                return false;
            }
            catch (ZipException ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[SCCore.P4K] {ex.Message}");
                return false;
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<P4KFileEntry>> ScanDirectoryAsync(string directory, string filePattern,
        CancellationToken cancellationToken = default)
    {
        ZipFile? zipFile = GetZipFileSnapshot();
        if (zipFile == null)
        {
            return Array.Empty<P4KFileEntry>();
        }

        return await Task.Run(() => ScanDirectoryInternal(zipFile, directory, filePattern, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<byte[]?> ReadFileAsync(P4KFileEntry entry, CancellationToken cancellationToken = default)
    {
        ZipFile? zipFile = GetZipFileSnapshot();
        if (zipFile == null)
        {
            return null;
        }

        return await Task.Run(() => ReadFileInternal(zipFile, entry, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<string?> ReadFileAsTextAsync(P4KFileEntry entry, CancellationToken cancellationToken = default)
    {
        byte[]? bytes = await ReadFileAsync(entry, cancellationToken).ConfigureAwait(false);
        if (bytes == null || bytes.Length == 0)
        {
            return null;
        }

        try
        {
            return Encoding.UTF8.GetString(bytes);
        }
        catch (DecoderFallbackException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[SCCore.P4K] {ErrorMessages.P4KDecodeTextFailed}: {ex.Message}");
            return null;
        }
        catch (ArgumentException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[SCCore.P4K] {ErrorMessages.P4KDecodeTextFailed}: {ex.Message}");
            return null;
        }
    }

    public void CloseArchive()
    {
        lock (_lock)
        {
            CloseArchiveInternal();
        }
    }

    private bool OpenArchiveInternal(string validatedPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            CloseArchiveInternal();

            _fileStream = File.OpenRead(validatedPath);
            cancellationToken.ThrowIfCancellationRequested();

            _zipFile = new ZipFile(_fileStream);
            cancellationToken.ThrowIfCancellationRequested();

            if (!TrySetEncryptionKey(_zipFile))
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"[SCCore.P4K] {ErrorMessages.P4KEncryptionKey}");
                CloseArchiveInternal();
                return false;
            }

            return true;
        }
    }

    private ZipFile? GetZipFileSnapshot()
    {
        lock (_lock)
        {
            return _zipFile;
        }
    }

    private void CloseArchiveInternal()
    {
        if (_zipFile != null)
        {
            _zipFile.Close();
            _zipFile = null;
        }

        if (_fileStream != null)
        {
            _fileStream.Dispose();
            _fileStream = null;
        }
    }

    private static IReadOnlyList<P4KFileEntry> ScanDirectoryInternal(ZipFile zipFile, string directory, string filePattern,
        CancellationToken cancellationToken)
    {
        try
        {
            List<P4KFileEntry> results = new();
            string normalizedPattern = NormalizePath(filePattern);
            string normalizedDirectory = NormalizePath(directory);

            foreach (ZipEntry? entry in zipFile)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry == null || string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                string normalizedEntryName = NormalizePath(entry.Name);

                if (normalizedEntryName.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase) &&
                    normalizedEntryName.EndsWith(normalizedPattern, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new P4KFileEntry
                    {
                        Path = entry.Name,
                        Offset = entry.Offset,
                        CompressedSize = entry.CompressedSize,
                        UncompressedSize = entry.Size,
                        IsCompressed = entry.CompressionMethod != CompressionMethod.Stored
                    });
                }
            }

            return results;
        }
        catch (ObjectDisposedException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[SCCore.P4K] Failed to scan P4K directory: {ex.Message}");
            return Array.Empty<P4KFileEntry>();
        }
        catch (InvalidOperationException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[SCCore.P4K] Failed to scan P4K directory: {ex.Message}");
            return Array.Empty<P4KFileEntry>();
        }
    }

    private static byte[]? ReadFileInternal(ZipFile zipFile, P4KFileEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            ZipEntry? zipEntry = FindZipEntry(zipFile, entry.Path);
            if (zipEntry == null)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[SCCore.P4K] P4K entry not found: '{entry.Path}'");
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            using Stream? stream = zipFile.GetInputStream(zipEntry);
            using MemoryStream memoryStream = new((int)zipEntry.Size);

            cancellationToken.ThrowIfCancellationRequested();
            stream.CopyTo(memoryStream);
            cancellationToken.ThrowIfCancellationRequested();

            byte[] data = memoryStream.ToArray();

            return data;
        }
        catch (ObjectDisposedException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[SCCore.P4K] Failed to read P4K file '{entry.Path}': {ex.Message}");
            return null;
        }
        catch (ZipException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[SCCore.P4K] Failed to read P4K file '{entry.Path}': {ex.Message}");
            return null;
        }
        catch (IOException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[SCCore.P4K] Failed to read P4K file '{entry.Path}': {ex.Message}");
            return null;
        }
    }

    private static ZipEntry? FindZipEntry(ZipFile zipFile, string entryPath)
    {
        ZipEntry? entry = zipFile.GetEntry(entryPath);
        if (entry != null)
        {
            return entry;
        }

        string normalized = NormalizePath(entryPath);

        if (!normalized.StartsWith(SCConstants.Paths.DataPrefix, StringComparison.OrdinalIgnoreCase))
        {
            entry = zipFile.GetEntry(SCConstants.Paths.DataPrefix + normalized);
            if (entry != null)
            {
                return entry;
            }
        }
        else
        {
            string withoutPrefix = normalized.Substring(SCConstants.Paths.DataPrefix.Length);
            entry = zipFile.GetEntry(withoutPrefix);
            if (entry != null)
            {
                return entry;
            }
        }

        foreach (ZipEntry? e in zipFile)
        {
            if (e != null && string.Equals(NormalizePath(e.Name), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return e;
            }
        }

        return null;
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/').TrimStart('/').ToUpperInvariant();

    private static bool TrySetEncryptionKey(ZipFile? zipFile)
    {
        if (zipFile == null)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, "[SCCore.P4K] Cannot set encryption key - ZipFile is null");
            return false;
        }

        PropertyInfo? keyProperty = EncryptionKeyProperty.Value;
        if (keyProperty == null)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                "[SCCore.P4K] SharpZipLib 'Key' property not found - library version may be incompatible");
            return false;
        }

        if (keyProperty.PropertyType != typeof(byte[]))
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[SCCore.P4K] 'Key' property type mismatch - expected byte[], got {keyProperty.PropertyType.Name}");
            return false;
        }

        if (!keyProperty.CanWrite)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                "[SCCore.P4K] 'Key' property is read-only - cannot set encryption key");
            return false;
        }

        try
        {
            keyProperty.SetValue(zipFile, SCConstants.EncryptionKey);
            return true;
        }
        catch (TargetException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[SCCore.P4K] Failed to set encryption key - target object error: {ex.Message}");
            return false;
        }
        catch (MethodAccessException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[SCCore.P4K] Failed to set encryption key - access denied: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[SCCore.P4K] Failed to set encryption key - unexpected error: {ex.Message}");
            return false;
        }
    }
}
