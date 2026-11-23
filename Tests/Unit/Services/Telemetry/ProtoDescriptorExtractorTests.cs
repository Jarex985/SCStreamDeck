using FluentAssertions;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Sc.Game.Telemetry.V1;
using SCStreamDeck.Services.Telemetry;

namespace Tests.Unit.Services.Telemetry;

public sealed class ProtoDescriptorExtractorTests
{
    private const string ApiName = "sc/game/telemetry/v1/api.proto";
    private const string TypesName = "sc/game/telemetry/v1/types.proto";

    // The bundled descriptor, but named like the files embedded in the game.
    private static byte[] BundledDescriptorAs(string name)
    {
        FileDescriptorProto proto = FileDescriptorProto.Parser.ParseFrom(
            ScTelemetryV1Reflection.Descriptor.SerializedData);
        proto.Name = name;
        return proto.ToByteArray();
    }

    // Stands in for an executable: noise, decoys, the embedded descriptors, more noise.
    private static byte[] FakeExecutable(params byte[][] embedded)
    {
        Random random = new(1234);
        List<byte> bytes = [];

        void Noise(int count)
        {
            byte[] noise = new byte[count];
            random.NextBytes(noise);
            bytes.AddRange(noise);
        }

        Noise(5000);
        bytes.AddRange("see sc/game/telemetry/v1 in a log message"u8.ToArray()); // decoy: not a descriptor header
        bytes.AddRange(new byte[] { 0x0A, 0x20 }.Concat("sc/game/telemetry/v1/bogus.proto"u8.ToArray())); // decoy: no fields
        Noise(300);

        foreach (byte[] descriptor in embedded)
        {
            bytes.AddRange(descriptor);
            // Bytes that look like further descriptor fields but are not valid (a message_type that does not parse).
            bytes.AddRange(new byte[] { 0x22, 0x03, 0xFF, 0xFF, 0xFF });
            Noise(777);
        }

        return bytes.ToArray();
    }

    private static FileDescriptorProto TypesFile() => new()
    {
        Name = TypesName,
        Package = "sc.game.telemetry.v1",
        Syntax = "proto3",
        MessageType =
        {
            new DescriptorProto
            {
                Name = "Gauge",
                Field =
                {
                    Field("current", 1, FieldDescriptorProto.Types.Type.Float),
                    Field("max", 2, FieldDescriptorProto.Types.Type.Float)
                }
            }
        }
    };

    private static FileDescriptorProto ApiFile() => new()
    {
        Name = ApiName,
        Package = "sc.game.telemetry.v1",
        Syntax = "proto3",
        Dependency = { TypesName },
        MessageType =
        {
            new DescriptorProto
            {
                Name = "TelemetryStreamResponse",
                Field =
                {
                    new FieldDescriptorProto
                    {
                        Name = "fuel", Number = 1, Label = FieldDescriptorProto.Types.Label.Optional,
                        Type = FieldDescriptorProto.Types.Type.Message, TypeName = ".sc.game.telemetry.v1.Gauge"
                    }
                }
            }
        },
        Service =
        {
            new ServiceDescriptorProto
            {
                Name = "GameTelemetryService",
                Method =
                {
                    new MethodDescriptorProto
                    {
                        Name = "TelemetryStream", InputType = ".sc.game.telemetry.v1.TelemetryStreamResponse",
                        OutputType = ".sc.game.telemetry.v1.TelemetryStreamResponse", ServerStreaming = true
                    }
                }
            }
        }
    };

    private static FieldDescriptorProto Field(string name, int number, FieldDescriptorProto.Types.Type type) => new()
    {
        Name = name, Number = number, Type = type, Label = FieldDescriptorProto.Types.Label.Optional
    };

    [Fact]
    public void Extract_FindsEmbeddedDescriptorAmongNoiseAndDecoys()
    {
        byte[] exe = FakeExecutable(BundledDescriptorAs(ApiName));

        IReadOnlyList<FileDescriptorProto> found = ProtoDescriptorExtractor.Extract(exe);

        found.Should().ContainSingle().Which.Name.Should().Be(ApiName);
    }

