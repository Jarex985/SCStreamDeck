using FluentAssertions;
using Moq;
using SCStreamDeck.Services.Installation;

namespace Tests.Unit.Services.Installation;

public sealed class VersionProviderServiceTests
{
    [Fact]
    public void GetPluginVersion_CachesResult()
    {
        Mock<IPathProvider> pathProvider = new();
        pathProvider.SetupGet(p => p.BaseDirectory).Returns(AppContext.BaseDirectory);
        VersionProviderService service = new(pathProvider.Object);

        string version1 = service.GetPluginVersion();
        string version2 = service.GetPluginVersion();

        version1.Should().Be(version2);
        pathProvider.VerifyGet(p => p.BaseDirectory, Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetPluginVersionAsync_MissingManifest_Throws()
    {
        Mock<IPathProvider> pathProvider = new();
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        pathProvider.SetupGet(p => p.BaseDirectory).Returns(tempDir);
        VersionProviderService service = new(pathProvider.Object);

        Func<Task> act = async () => await service.GetPluginVersionAsync();

        await act.Should().ThrowAsync<FileNotFoundException>();
    }
}
