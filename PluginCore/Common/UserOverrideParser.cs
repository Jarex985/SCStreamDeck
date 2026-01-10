using System.Xml;
using BarRaider.SdTools;
using SCStreamDeck.SCCore.Logging;
using SCStreamDeck.SCCore.Models;

namespace SCStreamDeck.SCCore.Common;

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
    public UserOverrides? Parse(string actionMapsPath)
    {
        if (!SecurePathValidator.TryNormalizePath(actionMapsPath, out var validPath))
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[UserOverrideParser] {ErrorMessages.InvalidPath}: {actionMapsPath}");
            return null;
        }

        if (!File.Exists(validPath)) return null;

        try
        {
            var xmlText = File.ReadAllText(validPath);
            return ParseXml(xmlText);
        }

        catch (IOException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"[UserOverrideParser] Failed to read actionmaps.xml: {ex.Message}");
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"[UserOverrideParser] Access denied to actionmaps.xml: {ex.Message}");
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

        // Build lookup - handle duplicates by keeping last occurrence
        var actionLookup = new Dictionary<string, KeybindingActionData>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in actions) actionLookup[action.Name] = action;

        ApplyBindingOverrides(actionLookup, overrides.Keyboard,
            (action, binding) => action.Bindings.Keyboard = binding);

        ApplyBindingOverrides(actionLookup, overrides.Mouse,
            (action, binding) => action.Bindings.Mouse = binding);

        ApplyBindingOverrides(actionLookup, overrides.Joystick,
            (action, binding) => action.Bindings.Joystick = binding);

        ApplyBindingOverrides(actionLookup, overrides.Gamepad,
            (action, binding) => action.Bindings.Gamepad = binding);
    }

    private static void ApplyBindingOverrides(Dictionary<string, KeybindingActionData> actionLookup,
        IReadOnlyDictionary<string, string?> overrides,
        Action<KeybindingActionData, string?> applyBinding)
    {
        foreach (var (actionName, binding) in overrides)
            if (actionLookup.TryGetValue(actionName, out var action))
                applyBinding(action, binding);
    }

    private UserOverrides ParseXml(string xmlText)
    {
        var keyboard = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var mouse = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var joystick = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var gamepad = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        using var sr = new StringReader(xmlText);
        using var xmlReader = XmlReader.Create(sr, new XmlReaderSettings
        {
            IgnoreComments = true,
            IgnoreWhitespace = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });

        while (xmlReader.Read())
        {
            if (xmlReader.NodeType != XmlNodeType.Element) continue;
            if (!xmlReader.Name.Equals("action", StringComparison.OrdinalIgnoreCase)) continue;

            var actionName = xmlReader.GetAttribute("name") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(actionName)) continue;
            if (xmlReader.IsEmptyElement) continue;

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
        var depth = xmlReader.Depth;

        while (xmlReader.Read())
        {
            // Stop when we reach the closing </action> tag
            if (xmlReader.NodeType == XmlNodeType.EndElement &&
                xmlReader.Depth == depth &&
                xmlReader.Name.Equals("action", StringComparison.OrdinalIgnoreCase))
                break;

            if (xmlReader.NodeType != XmlNodeType.Element) continue;
            if (!xmlReader.Name.Equals("rebind", StringComparison.OrdinalIgnoreCase)) continue;

            var input = xmlReader.GetAttribute("input") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input)) continue;

            // Parse input format: "kb1_apostrophe" -> prefix="kb", suffix="apostrophe"
            var prefix = input.Length >= 2 ? input[..2].ToLowerInvariant() : string.Empty;
            var normalized = NormalizeInputSuffix(input);

            if (string.IsNullOrWhiteSpace(normalized)) continue;

            // Map to appropriate dictionary based on prefix
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
    }

    private static string NormalizeInputSuffix(string input)
    {
        var idx = input.IndexOf('_');
        if (idx < 0 || idx == input.Length - 1) return string.Empty;

        return input[(idx + 1)..].Trim();
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