using System.Security;
using FluentAssertions;
using SCStreamDeck.Services.Installation;

namespace Tests.Unit.Services.Installation;

public sealed class PathProviderServiceTests
{
    [Fact]
    public void GetKeybindingJsonPath_UppercasesChannel()
    {
        PathProviderService service = new();

        string path = service.GetKeybindingJsonPath("live");

        Path.GetFileName(path).Should().Be("LIVE-keybindings.json");
    }

    [Fact]
    public void EnsureDirectoriesExist_CreatesCacheDirectory()
    {
        PathProviderService service = new();
        string cacheDir = service.CacheDirectory;
        if (Directory.Exists(cacheDir))
        {
            Directory.Delete(cacheDir, true);
        }

        service.EnsureDirectoriesExist();

        Directory.Exists(cacheDir).Should().BeTrue();
    }

    [Fact]
    public void GetSecureCachePath_ReturnsPathWithinCacheOrThrowsWhenBlockedBase()
    {
        PathProviderService service = new();
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
}
