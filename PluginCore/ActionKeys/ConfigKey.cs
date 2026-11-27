using System.Diagnostics.CodeAnalysis;
using BarRaider.SdTools;
using BarRaider.SdTools.Events;
using BarRaider.SdTools.Payloads;
using BarRaider.SdTools.Wrappers;
using Newtonsoft.Json.Linq;
using SCStreamDeck.Infrastructure;
using SCStreamDeck.Logging;
using SCStreamDeck.Services.Telemetry;

namespace SCStreamDeck.ActionKeys;

/// <summary>
///     Holds the settings shared by the telemetry keys: the flags rules can set and test, and the format templates
///     (colors, image, flash) rules can use. Everything is edited in this key's property inspector; the key itself
///     does nothing when pressed.
/// </summary>
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Stream Deck action instantiated via SDK reflection")]
[PluginActionId("com.jarex985.scstreamdeck.config")]
public sealed class ConfigKey : KeyAndEncoderBase
{
    private readonly TelemetryTemplateService _templates;
    private readonly TelemetryFlagService _flags;

    [ExcludeFromCodeCoverage]
    public ConfigKey(SDConnection connection, InitialPayload payload) : base(connection, payload)
    {
        ConfigKeyDependencies deps = ActionDependencies.ForConfigKey();
        _templates = deps.Templates;
        _flags = deps.Flags;

        try
        {
            // The answer arrives in ReceivedGlobalSettings; it also restores lists Stream Deck lost.
            _ = Connection.GetGlobalSettingsAsync();
        }
        catch (Exception ex)
        {
            Log.Err($"[{nameof(ConfigKey)}] Failed to request global settings: {ex.Message}", ex);
        }
    }

    [ExcludeFromCodeCoverage]
    public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        JObject? restored = TelemetryGlobalSettings.Apply(_templates, _flags, payload.Settings);
        if (restored == null)
        {
            return;
        }

        try
        {
            _ = Connection.SetGlobalSettingsAsync(restored);
        }
        catch (Exception ex)
        {
            Log.Err($"[{nameof(ConfigKey)}] Failed to restore the format templates and flags: {ex.Message}", ex);
        }
    }

    public override void KeyPressed(KeyPayload payload)
    {
    }

    public override void KeyReleased(KeyPayload payload)
    {
    }

    public override void OnTick()
    {
    }

    public override void ReceivedSettings(ReceivedSettingsPayload payload)
    {
    }

    public override void DialRotate(DialRotatePayload payload)
    {
    }

    public override void DialDown(DialPayload payload)
    {
    }

    public override void DialUp(DialPayload payload)
    {
    }

    public override void TouchPress(TouchpadPressPayload payload)
    {
    }

    public override void Dispose() => GC.SuppressFinalize(this);
}
