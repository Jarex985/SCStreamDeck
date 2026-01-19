using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Data;

/// <summary>
///     Service for reading Star Citizen P4K archive files.
/// </summary>
public interface IP4KArchiveService
{
    /// <summary>
    ///     Indicates whether an archive is currently open.
    /// </summary>
    bool IsArchiveOpen { get; }

    /// <summary>
    ///     Opens a P4K archive file for reading.
    /// </summary>
    /// <param name="p4KPath">Path to the Data.p4k file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if archive was opened successfully</returns>
    /// <exception cref="ArgumentException">Thrown when p4KPath is null or whitespace.</exception>
    /// <exception cref="IOException">Thrown when archive cannot be opened.</exception>
    /// <exception cref="ZipException">Thrown when archive format is invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task<bool> OpenArchiveAsync(string p4KPath, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Scans the archive for files matching a pattern in a specific directory.
    /// </summary>
    /// <param name="directory">Directory path within archive (e.g., "Data/Libs/Config")</param>
    /// <param name="filePattern">File pattern to match (e.g., "defaultProfile.xml")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of matching file entries</returns>
    /// <exception cref="ArgumentNullException">Thrown when directory or filePattern is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when archive is closed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when archive operations fail.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task<IReadOnlyList<P4KFileEntry>> ScanDirectoryAsync(string directory, string filePattern,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reads a file from the archive as bytes.
    /// </summary>
    /// <param name="entry">File entry from ScanDirectoryAsync</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File content as byte array, or null if entry not found</returns>
    /// <exception cref="ArgumentNullException">Thrown when entry is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when archive is closed.</exception>
    /// <exception cref="ZipException">Thrown when file cannot be extracted.</exception>
    /// <exception cref="IOException">Thrown when file I/O fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task<byte[]?> ReadFileAsync(P4KFileEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reads a file from the archive as text (UTF-8).
    /// </summary>
    /// <param name="entry">File entry from ScanDirectoryAsync</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File content as string, or null if entry not found or decoding fails</returns>
    /// <exception cref="ArgumentNullException">Thrown when entry is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when archive is closed.</exception>
    /// <exception cref="ZipException">Thrown when file cannot be extracted.</exception>
    /// <exception cref="IOException">Thrown when file I/O fails.</exception>
    /// <exception cref="DecoderFallbackException">Thrown when UTF-8 decoding fails.</exception>
    /// <exception cref="ArgumentException">Thrown when bytes cannot be decoded to text.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task<string?> ReadFileAsTextAsync(P4KFileEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Closes the currently opened archive.
    /// </summary>
    void CloseArchive();
}
