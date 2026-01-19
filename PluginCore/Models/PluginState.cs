using System.Text.Json;
using System.Text.Json.Serialization;
using BarRaider.SdTools;

namespace SCStreamDeck.Models;

/// <summary>
///     Modern plugin state for caching installation data and initialization status.
///     Stored as JSON file in plugin cache directory for immediate availability.
/// </summary>
public sealed record PluginState(
    [property: JsonPropertyName("pluginVersion")]
    string PluginVersion,
    [property: JsonPropertyName("lastInitialized")]
    DateTime LastInitialized,
    [property: JsonPropertyName("selectedChannel")]
    SCChannel SelectedChannel,
    [property: JsonPropertyName("liveInstallation")]
    InstallationState? LiveInstallation,
    [property: JsonPropertyName("hotfixInstallation")]
    InstallationState? HotfixInstallation,
    [property: JsonPropertyName("ptuInstallation")]
    InstallationState? PtuInstallation,
    [property: JsonPropertyName("eptuInstallation")]
    InstallationState? EptuInstallation
)
{
    private static readonly JsonSerializerOptions s_loadOptions =
        new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };

    private static readonly JsonSerializerOptions s_saveOptions =
        new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    /// <summary>
    ///     Gets the installation state for the specified channel.
    /// </summary>
    public InstallationState? GetInstallation(SCChannel channel) =>
        channel switch
        {
            SCChannel.Live => LiveInstallation,
            SCChannel.Hotfix => HotfixInstallation,
            SCChannel.Ptu => PtuInstallation,
            SCChannel.Eptu => EptuInstallation,
            _ => null
        };

    /// <summary>
    ///     Gets all cached installation candidates (LIVE, PTU, and/or EPTU).
    /// </summary>
    public IReadOnlyList<SCInstallCandidate> GetCachedCandidates()
    {
        List<SCInstallCandidate> candidates = [];

        if (LiveInstallation != null)
        {
            candidates.Add(LiveInstallation.ToCandidate());
        }

        if (HotfixInstallation != null)
        {
            candidates.Add(HotfixInstallation.ToCandidate());
        }

        if (PtuInstallation != null)
        {
            candidates.Add(PtuInstallation.ToCandidate());
        }

        if (EptuInstallation != null)
        {
            candidates.Add(EptuInstallation.ToCandidate());
        }

        return candidates;
    }

    /// <summary>
    ///     Validates that the current state is still valid (installation exists and is accessible).
    /// </summary>
    public bool IsValid()
    {
        InstallationState? installation = GetInstallation(SelectedChannel);
        return installation != null && installation.Validate();
    }

    /// <summary>
    ///     Creates a new PluginState with updated installation for the specified channel.
    /// </summary>
    public PluginState WithInstallation(SCChannel channel, InstallationState installation) =>
        channel switch
        {
            SCChannel.Live => this with { LiveInstallation = installation },
            SCChannel.Hotfix => this with { HotfixInstallation = installation },
            SCChannel.Ptu => this with { PtuInstallation = installation },
            SCChannel.Eptu => this with { EptuInstallation = installation },
            _ => this
        };

    /// <summary>
    ///     Creates a new PluginState with the installation removed for the specified channel.
    /// </summary>
    public PluginState WithoutInstallation(SCChannel channel) =>
        channel switch
        {
            SCChannel.Live => this with { LiveInstallation = null },
            SCChannel.Hotfix => this with { HotfixInstallation = null },
            SCChannel.Ptu => this with { PtuInstallation = null },
            SCChannel.Eptu => this with { EptuInstallation = null },
            _ => this
        };

    /// <summary>
    ///     Creates a new PluginState with updated selected channel.
    /// </summary>
    public PluginState WithSelectedChannel(SCChannel channel) => this with { SelectedChannel = channel };

    /// <summary>
    ///     Creates a new PluginState with updated last initialized timestamp.
    /// </summary>
    public PluginState WithLastInitialized(DateTime timestamp) => this with { LastInitialized = timestamp };

    /// <summary>
    ///     Loads plugin state from disk. Returns null if file doesn't exist or is invalid.
    /// </summary>
    public static async Task<PluginState?> LoadAsync(string cacheDir, CancellationToken cancellationToken = default)
    {
        string filePath = Path.Combine(cacheDir, ".plugin-state.json");

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            await using FileStream stream = File.OpenRead(filePath);
            PluginState? state = await JsonSerializer.DeserializeAsync<PluginState>(
                stream,
                s_loadOptions,
                cancellationToken).ConfigureAwait(false);

            return state;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(PluginState)}]: Failed to load plugin state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Saves plugin state to disk.
    /// </summary>
    public async Task SaveAsync(string cacheDir, CancellationToken cancellationToken = default)
    {
        string filePath = Path.Combine(cacheDir, ".plugin-state.json");

        try
        {
            await using FileStream stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(
                stream,
                this,
                s_saveOptions,
                cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            // Log error but don't throw - state saving failure shouldn't crash initialization
            throw new InvalidOperationException($"Failed to save plugin state: {ex.Message}", ex);
        }
    }
}
