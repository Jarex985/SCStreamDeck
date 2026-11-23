using FluentAssertions;
using SCStreamDeck.Services.Telemetry;

namespace Tests.Unit.Services.Telemetry;

public sealed class TelemetryRuleEvaluatorTests
{
    private static TelemetryValue Number(double n) => new(TelemetryValueKind.Number, n);

    private static TelemetryRule Rule(string op, string? value, string? value2 = null, string? text = null,
        string? function = null) =>
        new() { Op = op, Value = value, Value2 = value2, Text = text, Function = function };

    [Fact]
    public void FirstMatchingRuleWins()
    {
        TelemetryDisplaySettings settings = new()
        {
            Rules = [Rule("lt", "25", text: "critical"), Rule("lt", "50", text: "low")],
            DefaultRule = new TelemetryRule { Text = "ok" }
        };

        TelemetryRuleEvaluator.Evaluate(settings, Number(10)).Rule.Text.Should().Be("critical");
        TelemetryRuleEvaluator.Evaluate(settings, Number(30)).Rule.Text.Should().Be("low");

        TelemetryRuleMatch fallback = TelemetryRuleEvaluator.Evaluate(settings, Number(80));
        fallback.Rule.Text.Should().Be("ok");
        fallback.Index.Should().Be(-1);
        fallback.HasData.Should().BeTrue();
    }

    [Fact]
    public void WithoutData_OnlyTheDefaultRuleApplies()
    {
        TelemetryDisplaySettings settings = new()
        {
            Rules = [Rule("lt", "25", function: "v_toggle_lights")],
            DefaultRule = new TelemetryRule { Text = "n/a" }
        };

        TelemetryRuleMatch match = TelemetryRuleEvaluator.Evaluate(settings, null);

        match.Rule.Text.Should().Be("n/a");
        match.HasData.Should().BeFalse();
    }

    [Theory]
    [InlineData("lt", "5", null, 4.9, true)]
    [InlineData("lt", "5", null, 5.0, false)]
    [InlineData("lte", "5", null, 5.0, true)]
    [InlineData("gt", "5", null, 5.1, true)]
    [InlineData("gte", "5", null, 5.0, true)]
    [InlineData("eq", "5", null, 5.0, true)]
    [InlineData("neq", "5", null, 5.0, false)]
    [InlineData("between", "10", "20", 15.0, true)]
    [InlineData("between", "20", "10", 10.0, true)]
    [InlineData("between", "10", "20", 21.0, false)]
    [InlineData("lt", "2,5", null, 2.0, true)]
    [InlineData("lt", "abc", null, 1.0, false)]
    [InlineData("between", "1", "", 1.0, false)]
    public void NumericOperators(string op, string value, string? value2, double actual, bool expected)
    {
        TelemetryRuleEvaluator.Matches(Rule(op, value, value2), Number(actual)).Should().Be(expected);
    }

    [Theory]
    [InlineData("eq", "true", true, true)]
    [InlineData("eq", "true", false, false)]
    [InlineData("neq", "true", false, true)]
    [InlineData("eq", "maybe", true, false)]
    public void BooleanOperators(string op, string value, bool actual, bool expected)
    {
        TelemetryValue value1 = new(TelemetryValueKind.Boolean, Boolean: actual);

        TelemetryRuleEvaluator.Matches(Rule(op, value), value1).Should().Be(expected);
    }

    [Fact]
    public void EnumOperators_CompareProtoNamesIgnoringCase()
    {
        TelemetryValue seated = new(TelemetryValueKind.Enum, Text: "ACTOR_LOCOMOTION_STATE_SEATED");

        TelemetryRuleEvaluator.Matches(Rule("eq", "actor_locomotion_state_seated"), seated).Should().BeTrue();
        TelemetryRuleEvaluator.Matches(Rule("neq", "ACTOR_LOCOMOTION_STATE_EVA"), seated).Should().BeTrue();
        TelemetryRuleEvaluator.Matches(Rule("lt", "1"), seated).Should().BeFalse();
    }
}

public sealed class TelemetryAdvancedRuleTests
{
    [Fact]
    public void PressActions_PreferTheListOverTheSingleFunction()
    {
        TelemetryRule rule = new()
        {
            Function = "v_toggle_lights",
            Actions =
            [
                new TelemetryPressAction { Function = "v_toggle_landing_system", DelayMs = 100 },
                new TelemetryPressAction { Function = "  " },
                new TelemetryPressAction { Function = "v_toggle_vtol" }
            ]
        };

        rule.GetPressActions().Select(a => a.Function).Should().Equal("v_toggle_landing_system", "v_toggle_vtol");
    }

    [Fact]
    public void PressActions_FallBackToTheSingleFunctionOrNothing()
    {
        new TelemetryRule { Function = "v_toggle_lights" }.GetPressActions()
            .Should().ContainSingle().Which.Function.Should().Be("v_toggle_lights");
        new TelemetryRule { Actions = [new TelemetryPressAction()] }.GetPressActions().Should().BeEmpty();
        new TelemetryRule().GetPressActions().Should().BeEmpty();
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(-5, 0)]
    [InlineData(250, 250)]
    [InlineData(10_000_000, TelemetryPressAction.MaxDelayMs)]
    public void PressActionDelay_IsClamped(int? delay, int expected) =>
        new TelemetryPressAction { DelayMs = delay }.EffectiveDelayMs.Should().Be(expected);

