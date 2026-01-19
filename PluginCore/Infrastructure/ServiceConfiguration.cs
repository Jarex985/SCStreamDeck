using Microsoft.Extensions.DependencyInjection;
using SCStreamDeck.Services.Audio;
using SCStreamDeck.Services.Core;
using SCStreamDeck.Services.Data;
using SCStreamDeck.Services.Installation;
using SCStreamDeck.Services.Keybinding;
using WindowsInput;

namespace SCStreamDeck.Infrastructure;

/// <summary>
///     Configures and registers all SCCore services for dependency injection.
/// </summary>
public static class ServiceConfiguration
{
    /// <summary>
    ///     Registers all SCCore services to the service collection.
    /// </summary>
    public static IServiceCollection AddSCCoreServices(this IServiceCollection services)
    {
        // Register external dependencies
        services.AddSingleton<IInputSimulator, InputSimulator>();
        services.AddSingleton<IAudioPlayerService, AudioPlayerService>();

        // Register common services

        services.AddSingleton<IPathProvider, PathProviderService>();
        services.AddSingleton<IVersionProvider, VersionProviderService>();

        // Register core services
        services.AddSingleton<IStateService, StateService>();
        services.AddSingleton<IP4KArchiveService, P4KArchiveService>();
        services.AddSingleton<ICryXmlParserService, CryXmlParserService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IInstallLocatorService, InstallLocatorService>();

        // Register keybinding parser and output services (must be before IKeybindingProcessorService)
        services.AddSingleton<IKeybindingXmlParserService, KeybindingXmlParserService>();
        services.AddSingleton<IKeybindingMetadataService, KeybindingMetadataService>();
        services.AddSingleton<IKeybindingOutputService, KeybindingOutputService>();
        services.AddSingleton<IKeybindingLoaderService, KeybindingLoaderService>();
        services.AddSingleton<IKeybindingParserService, KeybindingParserService>();
        services.AddSingleton<IKeybindingExecutorService, KeybindingExecutorService>();

        services.AddSingleton<IKeybindingProcessorService, KeybindingProcessorService>();
        services.AddSingleton<IKeybindingService, KeybindingService>();
        services.AddSingleton<IInitializationService, InitializationService>();

        return services;
    }

    /// <summary>
    ///     Builds and initializes the service provider.
    ///     Should be called early in Program.cs before StreamDeck initialization.
    /// </summary>
    public static IServiceProvider BuildAndInitialize()
    {
        ServiceCollection services = new();
        services.AddSCCoreServices();

        ServiceProvider serviceProvider = services.BuildServiceProvider();
        ServiceLocator.Initialize(serviceProvider);

        return serviceProvider;
    }
}
