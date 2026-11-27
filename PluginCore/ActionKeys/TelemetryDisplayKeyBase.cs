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
///     Shared behavior of the telemetry display keys. Ordered rules map telemetry values to text, image and colors,
///     and each rule can optionally run a keybinding action when the key is pressed while that rule is active.
///     The simple key tests one attribute; the advanced key lets each rule combine conditions on several attributes.
/// </summary>
public abstract class TelemetryDisplayKeyBase : SCActionBase
{
    private static readonly TimeSpan s_minRenderInterval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan s_tapDuration = TimeSpan.FromMilliseconds(50);

    private readonly object _gate = new();
    private readonly CancellationTokenSource _lifetime = new();
    private int _sequenceRunning;
    private readonly ITelemetryKeyEvaluator _evaluator;
    private readonly TelemetryKeyRenderer _renderer = new();
    private readonly TelemetryService _telemetry;
    private readonly ITelemetrySchemaProvider _schemas;
    private readonly TelemetryTemplateService _templates;
    private readonly TelemetryFlagService _flags;
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
    protected TelemetryDisplayKeyBase(SDConnection connection, InitialPayload payload, ITelemetryKeyEvaluator evaluator)
        : base(connection, payload)
    {
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        TelemetryKeyDependencies deps = ActionDependencies.ForTelemetryDisplayKey();
        _telemetry = deps.Telemetry;
        _schemas = deps.Schemas;
        _templates = deps.Templates;
        _flags = deps.Flags;
        _display = ParseSettings(payload.Settings);
        _flashTimer = new Timer(_ => Refresh(false), null, Timeout.Infinite, Timeout.Infinite);

        Connection.OnPropertyInspectorDidAppear += OnPropertyInspectorDidAppear;
        Connection.OnSendToPlugin += OnSendToPlugin;
        _schemas.Changed += OnSchemaChanged;
        _templates.Changed += OnTemplatesChanged;
        _flags.Changed += OnFlagsChanged;
        _schemas.EnsureRefreshStarted();
        RequestGlobalSettings();

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
        if (_dirty || _evaluator is ITelemetryKeyTimeDriven)
        {
            Refresh(false);
        }
    }

