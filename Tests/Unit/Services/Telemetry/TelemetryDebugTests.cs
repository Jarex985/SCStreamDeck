using FluentAssertions;
using Google.Protobuf;
using Sc.Game.Telemetry.V1;
using SCStreamDeck.Services.Telemetry;

namespace Tests.Unit.Services.Telemetry;

public sealed class TelemetryDebugTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"telemetry-debug-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private static byte[] Frame(byte[]? extraInstrumentBytes = null)
    {
        VehicleFlightInstruments instruments = new() { Speed = 12f, HydrogenFuel = 0.5f };
        if (extraInstrumentBytes != null)
        {
            instruments = VehicleFlightInstruments.Parser.ParseFrom(instruments.ToByteArray().Concat(extraInstrumentBytes).ToArray());
        }

        return new TelemetryStreamResponse
        {
            Vehicle = new VehicleFrame { State = new VehicleState { Flight = new VehicleFlight { Instruments = instruments } } }
        }.ToByteArray();
    }

    [Fact]
    public void Dump_NamesFieldsAndListsWhatWasNotSent()
    {
        string text = TelemetryRawFrameDumper.Dump(TelemetrySchema.Bundled.Response, Frame());

        text.Should().Contain("speed: 12");
        text.Should().Contain("hydrogen_fuel: 0.5");
        text.Should().Contain("not sent, so 0/false/default:").And.Contain("quantum_fuel=3");
        text.Should().NotContain("NOT IN SCHEMA");
    }

    [Fact]
    public void Dump_MarksFieldsTheSchemaDoesNotKnow()
    {
        // field 11, fixed32 (a float the schema has no entry for)
        byte[] unknown = [(11 << 3) | 5, 0, 0, 0x80, 0x3F];

        string text = TelemetryRawFrameDumper.Dump(TelemetrySchema.Bundled.Response, Frame(unknown));

        text.Should().Contain("11 #11:").And.Contain("NOT IN SCHEMA");
    }

    [Fact]
    public void Dump_CollectsThePathsOfSentScalars()
    {
        HashSet<string> sent = [];

        TelemetryRawFrameDumper.Dump(TelemetrySchema.Bundled.Response, Frame(), sent);

        sent.Should().Contain("vehicle.state.flight.instruments.speed");
        sent.Should().NotContain("vehicle.state.flight.instruments.quantum_fuel");
    }

    [Fact]
    public void Recorder_WritesThrottledFramesAndSummarisesAttributesNeverSent()
    {
        DateTime now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        TelemetryDebugRecorder recorder = new(() => _directory, () => now) { Interval = TimeSpan.FromSeconds(2) };
        TelemetrySchema schema = TelemetrySchema.Bundled;

        recorder.Record(Frame(), schema);
        recorder.FramesSeen.Should().Be(0, "nothing is recorded while debugging is off");

        recorder.Toggle().Should().BeTrue();
        recorder.Record(Frame(), schema);
        now = now.AddMilliseconds(500);
        recorder.Record(Frame(), schema);
        now = now.AddSeconds(3);
        recorder.Record(Frame(), schema);

        recorder.FramesSeen.Should().Be(3);
        recorder.FramesWritten.Should().Be(2);

        recorder.Toggle().Should().BeFalse();
        string text = File.ReadAllText(Path.Combine(_directory, TelemetryDebugRecorder.FileName));
        text.Should().Contain("=== frame 1").And.Contain("=== frame 2").And.NotContain("=== frame 3");
        text.Should().Contain("=== summary: 3 frames received, 2 written");
        text.Should().Contain("vehicle.state.flight.instruments.quantum_fuel");
        text.Should().NotContain("  vehicle.state.flight.instruments.speed");
    }
}
