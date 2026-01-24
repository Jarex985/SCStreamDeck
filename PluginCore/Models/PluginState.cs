using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SCStreamDeck.Common;
using SCStreamDeck.Logging;

namespace SCStreamDeck.Models;

/// <summary>
///     Modern plugin state for caching installation data and initialization status.
/// </summary>
public sealed record PluginState(
    [property: JsonProperty("lastInitialized")]
    DateTime LastInitialized,
    [property: JsonProperty("selectedChannel")]
    SCChannel SelectedChannel,
    [property: JsonProperty("selectedTheme")]
    string? SelectedTheme,
    [property: JsonProperty("liveInstallation")]
    InstallationState? LiveInstallation,
    [property: JsonProperty("hotfixInstallation")]
    InstallationState? HotfixInstallation,
    [property: JsonProperty("ptuInstallation")]
    InstallationState? PtuInstallation,
    [property: JsonProperty("eptuInstallation")]
    InstallationState? EptuInstallation
)
{
    private static readonly JsonSerializerSettings s_loadSettings = new() { Converters = { new StringEnumConverter() } };

    private static readonly JsonSerializerSettings s_saveSettings = new()
    {
        Formatting = Formatting.Indented, Converters = { new StringEnumConverter() }
    };

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
    ///     Validates that the state contains at least one accessible installation.
    /// </summary>
    public bool IsValid() => GetAllInstallations().Any(i => i != null && i.Validate());

    private IEnumerable<InstallationState?> GetAllInstallations()
    {
        yield return LiveInstallation;
        yield return HotfixInstallation;
        yield return PtuInstallation;
        yield return EptuInstallation;
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
    ///     Creates a new PluginState with updated theme selection.
    /// </summary>
    public PluginState WithSelectedTheme(string? themeFile) => this with { SelectedTheme = themeFile };

    /// <summary>
    ///     Creates a new PluginState with updated last initialized timestamp.
    /// </summary>
    public PluginState WithLastInitialized(DateTime timestamp) => this with { LastInitialized = timestamp };

    /// <summary>
    ///     Loads plugin state from disk. Returns null if file doesn't exist or is invalid.
    /// </summary>
    public static async Task<PluginState?> LoadAsync(
        IFileSystem fileSystem,
        string cacheDir,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);

        string filePath = Path.Combine(cacheDir, ".plugin-state.json");

        if (!fileSystem.FileExists(filePath))
        {
            return null;
        }

        try
        {
            string json = await fileSystem.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<PluginState>(json, s_loadSettings);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            Log.Err($"[{nameof(PluginState)}]: Failed to load plugin state: {ex.Message}", ex);
            return null;
        }
    }

    /// <summary>
    ///     Saves plugin state to disk.
    /// </summary>
    public async Task SaveAsync(
        IFileSystem fileSystem,
        string cacheDir,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);

        string filePath = Path.Combine(cacheDir, ".plugin-state.json");

        try
        {
            string json = JsonConvert.SerializeObject(this, s_saveSettings);
            await fileSystem.WriteAllTextAsync(filePath, json, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Failed to save plugin state: {ex.Message}", ex);
        }
    }
}
