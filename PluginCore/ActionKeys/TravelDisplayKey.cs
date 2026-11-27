using System.Diagnostics.CodeAnalysis;
using BarRaider.SdTools;
using SCStreamDeck.Infrastructure;
using SCStreamDeck.Services.Telemetry;

namespace SCStreamDeck.ActionKeys;

/// <summary>
///     Shows the elapsed minutes (tenths) and the estimated distance (m, then km) of the travel measurement started
///     with the Travel Timer key. Rules, images, flash and sounds work as on the other telemetry keys.
/// </summary>
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Stream Deck action instantiated via SDK reflection")]
[PluginActionId("com.jarex985.scstreamdeck.traveldisplay")]
public sealed class TravelDisplayKey(SDConnection connection, InitialPayload payload)
    : TelemetryDisplayKeyBase(connection, payload, new TravelDisplayEvaluator(ServiceLocator.GetService<TravelTracker>()));
