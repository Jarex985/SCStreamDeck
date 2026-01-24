using FluentAssertions;
using Newtonsoft.Json.Linq;
using SCStreamDeck.Models;
using SCStreamDeck.Services.UI;

namespace Tests.Unit.Services.UI;

public sealed class ControlPanelPayloadBuilderTests
{
    [Fact]
    public void Build_ReturnsChannelsForAllEnumValues_WhenStateNull()
    {
        JObject payload = ControlPanelPayloadBuilder.Build(
            null,
            true,
            SCChannel.Hotfix,
            ch => ch == SCChannel.Ptu,
            _ => true);

        payload["initialized"]!.Value<bool>().Should().BeTrue();
        payload["currentChannel"]!.Value<string>().Should().Be("Hotfix");
        payload["preferredChannel"]!.Value<string>().Should().Be("Live");
        payload["preferredAvailable"]!.Value<bool>().Should().BeFalse();
        payload["lastInitialized"]!.Value<string>().Should().BeEmpty();

        JArray channels = (JArray)payload["channels"]!;
        channels.Should().HaveCount(4);
        channels.Select(c => c["channel"]!.Value<string>()).Should().Equal("Live", "Hotfix", "Ptu", "Eptu");

        JObject ptu = (JObject)channels.Single(c => c["channel"]!.Value<string>() == "Ptu");
        ptu["configured"]!.Value<bool>().Should().BeFalse();
        ptu["valid"]!.Value<bool>().Should().BeFalse();
        ptu["keybindingsJsonExists"]!.Value<bool>().Should().BeTrue();

        JObject live = (JObject)channels.Single(c => c["channel"]!.Value<string>() == "Live");
        live["keybindingsJsonExists"]!.Value<bool>().Should().BeFalse();
    }

    [Fact]
    public void Build_UsesStatePreferredChannelAndLastInitialized()
    {
        DateTime lastInitialized = new(2026, 1, 23, 12, 34, 56, DateTimeKind.Utc);

        InstallationState live = new(@"C:\SC", SCChannel.Live, @"C:\SC\LIVE");
        InstallationState ptu = new(@"D:\SC", SCChannel.Ptu, @"D:\SC\PTU", true);

        PluginState state = new(
            lastInitialized,
            SCChannel.Ptu,
            null,
            live,
            null,
            ptu,
            null);

        JObject payload = ControlPanelPayloadBuilder.Build(
            state,
            false,
            SCChannel.Live,
            _ => false,
            i => i.Channel == SCChannel.Ptu);

        payload["preferredChannel"]!.Value<string>().Should().Be("Ptu");
        payload["preferredAvailable"]!.Value<bool>().Should().BeTrue();
        payload["lastInitialized"]!.Value<string>().Should().Be(lastInitialized.ToString("O"));
    }

    [Fact]
    public void Build_IncludesInstallationDetailsAndComputedDataP4KPath()
    {
        InstallationState eptu = new(@"E:\Games\StarCitizen", SCChannel.Eptu, @"E:\Games\StarCitizen\EPTU", true);
        PluginState state = new(
            new DateTime(2026, 1, 23, 0, 0, 0, DateTimeKind.Utc),
            SCChannel.Eptu,
            null,
            null,
            null,
            null,
            eptu);

        JObject payload = ControlPanelPayloadBuilder.Build(
            state,
            true,
            SCChannel.Eptu,
            ch => ch == SCChannel.Eptu,
            _ => true);

        JArray channels = (JArray)payload["channels"]!;
        JObject entry = (JObject)channels.Single(c => c["channel"]!.Value<string>() == "Eptu");

        entry["configured"]!.Value<bool>().Should().BeTrue();
        entry["valid"]!.Value<bool>().Should().BeTrue();
        entry["isCustomPath"]!.Value<bool>().Should().BeTrue();
        entry["rootPath"]!.Value<string>().Should().Be(@"E:\Games\StarCitizen");
        entry["channelPath"]!.Value<string>().Should().Be(@"E:\Games\StarCitizen\EPTU");
        entry["dataP4KPath"]!.Value<string>().Should().Be(@"E:\Games\StarCitizen\EPTU\Data.p4k");
        entry["keybindingsJsonExists"]!.Value<bool>().Should().BeTrue();
    }

    [Fact]
    public void BuildFailurePayload_MatchesLegacyFallbackShape()
    {
        JObject payload = ControlPanelPayloadBuilder.BuildFailurePayload();

        payload["initialized"]!.Value<bool>().Should().BeFalse();
        payload["currentChannel"]!.Value<string>().Should().Be("Live");
        payload["preferredChannel"]!.Value<string>().Should().Be("Live");
        payload["preferredAvailable"]!.Value<bool>().Should().BeFalse();
        payload["lastInitialized"]!.Value<string>().Should().BeEmpty();
        ((JArray)payload["channels"]!).Should().BeEmpty();
    }

    [Fact]
    public void Build_PayloadHasStableShape_ForPropertyInspectorContract()
    {
        DateTime lastInitialized = new(2026, 1, 23, 12, 34, 56, DateTimeKind.Utc);

        InstallationState live = new(@"C:\\SC", SCChannel.Live, @"C:\\SC\\LIVE");
        InstallationState ptu = new(@"D:\\SC", SCChannel.Ptu, @"D:\\SC\\PTU", true);

        PluginState state = new(
            lastInitialized,
            SCChannel.Ptu,
            null,
            live,
            null,
            ptu,
            null);

        JObject payload = ControlPanelPayloadBuilder.Build(
            state,
            true,
            SCChannel.Live,
            ch => ch == SCChannel.Ptu,
            _ => true);

        payload.Properties().Select(p => p.Name)
            .Should()
            .Equal("initialized", "currentChannel", "preferredChannel", "preferredAvailable", "lastInitialized", "channels");

        JArray channels = (JArray)payload["channels"]!;
        channels.Should().HaveCount(4);

        foreach (JToken token in channels)
        {
            JObject entry = (JObject)token;
            entry.Properties().Select(p => p.Name)
                .Should()
                .Equal(
                    "channel",
                    "configured",
                    "valid",
                    "isCustomPath",
                    "rootPath",
                    "channelPath",
                    "dataP4KPath",
                    "keybindingsJsonExists");
        }
    }
}
