namespace SCStreamDeck.Models;

/// <summary>
///     Represents a detected Star Citizen installation.
/// </summary>
public sealed record SCInstallCandidate(
    string RootPath,
    SCChannel Channel,
    string ChannelPath,
    string DataP4KPath,
    InstallSource Source = InstallSource.AutoDetected
);

/// <summary>
///     Indicates how an installation was detected
/// </summary>
public enum InstallSource
{
    AutoDetected,
    UserProvided
}