    [Fact]
    public void ExtractedSchema_MatchesTheBundledOne()
    {
        byte[] exe = FakeExecutable(BundledDescriptorAs(ApiName));

        IReadOnlyList<FileDescriptor> files = ProtoDescriptorExtractor.Build(ProtoDescriptorExtractor.Extract(exe));
        bool created = TelemetrySchema.TryCreate(files, "test", out TelemetrySchema? schema);

        created.Should().BeTrue();
        schema!.Fields.Select(f => f.Path).Should().Equal(TelemetrySchema.Bundled.Fields.Select(f => f.Path));

        // And it can decode frames produced by the compiled classes.
        TelemetryStreamResponse frame = new() { Actor = new ActorFrame { Vitals = new ActorVitals { Pressure = 7 } } };
        TelemetrySnapshot snapshot = TelemetryFrameDecoder.Decode(schema.Response, frame.ToByteArray());
        snapshot.TryGetValue("actor.vitals.pressure", out TelemetryValue pressure).Should().BeTrue();
        pressure.Number.Should().Be(7);
    }

    [Fact]
    public void Extract_OrdersDependenciesFirstSoTheyCanBeBuilt()
    {
        // api.proto appears before the types.proto it imports, as in a real binary the order is arbitrary.
        byte[] exe = FakeExecutable(ApiFile().ToByteArray(), TypesFile().ToByteArray());

        IReadOnlyList<FileDescriptorProto> found = ProtoDescriptorExtractor.Extract(exe);

        found.Select(f => f.Name).Should().Equal(TypesName, ApiName);
        bool created = TelemetrySchema.TryCreate(ProtoDescriptorExtractor.Build(found), "test", out TelemetrySchema? schema);
        created.Should().BeTrue();
        schema!.Fields.Select(f => f.Path).Should().Contain("fuel.current", "fuel.percent");
    }

    [Fact]
    public void Extract_KeepsTheLargestCopyOfADuplicatedDescriptor()
    {
        FileDescriptorProto small = TypesFile();
        FileDescriptorProto large = TypesFile();
        large.MessageType.Add(new DescriptorProto { Name = "Extra" });

        byte[] exe = FakeExecutable(small.ToByteArray(), large.ToByteArray());

        ProtoDescriptorExtractor.Extract(exe).Should().ContainSingle()
            .Which.MessageType.Should().HaveCount(2);
    }

    [Fact]
    public void Extract_WithoutTelemetryDefinitionsReturnsNothing()
    {
        byte[] noise = new byte[20_000];
        new Random(5).NextBytes(noise);

        ProtoDescriptorExtractor.Extract(noise).Should().BeEmpty();
    }

    [Fact]
    public void ExtractFromFile_ReadsAnExecutableOnDisk()
    {
        string path = Path.Combine(Path.GetTempPath(), $"fake-sc-{Guid.NewGuid():N}.exe");
        try
        {
            File.WriteAllBytes(path, FakeExecutable(BundledDescriptorAs(ApiName)));

            IReadOnlyList<FileDescriptorProto> found = ProtoDescriptorExtractor.ExtractFromFile(path);

            found.Should().ContainSingle().Which.Name.Should().Be(ApiName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Build_ResolvesWellKnownTypeDependencies()
    {
        FileDescriptorProto api = ApiFile();
        api.Dependency.Add("google/protobuf/timestamp.proto");
        api.MessageType[0].Field.Add(new FieldDescriptorProto
        {
            Name = "at", Number = 2, Label = FieldDescriptorProto.Types.Label.Optional,
            Type = FieldDescriptorProto.Types.Type.Message, TypeName = ".google.protobuf.Timestamp"
        });

        IReadOnlyList<FileDescriptor> files = ProtoDescriptorExtractor.Build(
            ProtoDescriptorExtractor.Extract(FakeExecutable(api.ToByteArray(), TypesFile().ToByteArray())));

        TelemetrySchema.TryCreate(files, "test", out TelemetrySchema? schema).Should().BeTrue();
        schema!.Response.FindFieldByName("at").MessageType.FullName.Should().Be("google.protobuf.Timestamp");
    }

    [Fact]
    public void TryCreate_RejectsDefinitionsWithoutTheTelemetryService()
    {
        IReadOnlyList<FileDescriptor> files = ProtoDescriptorExtractor.Build([TypesFile()]);

        TelemetrySchema.TryCreate(files, "test", out TelemetrySchema? schema).Should().BeFalse();
        schema.Should().BeNull();
    }
}
