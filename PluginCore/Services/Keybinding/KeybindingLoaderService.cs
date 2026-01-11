﻿using BarRaider.SdTools;
using Newtonsoft.Json;
using SCStreamDeck.Common;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Keybinding;

/// <summary>
///     Service for loading and caching keybinding actions and activation modes.
/// </summary>
public sealed class KeybindingLoaderService : IKeybindingLoaderService
{
    private readonly Dictionary<string, KeybindingAction> _actions = new(StringComparer.OrdinalIgnoreCase);
    private readonly nint _currentKeyboardLayout = NativeMethods.GetKeyboardLayout(0);
    private readonly object _lock = new();
    private Dictionary<string, ActivationModeMetadata> _activationModes = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _isLoaded;

    public bool IsLoaded => _isLoaded;

    public async Task<bool> LoadKeybindingsAsync(string jsonPath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!SecurePathValidator.TryNormalizePath(jsonPath, out string validatedPath))
            {
                _isLoaded = false;
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[{nameof(KeybindingLoaderService)}]: {ErrorMessages.InvalidPath}");
                return false;
            }

            if (!File.Exists(validatedPath))
            {
                _isLoaded = false;
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[{nameof(KeybindingLoaderService)}]: {ErrorMessages.FileReadFailed}");
                return false;
            }

            string json = await File.ReadAllTextAsync(validatedPath, cancellationToken).ConfigureAwait(false);
            KeybindingDataFile? dataFile = JsonConvert.DeserializeObject<KeybindingDataFile>(json);

            if (dataFile?.Actions == null)
            {
                _isLoaded = false;
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[{nameof(KeybindingLoaderService)}]: {ErrorMessages.JsonParseFailed}");
                return false;
            }

            lock (_lock)
            {
                _actions.Clear();

                // Map DTO actions to domain model
                foreach (KeybindingActionData action in dataFile.Actions)
                {
                    KeybindingAction keybindingAction = new()
                    {
                        ActionName = action.Name ?? string.Empty,
                        MapName = action.MapName ?? string.Empty,
                        UiLabel = action.Label ?? string.Empty,
                        UiDescription = action.Description ?? string.Empty,
                        UiCategory = action.Category ?? string.Empty,
                        KeyboardBinding = action.Bindings?.Keyboard ?? string.Empty,
                        MouseBinding = action.Bindings?.Mouse ?? string.Empty,
                        JoystickBinding = action.Bindings?.Joystick ?? string.Empty,
                        GamepadBinding = action.Bindings?.Gamepad ?? string.Empty,
                        ActivationMode = action.ActivationMode
                    };
                    _actions[keybindingAction.ActionName] = keybindingAction;
                }

                // Load activation modes from metadata
                if (dataFile.Metadata.ActivationModes is { Count: > 0 })
                {
                    _activationModes = dataFile.Metadata.ActivationModes;
                }

                _isLoaded = true;
            }

            return true;
        }
        catch (IOException ex)
        {
            _isLoaded = false;
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(KeybindingLoaderService)}]: {ErrorMessages.FileReadFailed} '{Path.GetFileName(jsonPath)}': {ex.Message}");
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _isLoaded = false;
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(KeybindingLoaderService)}]: {ErrorMessages.FileAccessDenied} '{Path.GetFileName(jsonPath)}': {ex.Message}");
            return false;
        }
        catch (JsonException ex)
        {
            _isLoaded = false;
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(KeybindingLoaderService)}]: {ErrorMessages.JsonParseFailed} '{Path.GetFileName(jsonPath)}': {ex.Message}");
            return false;
        }
    }

    public bool TryGetAction(string? actionName, out KeybindingAction? action)
    {
        lock (_lock)
        {
            return _actions.TryGetValue(actionName!, out action);
        }
    }

    public IReadOnlyList<KeybindingAction> GetAllActions()
    {
        lock (_lock)
        {
            return _actions.Values.ToList();
        }
    }

    public IntPtr GetKeyboardLayoutId() => _currentKeyboardLayout;

    public IReadOnlyDictionary<string, ActivationModeMetadata> GetActivationModes()
    {
        lock (_lock)
        {
            return new Dictionary<string, ActivationModeMetadata>(_activationModes);
        }
    }
}
