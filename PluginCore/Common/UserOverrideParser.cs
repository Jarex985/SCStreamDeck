using System.Xml;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;

namespace SCStreamDeck.Common;

/// <summary>
///     Parses user keybinding overrides from Star Citizen's actionmaps.xml file.
/// </summary>
internal static class UserOverrideParser
{
    /// <summary>
    ///     Parses the actionmaps.xml file and returns user binding overrides.
    /// </summary>
    /// <param name="actionMapsPath">Path to the actionmaps.xml file.</param>
    /// <returns>Parsed overrides, or null if file doesn't exist or parsing fails.</returns>
    public static UserOverrides? Parse(string actionMapsPath)
    {
        if (!TryValidatePath(actionMapsPath, out string validPath))
        {
            return null;
        }

        try
        {
            string xmlText = File.ReadAllText(validPath);
            return ParseXml(xmlText);
        }

        catch (Exception ex) when (ex is XmlException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            Log.Err($"[{nameof(UserOverrideParser)}] {ex.Message}", ex);
            return null;
        }
    }


    /// <summary>
    ///     Applies the parsed overrides to a list of keybinding actions.
    /// </summary>
    /// <param name="actions">The actions to apply overrides to.</param>
    /// <param name="overrides">The overrides to apply.</param>
    public static void ApplyOverrides(List<KeybindingActionData> actions, UserOverrides overrides)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(overrides);

        Dictionary<string, Dictionary<string, KeybindingActionData>> actionsByMapAndName = BuildActionsByMapAndName(actions);
        Dictionary<string, List<KeybindingActionData>> actionsByName = BuildActionsByName(actions);

