using BarRaider.SdTools;
using SCStreamDeck.SCCore.Buttons.Base;
using SCStreamDeck.SCCore.Common;
using SCStreamDeck.SCCore.Models;

// ReSharper disable once UnusedType.Global

namespace SCStreamDeck.SCCore.Buttons;

/// <summary>
///     Adaptive Star Citizen button.
///     Automatically adjusts behavior based on action activation modes.
/// </summary>
[PluginActionId("com.jarex985.scstreamdeck.adaptivekey")]
public sealed class AdaptiveKey(SDConnection connection, InitialPayload payload) : SCActionBase(connection, payload)
{
    protected override void ExecuteButtonAction(bool isKeyDown)
    {
        if (string.IsNullOrWhiteSpace(Settings.Function))
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, $"{GetType().Name}: No function configured");
            return;
        }

        if (!IsReady)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"{GetType().Name}: Not ready - initialization in progress");
            return;
        }

        if (!KeybindingService.TryGetAction(Settings.Function, out KeybindingAction? action) || action == null)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"{GetType().Name}: Function '{Settings.Function}' not found");
            return;
        }

        string? executableBinding = null;
        InputType bindingType = InputType.Unknown;

        if (!string.IsNullOrWhiteSpace(action.KeyboardBinding))
        {
            executableBinding = action.KeyboardBinding;
            bindingType = InputType.Keyboard;
        }
        else if (!string.IsNullOrWhiteSpace(action.MouseBinding))
        {
            bindingType = action.MouseBinding.GetInputType();
            if (bindingType == InputType.MouseButton || bindingType == InputType.MouseWheel)
            {
                executableBinding = action.MouseBinding;
            }
        }

        if (string.IsNullOrWhiteSpace(executableBinding))
        {
            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"{GetType().Name}: Function '{Settings.Function}' not bound to executable input");
            return;
        }

        KeybindingExecutionContext context = new()
        {
            ActionName = Settings.Function,
            Binding = executableBinding,
            ActivationMode = action.ActivationMode,
            IsKeyDown = isKeyDown
        };

        ExecuteKeybindingAsyncSafe(context, executableBinding, bindingType, action.ActivationMode, isKeyDown);
    }

    /// <summary>
    ///     Executes keybinding asynchronously.
    /// </summary>
    private void ExecuteKeybindingAsyncSafe(
        KeybindingExecutionContext context,
        string executableBinding,
        InputType bindingType,
        ActivationMode activationMode,
        bool isKeyDown) =>
        _ = Task.Run(async () =>
        {
            try
            {
                bool success = await KeybindingService.ExecuteAsync(context).ConfigureAwait(false);
                // TODO: Remove this log or make it debug level later with if Debug
                if (success)
                {
                    string actionText = isKeyDown ? "pressed" : "released";
                    Logger.Instance.LogMessage(TracingLevel.DEBUG,
                        $"{GetType().Name}: {actionText} '{context.ActionName}' ({activationMode}) → '{executableBinding}' ({bindingType})");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"{GetType().Name}: Exception executing '{executableBinding}': {ex.Message}");
            }
        });
}
