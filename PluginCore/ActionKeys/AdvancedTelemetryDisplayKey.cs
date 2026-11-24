using System.Diagnostics.CodeAnalysis;
using BarRaider.SdTools;
using SCStreamDeck.Services.Telemetry;

namespace SCStreamDeck.ActionKeys;

/// <summary>
///     Telemetry key whose rules combine conditions on several attributes (all/any), for example
///     "landed AND hydrogen fuel low".
/// </summary>
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Stream Deck action instantiated via SDK reflection")]
[PluginActionId("com.jarex985.scstreamdeck.telemetryadvanced")]
public sealed class AdvancedTelemetryDisplayKey(SDConnection connection, InitialPayload payload)
    : TelemetryDisplayKeyBase(connection, payload, new RuleKeyEvaluator(true));
