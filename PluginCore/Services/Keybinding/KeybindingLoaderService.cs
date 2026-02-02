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
    private const string FunctionIdV2Prefix = "v2|";

    // Canonical storage (v2 ids only) so GetAllActions() never returns duplicates.
    private readonly Dictionary<string, KeybindingAction> _actionsById = new(StringComparer.OrdinalIgnoreCase);

    // Backward compatibility / migration: legacy ids and other aliases resolve to v2 ids.
    private readonly Dictionary<string, string> _aliasToId = new(StringComparer.OrdinalIgnoreCase);

    // Helps resolve legacy ids when the category suffix changed (e.g. localization/fallback changes).
    private readonly Dictionary<string, List<string>> _actionNameToIds = new(StringComparer.OrdinalIgnoreCase);
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
            if (TryResolveIdLocked(actionName, out string? resolvedId) &&
                _actionsById.TryGetValue(resolvedId!, out action))
            {
                return true;
            }

            action = null;
            return false;
        }
    }

    public bool TryNormalizeActionId(string? actionId, out string normalizedId)
    {
        if (string.IsNullOrWhiteSpace(actionId))
        {
            normalizedId = string.Empty;
            return false;
        }

        lock (_lock)
        {
            if (!TryResolveIdLocked(actionId, out string? resolvedId) || string.IsNullOrWhiteSpace(resolvedId))
            {
                normalizedId = string.Empty;
                return false;
            }

            normalizedId = resolvedId;
            return true;
        }
    }


    public IReadOnlyList<KeybindingAction> GetAllActions()
    {
        lock (_lock)
        {
            return _actionsById.Values.ToList();
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
            if (!TryGetAction(actionName, out KeybindingAction? action) || action == null)
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
            _actionsById.Clear();
            _aliasToId.Clear();
            _actionNameToIds.Clear();

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
            _actionsById.Clear();
            _aliasToId.Clear();
            _actionNameToIds.Clear();

            foreach (KeybindingAction keybindingAction in dataFile.Actions.Select(MapAction))
            {
                string v2Id = BuildV2Id(keybindingAction.ActionName, keybindingAction.MapName);
                _actionsById[v2Id] = keybindingAction;

                AddAliasLocked(v2Id, keybindingAction);
            }

            // Optional convenience alias: allow resolving by actionName only when unique.
            foreach ((string actionName, List<string> ids) in _actionNameToIds)
            {
                if (ids.Count == 1)
                {
                    _aliasToId[actionName] = ids[0];
                }
            }

            _activationModesByName = metadata.ActivationModes is { Count: > 0 }
                ? new Dictionary<string, ActivationModeMetadata>(metadata.ActivationModes,
                    StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, ActivationModeMetadata>(StringComparer.OrdinalIgnoreCase);

            _activationModesByMode = MapActivationModesByMode(_activationModesByName);

            _isLoaded = true;
        }
    }

    private static string BuildV2Id(string actionName, string mapName) =>
        $"{FunctionIdV2Prefix}{actionName}|{mapName}";

    private static string BuildLegacyId(string actionName, string category) =>
        $"{actionName}_{category}";

    private void AddAliasLocked(string v2Id, KeybindingAction action)
    {
        // Direct / canonical
        _aliasToId[v2Id] = v2Id;

        // Legacy v1 ids used by existing profiles: <actionName>_<category>
        if (!string.IsNullOrWhiteSpace(action.UiCategory))
        {
            _aliasToId[BuildLegacyId(action.ActionName, action.UiCategory)] = v2Id;
        }

        if (!_actionNameToIds.TryGetValue(action.ActionName, out List<string>? ids))
        {
            ids = [];
            _actionNameToIds[action.ActionName] = ids;
        }

        ids.Add(v2Id);
    }

    private bool TryResolveIdLocked(string input, out string? resolvedId)
    {
        resolvedId = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        // v2 ids are canonical
        if (input.StartsWith(FunctionIdV2Prefix, StringComparison.OrdinalIgnoreCase))
        {
            resolvedId = input;
            return _actionsById.ContainsKey(resolvedId);
        }

        // Fast path: exact alias match (legacy ids, actionName-only unique, etc.)
        if (_aliasToId.TryGetValue(input, out string? id))
        {
            resolvedId = id;
            return true;
        }

        // Legacy fallback: try to extract the longest actionName prefix.
        // This allows old saved ids to keep working even if the category suffix changed.
        string? actionName = TryExtractLongestMatchingActionName(input);
        if (string.IsNullOrWhiteSpace(actionName))
        {
            return false;
        }

        if (!_actionNameToIds.TryGetValue(actionName, out List<string>? candidates) || candidates.Count == 0)
        {
            return false;
        }

        if (candidates.Count == 1)
        {
            resolvedId = candidates[0];
            return true;
        }

        // If ambiguous, try to match the category suffix against current UiCategory.
        string suffix = input.Length > actionName.Length + 1
            ? input[(actionName.Length + 1)..]
            : string.Empty;

        if (!string.IsNullOrWhiteSpace(suffix))
        {
            foreach (string candidateId in candidates)
            {
                if (_actionsById.TryGetValue(candidateId, out KeybindingAction? action) &&
                    string.Equals(action.UiCategory, suffix, StringComparison.OrdinalIgnoreCase))
                {
                    resolvedId = candidateId;
                    return true;
                }
            }
        }

        return false;
    }

    private string? TryExtractLongestMatchingActionName(string input)
    {
        int underscoreIndex = input.LastIndexOf('_');
        while (underscoreIndex > 0)
        {
            string candidate = input[..underscoreIndex];
            if (_actionNameToIds.ContainsKey(candidate))
            {
                return candidate;
            }

            underscoreIndex = candidate.LastIndexOf('_');
        }

        return null;
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
        MapLabel = action.MapLabel ?? string.Empty,
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
