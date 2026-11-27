using FluentAssertions;
using Google.Protobuf.Reflection;
using SCStreamDeck.Logging;
using SCStreamDeck.Services.Telemetry;

namespace Tests.Unit.Services.Telemetry;

public sealed class TelemetrySchemaExporterTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"telemetry-export-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    [Fact]
    public void Export_WritesReadableProtoAndDescriptorSet()
    {
        IReadOnlyList<string> written = TelemetrySchemaExporter.Export(TelemetrySchema.Bundled, _directory, @"C:\game\StarCitizen.exe");

        written.Should().HaveCount(2);
        written.All(File.Exists).Should().BeTrue();

        string proto = File.ReadAllText(Path.Combine(_directory, TelemetrySchemaExporter.ProtoFileName));
        proto.Should().Contain(@"Source: C:\game\StarCitizen.exe");
        proto.Should().Contain("package sc.game.telemetry.v1;");
        proto.Should().Contain("service GameTelemetryService {");
        proto.Should().Contain("rpc TelemetryStream (TelemetryStreamRequest) returns (stream TelemetryStreamResponse);");
        proto.Should().Contain("optional VehicleComponentDeploymentState gear = 2;");
        proto.Should().Contain("repeated float faces = 2;");
        proto.Should().Contain("reserved 1;");
        proto.Should().Contain("ACTOR_LOCOMOTION_STATE_SEATED = 3;");
        // Nested enums stay inside their message and keep their relative type name.
        proto.Should().MatchRegex(@"message VehicleDoorState \{\s+enum DoorOpenState \{");
        proto.Should().Contain("VehicleDoorState.DoorOpenState open_state = 1;");

        FileDescriptorSet set = FileDescriptorSet.Parser.ParseFrom(
            File.ReadAllBytes(Path.Combine(_directory, TelemetrySchemaExporter.DescriptorSetFileName)));
        set.File.Should().ContainSingle().Which.Service.Should().ContainSingle()
            .Which.Name.Should().Be("GameTelemetryService");
    }

    [Fact]
    public void Export_OverwritesPreviousExports()
    {
        TelemetrySchemaExporter.Export(TelemetrySchema.Bundled, _directory);
        string first = File.ReadAllText(Path.Combine(_directory, TelemetrySchemaExporter.ProtoFileName));

        Action again = () => TelemetrySchemaExporter.Export(TelemetrySchema.Bundled, _directory);

        again.Should().NotThrow();
        first.Should().Contain("bundled proto");
    }

    [Fact]
    public void LogLocation_ResolvesToAnExistingDirectory() =>
        Directory.Exists(PluginLogLocation.GetDirectory()).Should().BeTrue();
}
