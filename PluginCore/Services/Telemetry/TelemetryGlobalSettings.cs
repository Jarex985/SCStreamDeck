using Newtonsoft.Json.Linq;

namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     The plugin's global settings hold the lists shared by all telemetry keys (format templates and flags).
/// </summary>
public static class TelemetryGlobalSettings
{
    /// <summary>
    ///     Hands the global settings to the template and flag services. Returns the settings to write back to Stream
    ///     Deck when either list had to be restored from its backup, otherwise null.
    /// </summary>
    public static JObject? Apply(TelemetryTemplateService templates, TelemetryFlagService flags, JObject? settings)
    {
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(flags);

        JObject? restoredTemplates = templates.ApplyGlobalSettings(settings);
        JObject? restoredFlags = flags.ApplyGlobalSettings(restoredTemplates ?? settings);
        return restoredFlags ?? restoredTemplates;
    }
}
