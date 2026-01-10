using BarRaider.SdTools;
using SCStreamDeck.SCCore.Buttons.Base;
using SCStreamDeck.SCCore.Buttons.Settings;
using SCStreamDeck.SCCore.Common;
using SCStreamDeck.SCCore.Models;
using SCStreamDeck.SCCore.Services.Core;
using SCStreamDeck.SCCore.Services.Keybinding;

// ReSharper disable once UnusedType.Global

namespace SCStreamDeck.SCCore.Buttons;

/// <summary>
///     Adaptive Star Citizen button.
///     Automatically adjusts behavior based on action activation modes.
/// </summary>
[PluginActionId("com.jarex985.scstreamdeck.adaptivebutton")]
public sealed class AdaptiveButton : SCButtonBase
{
    private FunctionSettings _settings;

    public AdaptiveButton(SDConnection connection, InitialPayload payload) : base(connection, payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        // Load settings from payload
        if (payload.Settings == null || payload.Settings.Count == 0)
        {
            _settings = new FunctionSettings();
        }
        else
        {
            _settings = payload.Settings.ToObject<FunctionSettings>() ?? new FunctionSettings();
        }
        
    }

    protected override void ExecuteButtonAction(bool isKeyDown)
    {
        if (string.IsNullOrWhiteSpace(_settings.Function))
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, "AdaptiveButton: No function configured");
            return;
        }

        if (!IsReady)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN,
                "AdaptiveButton: Not ready - initialization in progress");
            return;
        }

        if (!KeybindingService.TryGetAction(_settings.Function, out var action) || action == null)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"AdaptiveButton: Function '{_settings.Function}' not found");
            return;
        }

        string? executableBinding = null;
        var bindingType = InputType.Unknown;

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
                $"AdaptiveButton: Function '{_settings.Function}' not bound to executable input");
            return;
        }

        var context = new KeybindingExecutionContext
        {
            ActionName = _settings.Function,
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
        bool isKeyDown)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var success = await KeybindingService.ExecuteAsync(context).ConfigureAwait(false);
                // TODO: Remove this log or make it debug level later with if Debug
                if (success)
                {
                    var actionText = isKeyDown ? "pressed" : "released";
                    Logger.Instance.LogMessage(TracingLevel.DEBUG,
                        $"AdaptiveButton: {actionText} '{context.ActionName}' ({activationMode}) → '{executableBinding}' ({bindingType})");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"AdaptiveButton: Exception executing '{executableBinding}': {ex.Message}");
            }
        });
    }

    public override void ReceivedSettings(ReceivedSettingsPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.Settings != null && payload.Settings.Count > 0)
            _settings = payload.Settings.ToObject<FunctionSettings>() ?? _settings;
    }
    
}
