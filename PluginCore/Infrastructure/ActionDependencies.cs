using SCStreamDeck.Services.Audio;
using SCStreamDeck.Services.Core;
using SCStreamDeck.Services.Installation;
using SCStreamDeck.Services.Keybinding;
using SCStreamDeck.Services.UI;

namespace SCStreamDeck.Infrastructure;

/// <summary>
///     Central place for resolving StreamDeck action dependencies.
///     StreamDeck-Tools actions are reflection-constructed, so constructor injection is not available.
///     Keep ServiceLocator usage contained here.
/// </summary>
internal static class ActionDependencies
{
    public static SCActionBaseDependencies ForSCActionBase() => new(
        ServiceLocator.GetService<InitializationService>(),
        ServiceLocator.GetService<KeybindingService>(),
        ServiceLocator.GetService<AudioPlayerService>());

    public static ControlPanelKeyDependencies ForControlPanelKey() => new(
        ServiceLocator.GetService<InitializationService>(),
        ServiceLocator.GetService<StateService>(),
        ServiceLocator.GetService<ThemeService>(),
        ServiceLocator.GetService<PathProviderService>(),
        ServiceLocator.GetService<IKeybindingsJsonCache>());
}

internal sealed record SCActionBaseDependencies(
    InitializationService InitializationService,
    KeybindingService KeybindingService,
    AudioPlayerService AudioPlayerService);

internal sealed record ControlPanelKeyDependencies(
    InitializationService InitializationService,
    StateService StateService,
    ThemeService ThemeService,
    PathProviderService PathProviderService,
    IKeybindingsJsonCache KeybindingsJsonCache);
