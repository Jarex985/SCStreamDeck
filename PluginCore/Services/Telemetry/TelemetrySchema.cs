using Google.Protobuf.Reflection;
using Sc.Game.Telemetry.V1;

namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     The telemetry protocol as known at runtime: the response message used to decode frames and the attributes
///     derived from it. Comes from the installed game (see <see cref="ProtoDescriptorExtractor" />) or, as a
///     fallback, from the proto compiled into the plugin.
/// </summary>
public sealed class TelemetrySchema
{
    public const string ResponseFullName = "sc.game.telemetry.v1.TelemetryStreamResponse";
    public const string ServiceFullName = "sc.game.telemetry.v1.GameTelemetryService";

    private static readonly Lazy<TelemetrySchema> s_bundled = new(() =>
        new TelemetrySchema(TelemetryStreamResponse.Descriptor, [TelemetryStreamResponse.Descriptor.File], "bundled proto"));

    private readonly Dictionary<string, TelemetryField> _byPath;

    private TelemetrySchema(MessageDescriptor response, IReadOnlyList<FileDescriptor> files, string source)
    {
        Response = response;
        Files = files;
        Source = source;
        Fields = TelemetryFieldCatalog.Build(response);
        _byPath = Fields.ToDictionary(f => f.Path, StringComparer.Ordinal);
    }

    /// <summary>The schema compiled into the plugin (the proto in <c>Protos/</c>).</summary>
    public static TelemetrySchema Bundled => s_bundled.Value;

    public MessageDescriptor Response { get; }

    /// <summary>All file descriptors of this schema (including imported well-known types), dependencies first.</summary>
    public IReadOnlyList<FileDescriptor> Files { get; }

    /// <summary>Human readable origin, for logs.</summary>
    public string Source { get; }

    public IReadOnlyList<TelemetryField> Fields { get; }

    public TelemetryField? Find(string? path) =>
        !string.IsNullOrWhiteSpace(path) && _byPath.TryGetValue(path, out TelemetryField? field) ? field : null;

    /// <summary>
    ///     Builds a schema from file descriptors; fails unless they contain the telemetry service and its response message.
    /// </summary>
    public static bool TryCreate(IReadOnlyList<FileDescriptor> files, string source, out TelemetrySchema? schema)
    {
        schema = null;
        ArgumentNullException.ThrowIfNull(files);

        bool hasService = files.Any(f => f.Services.Any(s => s.FullName == ServiceFullName));
        MessageDescriptor? response = files
            .SelectMany(f => f.MessageTypes)
            .FirstOrDefault(m => m.FullName == ResponseFullName);

        if (!hasService || response == null)
        {
            return false;
        }

        schema = new TelemetrySchema(response, files, source);
        return true;
    }
}
