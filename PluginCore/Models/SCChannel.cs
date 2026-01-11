using System.Reflection;
using System.Runtime.Serialization;

namespace SCStreamDeck.SCCore.Models;

/// <summary>
///     Star Citizen installation channels.
/// </summary>
public enum SCChannel
{
    [EnumMember(Value = "LIVE")]
    Live,
    [EnumMember(Value = "HOTFIX")]
    Hotfix,
    [EnumMember(Value = "PTU")]
    Ptu,
    [EnumMember(Value = "EPTU")]
    Eptu
}

/// <summary>
///     Extension methods for SCChannel.
/// </summary>
public static class SCChannelExtensions
{
    /// <summary>
    ///     Gets the folder name for the channel (e.g., "LIVE" for Live).
    /// </summary>
    public static string GetFolderName(this SCChannel channel)
    {
        var member = typeof(SCChannel).GetMember(channel.ToString())[0];
        var attribute = member.GetCustomAttribute<EnumMemberAttribute>();
        return attribute?.Value ?? channel.ToString();
    }
}
