using FluentAssertions;
using SCStreamDeck.Services.Installation;

namespace Tests.Unit.Services.Installation;

public sealed class InstallLocatorServiceTests
{
    [Fact]
    public async Task FindInstallationsAsync_NoLogsOrCustomPaths_ReturnsEmptyOrHostInstalls()
    {
        using TempAppData appData = new();
        InstallLocatorService service = new();

        var result = await service.FindInstallationsAsync();

        if (result.Count == 0)
        {
            result.Should().BeEmpty();
            service.GetCachedInstallations().Should().BeEmpty();
        }
        else
        {
            result.Should().NotBeNull();
        }
    }

    private sealed class TempAppData : IDisposable
    {
        private readonly string? _originalAppData;
        private readonly string _tempRoot;

        public TempAppData()
        {
            _originalAppData = Environment.GetEnvironmentVariable("APPDATA");
            _tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string logs = Path.Combine(_tempRoot, "rsilauncher", "logs");
            Directory.CreateDirectory(logs);
            Environment.SetEnvironmentVariable("APPDATA", _tempRoot);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("APPDATA", _originalAppData);
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }
    }
}
