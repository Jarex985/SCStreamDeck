using System.Reflection;
using System.Security;
using System.Text;
using ICSharpCode.SharpZipLib.Zip;
using SCStreamDeck.Common;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Data;

/// <summary>
///     Modern P4K archive service using SharpZipLib directly.
/// </summary>
public sealed class P4KArchiveService(IFileSystem fileSystem) : IP4KArchiveService, IDisposable
{
    private static readonly Lazy<PropertyInfo?> s_encryptionKeyProperty = new(() =>
        typeof(ZipFile).GetProperty("Key", BindingFlags.NonPublic | BindingFlags.Instance));

    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    private readonly object _lock = new();

    private bool _disposed;
    private Stream? _fileStream;
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
            Log.Err($"[{nameof(P4KArchiveService)}] Invalid path");
            return false;
        }

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!SecurePathValidator.TryNormalizePath(p4KPath, out string validatedPath))
            {
                Log.Err($"[{nameof(P4KArchiveService)}] Invalid path");
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!_fileSystem.FileExists(validatedPath))
            {
                Log.Err($"[{nameof(P4KArchiveService)}] P4K archive not found: '{validatedPath}'");
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return OpenArchiveInternal(validatedPath, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or ZipException or UnauthorizedAccessException or SecurityException)
            {
                Log.Err($"[{nameof(P4KArchiveService)}] Failed to open archive", ex);
                return false;
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<P4KFileEntry>> ScanDirectoryAsync(string directory, string filePattern,
        CancellationToken cancellationToken = default) =>
        await Task.Run(() => ScanDirectoryInternal(directory, filePattern, cancellationToken), cancellationToken)
            .ConfigureAwait(false);

    public async Task<byte[]?> ReadFileAsync(P4KFileEntry entry, CancellationToken cancellationToken = default) =>
        await Task.Run(() => ReadFileInternal(entry, cancellationToken), cancellationToken)
            .ConfigureAwait(false);

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
        catch (Exception ex) when (ex is DecoderFallbackException or ArgumentException)
        {
            Log.Err($"[{nameof(P4KArchiveService)}] Failed to decode P4K file as text", ex);
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

            _fileStream = _fileSystem.OpenRead(validatedPath);
            cancellationToken.ThrowIfCancellationRequested();

            _zipFile = new ZipFile(_fileStream);
            cancellationToken.ThrowIfCancellationRequested();

            if (!TrySetEncryptionKey(_zipFile))
            {
                Log.Err($"[{nameof(P4KArchiveService)}] Failed to set encryption key");
                CloseArchiveInternal();
                return false;
            }

            return true;
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

    /// <summary>
    ///     Scans directory for matching entries in P4K archive.
    /// </summary>
    private IReadOnlyList<P4KFileEntry> ScanDirectoryInternal(string directory, string filePattern,
        CancellationToken cancellationToken)
    {
        try
        {
            lock (_lock)
            {
                if (_zipFile == null || _disposed)
                {
                    return Array.Empty<P4KFileEntry>();
                }

                ZipFile zipFile = _zipFile;
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
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
        {
            Log.Err($"[{nameof(P4KArchiveService)}] Failed to scan P4K directory", ex);
            return Array.Empty<P4KFileEntry>();
        }
    }

    private byte[]? ReadFileInternal(P4KFileEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            lock (_lock)
            {
                if (_zipFile == null || _disposed)
                {
                    return null;
                }

                cancellationToken.ThrowIfCancellationRequested();

                ZipFile zipFile = _zipFile;
                ZipEntry? zipEntry = FindZipEntry(zipFile, entry.Path);
                if (zipEntry == null)
                {
                    Log.Err($"[{nameof(P4KArchiveService)}] P4K entry not found: '{entry.Path}'");
                    return null;
                }

                cancellationToken.ThrowIfCancellationRequested();

                using Stream? stream = zipFile.GetInputStream(zipEntry);
                using MemoryStream memoryStream = new((int)zipEntry.Size);

                cancellationToken.ThrowIfCancellationRequested();
                stream.CopyTo(memoryStream);
                cancellationToken.ThrowIfCancellationRequested();

                return memoryStream.ToArray();
            }
        }
        catch (Exception ex) when (ex is IOException or ZipException or ObjectDisposedException)
        {
            Log.Err($"[{nameof(P4KArchiveService)}] Failed to read P4K file '{entry.Path}'", ex);
            return null;
        }
    }

    /// <summary>
    ///     Finds a ZipEntry by path with fallback strategies.
    /// </summary>
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

    /// <summary>
    ///     Sets encryption key on ZipFile instance using reflection.
    /// </summary>
    private bool TrySetEncryptionKey(ZipFile? zipFile)
    {
        if (zipFile == null)
        {
            Log.Err($"[{nameof(P4KArchiveService)}] Cannot set encryption key - ZipFile is null");
            return false;
        }

        PropertyInfo? keyProperty = s_encryptionKeyProperty.Value;
        if (keyProperty == null)
        {
            Log.Err(
                $"[{nameof(P4KArchiveService)}] SharpZipLib 'Key' property not found - library version may be incompatible");
            return false;
        }

        if (keyProperty.PropertyType != typeof(byte[]))
        {
            Log.Err(
                $"[{nameof(P4KArchiveService)}] 'Key' property type mismatch - expected byte[], got {keyProperty.PropertyType.Name}");
            return false;
        }

        if (!keyProperty.CanWrite)
        {
            Log.Err($"[{nameof(P4KArchiveService)}] 'Key' property is read-only - cannot set encryption key");
            return false;
        }

        try
        {
            keyProperty.SetValue(zipFile, SCConstants.EncryptionKey);
            return true;
        }

        catch (Exception ex)
        {
            Log.Err($"[{nameof(P4KArchiveService)}] Failed to set encryption key", ex);
            return false;
        }
    }
}
