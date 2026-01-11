using System.Text.Json.Serialization;
using SCStreamDeck.Common;

namespace SCStreamDeck.Models;

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
    private string ChannelPath => Path.Combine(RootPath, SCConstants.Paths.StarCitizenFolderName, Channel.ToString());

    /// <summary>
    ///     Gets the computed Data.p4k path.
    /// </summary>
    [JsonIgnore]
    private string DataP4KPath => Path.Combine(ChannelPath, SCConstants.Files.DataP4KFileName);

    /// <summary>
    ///     Converts to an SCInstallCandidate for use in initialization.
    /// </summary>
    public SCInstallCandidate ToCandidate() =>
        new(
            RootPath,
            Channel,
            ChannelPath,
            DataP4KPath
        );

    /// <summary>
    ///     Validates that the installation still exists and is accessible.
    /// </summary>
    public bool Validate() => Directory.Exists(ChannelPath) && File.Exists(DataP4KPath);

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
