using FluentAssertions;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Core;

namespace Tests.Unit.Services.Core;

public sealed class CachedInstallationsValidatorTests
{
    [Fact]
    public void Validate_WhenNullCandidates_ReturnsEmptyAndNeedsFullDetection()
    {
        CachedInstallationsValidator.Result result = CachedInstallationsValidator.Validate(
            null,
            _ => false,
            _ => false);

        result.ValidCandidates.Should().BeEmpty();
        result.InvalidCandidates.Should().BeEmpty();
        result.NeedsFullDetection.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenAllValid_ReturnsAllValidAndNotNeedsFullDetection()
    {
        SCInstallCandidate live = new(
            @"C:\\SC",
            SCChannel.Live,
            @"C:\\SC\\LIVE",
            @"C:\\SC\\LIVE\\Data.p4k");

        CachedInstallationsValidator.Result result = CachedInstallationsValidator.Validate(
            [live],
            _ => true,
            _ => true);

        result.ValidCandidates.Should().ContainSingle().Which.Should().Be(live);
        result.InvalidCandidates.Should().BeEmpty();
        result.NeedsFullDetection.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenSomeInvalid_ReturnsInvalidListAndNotNeedsFullDetection()
    {
        SCInstallCandidate live = new(
            @"C:\\SC",
            SCChannel.Live,
            @"C:\\SC\\LIVE",
            @"C:\\SC\\LIVE\\Data.p4k");

        SCInstallCandidate ptu = new(
            @"C:\\SC",
            SCChannel.Ptu,
            @"C:\\SC\\PTU",
            @"C:\\SC\\PTU\\Data.p4k");

        CachedInstallationsValidator.Result result = CachedInstallationsValidator.Validate(
            [live, ptu],
            p => p.Contains("LIVE", StringComparison.OrdinalIgnoreCase),
            p => p.Contains("LIVE", StringComparison.OrdinalIgnoreCase));

        result.ValidCandidates.Should().ContainSingle().Which.Should().Be(live);
        result.InvalidCandidates.Should().ContainSingle().Which.Should().Be(ptu);
        result.NeedsFullDetection.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenAllInvalid_ReturnsNeedsFullDetection()
    {
        SCInstallCandidate live = new(
            @"C:\\SC",
            SCChannel.Live,
            @"C:\\SC\\LIVE",
            @"C:\\SC\\LIVE\\Data.p4k");

        CachedInstallationsValidator.Result result = CachedInstallationsValidator.Validate(
            [live],
            _ => false,
            _ => false);

        result.ValidCandidates.Should().BeEmpty();
        result.InvalidCandidates.Should().ContainSingle().Which.Should().Be(live);
        result.NeedsFullDetection.Should().BeTrue();
    }
}