    public override async void KeyPressed(KeyPayload payload)
    {
        try
        {
            // The rule that was active when the key was pressed decides what runs, even if the press changes it.
            (TelemetryRuleMatch match, _, bool disabled, TelemetryRule? pressRule) = EvaluateCurrent();
            if (disabled)
            {
                return;
            }

            bool handled = false;
            if (_evaluator is ITelemetryKeyPressHandler pressHandler)
            {
                pressHandler.OnKeyPressed();
                PlayClickSoundIfConfigured();
                handled = true;
                Refresh(true);
                if (pressHandler.ConsumesPress)
                {
                    return;
                }
            }

            IReadOnlyList<TelemetryPressAction> actions = (pressRule ?? match.Rule).GetPressActions();
            if (!match.HasData)
            {
                // Without telemetry only flags can change; Star Citizen actions need the game.
                actions = actions.Where(a => a.IsFlagAction).ToList();
            }

            if (actions.Count == 0)
            {
                return;
            }

            if (!handled)
            {
                PlayClickSoundIfConfigured();
            }

            if (actions is [{ DelayMs: null or <= 0, IsFlagAction: false } single])
            {
                // One immediate action behaves like Adaptive Key: key down now, key up when released (holds work).
                Interlocked.Exchange(ref _pressedFunction, single.Function);
                await ExecuteAsync(single.Function!, true).ConfigureAwait(false);
                return;
            }

            _ = RunSequenceAsync(actions);
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

    // The sequence was chosen when the key was pressed: actions change telemetry, which may switch the active rule
    // mid-way. Presses during a running sequence are ignored so toggles cannot interleave.
    private async Task RunSequenceAsync(IReadOnlyList<TelemetryPressAction> actions)
    {
        if (Interlocked.CompareExchange(ref _sequenceRunning, 1, 0) != 0)
        {
            return;
        }

        try
        {
            CancellationToken token = _lifetime.Token;
            foreach (TelemetryPressAction action in actions)
            {
                if (action.EffectiveDelayMs > 0)
                {
                    await Task.Delay(action.EffectiveDelayMs, token).ConfigureAwait(false);
                }

                if (action.IsFlagAction)
                {
                    _flags.Apply(action);
                    continue;
                }

                // Each step is a short tap so activation modes that need a release (tap, release-triggered) fire.
                await ExecuteAsync(action.Function!, true).ConfigureAwait(false);
                await Task.Delay(s_tapDuration, token).ConfigureAwait(false);
                await ExecuteAsync(action.Function!, false).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Key disposed while the sequence was running.
        }
        catch (Exception ex)
        {
            Log.Err($"{GetType().Name}: action sequence failed: {ex.Message}", ex);
        }
        finally
        {
            Interlocked.Exchange(ref _sequenceRunning, 0);
        }
    }

    public override void Dispose()
    {
        _lifetime.Cancel();
        _subscription.Dispose();
        _schemas.Changed -= OnSchemaChanged;
        _templates.Changed -= OnTemplatesChanged;
        _flags.Changed -= OnFlagsChanged;
        Connection.OnPropertyInspectorDidAppear -= OnPropertyInspectorDidAppear;
        Connection.OnSendToPlugin -= OnSendToPlugin;

        lock (_gate)
        {
            _disposed = true;
            _flashTimer.Dispose();
            _renderer.Dispose();
        }

        _lifetime.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
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
            settings.StartRule ??= new TelemetryRule();
            settings.StopRule ??= new TelemetryRule();
            settings.ArmedRule ??= new TelemetryRule();
            settings.RecordingRule ??= new TelemetryRule();
            settings.SavedRule ??= new TelemetryRule();
            settings.DiscardedRule ??= new TelemetryRule();
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or InvalidCastException or FormatException)
        {
            Log.Warn($"[{nameof(TelemetryDisplayKeyBase)}] Invalid settings, using defaults: {ex.Message}");
            return new TelemetryDisplaySettings();
        }
    }

    private void RequestGlobalSettings()
    {
        try
        {
            // The answer arrives in ReceivedGlobalSettings.
            _ = Connection.GetGlobalSettingsAsync();
        }
        catch (Exception ex)
        {
            Log.Err($"[{nameof(TelemetryDisplayKeyBase)}] Failed to request global settings: {ex.Message}", ex);
        }
    }

    private void OnTelemetryUpdated() => Refresh(false);

    // A newer protocol definition was read from the game: refresh the attribute list and the key.
    private void OnSchemaChanged()
    {
        SendFieldsToPropertyInspector();
        Refresh(false);
    }

    private void OnTemplatesChanged() => Refresh(false);

    private void OnFlagsChanged() => Refresh(false);

    // Format templates and flags live in the plugin's global settings, shared by all telemetry keys.
    [ExcludeFromCodeCoverage]
    public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        JObject? restored = TelemetryGlobalSettings.Apply(_templates, _flags, payload.Settings);
        if (restored != null)
        {
            // Stream Deck lost the lists (for example after reinstalling the plugin); give them back.
            try
            {
                _ = Connection.SetGlobalSettingsAsync(restored);
            }
            catch (Exception ex)
            {
                Log.Err($"[{nameof(TelemetryDisplayKeyBase)}] Failed to restore the format templates and flags: {ex.Message}", ex);
            }
        }
    }

    private (TelemetryRuleMatch Match, TelemetryRenderModel Model, bool Disabled, TelemetryRule? PressRule) EvaluateCurrent()
    {
        TelemetryDisplaySettings display;
        lock (_gate)
        {
            display = _display;
        }

        TelemetryEvaluation evaluation =
            _evaluator.Evaluate(display, _flags.Overlay(_telemetry.Snapshot), _schemas.Current, DateTime.UtcNow);
        TelemetryRuleMatch match = evaluation.Match;

        TelemetryRuleAppearance appearance = _templates.Resolve(match.Rule);

        return (match, new TelemetryRenderModel(
            evaluation.Text, appearance.ImagePath, appearance.TextColor, appearance.BackgroundColor,
            LineColors: evaluation.LineColors), evaluation.Disabled, evaluation.PressRule);
    }

    private void Refresh(bool force)
    {
        try
        {
            (TelemetryRuleMatch match, TelemetryRenderModel model, _, _) = EvaluateCurrent();

            string? sound = _transitions.OnEvaluated(match, out bool entered);
            if (sound != null)
            {
                _ = Task.Run(() => PlaySoundFile(sound));
            }

            if (entered && _flags.Apply(match.Rule.SetFlags))
            {
                // The flags changed what the rules match; evaluate again with them. The change notification has
                // already refreshed the keys that use flags, this one included.
                Refresh(force);
                return;
            }

            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                DateTime now = DateTime.UtcNow;
                TimeSpan? untilSwitch = null;
                TelemetryRuleAppearance look = _templates.Resolve(match.Rule);
                int flashMs = TelemetryFlash.Normalize(look.FlashMs);
                TelemetryFlashTarget flashTarget = TelemetryFlash.ParseTarget(look.FlashTarget);

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
                    model = model with { Hidden = hidden, FlashTarget = flashTarget };
                }
                else
                {
                    _flashRule = null;
                }

                ArmFlashTimer(untilSwitch);

                string key = $"{model.Text}|{model.ImagePath}|{model.TextColor}|{model.BackgroundColor}|{model.Hidden}|{model.FlashTarget}|{string.Join(',', model.LineColors ?? [])}";
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
            Log.Err($"[{nameof(TelemetryDisplayKeyBase)}] Render failed: {ex.Message}", ex);
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
            Log.Err($"[{nameof(TelemetryDisplayKeyBase)}] Failed to set image: {ex.Message}", ex);
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
                ["telemetryFields"] = JArray.FromObject(_schemas.Current.Fields)
            });
        }
        catch (Exception ex)
        {
            Log.Err($"[{nameof(TelemetryDisplayKeyBase)}] Failed to send telemetry fields: {ex.Message}", ex);
        }
    }
}
