using System.Globalization;
using Newtonsoft.Json.Linq;
using SCStreamDeck.Common;
using SCStreamDeck.Models;
using InputDeviceType = SCStreamDeck.Models.InputType;

namespace SCStreamDeck.Services.Keybinding;

/// <summary>
///     Builds grouped Property Inspector payload for keybinding functions.
/// </summary>
internal static class FunctionsPayloadBuilder
{
    private const string MouseAxisToken = "maxis_";

    private const string BindingTypeKeyboard = "keyboard";
    private const string BindingTypeMouse = "mouse";
    private const string BindingTypeMouseAxis = "mouseaxis";
    private const string BindingTypeJoystick = "joystick";
    private const string BindingTypeGamepad = "gamepad";
    private const string BindingTypeUnbound = "unbound";

    private const string DisabledReasonAxisOnly = "Axis (Dial only)";
    private const string DisabledReasonControllerOnly = "Controller bind (not supported yet)";
    private const string DisabledReasonNoSupportedBinding = "No supported binding";

    internal static JArray BuildGroupedFunctionsPayload(
        IEnumerable<KeybindingAction> actions,
        nint hkl)
    {
        ArgumentNullException.ThrowIfNull(actions);

        // Use UILabel and UICategory directly from KeybindingAction
        List<ResolvedAction> resolved = actions
            .Select(a => new ResolvedAction(
                a,
                a.UiCategory,
                a.UiLabel,
                a.UiDescription))
            .Where(x => !string.IsNullOrWhiteSpace(x.ActionLabel))
            .ToList();

        // Group by (ActionName, Category) to handle duplicates across different maps
        List<GroupedActionEntry> groupedEntries = resolved
            .GroupBy(x => (x.Action.ActionName, x.GroupLabel))
            .Select(g => ToGroupedEntry(g, hkl))
            .ToList();

        // Disambiguate duplicate labels within same category
        DisambiguateDuplicateLabels(groupedEntries);

        // Group by category for optgroup structure
        IEnumerable<IGrouping<string, GroupedActionEntry>> grouped = groupedEntries
            .OrderBy(x => x.GroupLabelResolved)
            .ThenBy(x => x.ActionLabelResolved)
            .GroupBy(x => x.GroupLabelResolved);

        JArray groups = new();

        foreach (IGrouping<string, GroupedActionEntry> categoryGroup in grouped)
        {
            JArray options = new();

            foreach (GroupedActionEntry groupedEntry in categoryGroup.OrderBy(e => e.ActionLabelResolved))
            {
                (string text, string searchText, JObject details) = BuildPayloadEntry(groupedEntry);

                (bool disabled, string disabledReason) = GetDisabledStatus(groupedEntry);

                options.Add(new JObject
                {
                    ["value"] = $"{groupedEntry.ActionName}_{groupedEntry.GroupLabelResolved}",
                    ["text"] = text,
                    ["bindingType"] = InferBindingType(groupedEntry.Bindings),
                    ["searchText"] = searchText,

                    // Optional richer details for modern PI rendering
                    ["details"] = details,

                    // PI hinting / UX
                    ["disabled"] = disabled,
                    ["disabledReason"] = disabledReason
                });
            }

            groups.Add(new JObject { ["label"] = categoryGroup.Key, ["options"] = options });
        }

        return groups;
    }

    private static void DisambiguateDuplicateLabels(List<GroupedActionEntry> entries)
    {
        ApplyDuplicateLabelDisambiguators(entries);
        AppendActivationModeLabels(entries);
    }

