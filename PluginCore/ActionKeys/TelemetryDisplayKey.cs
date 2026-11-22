using System.Diagnostics.CodeAnalysis;
using BarRaider.SdTools;
using BarRaider.SdTools.Events;
using BarRaider.SdTools.Wrappers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SCStreamDeck.Common;
using SCStreamDeck.Infrastructure;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Telemetry;

namespace SCStreamDeck.ActionKeys;

/// <summary>
///     Shows one value of the Star Citizen telemetry stream. Ordered rules map the value to text, image and colors,
///     and each rule can optionally run a keybinding action when the key is pressed while that rule is active.
/// </summary>
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Stream Deck action instantiated via SDK reflection")]
[PluginActionId("com.jarex985.scstreamdeck.telemetrydisplay")]
public sealed class TelemetryDisplayKey : SCActionBase
{
    private static readonly TimeSpan s_minRenderInterval = TimeSpan.FromMilliseconds(200);

    private readonly object _gate = new();
    private readonly TelemetryKeyRenderer _renderer = new();
    private readonly TelemetryService _telemetry;
    private readonly IDisposable _subscription;

    private TelemetryDisplaySettings _display;
    private string? _renderedKey;
    private bool _renderedHidden;
    private DateTime _lastRenderUtc = DateTime.MinValue;
    private bool _dirty;
    private bool _disposed;
    private string? _pressedFunction;
    private TelemetryRule? _flashRule;
    private int _flashMs;
    private DateTime _flashStartUtc;
    private readonly Timer _flashTimer;
    private readonly TelemetryRuleTransitionTracker _transitions = new();

    [ExcludeFromCodeCoverage]
    public TelemetryDisplayKey(SDConnection connection, InitialPayload payload) : base(connection, payload)
    {
        _telemetry = ActionDependencies.ForTelemetryDisplayKey();
        _display = ParseSettings(payload.Settings);
        _flashTimer = new Timer(_ => Refresh(false), null, Timeout.Infinite, Timeout.Infinite);

        Connection.OnPropertyInspectorDidAppear += OnPropertyInspectorDidAppear;
        Connection.OnSendToPlugin += OnSendToPlugin;

        Refresh(true);
        _subscription = _telemetry.Subscribe(OnTelemetryUpdated);
    }

    [ExcludeFromCodeCoverage]
    public override void ReceivedSettings(ReceivedSettingsPayload payload)
    {
        base.ReceivedSettings(payload);

        lock (_gate)
        {
            _display = ParseSettings(payload.Settings);
        }

        _transitions.Reset();
        Refresh(true);
    }

    public override void OnTick()
    {
        if (_dirty)
        {
            Refresh(false);
        }
    }

