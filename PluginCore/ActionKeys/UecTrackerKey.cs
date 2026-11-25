using System.Diagnostics.CodeAnalysis;
using BarRaider.SdTools;
using SCStreamDeck.Infrastructure;
using SCStreamDeck.Services.Telemetry;

namespace SCStreamDeck.ActionKeys;

/// <summary>
///     Shows the in-game UEC balance and, below it, how much was earned (green) or lost (red) since the session
///     started. Pressing the key restarts the count from the current balance. Rules, images, flash and sounds work
///     as on the other telemetry keys and apply to the balance line.
/// </summary>
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Stream Deck action instantiated via SDK reflection")]
[PluginActionId("com.jarex985.scstreamdeck.uectracker")]
public sealed class UecTrackerKey(SDConnection connection, InitialPayload payload)
    : TelemetryDisplayKeyBase(connection, payload, new UecBalanceEvaluator(ServiceLocator.GetService<UecSessionTracker>()));
