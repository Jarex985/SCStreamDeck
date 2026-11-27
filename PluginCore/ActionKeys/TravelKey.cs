using System.Diagnostics.CodeAnalysis;
using BarRaider.SdTools;
using SCStreamDeck.Infrastructure;
using SCStreamDeck.Services.Telemetry;

namespace SCStreamDeck.ActionKeys;

/// <summary>
///     Measures the elapsed time and the estimated distance of a trip and shows them. Pressing it starts a
///     measurement, stops a running one, and starts a new one after a stop. Rules, images, flash and sounds work as
///     on the other telemetry keys.
/// </summary>
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Stream Deck action instantiated via SDK reflection")]
// The id of the former Travel Timer key, so keys already placed (and their Start look) keep working.
[PluginActionId("com.jarex985.scstreamdeck.traveltimer")]
public sealed class TravelKey(SDConnection connection, InitialPayload payload)
    : TelemetryDisplayKeyBase(connection, payload, new TravelEvaluator(ServiceLocator.GetService<TravelTracker>()));
