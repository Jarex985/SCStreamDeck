using System.Globalization;
using Newtonsoft.Json.Linq;
using SCStreamDeck.Common;
using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Keybinding;

/// <summary>
///     Builds grouped Property Inspector payload for keybinding functions.
/// </summary>
internal static class FunctionsPayloadBuilder
{
    internal static JArray BuildGroupedFunctionsPayload(
        IEnumerable<KeybindingAction> actions,
        nint hkl)
    {
        ArgumentNullException.ThrowIfNull(actions);

        // Use UILabel and UICategory directly from KeybindingAction
        List<ResolvedAction> resolved = actions
            .Select(a => new ResolvedAction(
                a,
                a.UICategory,
                a.UILabel,
                a.UIDescription))
            .Where(x => !string.IsNullOrWhiteSpace(x.ActionLabel))
            .ToList();

        // Group by ActionName (each action is unique)
        List<GroupedActionEntry> groupedEntries = resolved
            .GroupBy(x => x.Action.ActionName)
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
                    ["value"] = groupedEntry.CanonicalActionName,
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
        // Group by (Category, Label) to find duplicates
        List<IGrouping<(string GroupLabelResolved, string ActionLabelResolved), GroupedActionEntry>> labelGroups = entries
            .GroupBy(e => (e.GroupLabelResolved, e.ActionLabelResolved))
            .Where(g => g.Count() > 1)
            .ToList();

        // First pass: Add disambiguators for duplicate base labels
        foreach (IGrouping<(string GroupLabelResolved, string ActionLabelResolved), GroupedActionEntry> labelGroup in labelGroups)
        {
            List<GroupedActionEntry> actionsInGroup = labelGroup.ToList();
            List<string> actionNames = actionsInGroup.Select(e => e.CanonicalActionName).ToList();

            // Find common prefix among all action names in this group
            string commonPrefix = FindCommonPrefix(actionNames);

            foreach (GroupedActionEntry entry in actionsInGroup)
            {
                // Extract unique suffix from action name
                string uniquePart = entry.CanonicalActionName.Length > commonPrefix.Length
                    ? entry.CanonicalActionName.Substring(commonPrefix.Length)
                    : entry.CanonicalActionName;

                // Clean up and format suffix
                string suffix = FormatSuffix(uniquePart);

                // Update the entry with disambiguated label (without ActivationMode yet)
                // We need to create a new instance since records are immutable
                int index = entries.IndexOf(entry);
                entries[index] = entry with { ActionLabelResolved = $"{entry.ActionLabelResolved} ({suffix})" };
            }
        }

        // Second pass: Add ActivationMode to ALL entries
        for (int i = 0; i < entries.Count; i++)
        {
            GroupedActionEntry entry = entries[i];
            string activationModeLabel = FormatActivationMode(entry.ActivationMode);

            // Format: "Label (Disambiguator - ActivationMode)" or "Label (ActivationMode)"
            string currentLabel = entry.ActionLabelResolved;

            // Check if we already added a disambiguator (ends with ")")
            if (currentLabel.EndsWith(')'))
            {
                // Insert ActivationMode before the closing parenthesis
                // "Label (Disambiguator)" → "Label (Disambiguator - ActivationMode)"
                int lastParen = currentLabel.LastIndexOf(')');
                currentLabel = string.Concat(currentLabel.AsSpan(0, lastParen), $" - {activationModeLabel})");
            }
            else
            {
                // No disambiguator, just add ActivationMode
                // "Label" → "Label (ActivationMode)"
                currentLabel = $"{currentLabel} ({activationModeLabel})";
            }

            entries[i] = entry with { ActionLabelResolved = currentLabel };
        }
    }

