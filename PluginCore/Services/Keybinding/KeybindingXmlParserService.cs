using System.Globalization;
using System.Xml;
using BarRaider.SdTools;
using SCStreamDeck.SCCore.Common;
using SCStreamDeck.SCCore.Models;

namespace SCStreamDeck.SCCore.Services.Keybinding;

/// <summary>
///     Service for parsing Star Citizen keybinding XML data.
/// </summary>
public sealed class KeybindingXmlParserService : IKeybindingXmlParserService
{
    /// <summary>
    ///     Parses activation mode metadata from XML text.
    /// </summary>
    /// <param name="xmlText">The XML text to parse</param>
    /// <returns>Dictionary of activation mode names and their metadata</returns>
    public Dictionary<string, ActivationModeMetadata> ParseActivationModes(string xmlText)
    {
        Dictionary<string, ActivationModeMetadata> modes = new(StringComparer.OrdinalIgnoreCase);

        using StringReader sr = new(xmlText);
        using XmlReader xmlReader = XmlReader.Create(sr,
            new XmlReaderSettings
            {
                IgnoreComments = true, IgnoreWhitespace = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null
            });

        while (xmlReader.Read())
        {
            if (xmlReader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (!xmlReader.Name.Equals("ActivationMode", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? name = xmlReader.GetAttribute("name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            string? pressTriggerAttr = xmlReader.GetAttribute("pressTriggerThreshold");
            string? releaseTriggerAttr = xmlReader.GetAttribute("releaseTriggerThreshold");
            string? multiTapAttr = xmlReader.GetAttribute("multiTap");

            ActivationModeMetadata metadata = new()
            {
                OnPress = xmlReader.GetAttribute("onPress") == "1",
                OnHold = xmlReader.GetAttribute("onHold") == "1",
                OnRelease = xmlReader.GetAttribute("onRelease") == "1",
                PressTriggerThreshold = !string.IsNullOrWhiteSpace(pressTriggerAttr) &&
                                        float.TryParse(pressTriggerAttr, NumberStyles.Float, CultureInfo.InvariantCulture,
                                            out float pt)
                    ? pt
                    : -1,
                ReleaseTriggerThreshold = !string.IsNullOrWhiteSpace(releaseTriggerAttr) &&
                                          float.TryParse(releaseTriggerAttr, NumberStyles.Float, CultureInfo.InvariantCulture,
                                              out float rt)
                    ? rt
                    : -1,
                Retriggerable = xmlReader.GetAttribute("retriggerable") == "1",
                MultiTap = !string.IsNullOrWhiteSpace(multiTapAttr) &&
                           int.TryParse(multiTapAttr, out int mt)
                    ? mt
                    : 1
            };

            modes[name] = metadata;
        }

        return modes;
    }

    /// <summary>
    ///     Parses keybinding actions from XML text.
    /// </summary>
    /// <param name="xmlText">The XML text to parse</param>
    /// <returns>List of parsed keybinding actions</returns>
    public List<KeybindingActionData> ParseXmlToActions(string xmlText)
    {
        List<KeybindingActionData> actions = new();

        // Parse activation modes first (needed for inference)
        Dictionary<string, ActivationModeMetadata> activationModes = ParseActivationModes(xmlText);

        using StringReader sr = new(xmlText);
        using XmlReader xmlReader = XmlReader.Create(sr,
            new XmlReaderSettings
            {
                IgnoreComments = true, IgnoreWhitespace = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null
            });

        string currentMapName = string.Empty;
        string currentMapUiLabel = string.Empty;
        string currentMapUiCategory = string.Empty;

        while (xmlReader.Read())
        {
            if (xmlReader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (xmlReader.Name.Equals("actionmap", StringComparison.OrdinalIgnoreCase))
            {
                currentMapName = xmlReader.GetAttribute("name") ?? string.Empty;
                currentMapUiLabel = xmlReader.GetAttribute("UILabel") ?? string.Empty;
                currentMapUiCategory = xmlReader.GetAttribute("UICategory") ?? string.Empty;
            }
            else if (xmlReader.Name.Equals("action", StringComparison.OrdinalIgnoreCase))
            {
                KeybindingActionData? action = ParseAction(xmlReader, currentMapName, currentMapUiLabel, currentMapUiCategory,
                    activationModes);
                // Add all valid actions - binding filtering happens after user overrides are applied
                if (action != null)
                {
                    actions.Add(action);
                }
            }
        }

        return actions;
    }

    /// <summary>
    ///     Parses a single action from XML reader.
    /// </summary>
    /// <param name="xmlReader">The XML reader positioned at an action element</param>
    /// <param name="mapName">The current action map name</param>
    /// <param name="mapUILabel">The current action map UI label</param>
    /// <param name="mapUICategory">The current action map UI category</param>
    /// <param name="activationModes">Dictionary of activation mode metadata</param>
    /// <returns>Parsed action data or null if invalid</returns>
    private KeybindingActionData? ParseAction(
        XmlReader xmlReader,
        string mapName,
        string mapUILabel,
        string mapUICategory,
        Dictionary<string, ActivationModeMetadata> activationModes)
    {
        string actionName = xmlReader.GetAttribute("name") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actionName))
        {
            return null;
        }

        string uiLabel = xmlReader.GetAttribute("UILabel") ?? string.Empty;
        if (string.IsNullOrEmpty(uiLabel))
        {
            return null;
        }

        string uiDescription = xmlReader.GetAttribute("UIDescription") ?? string.Empty;

        // Try to get explicit activationMode, otherwise infer from action attributes
        string activationModeStr = xmlReader.GetAttribute("activationMode") ?? string.Empty;
        ActivationMode activationMode;

        if (!string.IsNullOrWhiteSpace(activationModeStr))
        {
            activationMode = ParseActivationMode(activationModeStr);
        }
        else
        {
            // No explicit activationMode - infer from action attributes using exact match first
            activationMode = InferActivationModeFromAttributes(xmlReader, activationModes, actionName);
        }

        // Extract raw binding strings
        string keyboard = (xmlReader.GetAttribute("keyboard") ?? string.Empty).Trim();
        string mouse = (xmlReader.GetAttribute("mouse") ?? string.Empty).Trim();
        string joystick = (xmlReader.GetAttribute("joystick") ?? string.Empty).Trim();
        string gamepad = (xmlReader.GetAttribute("gamepad") ?? string.Empty).Trim();

        // Apply business logic normalization
        (string normalizedKeyboard, string normalizedMouse, string normalizedJoystick, string normalizedGamepad) =
            NormalizeBindings(keyboard, mouse, joystick, gamepad);

        return new KeybindingActionData
        {
            Name = actionName,
            Label = uiLabel,
            Description = uiDescription,
            Category = mapUICategory,
            MapName = mapName,
            MapLabel = mapUILabel,
            ActivationMode = activationMode,
            Bindings = new InputBindings
            {
                Keyboard = string.IsNullOrWhiteSpace(normalizedKeyboard) ? null : normalizedKeyboard,
                Mouse = string.IsNullOrWhiteSpace(normalizedMouse) ? null : normalizedMouse,
                Joystick = string.IsNullOrWhiteSpace(normalizedJoystick) ? null : normalizedJoystick,
                Gamepad = string.IsNullOrWhiteSpace(normalizedGamepad) ? null : normalizedGamepad
            }
        };
    }

    /// <summary>
    ///     Parses activation mode string to enum.
    /// </summary>
    /// <param name="activationModeStr">The activation mode string</param>
    /// <returns>Parsed activation mode</returns>
    private static ActivationMode ParseActivationMode(string activationModeStr)
    {
        if (string.IsNullOrWhiteSpace(activationModeStr))
        {
            return ActivationMode.press;
        }

        if (Enum.TryParse(activationModeStr, true, out ActivationMode mode))
        {
            return mode;
        }

        Logger.Instance.LogMessage(TracingLevel.WARN,
            $"[KeybindingXmlParser] Unknown activation mode '{activationModeStr}', defaulting to 'press'");
        return ActivationMode.press;
    }


    /// <summary>
    ///     Infers activation mode from action attributes when no explicit activationMode is set.
    ///     This heuristic matches Star Citizen's default behavior for actions without explicit mode.
    /// </summary>
    /// <param name="xmlReader">The XML reader positioned at an action element</param>
    /// <param name="activationModes">Dictionary of activation mode metadata</param>
    /// <param name="actionName">Name of the action (for logging)</param>
    /// <returns>Inferred activation mode</returns>
    private static ActivationMode InferActivationModeFromAttributes(
        XmlReader xmlReader,
        Dictionary<string, ActivationModeMetadata> activationModes,
        string actionName)
    {
        bool onPress = xmlReader.GetAttribute("onPress") == "1";
        bool onRelease = xmlReader.GetAttribute("onRelease") == "1";
        bool onHold = xmlReader.GetAttribute("onHold") == "1";
        bool retriggerable = xmlReader.GetAttribute("retriggerable") == "1";

        // Try to find exact match with defined activation modes
        ActivationMode? exactMatch = FindExactModeMatch(onPress, onHold, onRelease, retriggerable, activationModes);
        if (exactMatch != null)
        {
            return exactMatch.Value;
        }

        // Fallback: heuristic if no exact match found
        ActivationMode inferred = InferFromHeuristic(onPress, onHold, onRelease, retriggerable);

        // Log warning if heuristic was used (not exact match)
        /*Logger.Instance.LogMessage(TracingLevel.WARN,
            $"[KeybindingXmlParser] Action '{actionName}' used heuristic activation mode: {inferred} " +
            $"(onPress={onPress}, onHold={onHold}, onRelease={onRelease}, retriggerable={retriggerable})");*/

        return inferred;
    }

    /// <summary>
    ///     Finds exact match of action attributes with defined activation modes.
    /// </summary>
    private static ActivationMode? FindExactModeMatch(
        bool onPress,
        bool onHold,
        bool onRelease,
        bool retriggerable,
        Dictionary<string, ActivationModeMetadata> activationModes)
    {
        foreach (KeyValuePair<string, ActivationModeMetadata> kvp in activationModes)
        {
            ActivationModeMetadata mode = kvp.Value;

            // Skip press/tap modes when we have onPress AND onRelease
            // This indicates hold behavior (key down + key up), not single press
            if (onPress && onRelease && !onHold &&
                (kvp.Key.Contains("press", StringComparison.OrdinalIgnoreCase) ||
                 kvp.Key.Contains("tap", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (mode.OnPress == onPress &&
                mode.OnHold == onHold &&
                mode.OnRelease == onRelease &&
                mode.Retriggerable == retriggerable)
            {
                // Try to parse the mode name to enum
                if (Enum.TryParse(kvp.Key, true, out ActivationMode enumValue))
                {
                    return enumValue;
                }
            }
        }

        return null;
    }

    /// <summary>
    ///     Infers activation mode from attributes using heuristic logic.
    /// </summary>
    private static ActivationMode InferFromHeuristic(
        bool onPress,
        bool onHold,
        bool onRelease,
        bool retriggerable)
    {
        // Default: press (trigger immediately on key down)
        if (onPress && !onRelease && !onHold)
        {
            return ActivationMode.press;
        }

        // Hold behavior: trigger on press and release
        if (onPress && onRelease && !onHold)
        {
            // If retriggerable is true, use hold (retriggerable)
            // Otherwise use hold_no_retrigger
            return retriggerable ? ActivationMode.hold : ActivationMode.hold_no_retrigger;
        }

        // onHold="1" indicates continuous hold behavior
        if (onHold)
        {
            return ActivationMode.hold;
        }

        // onRelease only: tap behavior
        if (onRelease && !onPress)
        {
            return ActivationMode.tap;
        }

        // Fallback to default press mode
        return ActivationMode.press;
    }

    /// <summary>
    ///     Normalizes input bindings by fixing misplaced bindings (e.g., mouse bindings in keyboard field).
    /// </summary>
    /// <param name="keyboard">Keyboard binding string</param>
    /// <param name="mouse">Mouse binding string</param>
    /// <param name="joystick">Joystick binding string</param>
    /// <param name="gamepad">Gamepad binding string</param>
    /// <returns>Normalized bindings</returns>
    private static (string Keyboard, string Mouse, string Joystick, string Gamepad) NormalizeBindings(
        string keyboard,
        string mouse,
        string joystick,
        string gamepad)
    {
        string normalizedKeyboard = keyboard.Trim();
        string normalizedMouse = mouse.Trim();
        string normalizedJoystick = joystick.Trim();
        string normalizedGamepad = gamepad.Trim();

        // Remove HMD_ prefix from keyboard bindings
        if (normalizedKeyboard.StartsWith(InputConstants.Keyboard.HmdPrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalizedKeyboard = string.Empty;
        }

        // Fix: Move misplaced mouse wheel bindings from keyboard to mouse
        if (!string.IsNullOrWhiteSpace(normalizedKeyboard) && normalizedKeyboard.IsMouseWheel())
        {
            normalizedMouse = normalizedKeyboard;
            normalizedKeyboard = string.Empty;
        }

        // Fix: Move misplaced mouse button bindings from keyboard to mouse (unless they have modifiers)
        if (!string.IsNullOrWhiteSpace(normalizedKeyboard) && normalizedKeyboard.IsMouseButton())
        {
            normalizedMouse = normalizedKeyboard;
            normalizedKeyboard = string.Empty;
        }

        return (
            normalizedKeyboard,
            normalizedMouse,
            normalizedJoystick,
            normalizedGamepad
        );
    }
}