    private static void ApplyDuplicateLabelDisambiguators(List<GroupedActionEntry> entries)
    {
        // Group by (Category, Label) to find duplicates
        List<IGrouping<(string GroupLabelResolved, string ActionLabelResolved), GroupedActionEntry>> labelGroups = entries
            .GroupBy(e => (e.GroupLabelResolved, e.ActionLabelResolved))
            .Where(g => g.Count() > 1)
            .ToList();

        // First pass: Add disambiguators for duplicate base labels
        foreach (IGrouping<(string GroupLabelResolved, string ActionLabelResolved), GroupedActionEntry> labelGroup in labelGroups)
        {
            List<GroupedActionEntry> actionsInGroup = labelGroup.ToList();
            List<string> actionNames = actionsInGroup.Select(e => e.ActionName).ToList();
            string commonPrefix = FindCommonPrefix(actionNames);

            foreach (GroupedActionEntry entry in actionsInGroup)
            {
                string suffix = BuildDisambiguatorSuffix(entry.ActionName, commonPrefix);

                // Update the entry with disambiguated label (without ActivationMode yet)
                // We need to create a new instance since records are immutable
                int index = entries.IndexOf(entry);
                entries[index] = entry with { ActionLabelResolved = $"{entry.ActionLabelResolved} ({suffix})" };
            }
        }
    }

    private static string BuildDisambiguatorSuffix(string actionName, string commonPrefix)
    {
        string uniquePart = actionName.Length > commonPrefix.Length
            ? actionName.Substring(commonPrefix.Length)
            : actionName;

        return FormatSuffix(uniquePart);
    }

    private static void AppendActivationModeLabels(List<GroupedActionEntry> entries)
    {
        // Second pass: Add ActivationMode to ALL entries
        for (int i = 0; i < entries.Count; i++)
        {
            GroupedActionEntry entry = entries[i];
            string activationModeLabel = FormatActivationMode(entry.ActivationMode);
            string currentLabel = AppendActivationModeLabel(entry.ActionLabelResolved, activationModeLabel);

            entries[i] = entry with { ActionLabelResolved = currentLabel };
        }
    }

    private static string AppendActivationModeLabel(string currentLabel, string activationModeLabel)
    {
        // Format: "Label (Disambiguator - ActivationMode)" or "Label (ActivationMode)"
        if (currentLabel.EndsWith(')'))
        {
            // Insert ActivationMode before the closing parenthesis
            // "Label (Disambiguator)" → "Label (Disambiguator - ActivationMode)"
            int lastParen = currentLabel.LastIndexOf(')');
            return string.Concat(currentLabel.AsSpan(0, lastParen), $" - {activationModeLabel})");
        }

        // No disambiguator, just add ActivationMode
        // "Label" → "Label (ActivationMode)"
        return $"{currentLabel} ({activationModeLabel})";
    }

    private static string FindCommonPrefix(List<string> strings)
    {
        if (strings.Count <= 1)
        {
            return string.Empty;
        }

        string first = strings[0];
        int prefixLength = GetSharedPrefixLength(strings, first);

        // Trim to last underscore to avoid cutting mid-word
        string prefix = first.Substring(0, prefixLength);
        return TrimPrefixToUnderscoreBoundary(prefix);
    }

    private static int GetSharedPrefixLength(List<string> strings, string first)
    {
        int prefixLength = 0;

        for (int i = 0; i < first.Length; i++)
        {
            if (strings.All(s => s.Length > i && s[i] == first[i]))
            {
                prefixLength = i + 1;
            }
            else
            {
                break;
            }
        }

        return prefixLength;
    }

    private static string TrimPrefixToUnderscoreBoundary(string prefix)
    {
        int lastUnderscore = prefix.LastIndexOf('_');
        return lastUnderscore > 0 ? prefix.Substring(0, lastUnderscore + 1) : prefix;
    }

    private static string FormatSuffix(string suffix)
    {
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return "Unknown";
        }

        // Remove leading/trailing underscores
        suffix = suffix.Trim('_');

        // Replace underscores with spaces
        suffix = suffix.Replace('_', ' ');

