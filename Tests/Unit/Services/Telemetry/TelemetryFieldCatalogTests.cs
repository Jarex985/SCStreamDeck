using FluentAssertions;
using Google.Protobuf;
using Sc.Game.Telemetry.V1;
using SCStreamDeck.Services.Telemetry;

namespace Tests.Unit.Services.Telemetry;

public sealed class TelemetryFieldCatalogTests
{
    private static TelemetrySchema Schema => TelemetrySchema.Bundled;

    private static TelemetrySnapshot Decode(TelemetryStreamResponse frame) =>
        TelemetryFrameDecoder.Decode(Schema.Response, frame.ToByteArray());

    private static TelemetryStreamResponse Frame(float pressure = 101f, float health = 50f, float healthMax = 100f) => new()
    {
        Actor = new ActorFrame
        {
            Vitals = new ActorVitals { Pressure = pressure, Health = new Gauge { Current = health, Max = healthMax } },
            State = new ActorState { Locomotion = ActorLocomotionState.Seated, UecBalance = 12345 },
            Environment = new ActorEnvironment { BreathableAtmosphere = true }
        }
    };

    [Fact]
    public void Fields_ListScalarFieldsAndSkipRepeated()
    {
        IReadOnlyList<TelemetryField> fields = Schema.Fields;

        fields.Should().Contain(f => f.Path == "actor.vitals.pressure" && f.Kind == TelemetryValueKind.Number);
        fields.Should().Contain(f => f.Path == "actor.vitals.health.percent");
        fields.Should().Contain(f => f.Path == "actor.environment.breathable_atmosphere" && f.Kind == TelemetryValueKind.Boolean);
        fields.Should().Contain(f => f.Path == "actor.state.uec_balance" && f.IsInteger);
        fields.Should().NotContain(f => f.Path.StartsWith("weapons.weapons", StringComparison.Ordinal));
        fields.Should().NotContain(f => f.Path.StartsWith("vehicle.state.shields.faces", StringComparison.Ordinal));
        fields.Select(f => f.Path).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Fields_EnumsCarryFriendlyLabels()
    {
        TelemetryField locomotion = Schema.Find("actor.state.locomotion")!;

        locomotion.Kind.Should().Be(TelemetryValueKind.Enum);
        locomotion.EnumValues.Should().Contain(o => o.Name == "ACTOR_LOCOMOTION_STATE_SEATED" && o.Label == "Seated");
    }

    [Fact]
    public void Decoder_ReadsNumbersGaugesAndEnums()
    {
        TelemetrySnapshot snapshot = Decode(Frame(pressure: 42.5f));

        snapshot.TryGetValue("actor.vitals.pressure", out TelemetryValue pressure).Should().BeTrue();
        pressure.Number.Should().BeApproximately(42.5, 0.001);

        snapshot.TryGetValue("actor.vitals.health.percent", out TelemetryValue percent).Should().BeTrue();
        percent.Number.Should().BeApproximately(50, 0.001);

        snapshot.TryGetValue("actor.state.locomotion", out TelemetryValue locomotion).Should().BeTrue();
        locomotion.Text.Should().Be("ACTOR_LOCOMOTION_STATE_SEATED");
        locomotion.DisplayText.Should().Be("Seated");

        snapshot.TryGetValue("actor.state.uec_balance", out TelemetryValue balance).Should().BeTrue();
        balance.IsInteger.Should().BeTrue();
        balance.Number.Should().Be(12345);
    }

    [Fact]
    public void Decoder_FillsDefaultsOfPresentMessagesButNotOfAbsentOnes()
    {
        // Pressure 0 and a false bool are not on the wire in proto3, but the message is present.
        TelemetrySnapshot snapshot = Decode(new TelemetryStreamResponse
        {
            Actor = new ActorFrame { Vitals = new ActorVitals { HeartRate = 70 } }
        });

        snapshot.TryGetValue("actor.vitals.pressure", out TelemetryValue pressure).Should().BeTrue();
        pressure.Number.Should().Be(0);
        snapshot.TryGetValue("actor.vitals.oxygen.current", out _).Should().BeFalse(); // gauge message absent
        snapshot.TryGetValue("actor.environment.breathable_atmosphere", out _).Should().BeFalse(); // message absent
        snapshot.TryGetValue("vehicle.state.name", out _).Should().BeFalse(); // part absent
    }

    [Fact]
    public void Decoder_OptionalScalarsExistOnlyWhenSent()
    {
        TelemetrySnapshot without = Decode(new TelemetryStreamResponse
        {
            Vehicle = new VehicleFrame { State = new VehicleState { Name = "Ship", Flight = new VehicleFlight() } }
        });
        TelemetrySnapshot with = Decode(new TelemetryStreamResponse
        {
            Vehicle = new VehicleFrame
            {
                State = new VehicleState
                {
                    Name = "Ship",
                    Flight = new VehicleFlight
                    {
                        State = new VehicleFlightState { IsLanded = true, Gear = VehicleComponentDeploymentState.Deployed }
                    }
                }
            }
        });

        without.TryGetValue("vehicle.state.name", out TelemetryValue name).Should().BeTrue();
        name.Text.Should().Be("Ship");
        without.TryGetValue("vehicle.state.flight.state.gear", out _).Should().BeFalse();
        with.TryGetValue("vehicle.state.flight.state.gear", out TelemetryValue gear).Should().BeTrue();
        gear.DisplayText.Should().Be("Deployed");
        with.TryGetValue("vehicle.state.flight.state.is_landed", out TelemetryValue landed).Should().BeTrue();
        landed.Boolean.Should().BeTrue();
    }

    [Theory]
    [InlineData("vehicle.state.flight.instruments.speed")] // vehicle part absent
    [InlineData("actor.vitals.oxygen.current")] // gauge absent
    [InlineData("actor.vitals.nope")]
    [InlineData("actor")]
    [InlineData("")]
    public void Decoder_MissingDataIsAbsent(string path)
    {
        Decode(Frame()).TryGetValue(path, out _).Should().BeFalse();
    }

    [Fact]
    public void Decoder_GaugeWithZeroMaxHasNoPercent()
    {
        Decode(Frame(healthMax: 0)).TryGetValue("actor.vitals.health.percent", out _).Should().BeFalse();
    }

    [Fact]
    public void Decoder_IgnoresUnknownFieldsAndRepeatedFields()
    {
        TelemetryStreamResponse frame = Frame();
        frame.Weapons = new WeaponsFrame { PresetName = "A", Weapons = { new WeaponState { Name = "gun" } } };
        byte[] bytes = frame.ToByteArray().Concat(new byte[] { 0xA8, 0x06, 0x01 }).ToArray(); // unknown field 105 varint

        TelemetrySnapshot snapshot = TelemetryFrameDecoder.Decode(Schema.Response, bytes);

        snapshot.TryGetValue("weapons.preset_name", out TelemetryValue preset).Should().BeTrue();
        preset.Text.Should().Be("A");
        snapshot.TryGetValue("actor.vitals.pressure", out _).Should().BeTrue();
    }
}
