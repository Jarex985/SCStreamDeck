using Grpc.Core;
using Grpc.Net.Client;
using Sc.Game.Telemetry.V1;

namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     Source of telemetry frames. Abstracted so the service can be tested without a game or a gRPC server.
/// </summary>
public interface ITelemetryStreamSource
{
    IAsyncEnumerable<TelemetryStreamResponse> StreamAsync(CancellationToken cancellationToken);
}

/// <summary>
///     Reads <c>TelemetryStream</c> from the game's local, unauthenticated gRPC server.
/// </summary>
public sealed class GrpcTelemetryStreamSource(string address = GrpcTelemetryStreamSource.DefaultAddress) : ITelemetryStreamSource
{
    public const string DefaultAddress = "http://127.0.0.1:2942";

    public async IAsyncEnumerable<TelemetryStreamResponse> StreamAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using GrpcChannel channel = GrpcChannel.ForAddress(address);
        GameTelemetryService.GameTelemetryServiceClient client = new(channel);
        using AsyncServerStreamingCall<TelemetryStreamResponse> call =
            client.TelemetryStream(new TelemetryStreamRequest(), cancellationToken: cancellationToken);

        await foreach (TelemetryStreamResponse frame in call.ResponseStream.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return frame;
        }
    }
}
