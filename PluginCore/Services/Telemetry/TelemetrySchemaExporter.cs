using System.Globalization;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     Writes the telemetry protocol in use to disk for inspection: readable <c>.proto</c> text and a binary
///     descriptor set (usable with <c>grpcurl -protoset</c>).
/// </summary>
public static class TelemetrySchemaExporter
{
    public const string ProtoFileName = "telemetry-protocol.proto";
    public const string DescriptorSetFileName = "telemetry-protocol.desc";

    private const string WellKnownPrefix = "google/protobuf/";

    /// <summary>
    ///     Exports <paramref name="schema" /> into <paramref name="directory" />, overwriting earlier exports.
    ///     Returns the paths written.
    /// </summary>
    public static IReadOnlyList<string> Export(TelemetrySchema schema, string directory, string? sourceDescription = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        Directory.CreateDirectory(directory);

        List<FileDescriptor> own = schema.Files
            .Where(f => !f.Name.StartsWith(WellKnownPrefix, StringComparison.Ordinal))
            .ToList();

        string protoPath = Path.Combine(directory, ProtoFileName);
        string header =
            $"// Star Citizen telemetry protocol, exported by SCStreamDeck on {DateTime.Now.ToString("u", CultureInfo.InvariantCulture)}{Environment.NewLine}" +
            $"// Source: {sourceDescription ?? schema.Source}{Environment.NewLine}" +
            $"// {schema.Fields.Count} displayable attributes. Unofficial; the game may change it with any patch.{Environment.NewLine}{Environment.NewLine}";
        File.WriteAllText(protoPath, header + ProtoTextWriter.Write(own));

        FileDescriptorSet set = new();
        set.File.AddRange(schema.Files.Select(f => f.ToProto()));
        string descriptorPath = Path.Combine(directory, DescriptorSetFileName);
        File.WriteAllBytes(descriptorPath, set.ToByteArray());

        return [protoPath, descriptorPath];
    }
}
