using FluentAssertions;
using SCStreamDeck.Services.Telemetry;

namespace Tests.Unit.Services.Telemetry;

public sealed class DecelerationTests
{
    private const string SpeedPath = "vehicle.state.flight.instruments.speed";
    private static readonly DateTime s_t0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int ms) => s_t0.AddMilliseconds(ms);

    private static TelemetrySnapshot Speed(double value) => new(new Dictionary<string, IReadOnlyDictionary<string, TelemetryValue>>
    {
        ["vehicle"] = new Dictionary<string, TelemetryValue>
        {
            [SpeedPath] = new(TelemetryValueKind.Number, value)
        }
    });

    private static TelemetryDisplaySettings Settings(
        string output = "both", double? unitsRounding = null, double? timeRounding = null, string? unit = "m") => new()
    {
        FieldPath = SpeedPath,
        Deceleration = new TelemetryDecelerationSettings
        {
            Output = output, UnitsRounding = unitsRounding, TimeRounding = timeRounding, Unit = unit
        }
    };

    [Fact]
    public void Estimator_NeedsAFullWindowThenAssumesAConstantRate()
    {
        DecelerationEstimator estimator = new();
        TimeSpan window = TimeSpan.FromSeconds(1);

        // 300 m/s dropping by 20 m/s every second, sampled every 100 ms.
        for (int i = 0; i < 10; i++)
        {
            estimator.Update(At(i * 100), 300 - (20 * i / 10.0), window).Should().BeNull();
        }

        DecelerationEstimate estimate = estimator.Update(At(1000), 280, window)!.Value;

        estimate.TimeSeconds.Should().BeApproximately(14, 0.001); // 280 / 20
        estimate.Units.Should().BeApproximately(1960, 0.001); // 280^2 / (2 * 20)
    }

    [Fact]
    public void Estimator_UsesTheConfiguredWindow()
    {
        DecelerationEstimator estimator = new();
        TimeSpan window = TimeSpan.FromSeconds(2);

        // Accelerates for 2 s, then brakes by 10 per second: a 1 s window would see braking earlier.
        for (int i = 0; i <= 20; i++)
        {
            estimator.Update(At(i * 100), 100 + (10 * i / 10.0), window);
        }

        DecelerationEstimate? estimate = null;
        for (int i = 21; i <= 40; i++)
        {
            estimate = estimator.Update(At(i * 100), 120 - (10 * (i - 20) / 10.0), window);
        }

        // At t = 4 s: value 100; two seconds earlier (t = 2 s) it was 120, so rate = 10/s.
        estimate!.Value.TimeSeconds.Should().BeApproximately(10, 0.01);
        estimate.Value.Units.Should().BeApproximately(500, 0.01);
    }

    [Theory]
    [InlineData(100, 100)] // constant
    [InlineData(100, 80)] // accelerating
    [InlineData(0, 10)] // already stopped
    public void Estimator_ReturnsNothingWithoutADeceleration(double now, double before)
    {
        DecelerationEstimator estimator = new();
        TimeSpan window = TimeSpan.FromSeconds(1);

        estimator.Update(At(0), before, window);

        estimator.Update(At(1000), now, window).Should().BeNull();
    }

    [Fact]
    public void Estimator_MissingDataClearsTheHistory()
    {
        DecelerationEstimator estimator = new();
        TimeSpan window = TimeSpan.FromSeconds(1);
        estimator.Update(At(0), 100, window);
        estimator.Update(At(1000), 90, window).Should().NotBeNull();

        estimator.Update(At(1100), null, window).Should().BeNull();

        // The window has to be covered again after the data came back.
        estimator.Update(At(1200), 85, window).Should().BeNull();
        estimator.Update(At(2200), 75, window).Should().NotBeNull();
    }

    [Theory]
    [InlineData(1234.4, 50.0, "1,250")]
    [InlineData(12.34, 0.5, "12.5")]
    [InlineData(7.26, null, "7.3")]
    [InlineData(7.26, 0.01, "7.26")]
    [InlineData(14.04, 0.0, "14.0")]
    [InlineData(1234.4, 1.0, "1,234")]
    public void FormatRounded_RoundsToTheStep(double value, double? step, string expected) =>
        TelemetryTextFormatter.FormatRounded(value, step).Should().Be(expected);

    [Fact]
    public void Evaluator_ShowsUnitsAndTimeOnceTheWindowIsCovered()
    {
        DecelerationEvaluator evaluator = new();
        TelemetryDisplaySettings settings = Settings(unitsRounding: 10);
        TelemetrySchema schema = TelemetrySchema.Bundled;

        TelemetryEvaluation first = evaluator.Evaluate(settings, Speed(300), schema, At(0));
        TelemetryEvaluation later = evaluator.Evaluate(settings, Speed(280), schema, At(1000));

        first.Text.Should().Be("—\n—");
        first.Match.Index.Should().Be(-1);
        later.Text.Should().Be("1,960 m\n14.0 s");
    }

    [Fact]
    public void Evaluator_UnitsCanBeShownInAnotherOrderOfMagnitude()
    {
        TelemetryDisplaySettings settings = Settings(unitsRounding: 0.5, unit: "km");
        settings.Deceleration.UnitsDivisor = 1000;
        settings.Rules = [new TelemetryRule { Op = "lt", Value = "2", Text = "NEAR {units}" }];
        DecelerationEvaluator evaluator = new();

        evaluator.Evaluate(settings, Speed(300), TelemetrySchema.Bundled, At(0));
        TelemetryEvaluation result = evaluator.Evaluate(settings, Speed(280), TelemetrySchema.Bundled, At(1000));

        // 1960 m = 1.96 km: rounded to the nearest 0.5 km and compared by rules in km.
        result.Text.Should().Be("NEAR 2.0 km");
        result.Match.Index.Should().Be(0);
    }

    [Theory]
    [InlineData(null, 1.0)]
    [InlineData(0.0, 1.0)]
    [InlineData(-5.0, 1.0)]
    [InlineData(1000.0, 1000.0)]
    public void UnitsDivisor_FallsBackToOne(double? configured, double expected) =>
        new TelemetryDecelerationSettings { UnitsDivisor = configured }.EffectiveUnitsDivisor.Should().Be(expected);

    private static TelemetrySnapshot Ship(double speed, bool landed) =>
        new(new Dictionary<string, IReadOnlyDictionary<string, TelemetryValue>>
        {
            ["vehicle"] = new Dictionary<string, TelemetryValue>
            {
                [SpeedPath] = new(TelemetryValueKind.Number, speed),
                ["vehicle.state.flight.state.is_landed"] =
                    new(TelemetryValueKind.Boolean, Boolean: landed, DisplayText: landed ? "On" : "Off")
            }
        });

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Evaluator_RulesCanCombineTheEstimateWithOtherAttributes(bool landed, bool expectedMatch)
    {
        TelemetryDisplaySettings settings = Settings();
        settings.Rules =
        [
            new TelemetryRule
            {
                Match = "all",
                Text = "BRAKE {value:deceleration.time}",
                Conditions =
                [
                    new TelemetryCondition { FieldPath = DecelerationEvaluator.TimePath, Op = "lt", Value = "20" },
                    new TelemetryCondition { FieldPath = "vehicle.state.flight.state.is_landed", Op = "eq", Value = "false" }
                ]
            }
        ];
        DecelerationEvaluator evaluator = new();

        evaluator.Evaluate(settings, Ship(300, landed), TelemetrySchema.Bundled, At(0));
        TelemetryEvaluation result = evaluator.Evaluate(settings, Ship(280, landed), TelemetrySchema.Bundled, At(1000));

        (result.Match.Index == 0).Should().Be(expectedMatch);
        if (expectedMatch)
        {
            result.Text.Should().Be("BRAKE 14.0");
        }
    }

    [Fact]
    public void Evaluator_ConditionsOnTheEstimateNeedAnEstimate()
    {
        TelemetryDisplaySettings settings = Settings();
        settings.Rules =
        [
            new TelemetryRule
            {
                Conditions = [new TelemetryCondition { FieldPath = DecelerationEvaluator.UnitsPath, Op = "gt", Value = "0" }]
            }
        ];
        DecelerationEvaluator evaluator = new();

        evaluator.Evaluate(settings, Ship(300, false), TelemetrySchema.Bundled, At(0));
        TelemetryEvaluation steady = evaluator.Evaluate(settings, Ship(300, false), TelemetrySchema.Bundled, At(1000));

        steady.Match.Index.Should().Be(-1); // not decelerating: no estimate, condition is false
    }

    [Fact]
    public void Evaluator_OutputChoiceAndRoundingApply()
    {
        TelemetrySchema schema = TelemetrySchema.Bundled;

        DecelerationEvaluator units = new();
        units.Evaluate(Settings("units", 50), Speed(300), schema, At(0));
        units.Evaluate(Settings("units", 50), Speed(280), schema, At(1000)).Text.Should().Be("Speed\n1,950 m");

        DecelerationEvaluator time = new();
        time.Evaluate(Settings("time", null, 5), Speed(300), schema, At(0));
        time.Evaluate(Settings("time", null, 5), Speed(280), schema, At(1000)).Text.Should().Be("Speed\n15 s");
    }

    [Fact]
    public void Evaluator_RulesCompareTheEstimateAndTokensCanBeUsed()
    {
        TelemetryDisplaySettings settings = Settings("time");
        settings.Rules =
        [
            new TelemetryRule { Op = "lt", Value = "20", Text = "STOP IN {time}\n{units}", Function = "v_toggle_lights" }
        ];
        DecelerationEvaluator evaluator = new();
        TelemetrySchema schema = TelemetrySchema.Bundled;

        evaluator.Evaluate(settings, Speed(300), schema, At(0));
        TelemetryEvaluation result = evaluator.Evaluate(settings, Speed(280), schema, At(1000));

        result.Match.Index.Should().Be(0);
        result.Match.HasData.Should().BeTrue();
        result.Text.Should().Be("STOP IN 14.0 s\n1,960.0 m");
    }

    [Fact]
    public void Evaluator_WithoutDecelerationUsesTheDefaultRule()
    {
        TelemetryDisplaySettings settings = Settings();
        settings.DefaultRule = new TelemetryRule { Text = "cruising" };
        DecelerationEvaluator evaluator = new();

        evaluator.Evaluate(settings, Speed(300), TelemetrySchema.Bundled, At(0));
        TelemetryEvaluation result = evaluator.Evaluate(settings, Speed(300), TelemetrySchema.Bundled, At(1000));

        result.Text.Should().Be("cruising");
        result.Match.Index.Should().Be(-1);
        result.Match.HasData.Should().BeFalse();
    }

    [Fact]
    public void Evaluator_RequiresANumericAttribute()
    {
        DecelerationEvaluator evaluator = new();
        TelemetryDisplaySettings settings = new() { FieldPath = "actor.state.locomotion" };

        evaluator.Evaluate(settings, null, TelemetrySchema.Bundled, At(0)).Text.Should().Contain("numeric");
        evaluator.Evaluate(new TelemetryDisplaySettings(), null, TelemetrySchema.Bundled, At(0)).Text.Should().Contain("numeric");
    }
}
