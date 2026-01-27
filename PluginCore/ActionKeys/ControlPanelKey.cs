using System.Diagnostics.CodeAnalysis;
using BarRaider.SdTools;
using BarRaider.SdTools.Events;
using BarRaider.SdTools.Payloads;
using BarRaider.SdTools.Wrappers;
using Newtonsoft.Json.Linq;
using SCStreamDeck.Infrastructure;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Core;
using SCStreamDeck.Services.UI;

namespace SCStreamDeck.ActionKeys;

/// <summary>
///     Control Panel action.
///     Intended as a "settings" UI entrypoint (no-op on press).
/// </summary>
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Stream Deck action instantiated via SDK reflection")]
[PluginActionId("com.jarex985.scstreamdeck.controlpanel")]
public sealed class ControlPanelKey : KeyAndEncoderBase
{
    private const string PiEventConnected = "propertyInspectorConnected";
    private const string PiEventSetTheme = "setTheme";
    private const string PiEventSetChannel = "setChannel";
    private const string PiEventForceRedetection = "forceRedetection";
    private const string PiEventFactoryReset = "factoryReset";
    private const string PiEventSetDataP4KOverride = "setDataP4KOverride";

    [ExcludeFromCodeCoverage]
    public ControlPanelKey(SDConnection connection, InitialPayload payload) : base(connection, payload)
    {
        ControlPanelKeyDependencies deps = ActionDependencies.ForControlPanelKey();
        InitializationService = deps.InitializationService;
        StateService = deps.StateService;
        ThemeService = deps.ThemeService;
        KeybindingsJsonCache = deps.KeybindingsJsonCache;

        Connection.OnPropertyInspectorDidAppear += OnPropertyInspectorDidAppear;
        Connection.OnSendToPlugin += OnSendToPlugin;

        SendPropertyInspectorUpdate();
    }

    private InitializationService InitializationService { get; }
    private StateService StateService { get; }
    private ThemeService ThemeService { get; }
    private IKeybindingsJsonCache KeybindingsJsonCache { get; }

    public override void KeyPressed(KeyPayload payload)
    {
    }

    public override void KeyReleased(KeyPayload payload)
    {
    }

    public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
    {
    }

    public override void ReceivedSettings(ReceivedSettingsPayload payload)
    {
    }

    public override void OnTick()
    {
    }

    public override void DialRotate(DialRotatePayload payload)
    {
    }

    public override void DialDown(DialPayload payload)
    {
    }

    public override void DialUp(DialPayload payload)
    {
    }

    public override void TouchPress(TouchpadPressPayload payload)
    {
    }

    public override void Dispose()
    {
        Connection.OnPropertyInspectorDidAppear -= OnPropertyInspectorDidAppear;
        Connection.OnSendToPlugin -= OnSendToPlugin;
    }

    private void OnPropertyInspectorDidAppear(object? sender,
        SDEventReceivedEventArgs<PropertyInspectorDidAppear> e) =>
        SendPropertyInspectorUpdate();

    [ExcludeFromCodeCoverage]
    private void OnSendToPlugin(object? sender, SDEventReceivedEventArgs<SendToPlugin> e)
    {
        ArgumentNullException.ThrowIfNull(e);

        try
        {
            if (e.Event?.Payload == null)
            {
                return;
            }

            string? piEvent = GetPiEventName(e.Event.Payload);

            if (string.IsNullOrWhiteSpace(piEvent))
            {
                return;
            }

            switch (piEvent)
            {
                case PiEventConnected:
                    SendPropertyInspectorUpdate();
                    return;
                case PiEventSetTheme:
                    HandleSetTheme(e.Event.Payload);
                    return;
                case PiEventSetChannel:
                    HandleSetChannel(e.Event.Payload);
                    return;
                case PiEventFactoryReset:
                    HandleFactoryReset();
                    return;
                case PiEventForceRedetection:
                    HandleForceRedetection();
                    return;
                case PiEventSetDataP4KOverride:
                    HandleSetDataP4KOverride(e.Event.Payload);
                    return;
                default:
                    return;
            }
        }
        catch (Exception ex)
        {
            Log.Err($"[{nameof(ControlPanelKey)}]: {ex.Message}", ex);
        }
    }

