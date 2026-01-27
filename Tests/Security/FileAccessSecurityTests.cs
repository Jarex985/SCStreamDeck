using System.Security;
using FluentAssertions;
using Moq;
using SCStreamDeck.Common;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Core;
using SCStreamDeck.Services.Data;
using SCStreamDeck.Services.Installation;
using Tests.Testing;

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
        P4KArchiveService service = new(new TestFileSystem());

        bool opened = await service.OpenArchiveAsync("C::/invalid.p4k");

        opened.Should().BeFalse();
        service.IsArchiveOpen.Should().BeFalse();
    }

    [Fact]
    public async Task StateService_SaveState_InvalidCachePath_ThrowsOrBlocks()
    {
        FakePathProvider pathProvider = new("C:/Windows/System32");
        Mock<IFileSystem> fileSystem = new(MockBehavior.Strict);
        StateService stateService = new(pathProvider, fileSystem.Object);

        PluginState state = new(DateTime.UtcNow, SCChannel.Live, null, null, null, null, null);

        Func<Task> act = async () => await stateService.SaveStateAsync(state);

        await act.Should().ThrowAsync<SecurityException>();
        pathProvider.EnsureDirectoriesExistCallCount.Should().Be(1);
        pathProvider.GetSecureCachePathCallCount.Should().Be(1);
    }

    private sealed class FakePathProvider(string cacheDirectory) : PathProviderService(cacheDirectory, cacheDirectory)
    {
        public int EnsureDirectoriesExistCallCount { get; private set; }
        public int GetSecureCachePathCallCount { get; private set; }

        public override void EnsureDirectoriesExist() => EnsureDirectoriesExistCallCount++;

        public override string GetSecureCachePath(string relativePath)
        {
            GetSecureCachePathCallCount++;
            return base.GetSecureCachePath(relativePath);
        }
    }
}
