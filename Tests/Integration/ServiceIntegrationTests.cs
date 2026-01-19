using FluentAssertions;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Core;
using Tests.Integration.Testing;

namespace Tests.Integration;

/// <summary>
/// Integration tests for service orchestration flows using real implementations.
/// </summary>
public sealed class ServiceIntegrationTests(ServiceIntegrationFixture fixture) : IClassFixture<ServiceIntegrationFixture>
{
    [Fact]
    public async Task EnsureInitialized_GeneratesAndLoadsKeybindings()
    {
        if (!HasDataP4K())
        {
            return;
        }

        InitializationResult result = await fixture.InitializationService.EnsureInitializedAsync();

        result.IsSuccess.Should().BeTrue();
        result.DetectedInstallations.Should().BePositive();
        fixture.InitializationService.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task SwitchChannel_LoadsChannelKeybindings()
    {
        if (!HasDataP4K())
        {
            return;
        }

        InitializationResult init = await fixture.InitializationService.EnsureInitializedAsync();
        init.IsSuccess.Should().BeTrue();

        bool switched = await fixture.InitializationService.SwitchChannelAsync(SCChannel.Live);
        switched.Should().BeTrue();
        fixture.InitializationService.CurrentChannel.Should().Be(SCChannel.Live);
    }

    private bool HasDataP4K()
    {
        return fixture.HasDataP4K || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SCSTREAMDECK_DATA_P4K_PATH"));
    }
}
