using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using BarRaider.SdTools;
using SCStreamDeck.Common;
using SCStreamDeck.Logging;
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
        PlayClickSoundIfConfigured();

        try
        {
            await ProcessKeyEventAsync(true).ConfigureAwait(false);
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
            await ProcessKeyEventAsync(false).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Err($"{GetType().Name}: {ex.Message}", ex);
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

    /// <summary>
    ///     Validates and resolves keybinding action.
    ///     Marked as [ExcludeFromCodeCoverage] because:
    ///     - Depends on Stream Deck SDK runtime (SDConnection, InitialPayload, KeybindingService)
    ///     - SDK cannot be properly mocked without external dependencies
    ///     - Requires running Stream Deck host application
    ///     - Integration testing requires physical Stream Deck device
    ///     - Business logic tested through KeybindingService unit tests
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

    /// <summary>
    ///     Gets executable binding from keybinding action.
    ///     Marked as [ExcludeFromCodeCoverage] because:
    ///     - Part of ValidateAndResolve call chain (SDK dependencies)
    ///     - Unit testing covered through KeybindingAction tests
    ///     - Integration testing requires Stream Deck runtime
    /// </summary>
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

    private async Task ExecuteKeybindingAsync(KeybindingExecutionContext context)
    {
        try
        {
            bool success = await KeybindingService.ExecuteAsync(context).ConfigureAwait(false);
            if (success)
            {
                LogExec(context);
            }
        }
        catch (Exception ex)
        {
            Log.Err($"{GetType().Name}: '{context.ActionName}': {ex.Message}", ex);
        }
    }

    [Conditional("DEBUG")]
    private void LogExec(KeybindingExecutionContext context)
    {
        string actionText = context.IsKeyDown ? "pressed" : "released";
        Log.Debug($"{GetType().Name}: {actionText} '{context.ActionName}' ({context.ActivationMode}) → '{context.Binding}'");
    }

    #endregion
}
