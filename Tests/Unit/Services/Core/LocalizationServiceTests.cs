using FluentAssertions;
using Moq;
using SCStreamDeck.Common;
using SCStreamDeck.Services.Core;
using SCStreamDeck.Services.Data;

namespace Tests.Unit.Services.Core;

public sealed class LocalizationServiceTests
{
    [Fact]
    public async Task LoadGlobalIniAsync_FallsBackToDefault_WhenNotFound()
    {
        Mock<IP4KArchiveService> p4k = new();
        LocalizationService service = new(p4k.Object);

        IReadOnlyDictionary<string, string> result = await service.LoadGlobalIniAsync(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
            "XX",
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
            CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadLanguageSettingAsync_ReturnsDefault_WhenConfigMissing()
    {
        Mock<IP4KArchiveService> p4k = new();
        LocalizationService service = new(p4k.Object);

        string language = await service.ReadLanguageSettingAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        language.Should().Be(SCConstants.Localization.DefaultLanguage);
    }
}
