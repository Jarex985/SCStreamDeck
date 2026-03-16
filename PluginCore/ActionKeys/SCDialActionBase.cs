using System.Diagnostics.CodeAnalysis;
using BarRaider.SdTools;
using BarRaider.SdTools.Events;
using BarRaider.SdTools.Payloads;
using BarRaider.SdTools.Wrappers;
using Newtonsoft.Json.Linq;
using SCStreamDeck.ActionKeys.Settings;
using SCStreamDeck.Common;
using SCStreamDeck.Infrastructure;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Audio;
using SCStreamDeck.Services.Core;
using SCStreamDeck.Services.Keybinding;

namespace SCStreamDeck.ActionKeys;

/// <summary>
///     Base class for Star Citizen Stream Deck Plus dial actions.
/// </summary>
public abstract class SCDialActionBase : EncoderBase
{
    [ExcludeFromCodeCoverage]
    protected SCDialActionBase(SDConnection connection, InitialPayload payload) : base(connection, payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        Settings = payload.Settings is { Count: > 0 }
            ? payload.Settings.ToObject<DialSettings>() ?? new DialSettings()
            : new DialSettings();

        SCActionBaseDependencies deps = ActionDependencies.ForSCActionBase();
        InitializationService = deps.InitializationService;
        KeybindingService = deps.KeybindingService;
        AudioPlayerService = deps.AudioPlayerService;

        InitializationService.KeybindingsStateChanged += OnKeybindingsStateChanged;
        Connection.OnPropertyInspectorDidAppear += OnPropertyInspectorDidAppear;
        Connection.OnSendToPlugin += OnSendToPlugin;

        TryMigrateFunctionSettingsIfPossible();

        if (CanExecuteBindings)
        {
            SendPropertyInspectorUpdate();
        }
    }

    private InitializationService InitializationService { get; }
    private AudioPlayerService AudioPlayerService { get; }
    protected KeybindingService KeybindingService { get; }

    protected bool CanExecuteBindings =>
        InitializationService.IsInitialized &&
        KeybindingService.IsLoaded &&
        InitializationService.KeybindingsJsonExists();

    protected DialSettings Settings { get; private set; }

    protected void PlayClickSoundIfConfigured()
    {
        if (string.IsNullOrWhiteSpace(Settings.ClickSoundPath))
        {
            return;
        }

        if (!File.Exists(Settings.ClickSoundPath))
        {
            Settings.ClickSoundPath = null;
            return;
        }

        try
        {
            AudioPlayerService.Play(Settings.ClickSoundPath);
        }
        catch (Exception ex)
        {
            Log.Err($"[{GetType().Name}] Audio playback error: {ex.Message}", ex);
        }
    }

    public override void TouchPress(TouchpadPressPayload payload)
    {
    }

    public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
    {
    }

    public override void OnTick()
    {
    }

    public override void Dispose()
    {
        Connection.OnPropertyInspectorDidAppear -= OnPropertyInspectorDidAppear;
        Connection.OnSendToPlugin -= OnSendToPlugin;
        InitializationService.KeybindingsStateChanged -= OnKeybindingsStateChanged;
        GC.SuppressFinalize(this);
    }

    [ExcludeFromCodeCoverage]
    public override void ReceivedSettings(ReceivedSettingsPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.Settings is { Count: > 0 })
        {
            Settings = payload.Settings.ToObject<DialSettings>() ?? Settings;
        }

        TryMigrateFunctionSettingsIfPossible();
    }

    private void SendPropertyInspectorUpdate()
    {
        try
        {
            if (!InitializationService.KeybindingsJsonExists() || !KeybindingService.IsLoaded)
            {
                Connection.SendToPropertyInspectorAsync(new JObject
                {
                    ["functionsLoaded"] = false,
                    ["functions"] = new JArray()
                });
                return;
            }

            IReadOnlyList<KeybindingAction> allActions = KeybindingService.GetAllActions();
            IntPtr hkl = KeyboardLayoutDetector.DetectCurrent().Hkl;
            JArray groups = FunctionsPayloadBuilder.BuildGroupedFunctionsPayload(allActions, hkl);

            Connection.SendToPropertyInspectorAsync(new JObject { ["functionsLoaded"] = true, ["functions"] = groups });
        }
        catch (Exception ex)
        {
            Log.Err($"[{GetType().Name}]: {ex.Message}", ex);
            Connection.SendToPropertyInspectorAsync(new JObject { ["functionsLoaded"] = false, ["functions"] = new JArray() });
        }
    }

    private void OnPropertyInspectorDidAppear(object? sender, SDEventReceivedEventArgs<PropertyInspectorDidAppear> e) =>
        SendPropertyInspectorUpdate();

    private void OnKeybindingsStateChanged()
    {
        TryMigrateFunctionSettingsIfPossible();
        SendPropertyInspectorUpdate();
    }

    private void TryMigrateFunctionSettingsIfPossible()
    {
        if (!CanExecuteBindings)
        {
            return;
        }

        bool changed = false;
        changed |= TryNormalizeFunctionSetting(Settings.RotateLeftFunction, value => Settings.RotateLeftFunction = value);
        changed |= TryNormalizeFunctionSetting(Settings.RotateRightFunction, value => Settings.RotateRightFunction = value);
        changed |= TryNormalizeFunctionSetting(Settings.PressFunction, value => Settings.PressFunction = value);

        if (!changed)
        {
            return;
        }

        try
        {
            _ = Connection.SetSettingsAsync(JObject.FromObject(Settings));
        }
        catch (Exception ex)
        {
            Log.Err($"[{GetType().Name}] Failed to persist migrated dial function ids: {ex.Message}", ex);
        }
    }

    private bool TryNormalizeFunctionSetting(string? currentValue, Action<string?> setValue)
    {
        if (string.IsNullOrWhiteSpace(currentValue))
        {
            return false;
        }

        if (!KeybindingService.TryNormalizeActionId(currentValue, out string normalizedId) ||
            string.IsNullOrWhiteSpace(normalizedId) ||
            string.Equals(currentValue, normalizedId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        setValue(normalizedId);
        return true;
    }

    [ExcludeFromCodeCoverage]
    private void OnSendToPlugin(object? sender, SDEventReceivedEventArgs<SendToPlugin> e)
    {
        try
        {
            if (e.Event?.Payload == null)
            {
                return;
            }

            string? piEvent = null;
            if (e.Event.Payload.TryGetValue("event", out JToken? eventToken))
            {
                piEvent = eventToken.ToString();
            }
            else if (e.Event.Payload.TryGetValue("property_inspector", out JToken? legacyToken))
            {
                piEvent = legacyToken.ToString();
            }

            if (piEvent == "propertyInspectorConnected")
            {
                SendPropertyInspectorUpdate();
            }
        }
        catch (Exception ex)
        {
            Log.Err($"[{GetType().Name}] PI message handling error: {ex.Message}", ex);
        }
    }
}
