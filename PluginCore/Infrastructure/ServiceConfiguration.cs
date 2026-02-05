using Microsoft.Extensions.DependencyInjection;
using SCStreamDeck.Common;
using SCStreamDeck.Services.Audio;
using SCStreamDeck.Services.Core;
using SCStreamDeck.Services.Data;
using SCStreamDeck.Services.Installation;
using SCStreamDeck.Services.Keybinding;
using SCStreamDeck.Services.UI;
using WindowsInput;

namespace SCStreamDeck.Infrastructure;

/// <summary>
///     Configures and registers plugin services for dependency injection.
/// </summary>
public static class ServiceConfiguration
{
    /// <summary>
    ///     Registers all plugin services to service collection.
    /// </summary>
    private static void AddPluginServices(this IServiceCollection services)
    {
        services.AddSingleton<IFileSystem, SystemFileSystem>();

        services.AddSingleton<IInputSimulator, InputSimulator>();
        services.AddSingleton<AudioPlayerService>();

        services.AddSingleton<PathProviderService>();

        services.AddSingleton<StateService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<IP4KArchiveService, P4KArchiveService>();
        services.AddSingleton<ICryXmlParserService, CryXmlParserService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IInstallLocatorService, InstallLocatorService>();

        services.AddSingleton<IKeybindingsJsonCache, KeybindingsJsonCache>();
        services.AddSingleton<ICachedInstallationsCleanupService, CachedInstallationsCleanupService>();

        services.AddSingleton<IKeybindingXmlParserService, KeybindingXmlParserService>();
        services.AddSingleton<IKeybindingMetadataService, KeybindingMetadataService>();
        services.AddSingleton<IKeybindingOutputService, KeybindingOutputService>();
        services.AddSingleton<KeybindingLoaderService>();
        services.AddSingleton<KeybindingExecutorService>();

        services.AddSingleton<KeybindingProcessorService>();
        services.AddSingleton<KeybindingService>();
        services.AddSingleton<ActionMapsWatcherService>();
        services.AddSingleton<InitializationService>();
    }

    /// <summary>
    ///     Builds and initializes the service provider.
    ///     Should be called early in Program.cs before StreamDeck initialization.
    /// </summary>
    public static void BuildAndInitialize()
    {
        ServiceCollection services = new();
        services.AddPluginServices();

        ServiceProvider serviceProvider = services.BuildServiceProvider();
        ServiceLocator.Initialize(serviceProvider);
    }
}