    private static string? GetPiEventName(JObject payload) =>
        payload.Value<string>("event") ?? payload.Value<string>("property_inspector");

    private void HandleSetTheme(JObject payload)
    {
        string? themeFile = payload.Value<string>("themeFile");
        if (!ThemeService.IsValidThemeFile(themeFile))
        {
            return;
        }

        RunBackground(async () => await StateService.UpdateSelectedThemeAsync(themeFile).ConfigureAwait(false));
    }

    private void HandleSetChannel(JObject payload)
    {
        string? channelStr = payload.Value<string>("channel");
        if (!Enum.TryParse(channelStr, true, out SCChannel channel))
        {
            SendPropertyInspectorUpdate();
            return;
        }

        RunBackground(async () =>
        {
            await StateService.UpdateSelectedChannelAsync(channel).ConfigureAwait(false);

            bool switched = await InitializationService.SwitchChannelAsync(channel).ConfigureAwait(false);
            if (!switched)
            {
                _ = await InitializationService.ForceRedetectionAsync().ConfigureAwait(false);
            }
        });
    }

    private void HandleFactoryReset() =>
        RunBackground(async () => await InitializationService.FactoryResetAsync().ConfigureAwait(false));

    private void HandleForceRedetection() =>
        RunBackground(async () => _ = await InitializationService.ForceRedetectionAsync().ConfigureAwait(false));

    private void HandleSetDataP4KOverride(JObject payload)
    {
        string? channelStr = payload.Value<string>("channel");
        string? dataP4KPath = payload.Value<string>("dataP4KPath");
        if (!Enum.TryParse(channelStr, true, out SCChannel channel))
        {
            SendPropertyInspectorUpdate();
            return;
        }

        RunBackground(async () =>
        {
            await InitializationService.ApplyCustomDataP4KOverrideAsync(channel, dataP4KPath)
                .ConfigureAwait(false);
        });
    }

    private void RunBackground(Func<Task> work) =>
        _ = Task.Run(async () =>
        {
            try
            {
                await work().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Err($"[{nameof(ControlPanelKey)}] Background operation failed: {ex.Message}", ex);
            }
            finally
            {
                await SendPropertyInspectorUpdateAsync().ConfigureAwait(false);
            }
        });

    private void SendPropertyInspectorUpdate() => _ = SendPropertyInspectorUpdateAsync();

    private async Task SendPropertyInspectorUpdateAsync()
    {
        try
        {
            JObject controlPanel = await BuildControlPanelPayloadAsync().ConfigureAwait(false);

            JArray themePayload = [];
            string selectedTheme = string.Empty;

            try
            {
                IReadOnlyList<ThemeInfo> themes = ThemeService.GetAvailableThemes();
                string? selectedThemeFile = await StateService.GetSelectedThemeAsync().ConfigureAwait(false);
                (themePayload, selectedTheme) = ThemePayloadBuilder.Build(
                    themes,
                    selectedThemeFile,
                    ThemeService.IsValidThemeFile);
            }
            catch (Exception ex)
            {
                Log.Err($"[{nameof(ControlPanelKey)}]: {ex.Message}", ex);
            }

            await Connection.SendToPropertyInspectorAsync(new JObject
            {
                ["themesLoaded"] = true,
                ["themes"] = themePayload,
                ["selectedTheme"] = selectedTheme,
                ["controlPanelLoaded"] = true,
                ["controlPanel"] = controlPanel
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Err($"[{nameof(ControlPanelKey)}] Property inspector update failed: {ex.Message}", ex);
        }
    }

    private async Task<JObject> BuildControlPanelPayloadAsync()
    {
        try
        {
            PluginState? state = await StateService.LoadStateAsync().ConfigureAwait(false);

            return ControlPanelPayloadBuilder.Build(
                state,
                InitializationService.IsInitialized,
                InitializationService.CurrentChannel,
                ch => KeybindingsJsonCache.Exists(ch),
                i => i.Validate());
        }
        catch (Exception ex)
        {
            Log.Err($"[{nameof(ControlPanelKey)}] Failed to build control panel payload: {ex.Message}", ex);
            return ControlPanelPayloadBuilder.BuildFailurePayload();
        }
    }
}
