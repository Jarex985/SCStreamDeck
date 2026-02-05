using Newtonsoft.Json;

namespace SCStreamDeck.Models;

/// <summary>
///     Root structure for the processed keybindings JSON file.
///     Contains metadata and actions with embedded localization (resolved labels).
/// </summary>
public sealed class KeybindingDataFile
{
    [JsonProperty("metadata")] public KeybindingMetadata? Metadata { get; set; } = new();

    [JsonProperty("actions")] public List<KeybindingActionData> Actions { get; set; } = [];
}

/// <summary>
///     Metadata for tracking source file state and enabling incremental updates.
/// </summary>
public sealed class KeybindingMetadata
{
    [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; }

    [JsonProperty("extractedAt")] public DateTime ExtractedAt { get; set; }

    [JsonProperty("language")] public string Language { get; set; } = "english";

    [JsonProperty("dataP4kPath")] public string DataP4KPath { get; set; } = "";

    [JsonProperty("dataP4kSize")] public long DataP4KSize { get; set; }

    [JsonProperty("dataP4kLastWrite")] public DateTime DataP4KLastWrite { get; set; }

    [JsonProperty("actionMapsPath")] public string? ActionMapsPath { get; set; }

    [JsonProperty("actionMapsSize")] public long? ActionMapsSize { get; set; }

    [JsonProperty("actionMapsLastWrite")] public DateTime? ActionMapsLastWrite { get; set; }

    /// <summary>
    ///     Activation mode metadata extracted from defaultProfile.xml.
    ///     Maps activation mode names to their behavior flags (onPress, onHold, onRelease, etc.).
    /// </summary>
    [JsonProperty("activationModes")]
    public Dictionary<string, ActivationModeMetadata>? ActivationModes { get; set; }
}

/// <summary>
///     Represents a single Star Citizen action with all its bindings and metadata.
///     Named KeybindingActionData to avoid conflict with existing KeybindingAction (execution model).
/// </summary>
public sealed class KeybindingActionData
{
    [JsonProperty("name")] public string Name { get; set; } = "";

    [JsonProperty("label")] public string Label { get; set; } = "";

    [JsonProperty("description")] public string Description { get; set; } = "";

    [JsonProperty("category")] public string Category { get; set; } = "";

    [JsonProperty("mapName")] public string MapName { get; set; } = "";

    [JsonProperty("mapLabel")] public string MapLabel { get; set; } = "";

    [JsonProperty("activationMode")] public ActivationMode ActivationMode { get; set; } = ActivationMode.press;

    [JsonProperty("isToggleCandidate")] public bool IsToggleCandidate { get; set; }

    [JsonProperty("bindings")] public InputBindings Bindings { get; set; } = new();
}

/// <summary>
///     Binding information for all input devices for a single action.
/// </summary>
public sealed class InputBindings
{
    [JsonProperty("keyboard")] public string? Keyboard { get; set; }

    [JsonProperty("mouse")] public string? Mouse { get; set; }

    [JsonProperty("joystick")] public string? Joystick { get; set; }

    [JsonProperty("gamepad")] public string? Gamepad { get; set; }
}
