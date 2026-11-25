using System.Globalization;
using FluentAssertions;
using SCStreamDeck.Services.Telemetry;

namespace Tests.Unit.Services.Telemetry;

public sealed class UecTests
{
    private static readonly DateTime s_t0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static TelemetrySnapshot Balance(double value) => new(new Dictionary<string, IReadOnlyDictionary<string, TelemetryValue>>
    {
        ["actor"] = new Dictionary<string, TelemetryValue>
        {
            [UecBalanceEvaluator.BalancePath] = new(TelemetryValueKind.Number, value, IsInteger: true)
        }
    });

    private static TelemetryEvaluation Evaluate(
        UecBalanceEvaluator evaluator, double? balance, TimeSpan at, TelemetryDisplaySettings? settings = null) =>
        evaluator.Evaluate(
            settings ?? new TelemetryDisplaySettings(), balance is { } b ? Balance(b) : null, TelemetrySchema.Bundled,
            s_t0 + at);

    [Theory]
    [InlineData(0, "0")]
    [InlineData(1234, "1,234")]
    [InlineData(-1234, "(1,234)")]
    [InlineData(-5, "(5)")]
    public void FormatAccounting_UsesBracketsForNegativesWithoutSymbolOrDecimals(long amount, string expected)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            TelemetryTextFormatter.FormatAccounting(amount).Should().Be(expected);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Tracker_CountsFromTheFirstBalanceAndRestartsOnReset()
    {
        UecSessionTracker tracker = new();

        tracker.Observe(1000, s_t0)!.Value.Change.Should().Be(0);
        tracker.Observe(1500, s_t0.AddSeconds(1))!.Value.Should().Be(new UecReading(1500, 500));
        tracker.Observe(900, s_t0.AddSeconds(2))!.Value.Change.Should().Be(-100);

        tracker.Reset();

        tracker.Observe(900, s_t0.AddSeconds(3))!.Value.Change.Should().Be(0);
        tracker.Observe(950, s_t0.AddSeconds(4))!.Value.Change.Should().Be(50);
    }

    [Fact]
    public void Tracker_ResetBeforeAnyBalanceStartsAtTheNextOne()
    {
        UecSessionTracker tracker = new();

        tracker.Reset();

        tracker.Observe(null, s_t0).Should().BeNull();
        tracker.Observe(700, s_t0.AddSeconds(1))!.Value.Change.Should().Be(0);
    }

    [Fact]
    public void Tracker_StartsANewSessionAfterALongGapButNotAfterABriefOne()
    {
        UecSessionTracker tracker = new();
        tracker.Observe(1000, s_t0);

        tracker.Observe(1100, s_t0.AddSeconds(30))!.Value.Change.Should().Be(100);
        tracker.Observe(5000, s_t0.AddSeconds(30) + UecSessionTracker.SessionGap + TimeSpan.FromSeconds(1))!.Value.Change
            .Should().Be(0);
    }

    [Fact]
    public void Evaluator_ShowsBalanceAndColoredChangeLine()
    {
        UecBalanceEvaluator evaluator = new(new UecSessionTracker());

        TelemetryEvaluation start = Evaluate(evaluator, 10_000, TimeSpan.Zero);
        TelemetryEvaluation gain = Evaluate(evaluator, 12_500, TimeSpan.FromSeconds(1));
        TelemetryEvaluation loss = Evaluate(evaluator, 9_000, TimeSpan.FromSeconds(2));

        start.Text.Split('\n').Should().Equal(10_000.ToString("N0", CultureInfo.CurrentCulture), "0");
        start.LineColors.Should().BeNull();

        gain.Text.Split('\n')[1].Should().Be(2_500.ToString("N0", CultureInfo.CurrentCulture));
        gain.LineColors.Should().Equal(null, UecBalanceEvaluator.GainColor);

        loss.Text.Split('\n')[1].Should().Be($"({1_000.ToString("N0", CultureInfo.CurrentCulture)})");
        loss.LineColors.Should().Equal(null, UecBalanceEvaluator.LossColor);
    }

    [Fact]
    public void Evaluator_PressResetsTheChange()
    {
        UecBalanceEvaluator evaluator = new(new UecSessionTracker());
        Evaluate(evaluator, 1_000, TimeSpan.Zero);
        Evaluate(evaluator, 3_000, TimeSpan.FromSeconds(1)).LineColors.Should().NotBeNull();

        evaluator.OnKeyPressed();

        TelemetryEvaluation after = Evaluate(evaluator, 3_000, TimeSpan.FromSeconds(2));
        after.Text.Split('\n')[1].Should().Be("0");
        after.LineColors.Should().BeNull();
    }

    [Fact]
    public void Evaluator_WithoutDataShowsPlaceholdersAndTheDefaultRule()
    {
        UecBalanceEvaluator evaluator = new(new UecSessionTracker());
        TelemetryDisplaySettings settings = new() { DefaultRule = new TelemetryRule { TextColor = "#00FF00" } };

        TelemetryEvaluation result = Evaluate(evaluator, null, TimeSpan.Zero, settings);

        result.Text.Should().Be("—\n—");
        result.Match.Index.Should().Be(-1);
        result.Match.Rule.TextColor.Should().Be("#00FF00");
    }

    [Fact]
    public void Evaluator_RulesCanTestTheBalanceAndTheChange()
    {
        UecBalanceEvaluator evaluator = new(new UecSessionTracker());
        TelemetryDisplaySettings settings = new()
        {
            Rules =
            [
                new TelemetryRule
                {
                    Conditions = [new TelemetryCondition { FieldPath = UecBalanceEvaluator.ChangePath, Op = TelemetryRule.OpLessThan, Value = "0" }],
                    Text = "LOST\n{change}"
                },
                new TelemetryRule { Op = TelemetryRule.OpLessThan, Value = "100", Text = "BROKE {value}" }
            ]
        };

        Evaluate(evaluator, 1_000, TimeSpan.Zero, settings).Match.Index.Should().Be(-1);

        TelemetryEvaluation lost = Evaluate(evaluator, 950, TimeSpan.FromSeconds(1), settings);
        lost.Match.Index.Should().Be(0);
        lost.Text.Should().Be("LOST\n(50)");
        lost.LineColors.Should().Equal(null, UecBalanceEvaluator.LossColor);

        Evaluate(evaluator, 90, TimeSpan.FromSeconds(2), settings).Text.Should().Be("LOST\n(910)");
    }
}
