using System.Diagnostics.CodeAnalysis;
using BarRaider.SdTools;
using SCStreamDeck.Common;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;

namespace SCStreamDeck.ActionKeys;

/// <summary>
///     ToggleKey: short press executes binding on KeyUp; long hold flips state at threshold and suppresses execution.
/// </summary>
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Stream Deck action instantiated via SDK reflection")]
[PluginActionId("com.jarex985.scstreamdeck.togglekey")]
public sealed class ToggleKey : SCActionBase
{
    private const double DefaultResetHoldSeconds = 1.0;
    private const double MinResetHoldSeconds = 0.2;
    private const double MaxResetHoldSeconds = 10.0;

    private readonly ToggleKeyCore _core;
    private int _activePressId;
    private CancellationTokenSource? _holdCts;
    private double _holdThresholdSeconds;
    private CancellationTokenSource? _lifetimeCts = new();
    private int _resetAlertPressId;

    [ExcludeFromCodeCoverage]
    public ToggleKey(SDConnection connection, InitialPayload payload) : base(connection, payload)
    {
        double thresholdSeconds = Settings.ResetHoldSeconds ?? DefaultResetHoldSeconds;
        if (!double.IsFinite(thresholdSeconds))
        {
            thresholdSeconds = DefaultResetHoldSeconds;
        }

        thresholdSeconds = Math.Clamp(thresholdSeconds, MinResetHoldSeconds, MaxResetHoldSeconds);
        Volatile.Write(ref _holdThresholdSeconds, thresholdSeconds);
        _core = new ToggleKeyCore(TimeSpan.FromSeconds(thresholdSeconds));

        // Best-effort: default to Off on construction.
        _ = RunSafeAsync(async () =>
        {
            await Connection.SetStateAsync(0).ConfigureAwait(false);
            await Connection.SetDefaultImageAsync().ConfigureAwait(false);
        });
    }

