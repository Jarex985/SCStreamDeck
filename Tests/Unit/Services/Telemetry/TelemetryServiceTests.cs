using System.Runtime.CompilerServices;
using FluentAssertions;
using SCStreamDeck.Services.Telemetry;

namespace Tests.Unit.Services.Telemetry;

public sealed class TelemetryServiceTests
{
    private sealed class ScriptedSource(Func<int, CancellationToken, IAsyncEnumerable<TelemetrySnapshot>> script)
        : ITelemetryStreamSource
    {
        private int _attempts;
        public int Attempts => _attempts;

        public IAsyncEnumerable<TelemetrySnapshot> StreamAsync(CancellationToken cancellationToken) =>
            script(Interlocked.Increment(ref _attempts), cancellationToken);
    }

    private static TelemetrySnapshot Part(string name, params (string Path, double Value)[] values) =>
        new(new Dictionary<string, IReadOnlyDictionary<string, TelemetryValue>>
        {
            [name] = values.ToDictionary(v => v.Path, v => new TelemetryValue(TelemetryValueKind.Number, v.Value))
        });

    private static TelemetrySnapshot Merge(params TelemetrySnapshot[] parts) =>
        new(parts.SelectMany(p => p.Parts).ToDictionary(p => p.Key, p => p.Value));

    private static readonly TelemetrySnapshot s_actor = Part("actor", ("actor.vitals.pressure", 1));
    private static readonly TelemetrySnapshot s_vehicle = Part("vehicle", ("vehicle.state.speed", 5));

    private static async IAsyncEnumerable<TelemetrySnapshot> Frames(
        IEnumerable<TelemetrySnapshot> frames, bool thenWait, [EnumeratorCancellation] CancellationToken token)
    {
        foreach (TelemetrySnapshot frame in frames)
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
    public void ApplyFrame_KeepsLastKnownValueOfMissingFrameParts()
    {
        using TelemetryService service = new(new ScriptedSource((_, token) => Frames([], true, token)));
        DateTime t0 = DateTime.UtcNow;

        service.ApplyFrame(Merge(s_actor, s_vehicle), t0);
        service.ApplyFrame(Part("actor", ("actor.vitals.pressure", 2)), t0.AddMilliseconds(100));

        service.Snapshot!.TryGetValue("vehicle.state.speed", out TelemetryValue speed).Should().BeTrue();
        speed.Number.Should().Be(5);
        service.Snapshot.TryGetValue("actor.vitals.pressure", out TelemetryValue pressure).Should().BeTrue();
        pressure.Number.Should().Be(2);
    }

    [Fact]
    public void ExpireStale_DropsOnlyPartsTheGameStoppedSending()
    {
        using TelemetryService service = new(new ScriptedSource((_, token) => Frames([], true, token)))
        {
            StaleAfter = TimeSpan.FromSeconds(3)
        };
        DateTime t0 = DateTime.UtcNow;

        // Seated: vehicle and actor arrive. Then the player leaves the seat and only the actor keeps coming.
        service.ApplyFrame(Merge(s_actor, s_vehicle), t0);
        service.ApplyFrame(s_actor, t0.AddSeconds(2));

        service.ExpireStale(t0.AddSeconds(2.5)).Should().BeFalse();
        service.Snapshot!.HasPart("vehicle").Should().BeTrue();

        service.ApplyFrame(s_actor, t0.AddSeconds(3.5));
        service.ExpireStale(t0.AddSeconds(3.6)).Should().BeTrue();

        service.Snapshot!.HasPart("vehicle").Should().BeFalse();
        service.Snapshot.HasPart("actor").Should().BeTrue();
        service.ExpireStale(t0.AddSeconds(3.7)).Should().BeFalse();
    }

    [Fact]
    public async Task StalePart_RaisesUpdatedWithoutNewFrames()
    {
        ScriptedSource source = new((_, token) => Frames([s_vehicle], true, token));
        using TelemetryService service = new(source)
        {
            StaleAfter = TimeSpan.FromMilliseconds(100), StaleCheckInterval = TimeSpan.FromMilliseconds(20)
        };

        using IDisposable subscription = service.Subscribe(() => { });

        (await WaitUntilAsync(() => service.Snapshot?.HasPart("vehicle") == true)).Should().BeTrue();
        (await WaitUntilAsync(() => service.Snapshot?.HasPart("vehicle") == false)).Should().BeTrue();
    }

    [Fact]
    public async Task Subscribe_StreamsFramesAndStopsWhenLastSubscriberLeaves()
    {
        ScriptedSource source = new((_, token) => Frames([s_actor], true, token));
        using TelemetryService service = new(source);

        IDisposable subscription = service.Subscribe(() => { });
        (await WaitUntilAsync(() => service.State == TelemetryConnectionState.Streaming)).Should().BeTrue();
        service.Snapshot!.HasPart("actor").Should().BeTrue();

        subscription.Dispose();
        (await WaitUntilAsync(() => service.State == TelemetryConnectionState.Disconnected)).Should().BeTrue();
        service.Snapshot.Should().BeNull();
    }

    [Fact]
    public async Task StreamFailure_ClearsSnapshotAndRetries()
    {
        ScriptedSource source = new((attempt, token) => attempt == 1
            ? ThrowingFrames()
            : Frames([s_actor], true, token));
        using TelemetryService service = new(source)
        {
            MinRetryDelay = TimeSpan.FromMilliseconds(10), MaxRetryDelay = TimeSpan.FromMilliseconds(20)
        };

        using IDisposable subscription = service.Subscribe(() => { });

        (await WaitUntilAsync(() => source.Attempts >= 2 && service.Snapshot != null)).Should().BeTrue();
        service.State.Should().Be(TelemetryConnectionState.Streaming);

        static async IAsyncEnumerable<TelemetrySnapshot> ThrowingFrames()
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
