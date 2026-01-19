using FluentAssertions;
using SCStreamDeck.Services.Data;

namespace Tests.Unit.Services.Data;

public sealed class P4KArchiveServiceTests
{
    [Fact]
    public async Task OpenArchiveAsync_WithNullPath_ReturnsFalse()
    {
        P4KArchiveService service = new();

        bool result = await service.OpenArchiveAsync(null!, CancellationToken.None);

        result.Should().BeFalse();
        service.IsArchiveOpen.Should().BeFalse();
    }

    [Fact]
    public async Task OpenArchiveAsync_WithNonexistentFile_ReturnsFalse()
    {
        P4KArchiveService service = new();
        string path = Path.Combine(Path.GetTempPath(), "missing-data.p4k");

        bool result = await service.OpenArchiveAsync(path, CancellationToken.None);

        result.Should().BeFalse();
        service.IsArchiveOpen.Should().BeFalse();
    }

    [Fact]
    public void CloseArchive_IsSafeWhenNotOpened()
    {
        P4KArchiveService service = new();

        service.CloseArchive();

        service.IsArchiveOpen.Should().BeFalse();
    }
}
