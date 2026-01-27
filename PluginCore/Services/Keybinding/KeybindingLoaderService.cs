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
public sealed class KeybindingLoaderService(IFileSystem fileSystem)
{
    private readonly Dictionary<string, KeybindingAction> _actions = new(StringComparer.OrdinalIgnoreCase);
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly object _lock = new();
    private Dictionary<ActivationMode, ActivationModeMetadata> _activationModesByMode = [];
    private Dictionary<string, ActivationModeMetadata> _activationModesByName = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _isLoaded;

    public bool IsLoaded => _isLoaded;

    public async Task<bool> LoadKeybindingsAsync(string jsonPath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!SecurePathValidator.TryNormalizePath(jsonPath, out string validatedPath))
            {
                SetNotLoaded();
                Log.Err($"[{nameof(KeybindingLoaderService)}] Invalid path");

                return false;
            }

            if (!_fileSystem.FileExists(validatedPath))
            {
                SetNotLoaded();
                Log.Err($"[{nameof(KeybindingLoaderService)}] File not found: '{validatedPath}'");

                return false;
            }

            KeybindingDataFile? dataFile =
                await ReadAndDeserializeAsync(validatedPath, cancellationToken).ConfigureAwait(false);
            if (!IsValidDataFile(dataFile))
            {
                SetNotLoaded();
                Log.Err($"[{nameof(KeybindingLoaderService)}] Invalid keybinding data file format");
                return false;
            }

            CacheDataFile(dataFile!);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            SetNotLoaded();
            Log.Err($"[{nameof(KeybindingLoaderService)}] '{Path.GetFileName(jsonPath)}': {ex.Message}", ex);
            return false;
        }
    }

    public bool TryGetAction(string? actionName, out KeybindingAction? action)
    {
        if (string.IsNullOrWhiteSpace(actionName))
        {
            action = null;
            return false;
        }

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

    public IReadOnlyDictionary<string, ActivationModeMetadata> GetActivationModes()
    {
        lock (_lock)
        {
            return new Dictionary<string, ActivationModeMetadata>(_activationModesByName);
        }
    }

    public IReadOnlyDictionary<ActivationMode, ActivationModeMetadata> GetActivationModesByMode()
    {
        lock (_lock)
        {
            return new Dictionary<ActivationMode, ActivationModeMetadata>(_activationModesByMode);
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

            return _activationModesByMode.GetValueOrDefault(action.ActivationMode);
        }
    }

    private async Task<KeybindingDataFile?> ReadAndDeserializeAsync(
        string validatedPath,
        CancellationToken cancellationToken)
    {
        string json = await _fileSystem.ReadAllTextAsync(validatedPath, cancellationToken).ConfigureAwait(false);
        return JsonConvert.DeserializeObject<KeybindingDataFile>(json);
    }

    private static bool IsValidDataFile(KeybindingDataFile? dataFile) =>
        dataFile is { Actions.Count: > 0, Metadata: not null };

    private void SetNotLoaded()
    {
        lock (_lock)
        {
            _actions.Clear();

            _activationModesByName = new Dictionary<string, ActivationModeMetadata>(StringComparer.OrdinalIgnoreCase);
            _activationModesByMode = [];
            _isLoaded = false;
        }
    }

    private void CacheDataFile(KeybindingDataFile dataFile)
    {
        KeybindingMetadata metadata = dataFile.Metadata!;

        lock (_lock)
        {
            _actions.Clear();

            foreach (KeybindingAction keybindingAction in dataFile.Actions.Select(MapAction))
            {
                _actions[$"{keybindingAction.ActionName}_{keybindingAction.UiCategory}"] = keybindingAction;
            }

            _activationModesByName = metadata.ActivationModes is { Count: > 0 }
                ? new Dictionary<string, ActivationModeMetadata>(metadata.ActivationModes,
                    StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, ActivationModeMetadata>(StringComparer.OrdinalIgnoreCase);

            _activationModesByMode = MapActivationModesByMode(_activationModesByName);

            _isLoaded = true;
        }
    }

    private static Dictionary<ActivationMode, ActivationModeMetadata> MapActivationModesByMode(
        IReadOnlyDictionary<string, ActivationModeMetadata> activationModes)
    {
        Dictionary<ActivationMode, ActivationModeMetadata> mapped = [];

        foreach ((string key, ActivationModeMetadata metadata) in activationModes)
        {
            if (!Enum.TryParse(key, true, out ActivationMode mode))
            {
                continue;
            }

            mapped[mode] = metadata;
        }

        return mapped;
    }

    private static KeybindingAction MapAction(KeybindingActionData action) => new()
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
}
