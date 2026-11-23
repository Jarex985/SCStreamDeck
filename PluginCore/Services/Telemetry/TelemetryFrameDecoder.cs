using System.Globalization;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     Decodes a serialized <c>TelemetryStreamResponse</c> using only protobuf descriptors, so it works with the
///     schema read from the installed game as well as with the bundled one. Output is flat: attribute path to value.
///     Repeated fields are skipped. As in generated code, scalars of a present message default to zero/empty/false,
///     while proto3 <c>optional</c> scalars exist only when sent.
/// </summary>
public static class TelemetryFrameDecoder
{
    private const int MaxDepth = 32;

    public static TelemetrySnapshot Decode(MessageDescriptor response, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(data);

        Dictionary<string, IReadOnlyDictionary<string, TelemetryValue>> parts = new(StringComparer.Ordinal);
        CodedInputStream input = new(data);

        uint tag;
        while ((tag = input.ReadTag()) != 0)
        {
            FieldDescriptor? field = response.FindFieldByNumber(WireFormat.GetTagFieldNumber(tag));
            if (field is not { FieldType: FieldType.Message, IsRepeated: false } ||
                WireFormat.GetTagWireType(tag) != WireFormat.WireType.LengthDelimited)
            {
                input.SkipLastField();
                continue;
            }

            Dictionary<string, TelemetryValue> values = parts.TryGetValue(field.Name, out IReadOnlyDictionary<string, TelemetryValue>? existing)
                ? (Dictionary<string, TelemetryValue>)existing
                : new Dictionary<string, TelemetryValue>(StringComparer.Ordinal);

            // PushLimit is not public; decode each nested message from its own (small) buffer.
            DecodeMessage(field.MessageType, field.Name, new CodedInputStream(input.ReadBytes().ToByteArray()), values, 1);

            parts[field.Name] = values;
        }

        return new TelemetrySnapshot(parts);
    }

    private static void DecodeMessage(
        MessageDescriptor message, string prefix, CodedInputStream input, Dictionary<string, TelemetryValue> values, int depth)
    {
        HashSet<int> seen = [];

        uint tag;
        while ((tag = input.ReadTag()) != 0)
        {
            WireFormat.WireType wireType = WireFormat.GetTagWireType(tag);
            FieldDescriptor? field = message.FindFieldByNumber(WireFormat.GetTagFieldNumber(tag));
            if (field == null || field.IsRepeated || depth >= MaxDepth)
            {
                input.SkipLastField();
                continue;
            }

            string path = $"{prefix}.{field.Name}";

            if (field.FieldType == FieldType.Message)
            {
                if (wireType != WireFormat.WireType.LengthDelimited)
                {
                    input.SkipLastField();
                    continue;
                }

                DecodeMessage(field.MessageType, path, new CodedInputStream(input.ReadBytes().ToByteArray()), values, depth + 1);
                seen.Add(field.FieldNumber);
                continue;
            }

            if (!TryReadScalar(field, wireType, input, out TelemetryValue value))
            {
                input.SkipLastField();
                continue;
            }

            values[path] = value;
            seen.Add(field.FieldNumber);
        }

        foreach (FieldDescriptor field in message.Fields.InFieldNumberOrder())
        {
            if (field.IsRepeated || field.FieldType == FieldType.Message || field.HasPresence || seen.Contains(field.FieldNumber))
            {
                continue;
            }

            if (TryDefault(field, out TelemetryValue defaultValue))
            {
                values[$"{prefix}.{field.Name}"] = defaultValue;
            }
        }

        if (message.FullName == TelemetryFieldCatalog.GaugeFullName)
        {
            AddGaugePercent(prefix, values);
        }
    }

    private static void AddGaugePercent(string prefix, Dictionary<string, TelemetryValue> values)
    {
        if (values.TryGetValue($"{prefix}.current", out TelemetryValue current) &&
            values.TryGetValue($"{prefix}.max", out TelemetryValue max) && max.Number > 0)
        {
            values[$"{prefix}.{TelemetryFieldCatalog.PercentSegment}"] =
                new TelemetryValue(TelemetryValueKind.Number, Math.Clamp(current.Number / max.Number * 100.0, 0, 100));
        }
    }

