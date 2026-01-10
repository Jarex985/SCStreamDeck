using System.Text.Json.Serialization;

namespace SCStreamDeck.SCCore.Models;

/// <summary>
///     Minimal state information for a Star Citizen installation.
///     Only stores essential data - paths are computed dynamically.
/// </summary>
public sealed record InstallationState(
    [property: JsonPropertyName("rootPath")]
    string RootPath,
    [property: JsonPropertyName("channel")]
    SCChannel Channel
)
{
    /// <summary>
    ///     Gets the computed channel path (e.g., "F:\Roberts Space Industries\StarCitizen\LIVE").
    /// </summary>
    [JsonIgnore]
    public string ChannelPath => Path.Combine(RootPath, "StarCitizen", Channel.ToString());

    /// <summary>
    ///     Gets the computed Data.p4k path.
    /// </summary>
    [JsonIgnore]
    public string DataP4kPath => Path.Combine(ChannelPath, "Data.p4k");

    /// <summary>
    ///     Gets the computed actionmaps.xml path.
    /// </summary>
    [JsonIgnore]
    public string ActionMapsPath =>
        Path.Combine(ChannelPath, "user", "client", "0", "Profiles", "default", "actionmaps.xml");

    /// <summary>
    ///     Converts to an SCInstallCandidate for use in initialization.
    /// </summary>
    public SCInstallCandidate ToCandidate()
    {
        return new SCInstallCandidate(
            RootPath,
            Channel,
            ChannelPath,
            DataP4kPath
        );
    }

    /// <summary>
    ///     Validates that the installation still exists and is accessible.
    /// </summary>
    public bool Validate()
    {
        return Directory.Exists(ChannelPath) && File.Exists(DataP4kPath);
    }

    /// <summary>
    ///     Creates InstallationState from a detected candidate.
    ///     Only stores the minimal required information.
    /// </summary>
    public static InstallationState FromCandidate(SCInstallCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return new InstallationState(
            candidate.RootPath,
            candidate.Channel
        );
    }
}