        ApplyDeviceOverrides(actionsByMapAndName, actionsByName, overrides.KeyboardByMap, overrides.Keyboard,
            (action, binding) =>
            {
                string normalized = binding?.Trim() ?? string.Empty;

                // Star Citizen inconsistently stores mouse buttons in the keyboard field.
                // Treat keyboard+mouse as a single primary slot: applying one clears the other.
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    action.Bindings.Keyboard = null;
                    action.Bindings.Mouse = null;
                    return;
                }

                if (normalized.IsMouseWheel() || normalized.IsMouseButton())
                {
                    action.Bindings.Mouse = normalized;
                    action.Bindings.Keyboard = null;
                    return;
                }

                action.Bindings.Keyboard = normalized;
                action.Bindings.Mouse = null;
            });

        ApplyDeviceOverrides(actionsByMapAndName, actionsByName, overrides.MouseByMap, overrides.Mouse,
            (action, binding) =>
            {
                string normalized = binding?.Trim() ?? string.Empty;

                // Primary slot: applying mouse overrides clears keyboard.
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    action.Bindings.Mouse = null;
                    action.Bindings.Keyboard = null;
                    return;
                }

                action.Bindings.Mouse = normalized;
                action.Bindings.Keyboard = null;
            });

        ApplyDeviceOverrides(actionsByMapAndName, actionsByName, overrides.JoystickByMap, overrides.Joystick,
            (action, binding) => action.Bindings.Joystick = binding);

        ApplyDeviceOverrides(actionsByMapAndName, actionsByName, overrides.GamepadByMap, overrides.Gamepad,
            (action, binding) => action.Bindings.Gamepad = binding);
    }

    private static Dictionary<string, Dictionary<string, KeybindingActionData>> BuildActionsByMapAndName(List<KeybindingActionData> actions)
    {
        Dictionary<string, Dictionary<string, KeybindingActionData>> actionLookup = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeybindingActionData action in actions)
        {
            string mapName = action.MapName ?? string.Empty;
            if (!actionLookup.TryGetValue(mapName, out Dictionary<string, KeybindingActionData>? actionsForMap))
            {
                actionsForMap = new Dictionary<string, KeybindingActionData>(StringComparer.OrdinalIgnoreCase);
                actionLookup[mapName] = actionsForMap;
            }

            actionsForMap[action.Name] = action;
        }

        return actionLookup;
    }

    private static Dictionary<string, List<KeybindingActionData>> BuildActionsByName(List<KeybindingActionData> actions)
    {
        Dictionary<string, List<KeybindingActionData>> actionsByName = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeybindingActionData action in actions)
        {
            if (!actionsByName.TryGetValue(action.Name, out List<KeybindingActionData>? list))
            {
                list = [];
                actionsByName[action.Name] = list;
            }

            list.Add(action);
        }

        return actionsByName;
    }

    private static void ApplyDeviceOverrides(
        Dictionary<string, Dictionary<string, KeybindingActionData>> actionsByMapAndName,
        Dictionary<string, List<KeybindingActionData>> actionsByName,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> overridesByMap,
        IReadOnlyDictionary<string, string?> globalOverrides,
        Action<KeybindingActionData, string?> applyBinding)
    {
        HashSet<string> updatedActionKeys = new(StringComparer.OrdinalIgnoreCase);

        foreach ((string mapName, IReadOnlyDictionary<string, string?> mapOverrides) in overridesByMap)
        {
            if (!actionsByMapAndName.TryGetValue(mapName, out Dictionary<string, KeybindingActionData>? actionsForMap))
            {
                continue;
            }

            foreach ((string actionName, string? binding) in mapOverrides)
            {
                if (actionsForMap.TryGetValue(actionName, out KeybindingActionData? action))
                {
                applyBinding(action, binding);
                updatedActionKeys.Add(BuildMapActionKey(action.MapName, action.Name));
            }
        }
        }

        foreach ((string actionName, string? binding) in globalOverrides)
        {
            if (!actionsByName.TryGetValue(actionName, out List<KeybindingActionData>? matchingActions))
            {
                continue;
            }

            foreach (KeybindingActionData action in matchingActions)
            {
                string key = BuildMapActionKey(action.MapName, action.Name);
                if (updatedActionKeys.Contains(key))
                {
                    continue;
                }

                applyBinding(action, binding);
            }
        }
    }

    private static string BuildMapActionKey(string? mapName, string actionName) =>
        $"{mapName ?? string.Empty}|{actionName}";


    private static UserOverrides ParseXml(string xmlText)
    {
        Dictionary<string, string?> keyboard = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string?> mouse = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string?> joystick = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string?> gamepad = new(StringComparer.OrdinalIgnoreCase);

        Dictionary<string, Dictionary<string, string?>> keyboardByMap = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, Dictionary<string, string?>> mouseByMap = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, Dictionary<string, string?>> joystickByMap = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, Dictionary<string, string?>> gamepadByMap = new(StringComparer.OrdinalIgnoreCase);

        using StringReader sr = new(xmlText);
        using XmlReader xmlReader = XmlReader.Create(sr,
            new XmlReaderSettings
            {
                IgnoreComments = true, IgnoreWhitespace = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null
            });

        string currentMapName = string.Empty;
        int currentActionMapDepth = -1;

        while (xmlReader.Read())
        {
            if (IsActionMapStartElement(xmlReader))
            {
                currentMapName = xmlReader.GetAttribute("name") ?? string.Empty;
                currentActionMapDepth = xmlReader.Depth;
                continue;
            }

            if (IsActionMapEndElement(xmlReader, currentActionMapDepth))
            {
                currentMapName = string.Empty;
                currentActionMapDepth = -1;
                continue;
            }

            if (!IsActionElement(xmlReader))
            {
                continue;
            }

            string actionName = xmlReader.GetAttribute("name") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(actionName) || xmlReader.IsEmptyElement)
            {
                continue;
            }

            ParseActionRebinds(xmlReader, currentMapName, actionName, keyboard, mouse, joystick, gamepad,
                keyboardByMap, mouseByMap, joystickByMap, gamepadByMap);
        }

        DeriveGlobalOverrides(keyboard, keyboardByMap);
        DeriveGlobalOverrides(mouse, mouseByMap);
        DeriveGlobalOverrides(joystick, joystickByMap);
        DeriveGlobalOverrides(gamepad, gamepadByMap);

        Dictionary<string, IReadOnlyDictionary<string, string?>> keyboardByMapReadOnly = ToReadOnlyByMap(keyboardByMap);
        Dictionary<string, IReadOnlyDictionary<string, string?>> mouseByMapReadOnly = ToReadOnlyByMap(mouseByMap);
        Dictionary<string, IReadOnlyDictionary<string, string?>> joystickByMapReadOnly = ToReadOnlyByMap(joystickByMap);
        Dictionary<string, IReadOnlyDictionary<string, string?>> gamepadByMapReadOnly = ToReadOnlyByMap(gamepadByMap);

        return new UserOverrides(keyboard, mouse, joystick, gamepad,
            keyboardByMapReadOnly, mouseByMapReadOnly, joystickByMapReadOnly, gamepadByMapReadOnly);
    }

    private static void ParseActionRebinds(
        XmlReader xmlReader,
        string mapName,
        string actionName,
        Dictionary<string, string?> keyboard,
        Dictionary<string, string?> mouse,
        Dictionary<string, string?> joystick,
        Dictionary<string, string?> gamepad,
        Dictionary<string, Dictionary<string, string?>> keyboardByMap,
        Dictionary<string, Dictionary<string, string?>> mouseByMap,
        Dictionary<string, Dictionary<string, string?>> joystickByMap,
        Dictionary<string, Dictionary<string, string?>> gamepadByMap)
    {
        int depth = xmlReader.Depth;

        while (xmlReader.Read())
        {
            if (IsEndOfAction(xmlReader, depth))
            {
                break;
            }

            if (!IsRebindElement(xmlReader))
            {
                continue;
            }

            string input = xmlReader.GetAttribute("input") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            string prefix = input.Length >= 2 ? input[..2].ToLowerInvariant() : string.Empty;
            string normalized = NormalizeInputSuffix(input);

            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            ApplyOverride(prefix, mapName, actionName, normalized, keyboard, mouse, joystick, gamepad,
                keyboardByMap, mouseByMap, joystickByMap, gamepadByMap);
        }
    }

    private static string NormalizeInputSuffix(string input)
    {
        int idx = input.IndexOf('_');
        if (idx < 0 || idx == input.Length - 1)
        {
            return string.Empty;
        }

        return input[(idx + 1)..].Trim();
    }

    private static bool IsActionElement(XmlReader reader) =>
        reader.NodeType == XmlNodeType.Element &&
        reader.Name.Equals("action", StringComparison.OrdinalIgnoreCase);

    private static bool IsRebindElement(XmlReader reader) =>
        reader.NodeType == XmlNodeType.Element &&
        reader.Name.Equals("rebind", StringComparison.OrdinalIgnoreCase);

    private static bool IsEndOfAction(XmlReader reader, int actionDepth) =>
        reader.NodeType == XmlNodeType.EndElement &&
        reader.Depth == actionDepth &&
        reader.Name.Equals("action", StringComparison.OrdinalIgnoreCase);

    private static void ApplyOverride(
        string prefix,
        string mapName,
        string actionName,
        string normalized,
        Dictionary<string, string?> keyboard,
        Dictionary<string, string?> mouse,
        Dictionary<string, string?> joystick,
        Dictionary<string, string?> gamepad,
        Dictionary<string, Dictionary<string, string?>> keyboardByMap,
        Dictionary<string, Dictionary<string, string?>> mouseByMap,
        Dictionary<string, Dictionary<string, string?>> joystickByMap,
        Dictionary<string, Dictionary<string, string?>> gamepadByMap)
    {
        switch (prefix)
        {
            case "kb":
                ApplyOverrideToTarget(mapName, actionName, normalized, keyboard, keyboardByMap);
                break;
            case "mo":
                ApplyOverrideToTarget(mapName, actionName, normalized, mouse, mouseByMap);
                break;
            case "js":
                ApplyOverrideToTarget(mapName, actionName, normalized, joystick, joystickByMap);
                break;
            case "gp":
                ApplyOverrideToTarget(mapName, actionName, normalized, gamepad, gamepadByMap);
                break;
        }
    }

    private static void ApplyOverrideToTarget(
        string mapName,
        string actionName,
        string normalized,
        Dictionary<string, string?> global,
        Dictionary<string, Dictionary<string, string?>> byMap)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            global[actionName] = normalized;
            return;
        }

        if (!byMap.TryGetValue(mapName, out Dictionary<string, string?>? mapOverrides))
        {
            mapOverrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            byMap[mapName] = mapOverrides;
        }

        mapOverrides[actionName] = normalized;
    }

    private static void DeriveGlobalOverrides(
        Dictionary<string, string?> global,
        Dictionary<string, Dictionary<string, string?>> byMap)
    {
        Dictionary<string, string?> firstValue = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> mismatched = new(StringComparer.OrdinalIgnoreCase);

        foreach ((string _mapName, Dictionary<string, string?> mapOverrides) in byMap)
        {
            foreach ((string actionName, string? binding) in mapOverrides)
            {
                if (global.ContainsKey(actionName))
                {
                    continue;
                }

                if (!firstValue.TryGetValue(actionName, out string? existing))
                {
                    firstValue[actionName] = binding;
                    continue;
                }

                if (!string.Equals(existing, binding, StringComparison.OrdinalIgnoreCase))
                {
                    mismatched.Add(actionName);
                }
            }
        }

        foreach ((string actionName, string? binding) in firstValue)
        {
            if (mismatched.Contains(actionName))
            {
                continue;
            }

            global[actionName] = binding;
        }
    }

    private static Dictionary<string, IReadOnlyDictionary<string, string?>> ToReadOnlyByMap(
        Dictionary<string, Dictionary<string, string?>> byMap)
    {
        Dictionary<string, IReadOnlyDictionary<string, string?>> result = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string mapName, Dictionary<string, string?> actionOverrides) in byMap)
        {
            result[mapName] = actionOverrides;
        }

        return result;
    }

    private static bool IsActionMapStartElement(XmlReader reader) =>
        reader.NodeType == XmlNodeType.Element &&
        reader.Name.Equals("actionmap", StringComparison.OrdinalIgnoreCase);

    private static bool IsActionMapEndElement(XmlReader reader, int actionMapDepth) =>
        actionMapDepth >= 0 &&
        reader.NodeType == XmlNodeType.EndElement &&
        reader.Depth == actionMapDepth &&
        reader.Name.Equals("actionmap", StringComparison.OrdinalIgnoreCase);

    private static bool TryValidatePath(string actionMapsPath, out string validPath)
    {
        if (!SecurePathValidator.TryNormalizePath(actionMapsPath, out validPath))
        {
            Log.Err($"[{nameof(UserOverrideParser)}] Invalid path '{actionMapsPath}'");
            return false;
        }

        return File.Exists(validPath);
    }
}

