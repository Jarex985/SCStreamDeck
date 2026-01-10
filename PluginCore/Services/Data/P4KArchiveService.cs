using System.Reflection;
using System.Text;
using BarRaider.SdTools;
using ICSharpCode.SharpZipLib.Zip;
using SCStreamDeck.SCCore.Common;
using SCStreamDeck.SCCore.Logging;
using SCStreamDeck.SCCore.Models;

namespace SCStreamDeck.SCCore.Services.Data;

/// <summary>
///     Modern P4K archive service using SharpZipLib directly.
/// </summary>
public sealed class P4KArchiveService : IP4KArchiveService, IDisposable
{
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

    public Task<bool> OpenArchiveAsync(string p4kPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(p4kPath))
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"[SCCore.P4K] {ErrorMessages.InvalidPath}");
            return Task.FromResult(false);
        }

        return Task.Run(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!SecurePathValidator.TryNormalizePath(p4kPath, out string validatedPath))
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"[SCCore.P4K] {ErrorMessages.InvalidPath}");
                    return false;
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (!File.Exists(validatedPath))
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"[SCCore.P4K] {ErrorMessages.P4KArchiveNotFound}");
                    return false;
                }

                cancellationToken.ThrowIfCancellationRequested();

                lock (_lock)
                {
                    CloseArchiveInternal();

                    _fileStream = File.OpenRead(validatedPath);
                    cancellationToken.ThrowIfCancellationRequested();

                    _zipFile = new ZipFile(_fileStream);
                    cancellationToken.ThrowIfCancellationRequested();

                    // Set AES decryption key using reflection
                    if (!TrySetEncryptionKey(_zipFile))
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR,
                            $"[SCCore.P4K] {ErrorMessages.P4KEncryptionKey}");
                        CloseArchiveInternal();
                        return false;
                    }
                }

                return true;
            }

            catch (IOException ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[SCCore.P4K] {ErrorMessages.P4KArchiveOpenFailed}, {ex.Message}");
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[SCCore.P4K] {ErrorMessages.P4KArchiveOpenFailed}, {ex.Message}");
                return false;
            }
            catch (ZipException ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[SCCore.P4K] {ErrorMessages.P4KArchiveOpenFailed}, {ex.Message}");
                return false;
            }
        }, cancellationToken);
    }

    public Task<IReadOnlyList<P4KFileEntry>> ScanDirectoryAsync(string directory, string filePattern,
        CancellationToken cancellationToken = default)
    {
        ZipFile? zipFile;
        lock (_lock)
        {
            zipFile = _zipFile;
        }

        if (zipFile == null)
        {
            return Task.FromResult<IReadOnlyList<P4KFileEntry>>([]);
        }

        return Task.Run(() =>
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

                    // Match if entry is in the specified directory AND ends with the pattern
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
                    $"[SCCore.P4K] {ErrorMessages.P4KScanFailed}, {ex.Message}");
                return Array.Empty<P4KFileEntry>();
            }
            catch (InvalidOperationException ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[SCCore.P4K] {ErrorMessages.P4KScanFailed}, {ex.Message}");
                return Array.Empty<P4KFileEntry>() as IReadOnlyList<P4KFileEntry>;
            }
        }, cancellationToken);
    }

    public Task<byte[]?> ReadFileAsync(P4KFileEntry entry, CancellationToken cancellationToken = default)
    {
        ZipFile? zipFile;
        lock (_lock)
        {
            zipFile = _zipFile;
        }

        if (zipFile == null)
        {
            return Task.FromResult<byte[]?>(null);
        }

        return Task.Run(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                ZipEntry? zipEntry = FindZipEntry(zipFile, entry.Path);
                if (zipEntry == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR,
                        $"[SCCore.P4K] {ErrorMessages.P4KEntryNotFound}: {entry.Path}");
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
                    $"[SCCore.P4K] {ErrorMessages.P4KReadFailed}: {entry.Path}, {ex.Message}");
                return null;
            }
            catch (ZipException ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[SCCore.P4K] {ErrorMessages.P4KReadFailed}: {entry.Path}, {ex.Message}");
                return null;
            }
            catch (IOException ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[SCCore.P4K] {ErrorMessages.P4KReadFailed}: {entry.Path}, {ex.Message}");
                return null;
            }
        }, cancellationToken);
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
                $"[SCCore.P4K] UTF8 decode fallback exception details: {ex}");
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"[SCCore.P4K] {ErrorMessages.P4KDecodeTextFailed}");
            return null;
        }
        catch (ArgumentException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[SCCore.P4K] UTF8 decode argument exception details: {ex}");
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"[SCCore.P4K] {ErrorMessages.P4KDecodeTextFailed}");
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

    private static ZipEntry? FindZipEntry(ZipFile zipFile, string entryPath)
    {
        // Try direct lookup first
        ZipEntry? entry = zipFile.GetEntry(entryPath);
        if (entry != null)
        {
            return entry;
        }

        // Try with normalized path variations
        string normalized = NormalizePath(entryPath);

        // Try with Data/ prefix
        if (!normalized.StartsWith(P4KConstants.DataPrefix, StringComparison.OrdinalIgnoreCase))
        {
            entry = zipFile.GetEntry(P4KConstants.DataPrefix + normalized);
            if (entry != null)
            {
                return entry;
            }
        }
        else
        {
            // Try without Data/ prefix
            string withoutPrefix = normalized.Substring(P4KConstants.DataPrefix.Length);
            entry = zipFile.GetEntry(withoutPrefix);
            if (entry != null)
            {
                return entry;
            }
        }

        // Last resort: case-insensitive scan
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

    /// <summary>
    ///     Attempts to set the AES encryption key on a ZipFile instance using reflection.
    ///     P4K archives use internal SharpZipLib encryption that requires accessing a non-public property.
    /// </summary>
    /// <param name="zipFile">The ZipFile instance to configure</param>
    /// <returns>True if the key was successfully set; false otherwise</returns>
    /// <remarks>
    ///     If SharpZipLib is updated and the property changes, this method will fail gracefully and log details.
    /// </remarks>
    private static bool TrySetEncryptionKey(ZipFile? zipFile)
    {
        if (zipFile == null)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, "[SCCore.P4K] Cannot set encryption key - ZipFile is null");
            return false;
        }

        try
        {
            // Use reflection to access the internal "Key" property
            PropertyInfo? keyProperty = zipFile.GetType().GetProperty("Key", BindingFlags.NonPublic | BindingFlags.Instance);

            if (keyProperty == null)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    "[SCCore.P4K] SharpZipLib 'Key' property not found - library version may be incompatible");
                Logger.Instance.LogMessage(TracingLevel.DEBUG,
                    $"[SCCore.P4K] Available properties: {string.Join(", ", zipFile.GetType().GetProperties(BindingFlags.NonPublic | BindingFlags.Instance).Select(p => p.Name))}");
                return false;
            }

            // Verify the property type matches expected byte[]
            if (keyProperty.PropertyType != typeof(byte[]))
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[SCCore.P4K] 'Key' property type mismatch - expected byte[], got {keyProperty.PropertyType.Name}");
                return false;
            }

            // Verify the property is writable
            if (!keyProperty.CanWrite)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    "[SCCore.P4K] 'Key' property is read-only - cannot set encryption key");
                return false;
            }

            // Set the encryption key
            keyProperty.SetValue(zipFile, P4KConstants.EncryptionKey);
            return true;
        }

        catch (TargetException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[SCCore.P4K] Failed to set encryption key - target object error: {ex.Message}");
            Logger.Instance.LogMessage(TracingLevel.DEBUG, $"[SCCore.P4K] TargetException details: {ex}");
            return false;
        }
        catch (MethodAccessException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[SCCore.P4K] Failed to set encryption key - access denied: {ex.Message}");
            Logger.Instance.LogMessage(TracingLevel.DEBUG, $"[SCCore.P4K] MethodAccessException details: {ex}");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[SCCore.P4K] Failed to set encryption key - unexpected error: {ex.Message}");
            Logger.Instance.LogMessage(TracingLevel.DEBUG, $"[SCCore.P4K] Exception details: {ex}");
            return false;
        }
    }
}
