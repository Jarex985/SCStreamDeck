using System.Globalization;
using System.Text;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     Renders a raw telemetry frame as text by field number, independent of what the schema says: fields the
///     schema does not know are marked, and fields the schema defines but the frame lacks are listed (proto3 does
///     not send a scalar whose value is 0). Used for troubleshooting "why does attribute X not show up".
/// </summary>
public static class TelemetryRawFrameDumper
{
    private const int MaxDepth = 32;
    private const int MaxHexBytes = 64;

    /// <summary>
    ///     Renders <paramref name="frame" />. When <paramref name="sentPaths" /> is given it receives the attribute path
    ///     (as used by <see cref="TelemetrySchema.Fields" />) of every scalar the frame contained.
    /// </summary>
    public static string Dump(MessageDescriptor? root, byte[] frame, ISet<string>? sentPaths = null)
    {
        ArgumentNullException.ThrowIfNull(frame);

        StringBuilder text = new();
        Walk(root, frame, string.Empty, 0, text, sentPaths);
        return text.ToString();
    }

    /// <summary>
    ///     Collects the attribute paths a frame contained without rendering any text.
    /// </summary>
    public static void CollectSentPaths(MessageDescriptor root, byte[] frame, ISet<string> sentPaths)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(sentPaths);

        Walk(root, frame, string.Empty, 0, null, sentPaths);
    }

    private readonly record struct RawField(int Number, WireFormat.WireType Wire, ulong Value, byte[] Bytes);

    private static bool TryParse(byte[] data, out List<RawField> fields)
    {
        fields = [];
        try
        {
            CodedInputStream input = new(data);
            uint tag;
            while ((tag = input.ReadTag()) != 0)
            {
                int number = WireFormat.GetTagFieldNumber(tag);
                WireFormat.WireType wire = WireFormat.GetTagWireType(tag);
                switch (wire)
                {
                    case WireFormat.WireType.Varint:
                        fields.Add(new RawField(number, wire, input.ReadUInt64(), []));
                        break;
                    case WireFormat.WireType.Fixed32:
                        fields.Add(new RawField(number, wire, input.ReadFixed32(), []));
                        break;
                    case WireFormat.WireType.Fixed64:
                        fields.Add(new RawField(number, wire, input.ReadFixed64(), []));
                        break;
                    case WireFormat.WireType.LengthDelimited:
                        fields.Add(new RawField(number, wire, 0, input.ReadBytes().ToByteArray()));
                        break;
                    default:
                        return false;
                }
            }

            return true;
        }
        catch (InvalidProtocolBufferException)
        {
            return false;
        }
    }

    private static void Walk(
        MessageDescriptor? descriptor, byte[] data, string path, int indent, StringBuilder? text, ISet<string>? sent)
    {
        if (!TryParse(data, out List<RawField> fields))
        {
            Line(text, indent, $"(cannot decode {data.Length} bytes: {Hex(data)})");
            return;
        }

        HashSet<int> seen = [];
        foreach (RawField raw in fields)
        {
            seen.Add(raw.Number);
            FieldDescriptor? field = descriptor?.FindFieldByNumber(raw.Number);
            string name = field?.Name ?? $"#{raw.Number}";
            string childPath = path.Length == 0 ? name : $"{path}.{name}";
            string flag = descriptor != null && field == null ? "   <-- NOT IN SCHEMA" : string.Empty;

            if (field != null && !WireMatches(field, raw.Wire))
            {
                flag = $"   <-- WIRE TYPE MISMATCH: sent as {raw.Wire}, schema says {field.FieldType}";
            }

            if (raw.Wire == WireFormat.WireType.LengthDelimited)
            {
                WalkLengthDelimited(field, raw, name, childPath, flag, indent, text, sent);
                continue;
            }

            string value = Scalar(field, raw);
            Line(text, indent, $"{raw.Number} {name}: {value}{flag}");
            if (field != null && flag.Length == 0)
            {
                sent?.Add(childPath);
            }
        }

        if (descriptor != null && text != null)
        {
            ListNotSent(descriptor, seen, indent, text);
        }
    }

    private static void WalkLengthDelimited(
        FieldDescriptor? field, RawField raw, string name, string childPath, string flag, int indent,
        StringBuilder? text, ISet<string>? sent)
    {
        if (field is { FieldType: FieldType.Message })
        {
            Line(text, indent, $"{raw.Number} {name}: {{{flag}");
            if (indent + 1 > MaxDepth)
            {
                Line(text, indent + 1, "(too deep)");
            }
            else
            {
                Walk(field.MessageType, raw.Bytes, field.IsRepeated ? childPath + "[]" : childPath, indent + 1, text, sent);
            }

            Line(text, indent, "}");
            return;
        }

        if (field is { FieldType: FieldType.String })
        {
            Line(text, indent, $"{raw.Number} {name}: \"{Encoding.UTF8.GetString(raw.Bytes)}\"{flag}");
            if (flag.Length == 0 && !field.IsRepeated)
            {
                sent?.Add(childPath);
            }

            return;
        }

        if (field == null && raw.Bytes.Length > 0)
        {
            // Unknown field: guess between text, nested message and bytes.
            if (raw.Bytes.All(b => b is >= 32 and < 127))
            {
                Line(text, indent, $"{raw.Number} {name}: \"{Encoding.ASCII.GetString(raw.Bytes)}\"{flag}");
                return;
            }

            if (TryParse(raw.Bytes, out List<RawField> nested) && nested.Count > 0)
            {
                Line(text, indent, $"{raw.Number} {name}: {{{flag}");
                Walk(null, raw.Bytes, childPath, indent + 1, text, sent);
                Line(text, indent, "}");
                return;
            }
        }

        Line(text, indent, $"{raw.Number} {name}: bytes[{raw.Bytes.Length}] {Hex(raw.Bytes)}{flag}");
    }

    private static string Scalar(FieldDescriptor? field, RawField raw)
    {
        switch (raw.Wire)
        {
            case WireFormat.WireType.Fixed32:
            {
                float single = BitConverter.Int32BitsToSingle(unchecked((int)raw.Value));
                string singleText = single.ToString("R", CultureInfo.InvariantCulture);
                return field?.FieldType == FieldType.Float
                    ? singleText
                    : $"float={singleText} int={raw.Value}";
            }
            case WireFormat.WireType.Fixed64:
            {
                double number = BitConverter.Int64BitsToDouble(unchecked((long)raw.Value));
                return field?.FieldType == FieldType.Double
                    ? Format(number)
                    : $"double={Format(number)} int={raw.Value}";
            }
            default:
                return field?.FieldType switch
                {
                    FieldType.Bool => raw.Value != 0 ? "true" : "false",
                    FieldType.Enum => EnumName(field, raw.Value),
                    FieldType.SInt32 or FieldType.SInt64 => (((long)(raw.Value >> 1)) ^ -(long)(raw.Value & 1)).ToString(CultureInfo.InvariantCulture),
                    FieldType.Int32 => unchecked((int)raw.Value).ToString(CultureInfo.InvariantCulture),
                    FieldType.Int64 => unchecked((long)raw.Value).ToString(CultureInfo.InvariantCulture),
                    _ => raw.Value.ToString(CultureInfo.InvariantCulture)
                };
        }
    }

    private static string EnumName(FieldDescriptor field, ulong value)
    {
        EnumValueDescriptor? member = field.EnumType.FindValueByNumber(unchecked((int)value));
        return member != null ? $"{member.Name} ({value})" : $"{value} (not a member of {field.EnumType.Name})";
    }

    private static bool WireMatches(FieldDescriptor field, WireFormat.WireType wire)
    {
        if (field.IsRepeated && wire == WireFormat.WireType.LengthDelimited)
        {
            return true; // packed
        }

        return field.FieldType switch
        {
            FieldType.Float or FieldType.Fixed32 or FieldType.SFixed32 => wire == WireFormat.WireType.Fixed32,
            FieldType.Double or FieldType.Fixed64 or FieldType.SFixed64 => wire == WireFormat.WireType.Fixed64,
            FieldType.String or FieldType.Bytes or FieldType.Message => wire == WireFormat.WireType.LengthDelimited,
            _ => wire == WireFormat.WireType.Varint
        };
    }

    private static void ListNotSent(MessageDescriptor descriptor, HashSet<int> seen, int indent, StringBuilder text)
    {
        List<string> scalars = [];
        List<string> messages = [];
        foreach (FieldDescriptor field in descriptor.Fields.InFieldNumberOrder())
        {
            if (seen.Contains(field.FieldNumber) || field.IsRepeated)
            {
                continue;
            }

            if (field.FieldType == FieldType.Message)
            {
                messages.Add($"{field.Name}={field.FieldNumber}");
            }
            else
            {
                scalars.Add($"{field.Name}={field.FieldNumber}");
            }
        }

        if (scalars.Count > 0)
        {
            Line(text, indent, $"(not sent, so 0/false/default: {string.Join(", ", scalars)})");
        }

        if (messages.Count > 0)
        {
            Line(text, indent, $"(sub-messages not sent: {string.Join(", ", messages)})");
        }
    }

    private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Hex(byte[] bytes) =>
        Convert.ToHexString(bytes, 0, Math.Min(bytes.Length, MaxHexBytes)) + (bytes.Length > MaxHexBytes ? "..." : string.Empty);

    private static void Line(StringBuilder? text, int indent, string line) =>
        text?.Append(' ', indent * 2).AppendLine(line);
}
