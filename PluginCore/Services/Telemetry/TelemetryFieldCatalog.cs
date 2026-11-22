using System.Globalization;
using System.Text;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Sc.Game.Telemetry.V1;

namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     Discovers displayable attributes from the protobuf descriptors and reads their values from messages,
///     so new fields in the proto show up without code changes.
/// </summary>
public static class TelemetryFieldCatalog
{
    private const string GaugeFullName = "sc.game.telemetry.v1.Gauge";
    private const string PercentSegment = "percent";

    private static readonly Lazy<IReadOnlyList<TelemetryField>> s_default =
        new(() => Build(TelemetryStreamResponse.Descriptor));

    public static IReadOnlyList<TelemetryField> Default => s_default.Value;

    public static TelemetryField? Find(string? path) =>
        string.IsNullOrWhiteSpace(path)
            ? null
            : Default.FirstOrDefault(f => string.Equals(f.Path, path, StringComparison.Ordinal));

    public static IReadOnlyList<TelemetryField> Build(MessageDescriptor root)
    {
        ArgumentNullException.ThrowIfNull(root);

        List<TelemetryField> fields = [];
        Walk(root, string.Empty, [], fields);
        return fields;
    }

    /// <summary>
    ///     Reads the value at <paramref name="path" />. Returns false when any message on the way is absent
    ///     (e.g. no vehicle frame yet) or the path does not exist.
    /// </summary>
    public static bool TryGetValue(IMessage? root, string? path, out TelemetryValue value)
    {
        value = default;
        if (root == null || string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string[] segments = path.Split('.');
        IMessage current = root;

        for (int i = 0; i < segments.Length; i++)
        {
            bool isLast = i == segments.Length - 1;

            if (isLast && segments[i] == PercentSegment && current.Descriptor.FullName == GaugeFullName)
            {
                return TryGetGaugePercent(current, out value);
            }

            FieldDescriptor? field = current.Descriptor.FindFieldByName(segments[i]);
            if (field == null || field.IsRepeated || field.IsMap)
            {
                return false;
            }

            if (field.FieldType == FieldType.Message)
            {
                if (isLast || !field.Accessor.HasValue(current) || field.Accessor.GetValue(current) is not IMessage next)
                {
                    return false;
                }

                current = next;
                continue;
            }

            if (!isLast)
            {
                return false;
            }

            if (field.HasPresence && !field.Accessor.HasValue(current))
            {
                return false;
            }

            return TryConvert(field, field.Accessor.GetValue(current), out value);
        }

        return false;
    }

    internal static string HumanizeEnumValue(EnumDescriptor enumType, string valueName)
    {
        string prefix = ToUpperSnake(enumType.Name) + "_";
        string trimmed = valueName.StartsWith(prefix, StringComparison.Ordinal) ? valueName[prefix.Length..] : valueName;
        return TitleCase(trimmed.Split('_', StringSplitOptions.RemoveEmptyEntries));
    }

    private static void Walk(MessageDescriptor message, string prefix, HashSet<string> visiting, List<TelemetryField> output)
    {
        if (!visiting.Add(message.FullName))
        {
            return;
        }

        foreach (FieldDescriptor field in message.Fields.InFieldNumberOrder())
        {
            if (field.IsRepeated || field.IsMap)
            {
                continue;
            }

            string path = prefix.Length == 0 ? field.Name : $"{prefix}.{field.Name}";

            if (field.FieldType == FieldType.Message)
            {
                if (field.MessageType.FullName == GaugeFullName)
                {
                    AddGauge(path, output);
                }
                else
                {
                    Walk(field.MessageType, path, visiting, output);
                }

                continue;
            }

            output.Add(CreateField(field, path));
        }

        visiting.Remove(message.FullName);
    }

    private static void AddGauge(string path, List<TelemetryField> output)
    {
        output.Add(new TelemetryField($"{path}.current", Label(path, "current"), Group(path), TelemetryValueKind.Number, false, null));
        output.Add(new TelemetryField($"{path}.max", Label(path, "max"), Group(path), TelemetryValueKind.Number, false, null));
        output.Add(new TelemetryField($"{path}.{PercentSegment}", Label(path, "percent"), Group(path), TelemetryValueKind.Number, false, null));
    }

    private static TelemetryField CreateField(FieldDescriptor field, string path)
    {
        string group = Group(path);
        string label = TitleCase(field.Name.Split('_', StringSplitOptions.RemoveEmptyEntries));

        switch (field.FieldType)
        {
            case FieldType.Bool:
                return new TelemetryField(path, label, group, TelemetryValueKind.Boolean, false, null);
            case FieldType.String:
                return new TelemetryField(path, label, group, TelemetryValueKind.Text, false, null);
            case FieldType.Enum:
                List<TelemetryEnumOption> options = field.EnumType.Values
                    .Select(v => new TelemetryEnumOption(v.Name, HumanizeEnumValue(field.EnumType, v.Name)))
                    .ToList();
                return new TelemetryField(path, label, group, TelemetryValueKind.Enum, false, options);
            case FieldType.Float:
            case FieldType.Double:
                return new TelemetryField(path, label, group, TelemetryValueKind.Number, false, null);
            default:
                return new TelemetryField(path, label, group, TelemetryValueKind.Number, true, null);
        }
    }

    private static bool TryConvert(FieldDescriptor field, object? raw, out TelemetryValue value)
    {
        value = default;
        switch (field.FieldType)
        {
            case FieldType.Bool when raw is bool b:
                value = new TelemetryValue(TelemetryValueKind.Boolean, Boolean: b, DisplayText: b ? "On" : "Off");
                return true;
            case FieldType.String when raw is string s:
                value = new TelemetryValue(TelemetryValueKind.Text, Text: s, DisplayText: s);
                return true;
            case FieldType.Enum when raw != null:
                int number = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                EnumValueDescriptor? member = field.EnumType.FindValueByNumber(number);
                string name = member?.Name ?? number.ToString(CultureInfo.InvariantCulture);
                string display = member == null ? name : HumanizeEnumValue(field.EnumType, name);
                value = new TelemetryValue(TelemetryValueKind.Enum, Text: name, DisplayText: display);
                return true;
            case FieldType.Float or FieldType.Double when raw is IConvertible floating:
                double fp = floating.ToDouble(CultureInfo.InvariantCulture);
                if (!double.IsFinite(fp))
                {
                    return false;
                }

                value = new TelemetryValue(TelemetryValueKind.Number, fp);
                return true;
            case FieldType.Message or FieldType.Group or FieldType.Bytes:
                return false;
            default:
                if (raw is IConvertible integral)
                {
                    value = new TelemetryValue(TelemetryValueKind.Number, integral.ToDouble(CultureInfo.InvariantCulture),
                        IsInteger: true);
                    return true;
                }

                return false;
        }
    }

    private static bool TryGetGaugePercent(IMessage gauge, out TelemetryValue value)
    {
        value = default;
        if (gauge is not Gauge g || g.Max <= 0 || !float.IsFinite(g.Current) || !float.IsFinite(g.Max))
        {
            return false;
        }

        value = new TelemetryValue(TelemetryValueKind.Number, Math.Clamp(g.Current / g.Max * 100.0, 0, 100));
        return true;
    }

    private static string Group(string path)
    {
        int lastDot = path.LastIndexOf('.');
        string container = lastDot < 0 ? path : path[..lastDot];
        return TitleCase(container.Split(['.', '_'], StringSplitOptions.RemoveEmptyEntries));
    }

    private static string Label(string gaugePath, string part)
    {
        string name = gaugePath[(gaugePath.LastIndexOf('.') + 1)..];
        return $"{TitleCase(name.Split('_', StringSplitOptions.RemoveEmptyEntries))} ({part})";
    }

    private static string TitleCase(IEnumerable<string> words) =>
        string.Join(' ', words.Select(w => char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));

    private static string ToUpperSnake(string pascal)
    {
        StringBuilder sb = new();
        for (int i = 0; i < pascal.Length; i++)
        {
            if (i > 0 && char.IsUpper(pascal[i]))
            {
                sb.Append('_');
            }

            sb.Append(char.ToUpperInvariant(pascal[i]));
        }

        return sb.ToString();
    }
}
