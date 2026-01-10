using BarRaider.SdTools;
using BarRaider.SdTools.Events;
using BarRaider.SdTools.Wrappers;
using Newtonsoft.Json.Linq;
using SCStreamDeck.SCCore.Common;
using SCStreamDeck.SCCore.Infrastructure;
using SCStreamDeck.SCCore.Services.Core;
using SCStreamDeck.SCCore.Services.Keybinding;

namespace SCStreamDeck.SCCore.Buttons.Base;

/// <summary>
///     Modern base class for SCCore-based Stream Deck buttons.
///     Provides shared functionality using SCCore services instead of legacy code.
/// </summary>
public abstract class SCButtonBase : KeypadBase
{
    /// <summary>
    ///     Constructor for SCButtonBase using SCCore dependency injection.
    /// </summary>
    protected SCButtonBase(SDConnection connection, InitialPayload payload) : base(connection, payload)
    {
        // Get services from ServiceLocator
        KeybindingService = ServiceLocator.GetService<IKeybindingService>();
        InitializationService = ServiceLocator.GetService<IInitializationService>();

        InitializationService.InitializationCompleted += OnInitializationCompleted;
        Connection.OnPropertyInspectorDidAppear += OnPropertyInspectorDidAppear;
        Connection.OnSendToPlugin += OnSendToPlugin;
    }

    /// <summary>
    ///     Gets the initialization service for managing plugin startup and state.
    /// </summary>
    protected IInitializationService InitializationService { get; }

    /// <summary>
    ///     Gets the keybinding service for accessing Star Citizen keybindings.
    /// </summary>
    protected IKeybindingService KeybindingService { get; }

    /// <summary>
    ///     Indicates whether the button is ready for execution (services initialized).
    /// </summary>
    protected bool IsReady => InitializationService.IsInitialized && KeybindingService.IsLoaded;

    /// <summary>
    ///     Abstract method that derived classes must implement to define button behavior.
    ///     Called when the button is pressed or released.
    /// </summary>
    /// <param name="isKeyDown">True if button was pressed, false if released</param>
    protected abstract void ExecuteButtonAction(bool isKeyDown);

    /// <summary>
    ///     Sends the current keybinding status and available actions to the Property Inspector.
    ///     Can be overridden by derived classes for custom Property Inspector updates.
    /// </summary>
    protected virtual void SendPropertyInspectorUpdate()
    {
        try
        {
            if (!KeybindingService.IsLoaded)
            {
                Connection.SendToPropertyInspectorAsync(new JObject
                {
                    ["functionsLoaded"] = false,
                    ["functions"] = new JArray(),
                    ["status"] = "Keybindings not loaded yet"
                });
                return;
            }

            var allActions = KeybindingService.GetAllActions();
            var hkl = KeyboardLayoutDetector.DetectCurrent().Hkl;

            // Build grouped functions payload using FunctionsPayloadBuilder
            var groups = FunctionsPayloadBuilder.BuildGroupedFunctionsPayload(allActions, hkl);

            Connection.SendToPropertyInspectorAsync(new JObject
            {
                ["functionsLoaded"] = true,
                ["functions"] = groups,
                ["status"] = $"Loaded {allActions.Count} keybindings from SCCore"
            });
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"{GetType().Name}: SendPropertyInspectorUpdate failed: {ex.Message}");

            Connection.SendToPropertyInspectorAsync(new JObject
            {
                ["functionsLoaded"] = false,
                ["functions"] = new JArray(),
                ["status"] = $"Error: {ex.Message}"
            });
        }
    }

    /// <summary>
    ///     Called when SCCore initialization completes (success or failure).
    ///     Virtual so derived classes can override for custom initialization handling.
    /// </summary>
    protected virtual void OnInitializationCompleted(object? sender, InitializationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.IsSuccess)
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"{GetType().Name}: Initialization failed - {result.ErrorMessage}");

        SendPropertyInspectorUpdate();
    }

    /// <summary>
    ///     Called when the Property Inspector appears.
    ///     Virtual so derived classes can override for custom behavior.
    /// </summary>
    protected virtual void OnPropertyInspectorDidAppear(object? sender,
        SDEventReceivedEventArgs<PropertyInspectorDidAppear> e)
    {
        SendPropertyInspectorUpdate();
    }

    /// <summary>
    ///     Called when the Property Inspector sends a message to the plugin.
    ///     Virtual so derived classes can override for custom message handling.
    /// </summary>
    protected virtual void OnSendToPlugin(object? sender, SDEventReceivedEventArgs<SendToPlugin> e)
    {
        ArgumentNullException.ThrowIfNull(e);

        try
        {
            if (e.Event?.Payload == null || !e.Event.Payload.TryGetValue("property_inspector", out var value))
                return;

            if (value.ToString() == "propertyInspectorConnected") SendPropertyInspectorUpdate();
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"{GetType().Name}: OnSendToPlugin error: {ex.Message}");
        }
    }

    /// <summary>
    ///     Sealed override of KeyPressed - calls ExecuteButtonAction.
    ///     Derived classes should implement ExecuteButtonAction, not override this.
    /// </summary>
    public sealed override void KeyPressed(KeyPayload payload)
    {
        ExecuteButtonAction(true);
    }

    /// <summary>
    ///     Sealed override of KeyReleased - calls ExecuteButtonAction.
    ///     Derived classes should implement ExecuteButtonAction, not override this.
    /// </summary>
    public sealed override void KeyReleased(KeyPayload payload)
    {
        ExecuteButtonAction(false);
    }

    /// <summary>
    ///     Called periodically by the Stream Deck SDK.
    ///     Virtual so derived classes can override if periodic updates are needed.
    /// </summary>
    public override void OnTick()
    {
        // Most buttons don't need periodic updates
        // Override in derived classes if needed
    }

    /// <summary>
    ///     Called when global settings are received.
    ///     Virtual so derived classes can override if global settings are needed.
    /// </summary>
    public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
    {
        // Override in derived classes if needed
    }

    /// <summary>
    ///     Disposes resources and unsubscribes from events.
    ///     Virtual so derived classes can extend cleanup.
    /// </summary>
    public override void Dispose()
    {
        InitializationService.InitializationCompleted -= OnInitializationCompleted;
        Connection.OnPropertyInspectorDidAppear -= OnPropertyInspectorDidAppear;
        Connection.OnSendToPlugin -= OnSendToPlugin;
        GC.SuppressFinalize(this);
    }
}
