using FluentAssertions;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Core;

namespace Tests.Unit.Services.Core;

public sealed class ChannelSelectorTests
{
    [Fact]
    public void SelectPreferredOrFallback_ReturnsPreferred_WhenPresent()
    {
        List<SCInstallCandidate> candidates =
        [
            new(@"C:\\SC", SCChannel.Live, @"C:\\SC\\LIVE", @"C:\\SC\\LIVE\\Data.p4k"),
            new(@"C:\\SC", SCChannel.Ptu, @"C:\\SC\\PTU", @"C:\\SC\\PTU\\Data.p4k")
        ];

        SCInstallCandidate selected = ChannelSelector.SelectPreferredOrFallback(
            candidates,
            SCChannel.Ptu,
            out bool usedFallback);

        selected.Channel.Should().Be(SCChannel.Ptu);
        usedFallback.Should().BeFalse();
    }

    [Fact]
    public void SelectPreferredOrFallback_UsesPriorityFallback_WhenPreferredMissing()
    {
        List<SCInstallCandidate> candidates =
        [
            new(@"C:\\SC", SCChannel.Ptu, @"C:\\SC\\PTU", @"C:\\SC\\PTU\\Data.p4k"),
            new(@"C:\\SC", SCChannel.Eptu, @"C:\\SC\\EPTU", @"C:\\SC\\EPTU\\Data.p4k")
        ];

        SCInstallCandidate selected = ChannelSelector.SelectPreferredOrFallback(
            candidates,
            SCChannel.Live,
            out bool usedFallback);

        selected.Channel.Should().Be(SCChannel.Ptu);
        usedFallback.Should().BeTrue();
    }

    [Fact]
    public void SelectPreferredOrFallback_FallsBackToFirst_WhenNoPriorityChannelsPresent()
    {
        List<SCInstallCandidate> candidates =
        [
            new(@"C:\\SC", (SCChannel)999, @"C:\\SC\\X", @"C:\\SC\\X\\Data.p4k"),
            new(@"C:\\SC", (SCChannel)1000, @"C:\\SC\\Y", @"C:\\SC\\Y\\Data.p4k")
        ];

        SCInstallCandidate selected = ChannelSelector.SelectPreferredOrFallback(
            candidates,
            SCChannel.Live,
            out bool usedFallback);

        selected.Should().BeSameAs(candidates[0]);
        usedFallback.Should().BeTrue();
    }
}