    private static bool TryReadScalar(FieldDescriptor field, WireFormat.WireType wireType, CodedInputStream input, out TelemetryValue value)
    {
        value = default;
        switch (field.FieldType)
        {
            case FieldType.Double when wireType == WireFormat.WireType.Fixed64:
                return TryNumber(input.ReadDouble(), false, out value);
            case FieldType.Float when wireType == WireFormat.WireType.Fixed32:
                return TryNumber(input.ReadFloat(), false, out value);
            case FieldType.Int32 when wireType == WireFormat.WireType.Varint:
                return TryNumber(input.ReadInt32(), true, out value);
            case FieldType.Int64 when wireType == WireFormat.WireType.Varint:
                return TryNumber(input.ReadInt64(), true, out value);
            case FieldType.UInt32 when wireType == WireFormat.WireType.Varint:
                return TryNumber(input.ReadUInt32(), true, out value);
            case FieldType.UInt64 when wireType == WireFormat.WireType.Varint:
                return TryNumber(input.ReadUInt64(), true, out value);
            case FieldType.SInt32 when wireType == WireFormat.WireType.Varint:
                return TryNumber(input.ReadSInt32(), true, out value);
            case FieldType.SInt64 when wireType == WireFormat.WireType.Varint:
                return TryNumber(input.ReadSInt64(), true, out value);
            case FieldType.Fixed32 when wireType == WireFormat.WireType.Fixed32:
                return TryNumber(input.ReadFixed32(), true, out value);
            case FieldType.Fixed64 when wireType == WireFormat.WireType.Fixed64:
                return TryNumber(input.ReadFixed64(), true, out value);
            case FieldType.SFixed32 when wireType == WireFormat.WireType.Fixed32:
                return TryNumber(input.ReadSFixed32(), true, out value);
            case FieldType.SFixed64 when wireType == WireFormat.WireType.Fixed64:
                return TryNumber(input.ReadSFixed64(), true, out value);
            case FieldType.Bool when wireType == WireFormat.WireType.Varint:
                value = BoolValue(input.ReadBool());
                return true;
            case FieldType.String when wireType == WireFormat.WireType.LengthDelimited:
                string text = input.ReadString();
                value = new TelemetryValue(TelemetryValueKind.Text, Text: text, DisplayText: text);
                return true;
            case FieldType.Enum when wireType == WireFormat.WireType.Varint:
                value = EnumValue(field.EnumType, input.ReadEnum());
                return true;
            default:
                return false;
        }
    }

    private static bool TryDefault(FieldDescriptor field, out TelemetryValue value)
    {
        value = default;
        switch (field.FieldType)
        {
            case FieldType.Bool:
                value = BoolValue(false);
                return true;
            case FieldType.String:
                value = new TelemetryValue(TelemetryValueKind.Text);
                return true;
            case FieldType.Enum:
                value = EnumValue(field.EnumType, 0);
                return true;
            case FieldType.Float or FieldType.Double:
                value = new TelemetryValue(TelemetryValueKind.Number);
                return true;
            case FieldType.Bytes or FieldType.Group or FieldType.Message:
                return false;
            default:
                value = new TelemetryValue(TelemetryValueKind.Number, IsInteger: true);
                return true;
        }
    }

    // Always consumes/returns true: the value was already read from the stream. Non-finite numbers become 0.
    private static bool TryNumber(IConvertible number, bool isInteger, out TelemetryValue value)
    {
        double d = number.ToDouble(CultureInfo.InvariantCulture);
        value = new TelemetryValue(TelemetryValueKind.Number, double.IsFinite(d) ? d : 0, IsInteger: isInteger);
        return true;
    }

    private static TelemetryValue BoolValue(bool b) =>
        new(TelemetryValueKind.Boolean, Boolean: b, DisplayText: b ? "On" : "Off");

    private static TelemetryValue EnumValue(EnumDescriptor type, int number)
    {
        EnumValueDescriptor? member = type.FindValueByNumber(number);
        string name = member?.Name ?? number.ToString(CultureInfo.InvariantCulture);
        string display = member == null ? name : TelemetryFieldCatalog.HumanizeEnumValue(type, name);
        return new TelemetryValue(TelemetryValueKind.Enum, Text: name, DisplayText: display);
    }
}
