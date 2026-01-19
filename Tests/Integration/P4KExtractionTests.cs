using FluentAssertions;
using SCStreamDeck.Common;
using SCStreamDeck.Models;
using Tests.Integration.Testing;

namespace Tests.Integration;

/// <summary>
/// Integration coverage for P4K extraction using real archives.
/// Data.p4k path is supplied via env or Tests/TestData.
/// </summary>
public sealed class P4KExtractionTests(P4KFixture fixture) : IClassFixture<P4KFixture>
{
    [Fact]
    public async Task OpenArchive_Succeeds_WithRealP4K()
    {
        if (!File.Exists(fixture.DataP4KPath))
        {
            return;
        }

        bool opened = await fixture.P4KArchiveService.OpenArchiveAsync(fixture.DataP4KPath);
        opened.Should().BeTrue();
        fixture.P4KArchiveService.IsArchiveOpen.Should().BeTrue();
    }

    [Fact]
    public async Task ScanAndRead_KnowsKeybindingFiles()
    {
        if (!File.Exists(fixture.DataP4KPath))
        {
            return;
        }

        await fixture.P4KArchiveService.OpenArchiveAsync(fixture.DataP4KPath);

        IReadOnlyList<P4KFileEntry> profiles = await fixture.P4KArchiveService.ScanDirectoryAsync(
            SCConstants.Paths.KeybindingConfigDirectory,
            SCConstants.Files.DefaultProfileFileName);

        profiles.Should().NotBeEmpty("defaultProfile.xml should exist inside Data.p4k");

        byte[]? bytes = await fixture.P4KArchiveService.ReadFileAsync(profiles[0]);
        bytes.Should().NotBeNull();
        bytes!.Length.Should().BeGreaterThan(0);
    }
}
