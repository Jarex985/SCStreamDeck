using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using BarRaider.SdTools;
using BarRaider.SdTools.Payloads;
using SCStreamDeck.Common;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;

namespace SCStreamDeck.ActionKeys;

/// <summary>
///     Adaptive Star Citizen dial.
///     Supports separate functions for rotate left, rotate right, and dial press.
/// </summary>
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Stream Deck action instantiated via SDK reflection")]
[PluginActionId("com.jarex985.scstreamdeck.adaptivedial")]
public sealed class AdaptiveDial(SDConnection connection, InitialPayload payload) : SCDialActionBase(connection, payload)
{
    public override async void DialRotate(DialRotatePayload payload)
    {
        try
        {
            await ProcessDialRotateAsync(payload.Ticks).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Err($"{GetType().Name}: {ex.Message}", ex);
        }
    }

    public override async void DialDown(DialPayload payload)
    {
        try
        {
            PlayClickSoundIfConfigured();
            await ProcessPressEventAsync(true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Err($"{GetType().Name}: {ex.Message}", ex);
        }
    }

    public override async void DialUp(DialPayload payload)
    {
        try
        {
            await ProcessPressEventAsync(false).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Err($"{GetType().Name}: {ex.Message}", ex);
        }
    }

    public override async void TouchPress(TouchpadPressPayload payload)
    {
        try
        {
            // SDK versions may differ on TouchpadPressPayload shape; read pressed-state defensively.
            bool isKeyDown = true;
            object? pressedValue = payload.GetType().GetProperty("Pressed")?.GetValue(payload);
            if (pressedValue is bool pressed)
            {
                isKeyDown = pressed;
            }

            if (isKeyDown)
            {
                PlayClickSoundIfConfigured();
            }

            await ProcessPressEventAsync(isKeyDown).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Err($"{GetType().Name}: {ex.Message}", ex);
        }
    }

    private async Task ProcessDialRotateAsync(int ticks)
    {
        if (ticks == 0)
        {
            return;
        }

        string? actionId = ResolveRotationFunction(Settings, ticks);
        (KeybindingAction, string)? validationResult = ValidateAndResolve(actionId);
        if (validationResult == null || string.IsNullOrWhiteSpace(actionId))
        {
            return;
        }

        int rotationSteps = Math.Abs(ticks);
        string executableBinding = validationResult.Value.Item2;

        for (int i = 0; i < rotationSteps; i++)
        {
            bool success = await KeybindingService.ExecutePressNoRepeatAsync(actionId, executableBinding)
                .ConfigureAwait(false);
            if (success)
            {
                LogRotateExec(actionId, ticks, executableBinding);
            }
        }
    }

    private async Task ProcessPressEventAsync(bool isKeyDown)
    {
        (KeybindingAction, string)? validationResult = ValidateAndResolve(Settings.PressFunction);
        if (validationResult == null || string.IsNullOrWhiteSpace(Settings.PressFunction))
        {
            return;
        }

        (KeybindingAction action, string executableBinding) = validationResult.Value;

        KeybindingExecutionContext context = new()
        {
            ActionName = Settings.PressFunction,
            Binding = executableBinding,
            ActivationMode = action.ActivationMode,
            IsKeyDown = isKeyDown
        };

        await ExecuteKeybindingAsync(context).ConfigureAwait(false);
    }

    internal static string? ResolveRotationFunction(Settings.DialSettings settings, int ticks)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return ticks switch
        {
            > 0 => settings.RotateRightFunction,
            < 0 => settings.RotateLeftFunction,
            _ => null
        };
    }

    [ExcludeFromCodeCoverage]
    private (KeybindingAction, string)? ValidateAndResolve(string? actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId) || !CanExecuteBindings)
        {
            return null;
        }

        if (!KeybindingService.TryGetAction(actionId, out KeybindingAction? action) || action == null)
        {
            return null;
        }

        string? executableBinding = ResolveExecutableBinding(action);
        return executableBinding == null ? null : (action, executableBinding);
    }

    internal static string? ResolveExecutableBinding(KeybindingAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

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

    private async Task ExecuteKeybindingAsync(KeybindingExecutionContext context)
    {
        try
        {
            bool success = await KeybindingService.ExecuteAsync(context).ConfigureAwait(false);
            if (success)
            {
                LogPressExec(context);
            }
        }
        catch (Exception ex)
        {
            Log.Err($"{GetType().Name}: '{context.ActionName}': {ex.Message}", ex);
        }
    }

    [Conditional("DEBUG")]
    private void LogPressExec(KeybindingExecutionContext context) =>
        Log.Debug(
            $"{GetType().Name}: {(context.IsKeyDown ? "pressed" : "released")} '{context.ActionName}' ({context.ActivationMode}) → '{context.Binding}'");

    [Conditional("DEBUG")]
    private static void LogRotateExec(string actionName, int ticks, string binding) =>
        Log.Debug($"{nameof(AdaptiveDial)}: rotated '{actionName}' ticks={ticks} → '{binding}'");
}
