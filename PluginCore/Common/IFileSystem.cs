namespace SCStreamDeck.Common;

public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);

    string ReadAllText(string path);
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);

    Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default);

    void WriteAllText(string path, string contents);

    Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default);

    Stream OpenRead(string path);

    void DeleteFile(string path);

    void MoveFile(string sourceFileName, string destFileName, bool overwrite);
}
