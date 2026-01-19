using System.Security;
using FluentAssertions;
using SCStreamDeck.Common;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Data;
using SCStreamDeck.Services.Installation;
using SCStreamDeck.Services.Core;

namespace Tests.Security;

public sealed class FileAccessSecurityTests
{
    [Fact]
    public void PathProvider_GetSecureCachePath_BlocksTraversal()
    {
        PathProviderService service = new();
        service.EnsureDirectoriesExist();

        Action act = () => service.GetSecureCachePath("../outside.txt");

        act.Should().Throw<SecurityException>();
    }

    [Fact]
    public void SecurePathValidator_BlocksCustomPathsToWindows()
    {
        string baseDir = Path.GetTempPath();

        bool result = SecurePathValidator.IsValidPath("C:/Windows/System32/config/SAM", baseDir, out string normalized);

        result.Should().BeFalse();
        normalized.Should().BeEmpty();
    }

    [Fact]
    public async Task P4KArchiveService_OpenArchive_InvalidPath_ReturnsFalse()
    {
        P4KArchiveService service = new();

        bool opened = await service.OpenArchiveAsync("C::/invalid.p4k");

        opened.Should().BeFalse();
        service.IsArchiveOpen.Should().BeFalse();
    }

    [Fact]
    public async Task StateService_SaveState_InvalidCachePath_ThrowsOrBlocks()
    {
        FakePathProvider pathProvider = new("C:/Windows/System32");
        FakeVersionProvider versionProvider = new();
        StateService stateService = new(pathProvider, versionProvider);

        PluginState state = new("1.0.0", DateTime.UtcNow, SCChannel.Live, null, null, null, null);

        Func<Task> act = async () => await stateService.SaveStateAsync(state);

        await act.Should().ThrowAsync<Exception>();
    }

    private sealed class FakePathProvider : IPathProvider
    {
        public FakePathProvider(string cacheDirectory)
        {
            CacheDirectory = cacheDirectory;
            BaseDirectory = cacheDirectory;
        }

        public string BaseDirectory { get; }
        public string CacheDirectory { get; }

        public string GetKeybindingJsonPath(string channel) => Path.Combine(CacheDirectory, $"{channel}-keybindings.json");
        public void EnsureDirectoriesExist() { }
        public string GetSecureCachePath(string relativePath) => SecurePathValidator.GetSecurePath(relativePath, CacheDirectory);
    }

    private sealed class FakeVersionProvider : IVersionProvider
    {
        public string GetPluginVersion() => "1.0.0";
        public Task<string> GetPluginVersionAsync(CancellationToken cancellationToken = default) => Task.FromResult("1.0.0");
    }
}
