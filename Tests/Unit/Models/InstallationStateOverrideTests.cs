using FluentAssertions;
using SCStreamDeck.Common;
using SCStreamDeck.Models;

namespace Tests.Unit.Models;

public sealed class InstallationStateOverrideTests : IDisposable
{
    private readonly string _channelDir;
    private readonly string _dataP4KPath;
    private readonly string _rootDir;

    public InstallationStateOverrideTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), $"SCStreamDeck_Tests_{Guid.NewGuid():N}");
        _channelDir = Path.Combine(_rootDir, "LIVE");
        _dataP4KPath = Path.Combine(_channelDir, SCConstants.Files.DataP4KFileName);

        Directory.CreateDirectory(_channelDir);
        File.WriteAllText(_dataP4KPath, "test");
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, true);
        }
    }

    [Fact]
    public void TryCreateFromDataP4KPath_ReturnsTrue_ForValidDataP4kPath()
    {
        bool ok = InstallationState.TryCreateFromDataP4KPath(SCChannel.Live, _dataP4KPath, out InstallationState? state);

        ok.Should().BeTrue();
        state.Should().NotBeNull();
        state.Channel.Should().Be(SCChannel.Live);
        state.IsCustomPath.Should().BeTrue();
        state.ChannelPath.Should().Be(_channelDir);
        state.RootPath.Should().Be(_rootDir);
        state.Validate().Should().BeTrue();
    }

    [Fact]
    public void TryCreateFromDataP4KPath_ReturnsFalse_WhenFileDoesNotExist()
    {
        string missing = Path.Combine(_channelDir, "missing-Data.p4k");
        bool ok = InstallationState.TryCreateFromDataP4KPath(SCChannel.Live, missing, out InstallationState? state);

        ok.Should().BeFalse();
        state.Should().BeNull();
    }

    [Fact]
    public void TryCreateFromDataP4KPath_ReturnsFalse_WhenFileIsNotDataP4k()
    {
        string wrong = Path.Combine(_channelDir, "Other.p4k");
        File.WriteAllText(wrong, "test");

        bool ok = InstallationState.TryCreateFromDataP4KPath(SCChannel.Live, wrong, out InstallationState? state);

        ok.Should().BeFalse();
        state.Should().BeNull();
    }

    [Fact]
    public void ToCandidate_UsesUserProvidedSource_WhenIsCustomPath()
    {
        InstallationState state = new(_rootDir, SCChannel.Live, _channelDir, true);

        SCInstallCandidate candidate = state.ToCandidate();

        candidate.Source.Should().Be(InstallSource.UserProvided);
        candidate.DataP4KPath.Should().Be(_dataP4KPath);
    }
}
