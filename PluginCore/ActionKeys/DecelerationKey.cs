using System.Diagnostics.CodeAnalysis;
using BarRaider.SdTools;
using SCStreamDeck.Infrastructure;
using SCStreamDeck.Services.Telemetry;

namespace SCStreamDeck.ActionKeys;

/// <summary>
///     Shows an estimate of the cumulative units and/or the time until a numeric attribute (for example ship speed)
///     reaches zero, based on how fast it dropped over the last moments. Rules, images, flash and sounds work as on
///     the other telemetry keys.
/// </summary>
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Stream Deck action instantiated via SDK reflection")]
[PluginActionId("com.jarex985.scstreamdeck.deceleration")]
public sealed class DecelerationKey(SDConnection connection, InitialPayload payload)
    : TelemetryDisplayKeyBase(connection, payload, new DecelerationEvaluator(ServiceLocator.GetService<DecelerationProfileService>()));
