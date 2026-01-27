using System.Security;
using FluentAssertions;
using SCStreamDeck.Services.Installation;

namespace Tests.Unit.Services.Installation;

public sealed class PathProviderServiceTests
{
    [Fact]
    public void GetKeybindingJsonPath_UppercasesChannel()
    {
        string baseDir = CreateTempDirectory();
        string cacheDir = Path.Combine(baseDir, "cache");
        PathProviderService service = new TestPathProviderService(baseDir, cacheDir);

        string path = service.GetKeybindingJsonPath("live");

        Path.GetFileName(path).Should().Be("LIVE-keybindings.json");
    }

    [Fact]
    public void EnsureDirectoriesExist_CreatesCacheDirectory()
    {
        string baseDir = CreateTempDirectory();
        string cacheDir = Path.Combine(baseDir, "cache");
        PathProviderService service = new TestPathProviderService(baseDir, cacheDir);
        string actualCacheDir = service.CacheDirectory;
        if (Directory.Exists(actualCacheDir))
        {
            Directory.Delete(actualCacheDir, true);
        }

        service.EnsureDirectoriesExist();

        Directory.Exists(actualCacheDir).Should().BeTrue();
    }

    [Fact]
    public void GetSecureCachePath_ReturnsPathWithinCacheOrThrowsWhenBlockedBase()
    {
        string baseDir = CreateTempDirectory();
        string cacheDir = Path.Combine(baseDir, "cache");
        PathProviderService service = new TestPathProviderService(baseDir, cacheDir);
        service.EnsureDirectoriesExist();

        string relative = Path.Combine("sub", "file.txt");

        if (service.CacheDirectory.Contains("windows", StringComparison.OrdinalIgnoreCase))
        {
            Action act = () => service.GetSecureCachePath(relative);
            act.Should().Throw<SecurityException>();
        }
        else
        {
            string fullPath = service.GetSecureCachePath(relative);
            fullPath.Should().StartWith(service.CacheDirectory, "Path should be under cache directory");
        }
    }

    private static string CreateTempDirectory()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private sealed class TestPathProviderService : PathProviderService
    {
        public TestPathProviderService(string baseDirectory, string cacheDirectory) : base(baseDirectory, cacheDirectory)
        {
        }
    }
}
