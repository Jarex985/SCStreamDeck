using System.Text.Json.Serialization;
using SCStreamDeck.Common;

namespace SCStreamDeck.Models;

/// <summary>
///     Minimal state information for a Star Citizen installation.
///     Stores actual paths instead of computing them to support any custom installation structure.
/// </summary>
public sealed record InstallationState(
    [property: JsonPropertyName("rootPath")]
    string RootPath,
    [property: JsonPropertyName("channel")]
    SCChannel Channel,
    [property: JsonPropertyName("channelPath")]
    string ChannelPath,
    [property: JsonPropertyName("isCustomPath")]
    bool IsCustomPath = false
)
{
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
            DataP4KPath,
            IsCustomPath ? InstallSource.UserProvided : InstallSource.AutoDetected
        );

    /// <summary>
    ///     Validates that the installation still exists and is accessible.
    /// </summary>
    public bool Validate() => Directory.Exists(ChannelPath) && File.Exists(DataP4KPath);

    /// <summary>
    ///     Creates InstallationState from a detected candidate.
    ///     Stores the actual channel path instead of computing it.
    /// </summary>
    public static InstallationState FromCandidate(SCInstallCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return new InstallationState(
            candidate.RootPath,
            candidate.Channel,
            candidate.ChannelPath,
            IsCustomPath: candidate.Source == InstallSource.UserProvided
        );
    }
}