    private static string FindCommonPrefix(List<string> strings)
    {
        if (strings.Count <= 1)
        {
            return string.Empty;
        }

        string first = strings[0];
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

        // Trim to last underscore to avoid cutting mid-word
        string prefix = first.Substring(0, prefixLength);
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

    private static string FormatActivationMode(ActivationMode mode) =>
        mode switch
        {
            ActivationMode.tap => "Tap",
            ActivationMode.tap_quicker => "Quick Tap",
            ActivationMode.press => "Press",
            ActivationMode.press_quicker => "Quick Press",
            ActivationMode.hold => "Hold",
            ActivationMode.hold_no_retrigger => "Hold",
            ActivationMode.delayed_press => "Delayed",
            ActivationMode.delayed_press_quicker => "Quick Delay",
            ActivationMode.delayed_press_medium => "Medium Delay",
            ActivationMode.delayed_press_long => "Long Delay",
            ActivationMode.delayed_hold => "Delayed Hold",
            ActivationMode.delayed_hold_long => "Long Hold",
            ActivationMode.delayed_hold_no_retrigger => "Delayed Hold",
            ActivationMode.double_tap => "Double Tap",
            ActivationMode.double_tap_nonblocking => "Double Tap",
            ActivationMode.all => "All",
            ActivationMode.hold_toggle => "Toggle",
            ActivationMode.smart_toggle => "Smart Toggle",
            _ => mode.ToString()
        };

    private static GroupedActionEntry ToGroupedEntry(
        IEnumerable<ResolvedAction> group,
        nint hkl)
    {
        List<ResolvedAction> list = group.ToList();
        string canonical = list.Select(x => x.Action.ActionName).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).First();

        List<BindingDisplay> bindings = new();

        foreach (ResolvedAction item in list)
        {
            KeybindingAction a = item.Action;

            string keyboardRaw = a.KeyboardBinding.Trim();

            // Some "keyboard" bindings in SC can actually represent modifier + mouse axis (e.g. "LAlt + maxis_z")
            if (IsMouseAxis(keyboardRaw))
            {
                AddBinding(bindings, InputDeviceType.MouseAxis, keyboardRaw, keyboardRaw, a.ActionName);
            }
            else
            {
                AddBinding(bindings, InputDeviceType.Keyboard, keyboardRaw,
                    DirectInputDisplayMapper.ToDisplay(keyboardRaw, hkl), a.ActionName);
            }

            // Mouse bindings can be buttons (mouse1/mouse2) or axes (e.g. maxis_z = wheel)
            string mouseRaw = a.MouseBinding.Trim();
            InputDeviceType mouseDevice = IsMouseAxis(mouseRaw) ? InputDeviceType.MouseAxis : InputDeviceType.Mouse;

            AddBinding(bindings, mouseDevice, mouseRaw, mouseRaw, a.ActionName);

            AddBinding(bindings, InputDeviceType.Joystick, a.JoystickBinding, a.JoystickBinding.Trim(), a.ActionName);
            AddBinding(bindings, InputDeviceType.Gamepad, a.GamepadBinding, a.GamepadBinding.Trim(), a.ActionName);
        }

        // Suppress duplicates by (Device, Raw)
        bindings = bindings
            .Where(b => !string.IsNullOrWhiteSpace(b.Raw))
            .GroupBy(b => (b.Device, b.Raw), new DeviceRawTupleComparer())
            .Select(g => g.OrderBy(x => x.Display).First())
            .OrderBy(b => b.Device)
            .ThenBy(b => b.Display)
            .ToList();

        ResolvedAction first = list[0];
        return new GroupedActionEntry(
            first.GroupLabel,
            first.ActionLabel,
            string.IsNullOrWhiteSpace(first.Description) ? null : first.Description,
            canonical,
            bindings,
            first.Action.ActivationMode);
    }

    private static (string text, string searchText, JObject details) BuildPayloadEntry(GroupedActionEntry entry)
    {
        // UX: keep option text compact; show full merged bindings in a separate PI details panel
        if (entry.Bindings.Count == 0)
        {
            return ($"{entry.ActionLabelResolved} (unbound)", string.Empty, new JObject());
        }

        // Minimal device presence summary. Example: "(K+M+J)"
        string deviceSummary = BuildDeviceSummary(entry.Bindings);
        string text = string.IsNullOrWhiteSpace(deviceSummary)
            ? entry.ActionLabelResolved
            : $"{entry.ActionLabelResolved} {deviceSummary}";

        string searchText = BuildSearchText(entry).ToLowerInvariant();

        JObject details = BuildDetailsPayload(entry);

        return (text, searchText, details);
    }

    private static string BuildDeviceSummary(IEnumerable<BindingDisplay> bindings)
    {
        List<InputDeviceType> devices = bindings
            .Where(b => !string.IsNullOrWhiteSpace(b.Raw))
            .Select(b => b.Device)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        return devices.Count == 0 ? string.Empty : $"({string.Join("+", devices.Select(Abbrev))})";

        static string Abbrev(InputDeviceType d) =>
            d switch
            {
                InputDeviceType.Keyboard => "K",
                InputDeviceType.Mouse => "M",
                InputDeviceType.MouseAxis => "Axis",
                InputDeviceType.Joystick => "J",
                InputDeviceType.Gamepad => "G",
                _ => "?"
            };
    }

