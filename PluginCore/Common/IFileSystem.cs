namespace SCStreamDeck.Common;

public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);

    string ReadAllText(string path);
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);

    Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default);

    Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default);

    Stream OpenRead(string path);

    void DeleteFile(string path);
}
