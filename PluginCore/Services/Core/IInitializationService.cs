using SCStreamDeck.SCCore.Models;

namespace SCStreamDeck.SCCore.Services.Core;

/// <summary>
///     Service for plugin initialization and state management.
///     Replaces legacy PluginInitializer singleton with DI-ready async service.
/// </summary>
public interface IInitializationService
{
    /// <summary>
    ///     Indicates whether plugin is initialized.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    ///     Ensures the plugin is initialized (lazy initialization).
    ///     Safe to call multiple times - only initializes once.
    /// </summary>
    Task<InitializationResult> EnsureInitializedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Switches to a different Star Citizen channel (LIVE/PTU).
    /// </summary>
    Task<bool> SwitchChannelAsync(SCChannel channel, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Invalidates cached state and forces fresh initialization on next startup.
    /// </summary>
    void InvalidateCache();

    /// <summary>
    ///     Gets the keybindings JSON path for the current channel.
    /// </summary>
    string GetKeybindingsJsonPath();
}

/// <summary>
///     Result of plugin initialization.
/// </summary>
public sealed class InitializationResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public SCChannel SelectedChannel { get; init; }
    public int DetectedInstallations { get; init; }

    public static InitializationResult Success(SCChannel channel, int installCount) =>
        new() { IsSuccess = true, SelectedChannel = channel, DetectedInstallations = installCount };

    public static InitializationResult Failure(string errorMessage) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage };
}
