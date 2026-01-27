using FluentAssertions;
using SCStreamDeck.ActionKeys;

namespace Tests.Unit.ActionKeys;

public sealed class ToggleKeyCoreTests
{
    [Fact]
    public void OnKeyDown_DefaultsVisualStateToOff()
    {
        ToggleKeyCore core = new(TimeSpan.FromSeconds(1));

        core.GetVisualState().Should().Be(0);
    }

    [Fact]
    public void ShortPress_KeyUp_BeforeThreshold_RequestsExecution()
    {
        ToggleKeyCore core = new(TimeSpan.FromSeconds(1));

        DateTime t0 = DateTime.UtcNow;
        int pressId = core.OnKeyDown(t0);

        ToggleKeyDecision decision = core.OnKeyUp(t0.AddMilliseconds(200), pressId);

        decision.ExecuteId.Should().NotBeNull();
        decision.ImmediateEffects.Should().BeEmpty();
    }

    [Fact]
    public void ShortPress_ExecutionSuccess_FlipsVisualState_AndPlaysClick()
    {
        ToggleKeyCore core = new(TimeSpan.FromSeconds(1));

        DateTime t0 = DateTime.UtcNow;
        int pressId = core.OnKeyDown(t0);
        ToggleKeyDecision decision = core.OnKeyUp(t0.AddMilliseconds(10), pressId);

        decision.ExecuteId.Should().NotBeNull();
        IReadOnlyList<ToggleKeyEffect> effects = core.OnExecutionCompleted(decision.ExecuteId!.Value, true);

        effects.Should().Contain(e => e.Kind == ToggleKeyEffectKind.SetVisualState && e.State == 1);
        effects.Should().Contain(e => e.Kind == ToggleKeyEffectKind.PlayClickSound);
        core.GetVisualState().Should().Be(1);
    }

    [Fact]
    public void ShortPress_ExecutionFailure_DoesNotFlipOrClick()
    {
        ToggleKeyCore core = new(TimeSpan.FromSeconds(1));

        DateTime t0 = DateTime.UtcNow;
        int pressId = core.OnKeyDown(t0);
        ToggleKeyDecision decision = core.OnKeyUp(t0.AddMilliseconds(10), pressId);

        IReadOnlyList<ToggleKeyEffect> effects = core.OnExecutionCompleted(decision.ExecuteId!.Value, false);

        effects.Should().BeEmpty();
        core.GetVisualState().Should().Be(0);
    }

    [Fact]
    public void LongHold_TimerElapsed_FlipsState_Clicks_AndSuppressesExecutionOnKeyUp()
    {
        ToggleKeyCore core = new(TimeSpan.FromSeconds(1));

        DateTime t0 = DateTime.UtcNow;
        int pressId = core.OnKeyDown(t0);

        ToggleKeyDecision threshold = core.OnHoldThresholdElapsed(pressId);
        threshold.ExecuteId.Should().BeNull();
        threshold.ImmediateEffects.Should().Contain(e => e.Kind == ToggleKeyEffectKind.SetVisualState && e.State == 1);
        threshold.ImmediateEffects.Should().Contain(e => e.Kind == ToggleKeyEffectKind.PlayClickSound);

        ToggleKeyDecision keyUp = core.OnKeyUp(t0.AddSeconds(2), pressId);
        keyUp.ExecuteId.Should().BeNull();
        keyUp.ImmediateEffects.Should().BeEmpty();
    }

    [Fact]
    public void KeyUp_JustBeforeThreshold_DoesNotTriggerReset_DefersToExecution()
    {
        ToggleKeyCore core = new(TimeSpan.FromSeconds(1));

        DateTime t0 = DateTime.UtcNow;
        int pressId = core.OnKeyDown(t0);

        ToggleKeyDecision keyUp = core.OnKeyUp(t0.AddMilliseconds(999), pressId);

        keyUp.ExecuteId.Should().NotBeNull();
        keyUp.ImmediateEffects.Should().BeEmpty();

        // Timer firing after KeyUp should be ignored.
        ToggleKeyDecision threshold = core.OnHoldThresholdElapsed(pressId);
        threshold.ExecuteId.Should().BeNull();
        threshold.ImmediateEffects.Should().BeEmpty();
    }