/// <summary>
///     Contains parsed user keybinding overrides from actionmaps.xml.
/// </summary>
/// <param name="Keyboard">Keyboard binding overrides by action name.</param>
/// <param name="Mouse">Mouse binding overrides by action name.</param>
/// <param name="Joystick">Joystick binding overrides by action name.</param>
/// <param name="Gamepad">Gamepad binding overrides by action name.</param>
internal sealed record UserOverrides(
    IReadOnlyDictionary<string, string?> Keyboard,
    IReadOnlyDictionary<string, string?> Mouse,
    IReadOnlyDictionary<string, string?> Joystick,
    IReadOnlyDictionary<string, string?> Gamepad,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> KeyboardByMap,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> MouseByMap,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> JoystickByMap,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> GamepadByMap)
{
    /// <summary>
    ///     Gets the total number of overrides across all input types.
    /// </summary>
    private int TotalCount => Keyboard.Count + Mouse.Count + Joystick.Count + Gamepad.Count +
                              CountByMap(KeyboardByMap) + CountByMap(MouseByMap) + CountByMap(JoystickByMap) + CountByMap(GamepadByMap);

    private static int CountByMap(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> byMap) =>
        byMap.Values.Sum(v => v.Count);

    /// <summary>
    ///     Returns true if there are any overrides.
    /// </summary>
    public bool HasOverrides => TotalCount > 0;
}
