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
