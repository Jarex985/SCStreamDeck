using FluentAssertions;
using SCStreamDeck.Services.Telemetry;

namespace Tests.Unit.Services.Telemetry;

public sealed class TelemetryRuleTransitionTrackerTests
{
    private static TelemetryRuleMatch Match(int index, string? sound = "a.wav", bool hasData = true) =>
        new(new TelemetryRule { SoundPath = sound }, index, hasData);

    [Fact]
    public void FirstEvaluationOnlyRecordsTheState()
    {
        TelemetryRuleTransitionTracker tracker = new();

        tracker.OnEvaluated(Match(0)).Should().BeNull();
    }

    [Fact]
    public void PlaysOnceWhenARuleBecomesActive()
    {
        TelemetryRuleTransitionTracker tracker = new();
        tracker.OnEvaluated(Match(-1, null, false));

        tracker.OnEvaluated(Match(0)).Should().Be("a.wav");
        tracker.OnEvaluated(Match(0)).Should().BeNull();
        tracker.OnEvaluated(Match(-1, null)).Should().BeNull();
        tracker.OnEvaluated(Match(0)).Should().Be("a.wav");
    }

    [Fact]
    public void SwitchingBetweenRulesPlaysTheNewRulesSound()
    {
        TelemetryRuleTransitionTracker tracker = new();
        tracker.OnEvaluated(Match(0));

        tracker.OnEvaluated(Match(1, "b.mp3")).Should().Be("b.mp3");
        tracker.OnEvaluated(Match(0, null)).Should().BeNull();
    }

    [Fact]
    public void ResetSuppressesTheNextTransition()
    {
        TelemetryRuleTransitionTracker tracker = new();
        tracker.OnEvaluated(Match(-1, null));
        tracker.Reset();

        tracker.OnEvaluated(Match(0)).Should().BeNull();
    }
}
