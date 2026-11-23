using System.Text;
using Google.Protobuf.Reflection;

namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     Discovers displayable attributes from protobuf descriptors, so new fields in the game's protocol show up
///     without code changes. Values are read by <see cref="TelemetryFrameDecoder" />.
/// </summary>
public static class TelemetryFieldCatalog
{
    internal const string GaugeFullName = "sc.game.telemetry.v1.Gauge";
    internal const string PercentSegment = "percent";

    public static IReadOnlyList<TelemetryField> Build(MessageDescriptor root)
    {
        ArgumentNullException.ThrowIfNull(root);

        List<TelemetryField> fields = [];
        Walk(root, string.Empty, [], fields);
        return fields;
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
