using System.Text;
using Google.Protobuf.Reflection;

namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     Renders protobuf file descriptors as readable <c>.proto</c> text (for inspection, not meant to be compiled
///     as one file: when several files are written they are separated by header comments).
/// </summary>
public static class ProtoTextWriter
{
    private const string Indent = "  ";

    public static string Write(IEnumerable<FileDescriptor> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        StringBuilder sb = new();
        foreach (FileDescriptor file in files)
        {
            sb.Append("// ===== ").Append(file.Name).AppendLine(" =====");
            WriteFile(sb, file.ToProto());
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void WriteFile(StringBuilder sb, FileDescriptorProto file)
    {
        sb.Append("syntax = \"").Append(string.IsNullOrEmpty(file.Syntax) ? "proto2" : file.Syntax).AppendLine("\";");
        if (!string.IsNullOrEmpty(file.Package))
        {
            sb.Append("package ").Append(file.Package).AppendLine(";");
        }

        foreach (string dependency in file.Dependency)
        {
            sb.Append("import \"").Append(dependency).AppendLine("\";");
        }

        foreach (ServiceDescriptorProto service in file.Service)
        {
            sb.AppendLine().Append("service ").Append(service.Name).AppendLine(" {");
            foreach (MethodDescriptorProto method in service.Method)
            {
                sb.Append(Indent).Append("rpc ").Append(method.Name).Append(" (")
                    .Append(method.ClientStreaming ? "stream " : string.Empty).Append(TypeName(method.InputType, file.Package))
                    .Append(") returns (")
                    .Append(method.ServerStreaming ? "stream " : string.Empty).Append(TypeName(method.OutputType, file.Package))
                    .AppendLine(");");
            }

            sb.AppendLine("}");
        }

        foreach (EnumDescriptorProto enumType in file.EnumType)
        {
            sb.AppendLine();
            WriteEnum(sb, enumType, 0);
        }

        foreach (DescriptorProto message in file.MessageType)
        {
            sb.AppendLine();
            WriteMessage(sb, message, file.Package, 0);
        }
    }

    private static void WriteEnum(StringBuilder sb, EnumDescriptorProto enumType, int depth)
    {
        string pad = string.Concat(Enumerable.Repeat(Indent, depth));
        sb.Append(pad).Append("enum ").Append(enumType.Name).AppendLine(" {");
        foreach (EnumValueDescriptorProto value in enumType.Value)
        {
            sb.Append(pad).Append(Indent).Append(value.Name).Append(" = ").Append(value.Number).AppendLine(";");
        }

        sb.Append(pad).AppendLine("}");
    }

    private static void WriteMessage(StringBuilder sb, DescriptorProto message, string package, int depth)
    {
        string pad = string.Concat(Enumerable.Repeat(Indent, depth));
        sb.Append(pad).Append("message ").Append(message.Name).AppendLine(" {");

        Dictionary<string, DescriptorProto> mapEntries = message.NestedType
            .Where(n => n.Options?.MapEntry == true)
            .ToDictionary(n => n.Name, StringComparer.Ordinal);

        foreach (EnumDescriptorProto nested in message.EnumType)
        {
            WriteEnum(sb, nested, depth + 1);
        }

        foreach (DescriptorProto nested in message.NestedType.Where(n => n.Options?.MapEntry != true))
        {
            WriteMessage(sb, nested, package, depth + 1);
        }

        HashSet<int> inRealOneof = message.Field.Where(f => f.HasOneofIndex && !f.Proto3Optional)
            .Select(f => f.OneofIndex).ToHashSet();

        for (int oneofIndex = 0; oneofIndex < message.OneofDecl.Count; oneofIndex++)
        {
            if (!inRealOneof.Contains(oneofIndex))
            {
                continue; // synthetic oneof of a proto3 optional field
            }

            sb.Append(pad).Append(Indent).Append("oneof ").Append(message.OneofDecl[oneofIndex].Name).AppendLine(" {");
            foreach (FieldDescriptorProto field in message.Field.Where(f => f.HasOneofIndex && !f.Proto3Optional && f.OneofIndex == oneofIndex))
            {
                sb.Append(pad).Append(Indent).Append(Indent).AppendLine(FieldText(field, package, mapEntries, withLabel: false));
            }

            sb.Append(pad).Append(Indent).AppendLine("}");
        }

        foreach (FieldDescriptorProto field in message.Field.Where(f => !(f.HasOneofIndex && !f.Proto3Optional)))
        {
            sb.Append(pad).Append(Indent).AppendLine(FieldText(field, package, mapEntries, withLabel: true));
        }

        foreach (DescriptorProto.Types.ReservedRange range in message.ReservedRange)
        {
            int last = range.End - 1;
            sb.Append(pad).Append(Indent).Append("reserved ").Append(range.Start)
                .Append(last > range.Start ? $" to {last}" : string.Empty).AppendLine(";");
        }

        foreach (string name in message.ReservedName)
        {
            sb.Append(pad).Append(Indent).Append("reserved \"").Append(name).AppendLine("\";");
        }

        sb.Append(pad).AppendLine("}");
    }

    private static string FieldText(
        FieldDescriptorProto field, string package, Dictionary<string, DescriptorProto> mapEntries, bool withLabel)
    {
        string type = ScalarType(field.Type) ?? TypeName(field.TypeName, package);

        if (field.Label == FieldDescriptorProto.Types.Label.Repeated && field.Type == FieldDescriptorProto.Types.Type.Message &&
            mapEntries.TryGetValue(type[(type.LastIndexOf('.') + 1)..], out DescriptorProto? entry) &&
            entry.Field.Count == 2)
        {
            string key = ScalarType(entry.Field[0].Type) ?? TypeName(entry.Field[0].TypeName, package);
            string value = ScalarType(entry.Field[1].Type) ?? TypeName(entry.Field[1].TypeName, package);
            return $"map<{key}, {value}> {field.Name} = {field.Number};";
        }

        string label = !withLabel ? string.Empty
            : field.Label == FieldDescriptorProto.Types.Label.Repeated ? "repeated "
            : field.Proto3Optional ? "optional "
            : string.Empty;

        return $"{label}{type} {field.Name} = {field.Number};";
    }

    private static string TypeName(string typeName, string package)
    {
        string name = typeName.TrimStart('.');
        string prefix = package + ".";
        return !string.IsNullOrEmpty(package) && name.StartsWith(prefix, StringComparison.Ordinal) ? name[prefix.Length..] : name;
    }

    private static string? ScalarType(FieldDescriptorProto.Types.Type type) => type switch
    {
        FieldDescriptorProto.Types.Type.Double => "double",
        FieldDescriptorProto.Types.Type.Float => "float",
        FieldDescriptorProto.Types.Type.Int64 => "int64",
        FieldDescriptorProto.Types.Type.Uint64 => "uint64",
        FieldDescriptorProto.Types.Type.Int32 => "int32",
        FieldDescriptorProto.Types.Type.Fixed64 => "fixed64",
        FieldDescriptorProto.Types.Type.Fixed32 => "fixed32",
        FieldDescriptorProto.Types.Type.Bool => "bool",
        FieldDescriptorProto.Types.Type.String => "string",
        FieldDescriptorProto.Types.Type.Bytes => "bytes",
        FieldDescriptorProto.Types.Type.Uint32 => "uint32",
        FieldDescriptorProto.Types.Type.Sfixed32 => "sfixed32",
        FieldDescriptorProto.Types.Type.Sfixed64 => "sfixed64",
        FieldDescriptorProto.Types.Type.Sint32 => "sint32",
        FieldDescriptorProto.Types.Type.Sint64 => "sint64",
        _ => null
    };
}