    private static JObject BuildDetailsPayload(GroupedActionEntry entry)
    {
        JObject details = new()
        {
            ["label"] = entry.ActionLabelResolved,
            ["description"] = entry.DescriptionResolved ?? string.Empty,
            ["canonicalActionName"] = entry.CanonicalActionName,
            ["activationMode"] = entry.ActivationMode.ToString()
        };

        // Group bindings by device for a clean PI view
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

        details["devices"] = devices;
        details["isBound"] = devices.Count > 0;

        // For PI logic: let it tag/disable axis-only options
        details["hasAxis"] = entry.Bindings.Any(b => b.Device == InputDeviceType.MouseAxis);
        details["hasButton"] = entry.Bindings.Any(b => b.Device != InputDeviceType.MouseAxis);

        return details;
    }

    private static bool IsMouseAxis(string raw) =>
        !string.IsNullOrWhiteSpace(raw) && raw.Contains("maxis_", StringComparison.OrdinalIgnoreCase);

    private static string BuildSearchText(GroupedActionEntry entry)
    {
        List<string> parts = new() { entry.ActionLabelResolved, entry.DescriptionResolved ?? string.Empty };

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
        if (bindings.Any(b => b.Device == InputDeviceType.Keyboard && !string.IsNullOrWhiteSpace(b.Raw)))
        {
            return "keyboard";
        }

        if (bindings.Any(b => b.Device == InputDeviceType.Mouse && !string.IsNullOrWhiteSpace(b.Raw)))
        {
            return "mouse";
        }

        if (bindings.Any(b => b.Device == InputDeviceType.MouseAxis && !string.IsNullOrWhiteSpace(b.Raw)))
        {
            return "mouseaxis";
        }

        if (bindings.Any(b => b.Device == InputDeviceType.Joystick && !string.IsNullOrWhiteSpace(b.Raw)))
        {
            return "joystick";
        }

        if (bindings.Any(b => b.Device == InputDeviceType.Gamepad && !string.IsNullOrWhiteSpace(b.Raw)))
        {
            return "gamepad";
        }

        return "unbound";
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
        bool hasKeyboard =
            entry.Bindings.Any(b => b.Device == InputDeviceType.Keyboard && !string.IsNullOrWhiteSpace(b.Raw));
        bool hasMouseButton =
            entry.Bindings.Any(b => b.Device == InputDeviceType.Mouse && !string.IsNullOrWhiteSpace(b.Raw));
        bool hasMouseAxis =
            entry.Bindings.Any(b => b.Device == InputDeviceType.MouseAxis && !string.IsNullOrWhiteSpace(b.Raw));
        bool hasJoystickOrGamepad =
            entry.Bindings.Any(b => b.Device is InputDeviceType.Joystick or InputDeviceType.Gamepad);

        bool enabledForStaticButton = hasKeyboard || hasMouseButton;
        bool disabled = !enabledForStaticButton;

        string disabledReason;
        if (!disabled)
        {
            disabledReason = string.Empty;
        }
        else if (hasMouseAxis)
        {
            disabledReason = "Axis (Dial only)";
        }
        else if (hasJoystickOrGamepad)
        {
            disabledReason = "Controller bind (not supported yet)";
        }
        else
        {
            disabledReason = "No supported binding";
        }

        return (disabled, disabledReason);
    }

    private sealed class DeviceRawTupleComparer : IEqualityComparer<(InputDeviceType Device, string Raw)>
    {
        public bool Equals((InputDeviceType Device, string Raw) x, (InputDeviceType Device, string Raw) y) =>
            x.Device == y.Device && string.Equals(x.Raw, y.Raw, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((InputDeviceType Device, string Raw) obj) =>
            HashCode.Combine(obj.Device, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Raw));
    }
}

internal enum InputDeviceType
{
    Keyboard,
    Mouse,
    MouseAxis,
    Joystick,
    Gamepad,
    Unbound
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
    string CanonicalActionName,
    IReadOnlyList<BindingDisplay> Bindings,
    ActivationMode ActivationMode)
{
    // Mutable label for disambiguation
    public string ActionLabelResolved { get; init; } = ActionLabelResolved;
}
