using System.Collections.Concurrent;
using BarRaider.SdTools;
using Newtonsoft.Json;
using SCStreamDeck.SCCore.Common;
using SCStreamDeck.SCCore.Logging;
using SCStreamDeck.SCCore.Models;

namespace SCStreamDeck.SCCore.Services.Keybinding;

/// <summary>
///     Service for loading and caching keybinding actions and activation modes.
/// </summary>
public sealed class KeybindingLoaderService : IKeybindingLoaderService
{
    private readonly Dictionary<string, KeybindingAction> _actions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private readonly nint _currentKeyboardLayout = NativeMethods.GetKeyboardLayout(0);
    private Dictionary<string, ActivationModeMetadata> _activationModes = new(StringComparer.OrdinalIgnoreCase);

    public bool IsLoaded { get; private set; }

    public async Task<bool> LoadKeybindingsAsync(string jsonPath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!SecurePathValidator.TryNormalizePath(jsonPath, out var validatedPath))
            {
                IsLoaded = false;
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[{nameof(KeybindingLoaderService)}]: {ErrorMessages.InvalidPath}");
                return false;
            }

            if (!File.Exists(validatedPath))
            {
                IsLoaded = false;
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[{nameof(KeybindingLoaderService)}]: {ErrorMessages.FileReadFailed}");
                return false;
            }

            var json = await File.ReadAllTextAsync(validatedPath, cancellationToken).ConfigureAwait(false);
            var dataFile = JsonConvert.DeserializeObject<KeybindingDataFile>(json);

            if (dataFile?.Actions == null)
            {
                IsLoaded = false;
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[{nameof(KeybindingLoaderService)}]: {ErrorMessages.JsonParseFailed}");
                return false;
            }

            lock (_lock)
            {
                _actions.Clear();

                // Map DTO actions to domain model
                foreach (var action in dataFile.Actions)
                {
                    var keybindingAction = new KeybindingAction
                    {
                        ActionName = action.Name ?? string.Empty,
                        MapName = action.MapName ?? string.Empty,
                        UILabel = action.Label ?? string.Empty,
                        UIDescription = action.Description ?? string.Empty,
                        UICategory = action.Category ?? string.Empty,
                        KeyboardBinding = action.Bindings?.Keyboard ?? string.Empty,
                        MouseBinding = action.Bindings?.Mouse ?? string.Empty,
                        JoystickBinding = action.Bindings?.Joystick ?? string.Empty,
                        GamepadBinding = action.Bindings?.Gamepad ?? string.Empty,
                        ActivationMode = action.ActivationMode
                    };
                    _actions[keybindingAction.ActionName] = keybindingAction;
                }

                // Load activation modes from metadata
                if (dataFile.Metadata?.ActivationModes != null && dataFile.Metadata.ActivationModes.Count > 0)
                {
                    _activationModes = dataFile.Metadata.ActivationModes;
                }

                IsLoaded = true;
            }

            return true;
        }
        catch (IOException ex)
        {
            IsLoaded = false;
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(KeybindingLoaderService)}]: {ErrorMessages.FileReadFailed} '{Path.GetFileName(jsonPath)}': {ex.Message}");
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            IsLoaded = false;
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(KeybindingLoaderService)}]: {ErrorMessages.FileAccessDenied} '{Path.GetFileName(jsonPath)}': {ex.Message}");
            return false;
        }
        catch (JsonException ex)
        {
            IsLoaded = false;
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(KeybindingLoaderService)}]: {ErrorMessages.JsonParseFailed} '{Path.GetFileName(jsonPath)}': {ex.Message}");
            return false;
        }
    }

    public bool TryGetAction(string actionName, out KeybindingAction? action)
    {
        lock (_lock)
        {
            return _actions.TryGetValue(actionName, out action);
        }
    }

    public IReadOnlyList<KeybindingAction> GetAllActions()
    {
        lock (_lock)
        {
            return _actions.Values.ToList();
        }
    }

    public IntPtr GetKeyboardLayoutId()
    {
        return _currentKeyboardLayout;
    }

    public IReadOnlyDictionary<string, ActivationModeMetadata> GetActivationModes()
    {
        lock (_lock)
        {
            return new Dictionary<string, ActivationModeMetadata>(_activationModes);
        }
    }
}