    private static TelemetryCondition Cond(string path, string op, string? value, string? value2 = null) =>
        new() { FieldPath = path, Op = op, Value = value, Value2 = value2 };

    private static Func<string, TelemetryValue?> Values(bool? landed, double? fuel) => path => path switch
    {
        "landed" when landed is { } l => new TelemetryValue(TelemetryValueKind.Boolean, Boolean: l),
        "fuel" when fuel is { } f => new TelemetryValue(TelemetryValueKind.Number, f),
        _ => null
    };

    private static TelemetryDisplaySettings LandedAndLowFuel(string? match = null) => new()
    {
        Rules =
        [
            new TelemetryRule
            {
                Text = "REFUEL",
                Match = match,
                Conditions = [Cond("landed", "eq", "true"), Cond("fuel", "lt", "20")]
            }
        ],
        DefaultRule = new TelemetryRule { Text = "ok" }
    };

    [Theory]
    [InlineData(true, 10.0, true)]
    [InlineData(true, 50.0, false)]
    [InlineData(false, 10.0, false)]
    public void AllConditionsMustHold(bool landed, double fuel, bool expected)
    {
        TelemetryRuleMatch match = TelemetryRuleEvaluator.EvaluateWithResolver(LandedAndLowFuel(), Values(landed, fuel));

        (match.Index == 0).Should().Be(expected);
        match.HasData.Should().BeTrue();
    }

    [Theory]
    [InlineData(true, 50.0, true)]
    [InlineData(false, 10.0, true)]
    [InlineData(false, 50.0, false)]
    public void AnyModeNeedsOneCondition(bool landed, double fuel, bool expected)
    {
        TelemetryRuleMatch match = TelemetryRuleEvaluator.EvaluateWithResolver(LandedAndLowFuel("any"), Values(landed, fuel));

        (match.Index == 0).Should().Be(expected);
    }

    [Fact]
    public void ConditionOnAttributeWithoutDataIsFalse()
    {
        TelemetryRuleMatch ship = TelemetryRuleEvaluator.EvaluateWithResolver(LandedAndLowFuel(), Values(null, null));
        TelemetryRuleMatch partial = TelemetryRuleEvaluator.EvaluateWithResolver(LandedAndLowFuel(), Values(true, null));

        ship.Index.Should().Be(-1);
        ship.HasData.Should().BeFalse();
        partial.Index.Should().Be(-1);
        partial.HasData.Should().BeTrue();
    }

    [Fact]
    public void SimpleAndAdvancedRulesCanBeMixed()
    {
        TelemetryDisplaySettings settings = new()
        {
            FieldPath = "fuel",
            Rules =
            [
                new TelemetryRule { Op = "lt", Value = "5", Text = "empty" },
                new TelemetryRule { Text = "landed", Conditions = [Cond("landed", "eq", "true")] }
            ]
        };

        TelemetryRuleEvaluator.EvaluateWithResolver(settings, Values(true, 2)).Rule.Text.Should().Be("empty");
        TelemetryRuleEvaluator.EvaluateWithResolver(settings, Values(true, 80)).Rule.Text.Should().Be("landed");
    }

    [Fact]
    public void Apply_ReplacesFieldTokens()
    {
        string text = TelemetryTextFormatter.Apply("Fuel {value:fuel} / {VALUE:speed}", "x", "y", path => $"<{path}>");

        text.Should().Be("Fuel <fuel> / <speed>");
        TelemetryTextFormatter.Apply("{value:fuel}", "x", "y").Should().Be("{value:fuel}");
    }
}

public sealed class TelemetryTextFormatterTests
{
    [Theory]
    [InlineData(false, null, 12.345, "12.3")]
    [InlineData(true, null, 12345, "12,345")]
    [InlineData(false, 0, 12.6, "13")]
    [InlineData(false, 2, 12.345, "12.35")]
    public void FormatValue_AppliesDecimals(bool integer, int? decimals, double number, string expected)
    {
        TelemetryValue value = new(TelemetryValueKind.Number, number, IsInteger: integer);

        string text = TelemetryTextFormatter.FormatValue(value, decimals);

        text.Should().Be(expected);
    }

    [Fact]
    public void FormatValue_WithoutData_ShowsPlaceholder() =>
        TelemetryTextFormatter.FormatValue(null, null).Should().Be(TelemetryTextFormatter.NoDataText);

    [Fact]
    public void Apply_ReplacesTokensAndLiteralNewlines()
    {
        TelemetryTextFormatter.Apply("{label}\\n{value} kPa", "Pressure", "12.3").Should().Be("Pressure\n12.3 kPa");
        TelemetryTextFormatter.Apply(null, "Pressure", "12.3").Should().Be("Pressure\n12.3");
    }
}