    public override async void KeyPressed(KeyPayload payload)
    {
        try
        {
            string? function = EvaluateCurrent().Match is { HasData: true } match ? match.Rule.Function : null;
            if (string.IsNullOrWhiteSpace(function))
            {
                return;
            }

            Interlocked.Exchange(ref _pressedFunction, function);
            PlayClickSoundIfConfigured();
            await ExecuteAsync(function, true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Err($"{GetType().Name}: {ex.Message}", ex);
        }
    }

    public override async void KeyReleased(KeyPayload payload)
    {
        try
        {
            // Release the action that was pressed, even if the active rule changed in between.
            string? function = Interlocked.Exchange(ref _pressedFunction, null);
            if (!string.IsNullOrWhiteSpace(function))
            {
                await ExecuteAsync(function, false).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Log.Err($"{GetType().Name}: {ex.Message}", ex);
        }
    }

    public override void Dispose()
    {
        _subscription.Dispose();
        Connection.OnPropertyInspectorDidAppear -= OnPropertyInspectorDidAppear;
        Connection.OnSendToPlugin -= OnSendToPlugin;

        lock (_gate)
        {
            _disposed = true;
            _flashTimer.Dispose();
            _renderer.Dispose();
        }

        base.Dispose();
    }

    private static TelemetryDisplaySettings ParseSettings(JObject? json)
    {
        if (json == null || json.Count == 0)
        {
            return new TelemetryDisplaySettings();
        }

        try
        {
            TelemetryDisplaySettings settings = json.ToObject<TelemetryDisplaySettings>() ?? new TelemetryDisplaySettings();
            settings.Rules ??= [];
            settings.DefaultRule ??= new TelemetryRule();
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or InvalidCastException or FormatException)
        {
            Log.Warn($"[{nameof(TelemetryDisplayKey)}] Invalid settings, using defaults: {ex.Message}");
            return new TelemetryDisplaySettings();
        }
    }

    private void OnTelemetryUpdated() => Refresh(false);

    private (TelemetryRuleMatch Match, TelemetryRenderModel Model) EvaluateCurrent()
    {
        TelemetryDisplaySettings display;
        lock (_gate)
        {
            display = _display;
        }

        TelemetryField? field = TelemetryFieldCatalog.Find(display.FieldPath);
        TelemetryValue? value = TelemetryFieldCatalog.TryGetValue(_telemetry.Snapshot, display.FieldPath, out TelemetryValue read)
            ? read
            : null;

        TelemetryRuleMatch match = TelemetryRuleEvaluator.Evaluate(display, value);

        string text;
        if (field == null)
        {
            text = "Select\nattribute";
        }
        else
        {
            string label = string.IsNullOrWhiteSpace(display.Label) ? field.Label : display.Label;
            text = TelemetryTextFormatter.Apply(match.Rule.Text, label, TelemetryTextFormatter.FormatValue(value, display.Decimals));
        }

        return (match, new TelemetryRenderModel(text, match.Rule.ImagePath, match.Rule.TextColor, match.Rule.BackgroundColor));
    }

    private void Refresh(bool force)
    {
        try
        {
            (TelemetryRuleMatch match, TelemetryRenderModel model) = EvaluateCurrent();

            string? sound = _transitions.OnEvaluated(match);
            if (sound != null)
            {
                _ = Task.Run(() => PlaySoundFile(sound));
            }

            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                DateTime now = DateTime.UtcNow;
                TimeSpan? untilSwitch = null;
                int flashMs = TelemetryFlash.Normalize(match.Rule.FlashMs);

                if (flashMs > 0)
                {
                    // Restart in the "shown" phase whenever another rule (or interval) takes over.
                    if (!ReferenceEquals(_flashRule, match.Rule) || _flashMs != flashMs)
                    {
                        _flashRule = match.Rule;
                        _flashMs = flashMs;
                        _flashStartUtc = now;
                    }

                    bool hidden = TelemetryFlash.IsHidden(now - _flashStartUtc, flashMs, out TimeSpan remaining);
                    untilSwitch = remaining;
                    model = model with { Hidden = hidden };
                }
                else
                {
                    _flashRule = null;
                }

                ArmFlashTimer(untilSwitch);

                string key = $"{model.Text}|{model.ImagePath}|{model.TextColor}|{model.BackgroundColor}|{model.Hidden}";
                if (!force && key == _renderedKey)
                {
                    _dirty = false;
                    return;
                }

                // A flash switch must not be delayed by the update throttle.
                bool flashSwitch = model.Hidden != _renderedHidden;
                if (!force && !flashSwitch && now - _lastRenderUtc < s_minRenderInterval)
                {
                    _dirty = true;
                    return;
                }

                _renderedKey = key;
                _renderedHidden = model.Hidden;
                _lastRenderUtc = now;
                _dirty = false;

                using System.Drawing.Bitmap bitmap = _renderer.Render(model);
                _ = SendImageAsync(Tools.ImageToBase64(bitmap, true));
            }
        }
        catch (Exception ex)
        {
            Log.Err($"[{nameof(TelemetryDisplayKey)}] Render failed: {ex.Message}", ex);
        }
    }

    private void ArmFlashTimer(TimeSpan? untilSwitch) =>
        // +1 ms so the timer fires just after the boundary and sees the new phase.
        _flashTimer.Change(untilSwitch is { } due ? due + TimeSpan.FromMilliseconds(1) : Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);

    private async Task SendImageAsync(string base64)
    {
        try
        {
            await Connection.SetImageAsync(base64).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Err($"[{nameof(TelemetryDisplayKey)}] Failed to set image: {ex.Message}", ex);
        }
    }

    private async Task ExecuteAsync(string function, bool isKeyDown)
    {
        if (!CanExecuteBindings ||
            !KeybindingService.TryGetAction(function, out KeybindingAction? action) || action == null)
        {
            return;
        }

        string? binding = KeybindingActionResolver.GetExecutableBinding(action);
        if (binding == null)
        {
            return;
        }

        KeybindingExecutionContext context = new()
        {
            ActionName = function, Binding = binding, ActivationMode = action.ActivationMode, IsKeyDown = isKeyDown
        };

        try
        {
            await KeybindingService.ExecuteAsync(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Err($"{GetType().Name}: '{function}': {ex.Message}", ex);
        }
    }

    [ExcludeFromCodeCoverage]
    private void OnPropertyInspectorDidAppear(object? sender, SDEventReceivedEventArgs<PropertyInspectorDidAppear> e) =>
        SendFieldsToPropertyInspector();

    [ExcludeFromCodeCoverage]
    private void OnSendToPlugin(object? sender, SDEventReceivedEventArgs<SendToPlugin> e)
    {
        JObject? payload = e.Event?.Payload;
        if (payload != null && payload.TryGetValue("event", out JToken? eventName) &&
            eventName.ToString() == "propertyInspectorConnected")
        {
            SendFieldsToPropertyInspector();
        }
    }

    private void SendFieldsToPropertyInspector()
    {
        try
        {
            Connection.SendToPropertyInspectorAsync(new JObject
            {
                ["telemetryFields"] = JArray.FromObject(TelemetryFieldCatalog.Default)
            });
        }
        catch (Exception ex)
        {
            Log.Err($"[{nameof(TelemetryDisplayKey)}] Failed to send telemetry fields: {ex.Message}", ex);
        }
    }
}
