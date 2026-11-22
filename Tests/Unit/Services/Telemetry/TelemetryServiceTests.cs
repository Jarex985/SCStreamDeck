using System.Runtime.CompilerServices;
using FluentAssertions;
using Sc.Game.Telemetry.V1;
using SCStreamDeck.Services.Telemetry;

namespace Tests.Unit.Services.Telemetry;

public sealed class TelemetryServiceTests
{
    private sealed class ScriptedSource(Func<int, CancellationToken, IAsyncEnumerable<TelemetryStreamResponse>> script)
        : ITelemetryStreamSource
    {
        private int _attempts;
        public int Attempts => _attempts;

        public IAsyncEnumerable<TelemetryStreamResponse> StreamAsync(CancellationToken cancellationToken) =>
            script(Interlocked.Increment(ref _attempts), cancellationToken);
    }

    private static async IAsyncEnumerable<TelemetryStreamResponse> Frames(
        IEnumerable<TelemetryStreamResponse> frames, bool thenWait, [EnumeratorCancellation] CancellationToken token)
    {
        foreach (TelemetryStreamResponse frame in frames)
        {
            yield return frame;
        }

        if (thenWait)
        {
            await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
        }
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition)
    {
        for (int i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        return condition();
    }

    [Fact]
    public void Merge_KeepsLastKnownValueOfMissingFrameParts()
    {
        TelemetryStreamResponse first = new() { Actor = new ActorFrame(), Vehicle = new VehicleFrame() };
        TelemetryStreamResponse second = new() { Actor = new ActorFrame { State = new ActorState { UecBalance = 5 } } };

        TelemetryStreamResponse merged = TelemetryService.Merge(first, second);

        merged.Vehicle.Should().BeSameAs(first.Vehicle);
        merged.Actor.Should().BeSameAs(second.Actor);
    }

    [Fact]
    public async Task Subscribe_StreamsFramesAndStopsWhenLastSubscriberLeaves()
    {
        ScriptedSource source = new((_, token) => Frames([new TelemetryStreamResponse { Actor = new ActorFrame() }], true, token));
        using TelemetryService service = new(source);

        IDisposable subscription = service.Subscribe(() => { });
        (await WaitUntilAsync(() => service.State == TelemetryConnectionState.Streaming)).Should().BeTrue();
        service.Snapshot!.Actor.Should().NotBeNull();

        subscription.Dispose();
        (await WaitUntilAsync(() => service.State == TelemetryConnectionState.Disconnected)).Should().BeTrue();
        service.Snapshot.Should().BeNull();
    }

    [Fact]
    public async Task StreamFailure_ClearsSnapshotAndRetries()
    {
        ScriptedSource source = new((attempt, token) => attempt == 1
            ? ThrowingFrames()
            : Frames([new TelemetryStreamResponse { Actor = new ActorFrame() }], true, token));
        using TelemetryService service = new(source)
        {
            MinRetryDelay = TimeSpan.FromMilliseconds(10), MaxRetryDelay = TimeSpan.FromMilliseconds(20)
        };

        using IDisposable subscription = service.Subscribe(() => { });

        (await WaitUntilAsync(() => source.Attempts >= 2 && service.Snapshot != null)).Should().BeTrue();
        service.State.Should().Be(TelemetryConnectionState.Streaming);

        static async IAsyncEnumerable<TelemetryStreamResponse> ThrowingFrames()
        {
            await Task.Yield();
            throw new InvalidOperationException("server unavailable");
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }
    }

    [Fact]
    public async Task SecondSubscriber_DoesNotStartAnotherStream()
    {
        ScriptedSource source = new((_, token) => Frames([], true, token));
        using TelemetryService service = new(source);

        using IDisposable first = service.Subscribe(() => { });
        using IDisposable second = service.Subscribe(() => { });
        await Task.Delay(50);

        source.Attempts.Should().Be(1);
    }
}
