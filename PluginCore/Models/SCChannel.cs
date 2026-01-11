namespace SCStreamDeck.Models;

/// <summary>
///     Star Citizen installation channels.
/// </summary>
public enum SCChannel
{
    Live,
    Hotfix,
    Ptu,
    Eptu
}

/// <summary>
///     Extension methods for SCChannel.
/// </summary>
public static class SCChannelExtensions
{
    private static readonly Dictionary<SCChannel, string> s_channelFolderNames = new()
    {
        { SCChannel.Live, "LIVE" }, { SCChannel.Hotfix, "HOTFIX" }, { SCChannel.Ptu, "PTU" }, { SCChannel.Eptu, "EPTU" }
    };

    /// <summary>
    ///     Gets the folder name for the channel (e.g., "LIVE" for Live).
    /// </summary>
    public static string GetFolderName(this SCChannel channel) => s_channelFolderNames[channel];
}
