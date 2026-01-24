using System.Text;
using SCStreamDeck.Common;

namespace Tests.Testing;

internal sealed class TestFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

    public bool FileExists(string path) => _files.ContainsKey(Normalize(path));

    public bool DirectoryExists(string path) => true;

    public string ReadAllText(string path) => _files[Normalize(path)];

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
        Task.FromResult(ReadAllText(path));

    public Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default) =>
        Task.FromResult(ReadAllText(path).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None));

    public void WriteAllText(string path, string contents) => _files[Normalize(path)] = contents;

    public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default)
    {
        WriteAllText(path, contents);
        return Task.CompletedTask;
    }

    public Stream OpenRead(string path)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(ReadAllText(path));
        return new MemoryStream(bytes);
    }

    public void DeleteFile(string path) => _files.Remove(Normalize(path));

    public void MoveFile(string sourceFileName, string destFileName, bool overwrite)
    {
        string sourceKey = Normalize(sourceFileName);
        string destKey = Normalize(destFileName);

        if (!_files.TryGetValue(sourceKey, out string? contents))
        {
            throw new FileNotFoundException($"File not found: '{sourceFileName}'");
        }

        if (!overwrite && _files.ContainsKey(destKey))
        {
            throw new IOException($"Destination exists: '{destFileName}'");
        }

        _files[destKey] = contents;
        _files.Remove(sourceKey);
    }

    public void AddFile(string path, string contents) => _files[Normalize(path)] = contents;

    private static string Normalize(string path) => path.Replace('\\', '/');
}
