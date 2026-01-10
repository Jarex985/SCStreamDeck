using BarRaider.SdTools;
using BarRaider.SdTools.Events;
using BarRaider.SdTools.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using SCStreamDeck.SCCore.Common;
using SCStreamDeck.SCCore.Models;
using SCStreamDeck.SCCore.Services.Core;
using SCStreamDeck.SCCore.Services.Keybinding;

namespace SCStreamDeck.SCCore.Buttons.Base;

/// <summary>
///     Base class for Star Citizen Stream Deck Keys.
/// </summary>
public abstract class SCButtonBase : KeypadBase
{
    /// <summary>
    ///     Static service provider for dependency injection in StreamDeck buttons.
    ///     Set in Program.cs after service initialization.
    /// </summary>
    private static IServiceProvider? s_serviceProvider;

    /// <summary>
    ///     Constructor for SCButtonBase.
    ///     Services are injected via static provider to work around SDK limitations.
    /// </summary>
    protected SCButtonBase(SDConnection connection, InitialPayload payload) : base(connection, payload)
    {
        if (s_serviceProvider == null)
        {
            throw new InvalidOperationException("SCButtonBase services not initialized. Call InitializeServices first.");
        }

        KeybindingService = s_serviceProvider.GetRequiredService<IKeybindingService>();
        InitializationService = s_serviceProvider.GetRequiredService<IInitializationService>();
        Connection.OnPropertyInspectorDidAppear += OnPropertyInspectorDidAppear;
        Connection.OnSendToPlugin += OnSendToPlugin;

        if (IsReady)
        {
            SendPropertyInspectorUpdate();
        }
    }

    private IInitializationService InitializationService { get; }
    protected IKeybindingService KeybindingService { get; }

    /// <summary>
    ///     Indicates whether the button is ready for execution (services initialized).
    /// </summary>
    protected bool IsReady => InitializationService.IsInitialized && KeybindingService.IsLoaded;

    /// <summary>
    ///     Initializes the service provider for button dependency injection.
    /// </summary>
    public static void InitializeServices(IServiceProvider serviceProvider) =>
        s_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <summary>
    ///     Abstract method that derived classes must implement to define button behavior.
    ///     Called when the button is pressed or released.
    /// </summary>
    /// <param name="isKeyDown">True if button was pressed, false if released</param>
    protected abstract void ExecuteButtonAction(bool isKeyDown);

    /// <summary>
    ///     Sends the current keybinding status and available actions to the Property Inspector.
    /// </summary>
    private void SendPropertyInspectorUpdate()
    {
        try
        {
            if (!KeybindingService.IsLoaded)
            {
                Connection.SendToPropertyInspectorAsync(new JObject
                {
                    ["functionsLoaded"] = false, ["functions"] = new JArray(), ["status"] = "Keybindings not loaded yet"
                });
                return;
            }

            IReadOnlyList<KeybindingAction> allActions = KeybindingService.GetAllActions();
            IntPtr hkl = KeyboardLayoutDetector.DetectCurrent().Hkl;

            // Build grouped functions payload using FunctionsPayloadBuilder
            JArray groups = FunctionsPayloadBuilder.BuildGroupedFunctionsPayload(allActions, hkl);

            Connection.SendToPropertyInspectorAsync(new JObject
            {
                ["functionsLoaded"] = true,
                ["functions"] = groups,
                ["status"] = $"Loaded {allActions.Count} keybindings from SCCore"
            });
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"{GetType().Name}: SendPropertyInspectorUpdate failed: {ex.Message}");
            Connection.SendToPropertyInspectorAsync(new JObject
            {
                ["functionsLoaded"] = false, ["functions"] = new JArray(), ["status"] = $"Error: {ex.Message}"
            });
        }
    }

    /// <summary>
    ///     Called when the Property Inspector appears.
    /// </summary>
    private void OnPropertyInspectorDidAppear(object? sender,
        SDEventReceivedEventArgs<PropertyInspectorDidAppear> e) =>
        SendPropertyInspectorUpdate();

    /// <summary>
    ///     Called when the Property Inspector sends a message to the plugin.
    ///     Virtual so derived classes can override for custom message handling.
    /// </summary>
    private void OnSendToPlugin(object? sender, SDEventReceivedEventArgs<SendToPlugin> e)
    {
        ArgumentNullException.ThrowIfNull(e);

        try
        {
            if (e.Event?.Payload == null || !e.Event.Payload.TryGetValue("property_inspector", out JToken? value))
            {
                return;
            }

            if (value.ToString() == "propertyInspectorConnected")
            {
                SendPropertyInspectorUpdate();
            }
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
    public sealed override void KeyPressed(KeyPayload payload) => ExecuteButtonAction(true);

    /// <summary>
    ///     Sealed override of KeyReleased - calls ExecuteButtonAction.
    ///     Derived classes should implement ExecuteButtonAction, not override this.
    /// </summary>
    public sealed override void KeyReleased(KeyPayload payload) => ExecuteButtonAction(false);

    /// <summary>
    ///     Disposes resources and unsubscribes from events.
    /// </summary>
    public override void Dispose()
    {
        Connection.OnPropertyInspectorDidAppear -= OnPropertyInspectorDidAppear;
        Connection.OnSendToPlugin -= OnSendToPlugin;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Called when global settings are received.
    /// </summary>
    public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
    {
        // Override in derived classes if needed
    }

    public override void OnTick()
    {
        // Override in derived classes if needed
    }
}
