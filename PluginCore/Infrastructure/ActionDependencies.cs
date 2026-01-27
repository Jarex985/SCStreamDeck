using SCStreamDeck.Services.Audio;
using SCStreamDeck.Services.Core;
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
        ServiceLocator.GetService<IKeybindingsJsonCache>());
}

internal sealed record SCActionBaseDependencies(
    InitializationService InitializationService,
    KeybindingService KeybindingService,
    AudioPlayerService AudioPlayerService);

internal sealed class ControlPanelKeyDependencies
{
    public ControlPanelKeyDependencies(
        InitializationService initializationService,
        StateService stateService,
        ThemeService themeService,
        IKeybindingsJsonCache keybindingsJsonCache)
    {
        ArgumentNullException.ThrowIfNull(initializationService);
        ArgumentNullException.ThrowIfNull(stateService);
        ArgumentNullException.ThrowIfNull(themeService);
        ArgumentNullException.ThrowIfNull(keybindingsJsonCache);

        InitializationService = initializationService;
        StateService = stateService;
        ThemeService = themeService;
        KeybindingsJsonCache = keybindingsJsonCache;
    }

    public InitializationService InitializationService { get; }
    public StateService StateService { get; }
    public ThemeService ThemeService { get; }
    public IKeybindingsJsonCache KeybindingsJsonCache { get; }
}
