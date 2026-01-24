using FluentAssertions;
using SCStreamDeck.Common;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Core;

namespace Tests.Unit.Services.Core;

public sealed class InstallationDetectionMergerTests : IDisposable
{
    private readonly string _root;

    public InstallationDetectionMergerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"SCStreamDeck_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    [Fact]
    public void Merge_UsesRsiLogCandidates_AsStartingPoint()
    {
        SCInstallCandidate live = new(
            @"C:\SC",
            SCChannel.Live,
            @"C:\SC\LIVE",
            @"C:\SC\LIVE\Data.p4k");

        (Dictionary<string, SCInstallCandidate> map, Dictionary<string, string> sources) =
            InstallationDetectionMerger.Merge(
                [live],
                Array.Empty<SCInstallCandidate>(),
                true);

        map.Values.Should().ContainSingle(c => c.Channel == SCChannel.Live);
        sources["Live"].Should().Be("RSI Logs");
    }

    [Fact]
    public void Merge_CustomOverride_WinsOverRsiLogs_ForSameRootAndChannel()
    {
        SCInstallCandidate auto = new(
            @"C:\SC",
            SCChannel.Live,
            @"C:\SC\LIVE",
            @"C:\SC\LIVE\Data.p4k");

        SCInstallCandidate custom = new(
            @"C:\SC",
            SCChannel.Live,
            @"D:\Custom\LIVE",
            @"D:\Custom\LIVE\Data.p4k",
            InstallSource.UserProvided);

        (Dictionary<string, SCInstallCandidate> map, Dictionary<string, string> sources) =
            InstallationDetectionMerger.Merge(
                [auto],
                [custom],
                true);

        map.Values.Should().ContainSingle(c => c.Channel == SCChannel.Live);
        map.Values.Single(c => c.Channel == SCChannel.Live).ChannelPath.Should().Be(@"D:\Custom\LIVE");
        sources["Live"].Should().Be("Custom Override");
    }

    [Fact]
    public void Merge_AddsNewChannelsFromCachedRoot_WhenNotFullDetection()
    {
        string rootNorm = SecurePathValidator.TryNormalizePath(_root, out string n) ? n : _root;

        string liveDir = Path.Combine(rootNorm, "LIVE");
        string ptuDir = Path.Combine(rootNorm, "PTU");

        Directory.CreateDirectory(liveDir);
        Directory.CreateDirectory(ptuDir);

        File.WriteAllText(Path.Combine(liveDir, SCConstants.Files.DataP4KFileName), "test");
        File.WriteAllText(Path.Combine(ptuDir, SCConstants.Files.DataP4KFileName), "test");

        SCInstallCandidate cachedLive = new(
            rootNorm,
            SCChannel.Live,
            liveDir,
            Path.Combine(liveDir, SCConstants.Files.DataP4KFileName));

        (Dictionary<string, SCInstallCandidate> map, Dictionary<string, string> sources) =
            InstallationDetectionMerger.Merge(
                Array.Empty<SCInstallCandidate>(),
                [cachedLive],
                false);

        map.Values.Should().Contain(c => c.Channel == SCChannel.Live);
        map.Values.Should().Contain(c => c.Channel == SCChannel.Ptu);
        sources["Ptu"].Should().Be("Cache (New Channel)");
    }
}
