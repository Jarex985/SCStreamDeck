using System.Xml;
using BarRaider.SdTools;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;

namespace SCStreamDeck.Common;

/// <summary>
///     Parses user keybinding overrides from Star Citizen's actionmaps.xml file.
/// </summary>
internal sealed class UserOverrideParser
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
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(UserOverrideParser)}] {ex.Message}");
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

        Dictionary<string, KeybindingActionData> actionLookup = BuildActionLookup(actions);

        ApplyBindingOverrides(actionLookup, overrides.Keyboard,
            (action, binding) => action.Bindings.Keyboard = binding);

        ApplyBindingOverrides(actionLookup, overrides.Mouse,
            (action, binding) => action.Bindings.Mouse = binding);

        ApplyBindingOverrides(actionLookup, overrides.Joystick,
            (action, binding) => action.Bindings.Joystick = binding);

        ApplyBindingOverrides(actionLookup, overrides.Gamepad,
            (action, binding) => action.Bindings.Gamepad = binding);
    }

    private static Dictionary<string, KeybindingActionData> BuildActionLookup(List<KeybindingActionData> actions)
    {
        Dictionary<string, KeybindingActionData> actionLookup = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeybindingActionData action in actions)
        {
            actionLookup[action.Name] = action;
        }

        return actionLookup;
    }

    private static void ApplyBindingOverrides(Dictionary<string, KeybindingActionData> actionLookup,
        IReadOnlyDictionary<string, string?> overrides,
        Action<KeybindingActionData, string?> applyBinding)
    {
        foreach ((string actionName, string? binding) in overrides)
        {
            if (actionLookup.TryGetValue(actionName, out KeybindingActionData? action))
            {
                applyBinding(action, binding);
            }
        }
    }


    private static UserOverrides ParseXml(string xmlText)
    {
        Dictionary<string, string?> keyboard = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string?> mouse = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string?> joystick = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string?> gamepad = new(StringComparer.OrdinalIgnoreCase);

        using StringReader sr = new(xmlText);
        using XmlReader xmlReader = XmlReader.Create(sr,
            new XmlReaderSettings
            {
                IgnoreComments = true, IgnoreWhitespace = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null
            });

        while (xmlReader.Read())
        {
            if (!IsActionElement(xmlReader))
            {
                continue;
            }

            string actionName = xmlReader.GetAttribute("name") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(actionName) || xmlReader.IsEmptyElement)
            {
                continue;
            }

            ParseActionRebinds(xmlReader, actionName, keyboard, mouse, joystick, gamepad);
        }


        return new UserOverrides(keyboard, mouse, joystick, gamepad);
    }

    private static void ParseActionRebinds(
        XmlReader xmlReader,
        string actionName,
        Dictionary<string, string?> keyboard,
        Dictionary<string, string?> mouse,
        Dictionary<string, string?> joystick,
        Dictionary<string, string?> gamepad)
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

            ApplyOverride(prefix, actionName, normalized, keyboard, mouse, joystick, gamepad);
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
        string actionName,
        string normalized,
        Dictionary<string, string?> keyboard,
        Dictionary<string, string?> mouse,
        Dictionary<string, string?> joystick,
        Dictionary<string, string?> gamepad)
    {
        switch (prefix)
        {
            case "kb":
                keyboard[actionName] = normalized;
                break;
            case "mo":
                mouse[actionName] = normalized;
                break;
            case "js":
                joystick[actionName] = normalized;
                break;
            case "gp":
                gamepad[actionName] = normalized;
                break;
        }
    }

    private static bool TryValidatePath(string actionMapsPath, out string validPath)
    {
        if (!SecurePathValidator.TryNormalizePath(actionMapsPath, out validPath))
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(UserOverrideParser)}] {ErrorMessages.InvalidPath} {actionMapsPath}");
            return false;
        }

        if (!File.Exists(validPath))
        {
            return false;
        }

        return true;
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
    IReadOnlyDictionary<string, string?> Gamepad)
{
    /// <summary>
    ///     Gets the total number of overrides across all input types.
    /// </summary>
    private int TotalCount => Keyboard.Count + Mouse.Count + Joystick.Count + Gamepad.Count;

    /// <summary>
    ///     Returns true if there are any overrides.
    /// </summary>
    public bool HasOverrides => TotalCount > 0;
}
