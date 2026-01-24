using FluentAssertions;
using SCStreamDeck.Models;

namespace Tests.Unit.Models;

public sealed class PluginStateTests
{
    #region GetCachedCandidates

    [Fact]
    public void GetCachedCandidates_ReturnsEmpty_WhenNoInstallations()
    {
        PluginState state = new(
            DateTime.UtcNow,
            SCChannel.Live,
            null,
            null,
            null,
            null,
            null
        );

        IReadOnlyList<SCInstallCandidate> candidates = state.GetCachedCandidates();

        candidates.Should().BeEmpty();
    }

    [Fact]
    public void GetCachedCandidates_ReturnsSingleCandidate_WhenOnlyLiveInstallation()
    {
        InstallationState liveInstallation = new(
            "C:\\SC",
            SCChannel.Live,
            @"C:\SC\Live"
        );

        PluginState state = new(
            DateTime.UtcNow,
            SCChannel.Live,
            null,
            liveInstallation,
            null,
            null,
            null
        );

        IReadOnlyList<SCInstallCandidate> candidates = state.GetCachedCandidates();

        candidates.Should().HaveCount(1);
        candidates[0].Channel.Should().Be(SCChannel.Live);
        candidates[0].DataP4KPath.Should().Be(@"C:\SC\Live\Data.p4k");
    }

    [Fact]
    public void GetCachedCandidates_ReturnsAllCandidates_WhenAllChannelsConfigured()
    {
        InstallationState liveInstallation = new(
            "C:\\SC",
            SCChannel.Live,
            @"C:\SC\Live"
        );

        InstallationState hotfixInstallation = new(
            "C:\\SC",
            SCChannel.Hotfix,
            @"C:\SC\Hotfix"
        );

        InstallationState ptuInstallation = new(
            "C:\\SC",
            SCChannel.Ptu,
            @"C:\SC\PTU"
        );

        InstallationState eptuInstallation = new(
            "C:\\SC",
            SCChannel.Eptu,
            @"C:\SC\EPTU"
        );

        PluginState state = new(
            DateTime.UtcNow,
            SCChannel.Live,
            null,
            liveInstallation,
            hotfixInstallation,
            ptuInstallation,
            eptuInstallation
        );

        IReadOnlyList<SCInstallCandidate> candidates = state.GetCachedCandidates();

        candidates.Should().HaveCount(4);

        candidates.Should().Contain(c => c.Channel == SCChannel.Live);
        candidates.Should().Contain(c => c.Channel == SCChannel.Hotfix);
        candidates.Should().Contain(c => c.Channel == SCChannel.Ptu);
        candidates.Should().Contain(c => c.Channel == SCChannel.Eptu);
    }

    [Fact]
    public void GetCachedCandidates_OrdersCandidatesCorrectly()
    {
        InstallationState liveInstallation = new(
            "C:\\SC",
            SCChannel.Live,
            @"C:\SC\Live"
        );

        InstallationState ptuInstallation = new(
            "C:\\SC",
            SCChannel.Ptu,
            @"C:\SC\PTU"
        );

        InstallationState eptuInstallation = new(
            "C:\\SC",
            SCChannel.Eptu,
            @"C:\SC\EPTU"
        );

        PluginState state = new(
            DateTime.UtcNow,
            SCChannel.Live,
            null,
            liveInstallation,
            null,
            ptuInstallation,
            eptuInstallation
        );

        IReadOnlyList<SCInstallCandidate> candidates = state.GetCachedCandidates();

        candidates.Should().HaveCount(3);
        candidates[0].Channel.Should().Be(SCChannel.Live);
        candidates[1].Channel.Should().Be(SCChannel.Ptu);
        candidates[2].Channel.Should().Be(SCChannel.Eptu);
    }

    [Fact]
    public void GetCachedCandidates_HandlesNullInstallations()
    {
        InstallationState liveInstallation = new(
            "C:\\SC",
            SCChannel.Live,
            @"C:\SC\Live"
        );

        PluginState state = new(
            DateTime.UtcNow,
            SCChannel.Live,
            null,
            liveInstallation,
            null,
            null,
            null
        );

        IReadOnlyList<SCInstallCandidate> candidates = state.GetCachedCandidates();

        candidates.Should().HaveCount(1);
        candidates[0].Channel.Should().Be(SCChannel.Live);
    }

    #endregion

    #region GetInstallation

    [Fact]
    public void GetInstallation_ReturnsCorrectInstallation_ForLive()
    {
        InstallationState liveInstallation = new(
            "C:\\SC",
            SCChannel.Live,
            @"C:\SC\Live"
        );

        PluginState state = new(
            DateTime.UtcNow,
            SCChannel.Live,
            null,
            liveInstallation,
            null,
            null,
            null
        );

        InstallationState? result = state.GetInstallation(SCChannel.Live);

        result.Should().NotBeNull();
        result.Should().Be(liveInstallation);
    }

    [Fact]
    public void GetInstallation_ReturnsNull_ForUnknownChannel()
    {
        InstallationState liveInstallation = new(
            "C:\\SC",
            SCChannel.Live,
            @"C:\SC\Live"
        );

        PluginState state = new(
            DateTime.UtcNow,
            SCChannel.Live,
            null,
            liveInstallation,
            null,
            null,
            null
        );

        InstallationState? result = state.GetInstallation((SCChannel)99);

        result.Should().BeNull();
    }

    #endregion
}
