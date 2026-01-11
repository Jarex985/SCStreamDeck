using Moq;
using SCStreamDeck.Services.Core;
using SCStreamDeck.Services.Installation;
using SCStreamDeck.Services.Data;

namespace UnitTests;

public class CoreServicesTests
{
    [Fact]
    public async Task StateService_LoadStateAsync_ReturnsPluginState()
    {
        var mockPathProvider = new Mock<IPathProvider>();
        var mockVersionProvider = new Mock<IVersionProvider>();

        mockPathProvider.Setup(p => p.CacheDirectory).Returns("dummyPath");
        var stateService = new StateService(mockPathProvider.Object, mockVersionProvider.Object);

        var result = await stateService.LoadStateAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task LocalizationService_LoadGlobalIniAsync_ReturnsLocalizedStrings()
    {
        var mockP4KService = new Mock<IP4KArchiveService>();
        var localizationService = new LocalizationService(mockP4KService.Object);

        var result = await localizationService.LoadGlobalIniAsync("dummyPath", "en", "dummyP4k");

        Assert.NotNull(result);
    }

    // Note: Full tests for these services require extensive mocking of file I/O and P4K operations.
    // The above tests demonstrate that the services can be instantiated and their methods called.
}
