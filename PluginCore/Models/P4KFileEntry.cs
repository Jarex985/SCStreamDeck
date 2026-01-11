namespace SCStreamDeck.Models;

/// <summary>
///     Represents a file entry in a P4K archive.
/// </summary>
public sealed class P4KFileEntry
{
    public required string Path { get; init; }
    public required long Offset { get; init; }
    public required long CompressedSize { get; init; }
    public required long UncompressedSize { get; init; }
    public required bool IsCompressed { get; init; }
}
