namespace SCStreamDeck.Common;

public sealed class SystemFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
        File.ReadAllTextAsync(path, cancellationToken);

    public Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default) =>
        File.ReadAllLinesAsync(path, cancellationToken);

    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);

    public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default) =>
        File.WriteAllTextAsync(path, contents, cancellationToken);

    public Stream OpenRead(string path) => File.OpenRead(path);

    public void DeleteFile(string path) => File.Delete(path);

    public void MoveFile(string sourceFileName, string destFileName, bool overwrite) =>
        File.Move(sourceFileName, destFileName, overwrite);
}
