using FluentAssertions;
using SCStreamDeck.Common;

namespace Tests.Unit.Common;

public sealed class RsiLauncherConfigReaderTests
{
    [Fact]
    public async Task ExtractPathsFromLogAsync_ReturnsEmpty_WhenFileMissing()
    {
        HashSet<string> paths = await RsiLauncherConfigReader.ExtractPathsFromLogAsync(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".missing.log"));

        paths.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractPathsFromLogAsync_ExtractsUniqueRoots_FromValidateLaunchAndInstallerEntries()
    {
        string content = string.Join(Environment.NewLine,
            "[LauncherSupport::validateDirectories] - C:\\\\Games\\\\StarCitizen\\\\LIVE, D:\\\\SC\\\\PTU",
            "Launching Star Citizen LIVE from (C:\\\\Games\\\\StarCitizen\\\\LIVE)",
            "[Installer] - Installing Star Citizen... at E:\\\\Games\\\\StarCitizen");

        using TempFile temp = new(content);

        HashSet<string> paths = await RsiLauncherConfigReader.ExtractPathsFromLogAsync(temp.FilePath);

        paths.Should().Contain(new[] { "C:\\Games\\StarCitizen", "D:\\SC", "E:\\Games\\StarCitizen" });
        paths.Count.Should().Be(3);
    }

    private sealed class TempFile : IDisposable
    {
        public TempFile(string content)
        {
            string dir = Path.Combine(Path.GetTempPath(), "SCStreamDeck.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);

            FilePath = Path.Combine(dir, "launcher.log");
            File.WriteAllText(FilePath, content);
        }

        public string FilePath { get; }

        public void Dispose()
        {
            try
            {
                string? dir = Path.GetDirectoryName(FilePath);
                if (dir != null && Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