    [Fact]
    public void TimerAndKeyUp_Race_ExactlyOnePathWins()
    {
        ToggleKeyCore core = new(TimeSpan.FromSeconds(1));
        DateTime t0 = DateTime.UtcNow;
        int pressId = core.OnKeyDown(t0);

        // Simulate keyup slightly before threshold.
        ToggleKeyDecision keyUp = core.OnKeyUp(t0.AddMilliseconds(900), pressId);
        keyUp.ExecuteId.Should().NotBeNull();
        core.OnHoldThresholdElapsed(pressId).ImmediateEffects.Should().BeEmpty();

        IReadOnlyList<ToggleKeyEffect> effects = core.OnExecutionCompleted(keyUp.ExecuteId!.Value, true);
        effects.Should().Contain(e => e.Kind == ToggleKeyEffectKind.SetVisualState && e.State == 1);

        // New press: timer triggers first, then keyup should not request execution.
        int pressId2 = core.OnKeyDown(t0.AddSeconds(5));
        core.OnHoldThresholdElapsed(pressId2).ImmediateEffects.Should().NotBeEmpty();

        ToggleKeyDecision keyUp2 = core.OnKeyUp(t0.AddSeconds(7), pressId2);
        keyUp2.ExecuteId.Should().BeNull();
    }

    [Fact]
    public void LongHold_KeyUp_AtThreshold_TriggersResetEffects_AndSuppressesExecution()
    {
        ToggleKeyCore core = new(TimeSpan.FromSeconds(1));

        DateTime t0 = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        int pressId = core.OnKeyDown(t0);

        ToggleKeyDecision keyUp = core.OnKeyUp(t0.AddSeconds(1), pressId);

        keyUp.ExecuteId.Should().BeNull();
        keyUp.ImmediateEffects.Should().Contain(e => e.Kind == ToggleKeyEffectKind.SetVisualState && e.State == 1);
        keyUp.ImmediateEffects.Should().Contain(e => e.Kind == ToggleKeyEffectKind.PlayClickSound);
        core.GetVisualState().Should().Be(1);

        // If the timer fires after KeyUp handled it, it must not trigger another reset.
        ToggleKeyDecision thresholdAfter = core.OnHoldThresholdElapsed(pressId);
        thresholdAfter.ExecuteId.Should().BeNull();
        thresholdAfter.ImmediateEffects.Should().BeEmpty();
    }

    [Fact]
    public void TimerAndKeyUp_Race_AtThreshold_ExactlyOnePathWins()
    {
        ToggleKeyCore core = new(TimeSpan.FromSeconds(1));
        DateTime t0 = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Timer first -> KeyUp suppressed.
        int pressId1 = core.OnKeyDown(t0);
        ToggleKeyDecision threshold1 = core.OnHoldThresholdElapsed(pressId1);
        threshold1.ImmediateEffects.Should().NotBeEmpty();

        ToggleKeyDecision keyUp1 = core.OnKeyUp(t0.AddSeconds(1), pressId1);
        keyUp1.ExecuteId.Should().BeNull();
        keyUp1.ImmediateEffects.Should().BeEmpty();
        core.GetVisualState().Should().Be(1);

        // KeyUp at threshold first -> timer suppressed.
        int pressId2 = core.OnKeyDown(t0.AddSeconds(10));
        ToggleKeyDecision keyUp2 = core.OnKeyUp(t0.AddSeconds(11), pressId2);
        keyUp2.ExecuteId.Should().BeNull();
        keyUp2.ImmediateEffects.Should().Contain(e => e.Kind == ToggleKeyEffectKind.SetVisualState && e.State == 0);
        keyUp2.ImmediateEffects.Should().Contain(e => e.Kind == ToggleKeyEffectKind.PlayClickSound);

        ToggleKeyDecision threshold2 = core.OnHoldThresholdElapsed(pressId2);
        threshold2.ExecuteId.Should().BeNull();
        threshold2.ImmediateEffects.Should().BeEmpty();
        core.GetVisualState().Should().Be(0);
    }

    [Fact]
    public void CustomThreshold_ChangesClassificationBoundary_ForSameHoldDuration()
    {
        DateTime t0 = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        ToggleKeyCore defaultCore = new(TimeSpan.FromSeconds(1));
        ToggleKeyCore customCore = new(TimeSpan.FromSeconds(2));

        int pressIdDefault = defaultCore.OnKeyDown(t0);
        int pressIdCustom = customCore.OnKeyDown(t0);

        ToggleKeyDecision defaultDecision = defaultCore.OnKeyUp(t0.AddSeconds(1.5), pressIdDefault);
        ToggleKeyDecision customDecision = customCore.OnKeyUp(t0.AddSeconds(1.5), pressIdCustom);

        defaultDecision.ExecuteId.Should().BeNull("hold >= 1.0s should classify as long hold");
        customDecision.ExecuteId.Should().NotBeNull("hold < 2.0s should classify as short press");
    }
}
