using Newtonsoft.Json.Linq;
using SCStreamDeck.Models;

namespace SCStreamDeck.Services.UI;

internal static class ControlPanelPayloadBuilder
{
    public static JObject Build(
        PluginState? state,
        bool initialized,
        SCChannel currentChannel,
        Func<SCChannel, bool> keybindingsJsonExists,
        Func<InstallationState, bool> isInstallationValid)
    {
        ArgumentNullException.ThrowIfNull(keybindingsJsonExists);
        ArgumentNullException.ThrowIfNull(isInstallationValid);

        SCChannel preferred = state?.SelectedChannel ?? SCChannel.Live;

        JArray channels = [];
        foreach (SCChannel ch in Enum.GetValues<SCChannel>())
        {
            InstallationState? install = state?.GetInstallation(ch);
            bool configured = install != null;
            bool valid = install != null && isInstallationValid(install);
            string dataP4KPath = install?.ToCandidate().DataP4KPath ?? string.Empty;

            channels.Add(new JObject
            {
                ["channel"] = ch.ToString(),
                ["configured"] = configured,
                ["valid"] = valid,
                ["isCustomPath"] = install?.IsCustomPath ?? false,
                ["rootPath"] = install?.RootPath ?? string.Empty,
                ["channelPath"] = install?.ChannelPath ?? string.Empty,
                ["dataP4KPath"] = dataP4KPath,
                ["keybindingsJsonExists"] = keybindingsJsonExists(ch)
            });
        }

        bool preferredAvailable = false;
        InstallationState? preferredInstall = state?.GetInstallation(preferred);
        if (preferredInstall != null)
        {
            preferredAvailable = isInstallationValid(preferredInstall);
        }

        return new JObject
        {
            ["initialized"] = initialized,
            ["currentChannel"] = currentChannel.ToString(),
            ["preferredChannel"] = preferred.ToString(),
            ["preferredAvailable"] = preferredAvailable,
            ["lastInitialized"] = state?.LastInitialized.ToString("O") ?? string.Empty,
            ["channels"] = channels
        };
    }

    public static JObject BuildFailurePayload() =>
        new()
        {
            ["initialized"] = false,
            ["currentChannel"] = nameof(SCChannel.Live),
            ["preferredChannel"] = nameof(SCChannel.Live),
            ["preferredAvailable"] = false,
            ["lastInitialized"] = string.Empty,
            ["channels"] = new JArray()
        };
}
