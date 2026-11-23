using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using SCStreamDeck.Logging;

namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     Source of telemetry frames. Abstracted so the service can be tested without a game or a gRPC server.
/// </summary>
public interface ITelemetryStreamSource
{
    IAsyncEnumerable<TelemetrySnapshot> StreamAsync(CancellationToken cancellationToken);
}

/// <summary>
///     Reads <c>TelemetryStream</c> from the game's local, unauthenticated gRPC server. Frames are received as raw
///     bytes and decoded with the schema currently in use, so no generated client is involved.
/// </summary>
public sealed class GrpcTelemetryStreamSource(
    ITelemetrySchemaProvider schemas,
    string address = GrpcTelemetryStreamSource.DefaultAddress) : ITelemetryStreamSource
{
    public const string DefaultAddress = "http://127.0.0.1:2942";

    private static readonly Marshaller<byte[]> s_raw = Marshallers.Create(bytes => bytes, bytes => bytes);

    private static readonly Method<byte[], byte[]> s_telemetryStream = new(
        MethodType.ServerStreaming, TelemetrySchema.ServiceFullName, "TelemetryStream", s_raw, s_raw);

    private readonly ITelemetrySchemaProvider _schemas = schemas ?? throw new ArgumentNullException(nameof(schemas));

    public async IAsyncEnumerable<TelemetrySnapshot> StreamAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using GrpcChannel channel = GrpcChannel.ForAddress(address);
        using AsyncServerStreamingCall<byte[]> call = channel.CreateCallInvoker().AsyncServerStreamingCall(
            s_telemetryStream, null, new CallOptions(cancellationToken: cancellationToken), []);

        await foreach (byte[] bytes in call.ResponseStream.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            TelemetrySnapshot frame;
            try
            {
                frame = TelemetryFrameDecoder.Decode(_schemas.Current.Response, bytes);
            }
            catch (InvalidProtocolBufferException ex)
            {
                // One bad frame must not end the stream.
                Log.Debug($"[{nameof(GrpcTelemetryStreamSource)}] Skipping undecodable frame: {ex.Message}");
                continue;
            }

            yield return frame;
        }
    }
}
