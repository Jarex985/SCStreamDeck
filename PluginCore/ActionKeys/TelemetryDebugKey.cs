using System.Diagnostics.CodeAnalysis;
using BarRaider.SdTools;
using BarRaider.SdTools.Events;
using BarRaider.SdTools.Payloads;
using BarRaider.SdTools.Wrappers;
using SCStreamDeck.Infrastructure;
using SCStreamDeck.Logging;
using SCStreamDeck.Services.Telemetry;

namespace SCStreamDeck.ActionKeys;

/// <summary>
///     Toggles the telemetry debug capture: while on, raw frames of the game's telemetry stream are written to
///     <c>telemetry-debug.log</c> next to the plugin log. The key shows whether it is on, the stream state and the
///     number of frames received.
/// </summary>
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Stream Deck action instantiated via SDK reflection")]
[PluginActionId("com.jarex985.scstreamdeck.telemetrydebug")]
public sealed class TelemetryDebugKey : KeyAndEncoderBase
{
    private readonly TelemetryService _telemetry;
    private readonly TelemetryDebugRecorder _recorder;
    private readonly TelemetryKeyRenderer _renderer = new();
    private readonly IDisposable _subscription;
    private readonly object _gate = new();

    private string? _renderedKey;
    private bool _disposed;

    [ExcludeFromCodeCoverage]
    public TelemetryDebugKey(SDConnection connection, InitialPayload payload) : base(connection, payload)
    {
        TelemetryDebugKeyDependencies deps = ActionDependencies.ForTelemetryDebugKey();
        _telemetry = deps.Telemetry;
        _recorder = deps.Recorder;

        _recorder.Changed += OnRecorderChanged;
        Render();

        // Keeps the stream open so frames can be captured even when no display key is on the deck.
        _subscription = _telemetry.Subscribe(() => { });
    }

    public override void KeyPressed(KeyPayload payload)
    {
        try
        {
            _recorder.Toggle();
            Render();
        }
        catch (Exception ex)
        {
            Log.Err($"[{nameof(TelemetryDebugKey)}] Toggle failed: {ex.Message}", ex);
        }
    }

    public override void KeyReleased(KeyPayload payload)
    {
    }

    // The frame counter changes constantly; refresh once per tick instead of per frame.
    public override void OnTick() => Render();

    public override void ReceivedSettings(ReceivedSettingsPayload payload)
    {
    }

    public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
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

    public override void Dispose()
    {
        _recorder.Changed -= OnRecorderChanged;
        _subscription.Dispose();

        lock (_gate)
        {
            _disposed = true;
            _renderer.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private void OnRecorderChanged() => Render();

    internal static TelemetryRenderModel BuildModel(bool enabled, TelemetryConnectionState state, int framesSeen) =>
        enabled
            ? new TelemetryRenderModel(
                $"DEBUG ON\n{StateText(state)}\n{framesSeen} frames", null, "#FFFFFF", "#B00020")
            : new TelemetryRenderModel("DEBUG\nOFF", null, "#BBBBBB", "#303030");

    private static string StateText(TelemetryConnectionState state) => state switch
    {
        TelemetryConnectionState.Streaming => "streaming",
        TelemetryConnectionState.Connecting => "connecting",
        _ => "no data"
    };

    private void Render()
    {
        try
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                TelemetryRenderModel model = BuildModel(_recorder.IsEnabled, _telemetry.State, _recorder.FramesSeen);
                string key = $"{model.Text}|{model.BackgroundColor}";
                if (key == _renderedKey)
                {
                    return;
                }

                _renderedKey = key;
                using System.Drawing.Bitmap bitmap = _renderer.Render(model);
                _ = SendImageAsync(BarRaider.SdTools.Tools.ImageToBase64(bitmap, true));
            }
        }
        catch (Exception ex)
        {
            Log.Err($"[{nameof(TelemetryDebugKey)}] Render failed: {ex.Message}", ex);
        }
    }

    private async Task SendImageAsync(string base64)
    {
        try
        {
            await Connection.SetImageAsync(base64).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Err($"[{nameof(TelemetryDebugKey)}] Failed to set image: {ex.Message}", ex);
        }
    }
}
