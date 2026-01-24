using Newtonsoft.Json;
using SCStreamDeck.Common;

namespace SCStreamDeck.Models;

/// <summary>
///     Minimal state information for a Star Citizen installation.
/// </summary>
public sealed record InstallationState(
    [property: JsonProperty("rootPath")] string RootPath,
    [property: JsonProperty("channel")] SCChannel Channel,
    [property: JsonProperty("channelPath")]
    string ChannelPath,
    [property: JsonProperty("isCustomPath")]
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
    /// </summary>
    public static InstallationState FromCandidate(SCInstallCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return new InstallationState(
            candidate.RootPath,
            candidate.Channel,
            candidate.ChannelPath,
            candidate.Source == InstallSource.UserProvided
        );
    }

    /// <summary>
    ///     Creates a custom installation state from a Data.p4k file path.
    ///     Intended for Control Panel overrides.
    /// </summary>
    public static bool TryCreateFromDataP4KPath(SCChannel channel, string dataP4KPath, out InstallationState? state)
    {
        state = null;

        if (string.IsNullOrWhiteSpace(dataP4KPath))
        {
            return false;
        }

        string normalized = SecurePathValidator.TryNormalizePath(dataP4KPath, out string n) ? n : dataP4KPath.Trim();

        if (!File.Exists(normalized))
        {
            return false;
        }

        if (!normalized.EndsWith(SCConstants.Files.DataP4KFileName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string? channelPath = Path.GetDirectoryName(normalized);
        string? rootPath = string.IsNullOrWhiteSpace(channelPath) ? null : Path.GetDirectoryName(channelPath);
        if (string.IsNullOrWhiteSpace(channelPath) || string.IsNullOrWhiteSpace(rootPath))
        {
            return false;
        }

        state = new InstallationState(
            SecurePathValidator.TryNormalizePath(rootPath, out string rootNorm) ? rootNorm : rootPath.Trim(),
            channel,
            SecurePathValidator.TryNormalizePath(channelPath, out string channelNorm) ? channelNorm : channelPath.Trim(),
            true);

        return true;
    }
}
