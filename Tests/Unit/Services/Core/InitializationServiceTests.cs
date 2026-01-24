using System.Reflection;
using FluentAssertions;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Core;

namespace Tests.Unit.Services.Core;

public sealed class InitializationServiceTests
{
    [Fact]
    public void LogDetectionSummary_DoesNotThrow_WhenCandidatesIsEmpty()
    {
        List<SCInstallCandidate> candidates = [];
        Dictionary<string, string> sources = new();
        List<string> rsiRootPaths = [];

        Action act = () => typeof(InitializationService)
            .GetMethod("LogDetectionSummary", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [candidates, sources, rsiRootPaths]);

        act.Should().NotThrow();
    }

    [Fact]
    public void LogDetectionSummary_DoesNotThrow_WhenHasSingleCandidate()
    {
        List<SCInstallCandidate> candidates =
        [
            new("C:\\SC", SCChannel.Live, @"C:\SC\LIVE", @"C:\SC\LIVE\Data.p4k")
        ];

        Dictionary<string, string> sources = new() { ["Live"] = "RSI Logs" };

        List<string> rsiRootPaths = [@"C:\SC"];

        Action act = () => typeof(InitializationService)
            .GetMethod("LogDetectionSummary", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [candidates, sources, rsiRootPaths]);

        act.Should().NotThrow();
    }

    [Fact]
    public void LogDetectionSummary_DoesNotThrow_WhenHasMultipleCandidates()
    {
        List<SCInstallCandidate> candidates =
        [
            new("C:\\SC", SCChannel.Live, @"C:\SC\LIVE", @"C:\SC\LIVE\Data.p4k"),
            new("C:\\SC", SCChannel.Ptu, @"C:\SC\PTU", @"C:\SC\PTU\Data.p4k"),
            new("C:\\SC", SCChannel.Eptu, @"C:\SC\EPTU", @"C:\SC\EPTU\Data.p4k")
        ];

        Dictionary<string, string> sources = new()
        {
            ["Live"] = "RSI Logs", ["Ptu"] = "RSI Logs", ["Eptu"] = "Cache (New Channel)"
        };

        List<string> rsiRootPaths = [@"C:\SC", @"D:\Games\SC"];

        Action act = () => typeof(InitializationService)
            .GetMethod("LogDetectionSummary", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [candidates, sources, rsiRootPaths]);

        act.Should().NotThrow();
    }

    [Fact]
    public void LogDetectionSummary_DoesNotThrow_WhenNoRsiRootPaths()
    {
        List<SCInstallCandidate> candidates =
        [
            new("C:\\SC", SCChannel.Live, @"C:\SC\LIVE", @"C:\SC\LIVE\Data.p4k")
        ];

        Dictionary<string, string> sources = new() { ["Live"] = "User Config" };

        List<string> rsiRootPaths = [];

        Action act = () => typeof(InitializationService)
            .GetMethod("LogDetectionSummary", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [candidates, sources, rsiRootPaths]);

        act.Should().NotThrow();
    }

    [Fact]
    public void LogDetectionSummary_DoesNotThrow_WhenHasUnknownSource()
    {
        List<SCInstallCandidate> candidates =
        [
            new("C:\\SC", SCChannel.Live, @"C:\SC\LIVE", @"C:\SC\LIVE\Data.p4k")
        ];

        Dictionary<string, string> sources = new();

        List<string> rsiRootPaths = [@"C:\SC"];

        Action act = () => typeof(InitializationService)
            .GetMethod("LogDetectionSummary", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [candidates, sources, rsiRootPaths]);

        act.Should().NotThrow();
    }
}
