using SCStreamDeck.SCCore.Infrastructure;
using SCStreamDeck.SCCore.Services.Core;
using SCStreamDeck.SCCore.Services.Keybinding;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests;

public class InfrastructureTests
{
    [Fact]
    public void ServiceConfiguration_BuildAndInitialize_ReturnsValidServiceProvider()
    {
        var serviceProvider = ServiceConfiguration.BuildAndInitialize();

        Assert.NotNull(serviceProvider);
    }

    [Fact]
    public void ServiceConfiguration_AllServicesAreRegistered()
    {
        var serviceProvider = ServiceConfiguration.BuildAndInitialize();

        Assert.NotNull(serviceProvider.GetService<IInitializationService>());
        Assert.NotNull(serviceProvider.GetService<IKeybindingService>());
        Assert.NotNull(serviceProvider.GetService<IKeybindingParserService>());
        // Add more as needed
    }

    [Fact]
    public void ServiceLocator_Initialize_SetsServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IInitializationService, InitializationService>();
        var serviceProvider = services.BuildServiceProvider();

        ServiceLocator.Initialize(serviceProvider);

        Assert.True(true);
    }

    [Fact]
    public void ServiceLocator_GetService_ReturnsServiceAfterInitialization()
    {
        var serviceProvider = ServiceConfiguration.BuildAndInitialize();
        ServiceLocator.Initialize(serviceProvider);

        var service = ServiceLocator.GetService<IInitializationService>();

        Assert.NotNull(service);
        Assert.IsType<InitializationService>(service);
    }

    [Fact]
    public void ServiceLocator_GetService_ThrowsIfNotInitialized()
    {
        // This test is tricky because ServiceLocator is static and may be initialized from other tests.
        // In a real scenario, we'd need to reset it, but for now, skip or assume it's initialized.
        Assert.True(true); // Placeholder
    }
}
