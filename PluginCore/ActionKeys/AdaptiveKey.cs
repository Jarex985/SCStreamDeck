using BarRaider.SdTools;
using SCStreamDeck.Common;
using SCStreamDeck.Models;

// ReSharper disable once UnusedType.Global

namespace SCStreamDeck.ActionKeys;

/// <summary>
///     Adaptive Star Citizen Key.
///     Automatically adjusts behavior based on action activation modes.
/// </summary>
[PluginActionId("com.jarex985.scstreamdeck.adaptivekey")]
public sealed class AdaptiveKey(SDConnection connection, InitialPayload payload) : SCActionBase(connection, payload)
{
    #region Public Methods

    public override async void KeyPressed(KeyPayload payload)
    {
        try
        {
            await ProcessKeyEventAsync(true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"{GetType().Name}: {ex.Message}");
        }
    }

    public override async void KeyReleased(KeyPayload payload)
    {
        try
        {
            await ProcessKeyEventAsync(false).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"{GetType().Name}: {ex.Message}");
        }
    }

    #endregion

    #region Private Methods

    private async Task ProcessKeyEventAsync(bool isKeyDown)
    {
        (KeybindingAction, string)? validationResult = ValidateAndResolve();
        if (validationResult == null)
        {
            return;
        }

        (KeybindingAction action, string executableBinding) = validationResult.Value;

        KeybindingExecutionContext context = new()
        {
            ActionName = Settings.Function!,
            Binding = executableBinding,
            ActivationMode = action.ActivationMode,
            IsKeyDown = isKeyDown
        };

        await ExecuteKeybindingAsync(context).ConfigureAwait(false);
    }

    private (KeybindingAction, string)? ValidateAndResolve()
    {
        if (string.IsNullOrWhiteSpace(Settings.Function) || !IsReady)
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

    private async Task ExecuteKeybindingAsync(KeybindingExecutionContext context)
    {
        try
        {
            bool success = await KeybindingService.ExecuteAsync(context).ConfigureAwait(false);
            if (success)
            {
#if DEBUG
                LogSuccessfulExecution(context);
#endif
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"{GetType().Name}: '{context.ActionName}': {ex.Message}");
        }
    }

    private void LogSuccessfulExecution(KeybindingExecutionContext context)
    {
        string actionText = context.IsKeyDown ? "pressed" : "released";
        Logger.Instance.LogMessage(TracingLevel.DEBUG,
            $"{GetType().Name}: {actionText} '{context.ActionName}' ({context.ActivationMode}) → '{context.Binding}'");
    }

    #endregion
}
