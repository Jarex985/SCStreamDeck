using System.Diagnostics.CodeAnalysis;
using BarRaider.SdTools;
using SCStreamDeck.Services.Telemetry;

namespace SCStreamDeck.ActionKeys;

/// <summary>
///     Shows one telemetry attribute; rules compare that attribute.
/// </summary>
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Stream Deck action instantiated via SDK reflection")]
[PluginActionId("com.jarex985.scstreamdeck.telemetrydisplay")]
public sealed class TelemetryDisplayKey(SDConnection connection, InitialPayload payload)
    : TelemetryDisplayKeyBase(connection, payload, new RuleKeyEvaluator(false));
