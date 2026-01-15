using BarRaider.SdTools;
using Newtonsoft.Json;
using SCStreamDeck.Common;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;
// ReSharper disable NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract

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

            if (dataFile == null || dataFile.Actions.Count == 0)
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
                foreach (KeybindingAction keybindingAction in dataFile.Actions.Select(action => new KeybindingAction
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
                         }))
                {
                    _actions[$"{keybindingAction.ActionName}_{keybindingAction.UiCategory}"] = keybindingAction;
                }

                if (dataFile.Metadata.ActivationModes is { Count: > 0 })
                {
                    _activationModes = dataFile.Metadata.ActivationModes;
                }

                _isLoaded = true;
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _isLoaded = false;
            string errorMessage = ex switch
            {
                IOException => ErrorMessages.FileReadFailed,
                UnauthorizedAccessException => ErrorMessages.FileAccessDenied,
                JsonException => ErrorMessages.JsonParseFailed,
                _ => string.Empty
            };

            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(KeybindingLoaderService)}]: {errorMessage} '{Path.GetFileName(jsonPath)}': {ex.Message}");
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

    public ActivationModeMetadata? GetMetadata(string actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName))
        {
            return null;
        }

        lock (_lock)
        {
            if (!_actions.TryGetValue(actionName, out KeybindingAction? action))
            {
                return null;
            }

            string modeKey = action.ActivationMode.ToString();
            return _activationModes.GetValueOrDefault(modeKey);
        }
    }
}
