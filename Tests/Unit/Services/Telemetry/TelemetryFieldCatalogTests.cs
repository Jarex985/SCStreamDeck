using FluentAssertions;
using Sc.Game.Telemetry.V1;
using SCStreamDeck.Services.Telemetry;

namespace Tests.Unit.Services.Telemetry;

public sealed class TelemetryFieldCatalogTests
{
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
    public void Default_ListsScalarFieldsAndSkipsRepeated()
    {
        IReadOnlyList<TelemetryField> fields = TelemetryFieldCatalog.Default;

        fields.Should().Contain(f => f.Path == "actor.vitals.pressure" && f.Kind == TelemetryValueKind.Number);
        fields.Should().Contain(f => f.Path == "actor.vitals.health.percent");
        fields.Should().Contain(f => f.Path == "actor.environment.breathable_atmosphere" && f.Kind == TelemetryValueKind.Boolean);
        fields.Should().Contain(f => f.Path == "actor.state.uec_balance" && f.IsInteger);
        fields.Should().NotContain(f => f.Path.StartsWith("weapons.weapons", StringComparison.Ordinal));
        fields.Should().NotContain(f => f.Path.StartsWith("vehicle.state.shields.faces", StringComparison.Ordinal));
        fields.Select(f => f.Path).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Default_EnumFieldsCarryFriendlyLabels()
    {
        TelemetryField locomotion = TelemetryFieldCatalog.Find("actor.state.locomotion")!;

        locomotion.Kind.Should().Be(TelemetryValueKind.Enum);
        locomotion.EnumValues.Should().Contain(o => o.Name == "ACTOR_LOCOMOTION_STATE_SEATED" && o.Label == "Seated");
    }

    [Fact]
    public void TryGetValue_ReadsNumbersGaugesAndEnums()
    {
        TelemetryStreamResponse frame = Frame(pressure: 42.5f);

        TelemetryFieldCatalog.TryGetValue(frame, "actor.vitals.pressure", out TelemetryValue pressure).Should().BeTrue();
        pressure.Number.Should().BeApproximately(42.5, 0.001);

        TelemetryFieldCatalog.TryGetValue(frame, "actor.vitals.health.percent", out TelemetryValue percent).Should().BeTrue();
        percent.Number.Should().BeApproximately(50, 0.001);

        TelemetryFieldCatalog.TryGetValue(frame, "actor.state.locomotion", out TelemetryValue locomotion).Should().BeTrue();
        locomotion.Text.Should().Be("ACTOR_LOCOMOTION_STATE_SEATED");
        locomotion.DisplayText.Should().Be("Seated");

        TelemetryFieldCatalog.TryGetValue(frame, "actor.state.uec_balance", out TelemetryValue balance).Should().BeTrue();
        balance.IsInteger.Should().BeTrue();
    }

    [Theory]
    [InlineData("vehicle.state.flight.instruments.speed")] // vehicle frame absent
    [InlineData("actor.vitals.oxygen.current")] // gauge absent
    [InlineData("actor.vitals.nope")]
    [InlineData("")]
    public void TryGetValue_ReturnsFalseWhenDataIsMissing(string path)
    {
        TelemetryFieldCatalog.TryGetValue(Frame(), path, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetValue_GaugeWithZeroMax_HasNoPercent()
    {
        TelemetryFieldCatalog.TryGetValue(Frame(healthMax: 0), "actor.vitals.health.percent", out _).Should().BeFalse();
    }
}
