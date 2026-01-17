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
    /// <summary>
    ///     Installation was auto-detected from RSI Launcher logs
    /// </summary>
    AutoDetected,

    /// <summary>
    ///     Installation was manually configured by user in custom-paths.ini
    /// </summary>
    UserProvided
}
