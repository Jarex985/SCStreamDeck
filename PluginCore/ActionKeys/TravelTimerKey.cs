using System.Diagnostics.CodeAnalysis;
using BarRaider.SdTools;
using SCStreamDeck.Infrastructure;
using SCStreamDeck.Services.Telemetry;

namespace SCStreamDeck.ActionKeys;

/// <summary>
///     Starts and stops the travel measurement (elapsed time and estimated distance). Pressing it while stopped
///     starts a new measurement. Rules, images, flash and sounds work as on the other telemetry keys and can test
///     the state of the measurement.
/// </summary>
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Stream Deck action instantiated via SDK reflection")]
[PluginActionId("com.jarex985.scstreamdeck.traveltimer")]
public sealed class TravelTimerKey(SDConnection connection, InitialPayload payload)
    : TelemetryDisplayKeyBase(connection, payload, new TravelToggleEvaluator(ServiceLocator.GetService<TravelTracker>()));