    public override void KeyPressed(KeyPayload payload)
    {
        try
        {
            DateTime now = DateTime.UtcNow;
            int pressId = _core.OnKeyDown(now);
            _activePressId = pressId;

            CancellationTokenSource? previous = Interlocked.Exchange(ref _holdCts, new CancellationTokenSource());
            previous?.Cancel();
            previous?.Dispose();

            CancellationToken token = _holdCts!.Token;
            TimeSpan holdThreshold = TimeSpan.FromSeconds(Volatile.Read(ref _holdThresholdSeconds));
            _ = RunSafeAsync(async () =>
            {
                await Task.Delay(holdThreshold, token).ConfigureAwait(false);
                ToggleKeyDecision decision = _core.OnHoldThresholdElapsed(pressId);

                if (decision.ImmediateEffects.Count > 0)
                {
                    Log.Debug($"[{nameof(ToggleKey)}] HoldThreshold pressId={pressId} -> reset state={_core.GetVisualState()}");
                }

                await ApplyEffectsAsync(decision.ImmediateEffects).ConfigureAwait(false);

                if (decision.ImmediateEffects.Count > 0)
                {
                    try
                    {
                        await Connection.ShowAlert().ConfigureAwait(false);
                        Volatile.Write(ref _resetAlertPressId, pressId);
                    }
                    catch (Exception ex)
                    {
                        Log.Err($"[{nameof(ToggleKey)}] Failed to show alert: {ex.Message}", ex);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Log.Err($"{GetType().Name}: {ex.Message}", ex);
        }
    }

    [ExcludeFromCodeCoverage]
    public override void ReceivedSettings(ReceivedSettingsPayload payload)
    {
        base.ReceivedSettings(payload);

        double thresholdSeconds = Settings.ResetHoldSeconds ?? DefaultResetHoldSeconds;
        if (!double.IsFinite(thresholdSeconds))
        {
            thresholdSeconds = DefaultResetHoldSeconds;
        }

        thresholdSeconds = Math.Clamp(thresholdSeconds, MinResetHoldSeconds, MaxResetHoldSeconds);
        Volatile.Write(ref _holdThresholdSeconds, thresholdSeconds);
        _core.SetHoldThreshold(TimeSpan.FromSeconds(thresholdSeconds));
    }

    public override void KeyReleased(KeyPayload payload) => _ = RunSafeAsync(() => HandleKeyReleasedAsync(payload));

    public override void Dispose()
    {
        _holdCts?.Cancel();
        _holdCts?.Dispose();
        _holdCts = null;

        CancellationTokenSource? lifetimeCts = Interlocked.Exchange(ref _lifetimeCts, null);
        if (lifetimeCts != null)
        {
            lifetimeCts.Cancel();
            lifetimeCts.Dispose();
        }

        base.Dispose();
    }

    private async Task ApplyEffectsAsync(IReadOnlyList<ToggleKeyEffect> effects)
    {
        if (effects.Count == 0)
        {
            return;
        }

        foreach (ToggleKeyEffect effect in effects)
        {
            switch (effect.Kind)
            {
                case ToggleKeyEffectKind.SetVisualState:
                    if (effect.State is { } state)
                    {
                        await Connection.SetStateAsync(state).ConfigureAwait(false);
                    }

                    continue;
                case ToggleKeyEffectKind.PlayClickSound:
                    PlayClickSoundIfConfigured();
                    continue;
                default:
                    continue;
            }
        }
    }

    private async Task HandleKeyReleasedAsync(KeyPayload payload)
    {
        _ = payload;
        CancellationToken lifetimeToken = _lifetimeCts?.Token ?? CancellationToken.None;

        await CancelHoldAsync().ConfigureAwait(false);

        DateTime now = DateTime.UtcNow;
        int pressId = _activePressId;

        ToggleKeyDecision decision = _core.OnKeyUp(now, pressId);
        await ApplyEffectsAsync(decision.ImmediateEffects).ConfigureAwait(false);

        if (decision.ExecuteId == null)
        {
            await HandleSuppressedKeyUpAsync(pressId, decision, lifetimeToken).ConfigureAwait(false);
            return;
        }

        (KeybindingAction, string)? validationResult = ValidateAndResolve();
        if (validationResult == null)
        {
            _ = _core.OnExecutionCompleted(decision.ExecuteId.Value, false);
            Log.Debug($"[{nameof(ToggleKey)}] KeyUp pressId={pressId} invalid/unavailable");
            return;
        }

        string executableBinding = validationResult.Value.Item2;
#if DEBUG
        KeybindingAction action = validationResult.Value.Item1;
        Log.Debug($"[{nameof(ToggleKey)}] Execute pressId={pressId} sdkState={payload.State} action={action.ActionName}");
#endif

        bool success = await KeybindingService.ExecutePressNoRepeatAsync(
                Settings.Function!,
                executableBinding,
                lifetimeToken)
            .ConfigureAwait(false);

        Log.Debug($"[{nameof(ToggleKey)}] Execute completed pressId={pressId} success={success}");

        IReadOnlyList<ToggleKeyEffect> effects = _core.OnExecutionCompleted(decision.ExecuteId.Value, success);
        await ApplyEffectsAsync(effects).ConfigureAwait(false);
    }

    private async Task CancelHoldAsync()
    {
        CancellationTokenSource? holdCts = _holdCts;
        if (holdCts == null)
        {
            return;
        }

        try
        {
            await holdCts.CancelAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // CTS can be disposed concurrently (e.g., replaced in KeyPressed).
        }
    }

    private async Task HandleSuppressedKeyUpAsync(int pressId, ToggleKeyDecision decision, CancellationToken lifetimeToken)
    {
        // If the threshold timer did the reset, we may need to force a refresh to clear the alert image.
        if (Volatile.Read(ref _resetAlertPressId) == pressId)
        {
            try
            {
                await Connection.SetStateAsync(_core.GetVisualState()).ConfigureAwait(false);
                Volatile.Write(ref _resetAlertPressId, 0);
            }
            catch (Exception ex)
            {
                Log.Err($"[{nameof(ToggleKey)}] Failed to refresh state after reset: {ex.Message}", ex);
            }
        }

        // If KeyUp handled the reset (timer was delayed/canceled), show the alert briefly.
        if (decision.ImmediateEffects.Count > 0)
        {
            try
            {
                await Connection.ShowAlert().ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromMilliseconds(250), lifetimeToken).ConfigureAwait(false);
                await Connection.SetStateAsync(_core.GetVisualState()).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected if the action is disposed while waiting.
            }
            catch (Exception ex)
            {
                Log.Err($"[{nameof(ToggleKey)}] Failed to show alert: {ex.Message}", ex);
            }
        }
    }

    private static Task RunSafeAsync(Func<Task> work) =>
        Task.Run(async () =>
        {
            try
            {
                await work().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when the action is disposed or hold timer is canceled.
            }
            catch (Exception ex)
            {
                Log.Err($"[{nameof(ToggleKey)}] Background task failed: {ex.Message}", ex);
            }
        });

    /// <summary>
    ///     Validates and resolves keybinding action.
    ///     Marked as [ExcludeFromCodeCoverage] because:
    ///     - Depends on Stream Deck SDK runtime (SDConnection, InitialPayload, KeybindingService)
    ///     - SDK cannot be properly mocked without external dependencies
    ///     - Requires running Stream Deck host application
    /// </summary>
    [ExcludeFromCodeCoverage]
    private (KeybindingAction, string)? ValidateAndResolve()
    {
        if (string.IsNullOrWhiteSpace(Settings.Function) || !CanExecuteBindings)
        {
            return null;
        }

        if (!KeybindingService.TryGetAction(Settings.Function, out KeybindingAction? action) || action == null)
        {
            return null;
        }

        string? executableBinding = GetExecutableBinding(action);

        if (executableBinding == null)
        {
            return null;
        }

        return (action, executableBinding);
    }

    [ExcludeFromCodeCoverage]
    private static string? GetExecutableBinding(KeybindingAction action)
    {
        if (!string.IsNullOrWhiteSpace(action.KeyboardBinding))
        {
            return action.KeyboardBinding;
        }

        if (string.IsNullOrWhiteSpace(action.MouseBinding))
        {
            return null;
        }

        InputType bindingType = action.MouseBinding.GetInputType();
        return bindingType is InputType.MouseButton or InputType.MouseWheel ? action.MouseBinding : null;
    }
}

internal enum ToggleKeyEffectKind
{
    SetVisualState,
    PlayClickSound
}

internal readonly record struct ToggleKeyEffect(ToggleKeyEffectKind Kind, uint? State = null);

internal readonly record struct ToggleKeyDecision(int? ExecuteId, IReadOnlyList<ToggleKeyEffect> ImmediateEffects);

/// <summary>
///     SDK-independent core state machine for ToggleKey.
/// </summary>
internal sealed class ToggleKeyCore(TimeSpan holdThreshold)
{
    private readonly object _gate = new();

    private TimeSpan _holdThreshold = holdThreshold;

    private bool _isHeld;
    private DateTime _keyDownAt;
    private int _nextExecuteId;
    private int? _pendingExecuteId;

    private int _pressId;
    private bool _resetTriggeredForPress;
    private uint _visualState;

    public void SetHoldThreshold(TimeSpan holdThreshold)
    {
        lock (_gate)
        {
            _holdThreshold = holdThreshold;
        }
    }

    public uint GetVisualState()
    {
        lock (_gate)
        {
            return _visualState;
        }
    }

    public int OnKeyDown(DateTime utcNow)
    {
        lock (_gate)
        {
            _pressId++;
            _isHeld = true;
            _resetTriggeredForPress = false;
            _pendingExecuteId = null;
            _keyDownAt = utcNow;
            return _pressId;
        }
    }

    public ToggleKeyDecision OnHoldThresholdElapsed(int pressId)
    {
        lock (_gate)
        {
            if (pressId != _pressId || !_isHeld || _resetTriggeredForPress)
            {
                return new ToggleKeyDecision(null, []);
            }

            _resetTriggeredForPress = true;
            uint newState = ToggleState(_visualState);
            _visualState = newState;

            return new ToggleKeyDecision(null,
            [
                new ToggleKeyEffect(ToggleKeyEffectKind.SetVisualState, newState),
                new ToggleKeyEffect(ToggleKeyEffectKind.PlayClickSound)
            ]);
        }
    }

    public ToggleKeyDecision OnKeyUp(DateTime utcNow, int pressId)
    {
        lock (_gate)
        {
            if (pressId != _pressId)
            {
                return new ToggleKeyDecision(null, []);
            }

            _isHeld = false;

            if (_resetTriggeredForPress)
            {
                return new ToggleKeyDecision(null, []);
            }

            TimeSpan held = utcNow - _keyDownAt;
            if (held >= _holdThreshold)
            {
                // Defensive: if the timer was delayed, treat as long hold and suppress execution.
                _resetTriggeredForPress = true;
                uint newState = ToggleState(_visualState);
                _visualState = newState;

                return new ToggleKeyDecision(null,
                [
                    new ToggleKeyEffect(ToggleKeyEffectKind.SetVisualState, newState),
                    new ToggleKeyEffect(ToggleKeyEffectKind.PlayClickSound)
                ]);
            }

            int executeId = ++_nextExecuteId;
            _pendingExecuteId = executeId;
            return new ToggleKeyDecision(executeId, []);
        }
    }

    public IReadOnlyList<ToggleKeyEffect> OnExecutionCompleted(int executeId, bool success)
    {
        lock (_gate)
        {
            if (_pendingExecuteId != executeId)
            {
                return [];
            }

            _pendingExecuteId = null;

            if (!success)
            {
                return [];
            }

            uint newState = ToggleState(_visualState);
            _visualState = newState;
            return
            [
                new ToggleKeyEffect(ToggleKeyEffectKind.SetVisualState, newState),
                new ToggleKeyEffect(ToggleKeyEffectKind.PlayClickSound)
            ];
        }
    }

    private static uint ToggleState(uint current) => current == 0 ? 1u : 0u;
}