        // Convert to Title Case (culture-invariant)
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(suffix.ToLowerInvariant());
    }

    /// <summary>
    ///     Formats an <see cref="ActivationMode" /> enum value into a user-friendly label for display.
    ///     Some modes share the same label (e.g., hold, hold_no_retrigger both return "Hold").
    ///     Unknown modes fall back to enum value's string representation.
    /// </summary>
    /// <param name="mode">The activation mode to format.</param>
    /// <returns>A user-friendly label for the activation mode.</returns>
    internal static string FormatActivationMode(ActivationMode mode) =>
        mode switch
        {
            ActivationMode.tap => LabelTap,
            ActivationMode.tap_quicker => LabelQuickTap,
            ActivationMode.press => LabelPress,
            ActivationMode.press_quicker => LabelQuickPress,
            ActivationMode.hold => LabelHold,
            ActivationMode.hold_no_retrigger => LabelHold,
            ActivationMode.delayed_press => LabelDelayed,
            ActivationMode.delayed_press_quicker => LabelQuickDelay,
            ActivationMode.delayed_press_medium => LabelMediumDelay,
            ActivationMode.delayed_press_long => LabelLongDelay,
            ActivationMode.delayed_hold => LabelDelayedHold,
            ActivationMode.delayed_hold_long => LabelLongHold,
            ActivationMode.delayed_hold_no_retrigger => LabelDelayedHold,
            ActivationMode.double_tap => LabelDoubleTap,
            ActivationMode.double_tap_nonblocking => LabelDoubleTap,
            ActivationMode.all => LabelAll,
            ActivationMode.hold_toggle => LabelToggle,
            ActivationMode.smart_toggle => LabelSmartToggle,
            _ => mode.ToString()
        };

    private static GroupedActionEntry ToGroupedEntry(
        IEnumerable<ResolvedAction> group,
        nint hkl)
    {
        List<ResolvedAction> list = group.ToList();
        string name = list.Select(x => x.Action.ActionName).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).First();

        List<BindingDisplay> bindings = CollectBindings(list, hkl);
        bindings = NormalizeBindings(bindings);

        ResolvedAction first = list[0];
        return new GroupedActionEntry(
            first.GroupLabel,
            first.ActionLabel,
            string.IsNullOrWhiteSpace(first.Description) ? null : first.Description,
            name,
            bindings,
            first.Action.ActivationMode);
    }

    private static List<BindingDisplay> CollectBindings(List<ResolvedAction> actions, nint hkl)
    {
        List<BindingDisplay> bindings = [];

        foreach (ResolvedAction item in actions)
        {
            KeybindingAction action = item.Action;
            AddKeyboardBinding(bindings, action, hkl);
            AddMouseBinding(bindings, action);
            AddBinding(bindings, InputDeviceType.Joystick, action.JoystickBinding, action.JoystickBinding, action.ActionName);
            AddBinding(bindings, InputDeviceType.Gamepad, action.GamepadBinding, action.GamepadBinding, action.ActionName);
        }

        return bindings;
    }

    private static void AddKeyboardBinding(List<BindingDisplay> bindings, KeybindingAction action, nint hkl)
    {
        string keyboardRaw = action.KeyboardBinding.Trim();

        if (IsMouseAxis(keyboardRaw))
        {
            AddBinding(bindings, InputDeviceType.MouseAxis, keyboardRaw, keyboardRaw, action.ActionName);
            return;
        }

        AddBinding(bindings, InputDeviceType.Keyboard, keyboardRaw,
            DirectInputDisplayMapper.ToDisplay(keyboardRaw, hkl), action.ActionName);
    }

    private static void AddMouseBinding(List<BindingDisplay> bindings, KeybindingAction action)
    {
        string mouseRaw = action.MouseBinding.Trim();
        InputDeviceType mouseDevice = IsMouseAxis(mouseRaw) ? InputDeviceType.MouseAxis : InputDeviceType.Mouse;

        AddBinding(bindings, mouseDevice, mouseRaw, mouseRaw, action.ActionName);
    }

    private static List<BindingDisplay> NormalizeBindings(List<BindingDisplay> bindings) =>
        // Suppress duplicates by (Device, Raw)
        bindings
            .Where(b => !string.IsNullOrWhiteSpace(b.Raw))
            .GroupBy(b => (b.Device, b.Raw), new DeviceRawTupleComparer())
            .Select(g => g.OrderBy(x => x.Display).First())
            .OrderBy(b => b.Device)
            .ThenBy(b => b.Display)
            .ToList();

    private static (string text, string searchText, JObject details) BuildPayloadEntry(GroupedActionEntry entry)
    {
        string text = entry.ActionLabelResolved;
        string searchText = BuildSearchText(entry).ToLowerInvariant();
        JObject details = BuildDetailsPayload(entry);

        return (text, searchText, details);
    }


    private static JObject BuildDetailsPayload(GroupedActionEntry entry)
    {
        JObject details = new()
        {
            ["label"] = entry.ActionLabelResolved,
            ["description"] = entry.DescriptionResolved ?? string.Empty,
            ["actionName"] = entry.ActionName,
            ["activationMode"] = entry.ActivationMode.ToString()
        };


        IOrderedEnumerable<IGrouping<InputDeviceType, BindingDisplay>> byDevice = entry.Bindings
            .Where(b => !string.IsNullOrWhiteSpace(b.Raw))
            .GroupBy(b => b.Device)
            .OrderBy(g => g.Key);

        JArray devices = new();

        foreach (IGrouping<InputDeviceType, BindingDisplay> g in byDevice)
        {
            IEnumerable<JObject> bindings = g
                .OrderBy(b => b.Display)
                .ThenBy(b => b.Raw)
                .Select(b => new JObject { ["raw"] = b.Raw, ["display"] = b.Display, ["sourceActionName"] = b.SourceActionName });

            devices.Add(new JObject { ["device"] = g.Key.ToString(), ["bindings"] = new JArray(bindings) });
        }

        // TODO: Come back later when able to test Dials
        details["devices"] = devices;
        details["isBound"] = devices.Count > 0;

        // For PI logic: let it tag/disable axis-only options
        details["hasAxis"] = entry.Bindings.Any(b => b.Device == InputDeviceType.MouseAxis);
        details["hasButton"] = entry.Bindings.Any(b => b.Device != InputDeviceType.MouseAxis);

        return details;
    }

    private static bool IsMouseAxis(string raw) =>
        !string.IsNullOrWhiteSpace(raw) && raw.Contains(MouseAxisToken, StringComparison.OrdinalIgnoreCase);

    private static string BuildSearchText(GroupedActionEntry entry)
    {
        List<string> parts = [entry.ActionLabelResolved, entry.DescriptionResolved ?? string.Empty];

        foreach (BindingDisplay b in entry.Bindings)
        {
            parts.Add(b.Display);
            parts.Add(b.Raw);
            parts.Add(b.SourceActionName);
        }

        return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static string InferBindingType(IReadOnlyList<BindingDisplay> bindings)
    {
        BindingPresence presence = BindingPresence.FromBindings(bindings);

        if (presence.HasKeyboard)
        {
            return BindingTypeKeyboard;
        }

        if (presence.HasMouseButton)
        {
            return BindingTypeMouse;
        }

        if (presence.HasMouseAxis)
        {
            return BindingTypeMouseAxis;
        }

        if (presence.HasJoystick)
        {
            return BindingTypeJoystick;
        }

        if (presence.HasGamepad)
        {
            return BindingTypeGamepad;
        }

        return BindingTypeUnbound;
    }

    private static void AddBinding(
        List<BindingDisplay> target,
        InputDeviceType device,
        string? raw,
        string? display,
        string sourceActionName)
    {
        raw = raw?.Trim() ?? string.Empty;
        display = display?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        target.Add(new BindingDisplay(device, raw, display, sourceActionName));
    }

    private static (bool Disabled, string DisabledReason) GetDisabledStatus(GroupedActionEntry entry)
    {
        // Only enable actions that have a binding we can realistically send as a button
        BindingPresence presence = BindingPresence.FromBindings(entry.Bindings);

        bool enabledForStaticButton = presence.HasKeyboard || presence.HasMouseButton;
        bool disabled = !enabledForStaticButton;

        string disabledReason;
        if (!disabled)
        {
            disabledReason = string.Empty;
        }
        else if (presence.HasMouseAxis)
        {
            disabledReason = DisabledReasonAxisOnly;
        }
        else if (presence.HasJoystick || presence.HasGamepad)
        {
            // TODO: Support controller binds once execution paths can handle them.
            disabledReason = DisabledReasonControllerOnly;
        }
        else
        {
            disabledReason = DisabledReasonNoSupportedBinding;
        }

        return (disabled, disabledReason);
    }

    private readonly record struct BindingPresence(
        bool HasKeyboard,
        bool HasMouseButton,
        bool HasMouseAxis,
        bool HasJoystick,
        bool HasGamepad)
    {
        public static BindingPresence FromBindings(IReadOnlyList<BindingDisplay> bindings)
        {
            bool hasKeyboard = false;
            bool hasMouseButton = false;
            bool hasMouseAxis = false;
            bool hasJoystick = false;
            bool hasGamepad = false;

            foreach (BindingDisplay binding in bindings)
            {
                if (string.IsNullOrWhiteSpace(binding.Raw))
                {
                    continue;
                }

                switch (binding.Device)
                {
                    case InputDeviceType.Keyboard:
                        hasKeyboard = true;
                        break;
                    case InputDeviceType.Mouse:
                        hasMouseButton = true;
                        break;
                    case InputDeviceType.MouseAxis:
                        hasMouseAxis = true;
                        break;
                    case InputDeviceType.Joystick:
                        hasJoystick = true;
                        break;
                    case InputDeviceType.Gamepad:
                        hasGamepad = true;
                        break;
                }

                if (hasKeyboard && hasMouseButton && hasMouseAxis && hasJoystick && hasGamepad)
                {
                    break;
                }
            }

            return new BindingPresence(hasKeyboard, hasMouseButton, hasMouseAxis, hasJoystick, hasGamepad);
        }
    }

    private sealed class DeviceRawTupleComparer : IEqualityComparer<(InputDeviceType Device, string Raw)>
    {
        public bool Equals((InputDeviceType Device, string Raw) x, (InputDeviceType Device, string Raw) y) =>
            x.Device == y.Device && string.Equals(x.Raw, y.Raw, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((InputDeviceType Device, string Raw) obj) =>
            HashCode.Combine(obj.Device, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Raw));
    }

    #region Activation Mode Labels

    private const string LabelTap = "Tap";
    private const string LabelQuickTap = "Quick Tap";
    private const string LabelPress = "Press";
    private const string LabelQuickPress = "Quick Press";
    private const string LabelHold = "Hold";
    private const string LabelDoubleTap = "Double Tap";
    private const string LabelAll = "All";
    private const string LabelToggle = "Toggle";
    private const string LabelSmartToggle = "Smart Toggle";
    private const string LabelDelayed = "Delayed";
    private const string LabelQuickDelay = "Quick Delay";
    private const string LabelMediumDelay = "Medium Delay";
    private const string LabelLongDelay = "Long Delay";
    private const string LabelDelayedHold = "Delayed Hold";
    private const string LabelLongHold = "Long Hold";

    #endregion
}

/// <summary>
///     Strongly-typed intermediate representation for resolved action data.
///     Replaces dynamic typing for compile-time null checks and type safety.
/// </summary>
internal sealed record ResolvedAction(
    KeybindingAction Action,
    string GroupLabel,
    string ActionLabel,
    string Description);

internal sealed record BindingDisplay(
    InputDeviceType Device,
    string Raw,
    string Display,
    string SourceActionName);

internal sealed record GroupedActionEntry(
    string GroupLabelResolved,
    string ActionLabelResolved,
    string? DescriptionResolved,
    string ActionName,
    IReadOnlyList<BindingDisplay> Bindings,
    ActivationMode ActivationMode);
