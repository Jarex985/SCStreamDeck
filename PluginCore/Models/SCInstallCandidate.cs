namespace SCStreamDeck.Models;

/// <summary>
///     Represents a detected Star Citizen installation.
/// </summary>
public sealed record SCInstallCandidate(
    string RootPath,
    SCChannel Channel,
    string ChannelPath,
    string DataP4KPath
);
