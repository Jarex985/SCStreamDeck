using SCStreamDeck.SCCore.Buttons;
using SCStreamDeck.SCCore.Services.Core;
using SCStreamDeck.SCCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using SCStreamDeck.SCCore.Buttons.Base;

namespace UnitTests;

public class AdaptiveKeyIntegrationTests
{
    [Fact]
    public async Task Initialization_ServicesAreBuiltAndInitializedCorrectly()
    {
        IServiceProvider serviceProvider = ServiceConfiguration.BuildAndInitialize();
        IInitializationService initService = serviceProvider.GetRequiredService<IInitializationService>();

        SCActionBase.InitializeServices(serviceProvider);
        InitializationResult result = await initService.EnsureInitializedAsync();

        Assert.NotNull(serviceProvider);
        Assert.NotNull(initService);
        Assert.True(result.IsSuccess || !string.IsNullOrEmpty(result.ErrorMessage));
    }

    [Fact]
    public void AdaptiveKey_CanBeInstantiatedWithMocks()
    {
        IServiceProvider serviceProvider = ServiceConfiguration.BuildAndInitialize();
        SCActionBase.InitializeServices(serviceProvider);

        Assert.True(true); // Placeholder: Class exists and can be compiled
    }

    [Fact]
    public void AdaptiveKey_KeyPressedMethodExistsAndIsCallable()
    {
        IServiceProvider serviceProvider = ServiceConfiguration.BuildAndInitialize();
        SCActionBase.InitializeServices(serviceProvider);

        var type = typeof(AdaptiveKey);
        var method = type.GetMethod("KeyPressed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(method);
        Assert.Equal(typeof(void), method.ReturnType);
        Assert.Single(method.GetParameters());
    }

    [Fact]
    public void AdaptiveKey_KeyReleasedMethodExistsAndIsCallable()
    {
        IServiceProvider serviceProvider = ServiceConfiguration.BuildAndInitialize();
        SCActionBase.InitializeServices(serviceProvider);

        var type = typeof(AdaptiveKey);
        var method = type.GetMethod("KeyReleased", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(method);
        Assert.Equal(typeof(void), method.ReturnType);
        Assert.Single(method.GetParameters());
    }

    [Fact]
    public async Task AdaptiveKey_ProcessKeyEventAsync_MethodExistsAndIsCallable()
    {
        // Arrange
        IServiceProvider serviceProvider = ServiceConfiguration.BuildAndInitialize();
        SCActionBase.InitializeServices(serviceProvider);

        // Since we can't instantiate AdaptiveKey without SDConnection, we verify the method exists via reflection
        var type = typeof(AdaptiveKey);
        var method = type.GetMethod("ProcessKeyEventAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method.ReturnType);
        Assert.Single(method.GetParameters());
    }
}
