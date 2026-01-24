using FluentAssertions;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Keybinding.ActivationHandlers;

namespace Tests.Unit.Services.Keybinding.ActivationHandlers;

public sealed class HoldHandlerTests
{
    [Fact]
    public void Execute_Hold_Mode_PressesOnDown_ReleasesOnUp()
    {
        HoldHandler handler = new();
        RecordingInputExecutor exec = new();

        ActivationModeMetadata metadata = ActivationModeMetadata.Empty();

        handler.Execute(Ctx(ActivationMode.hold, true, metadata), exec).Should().BeTrue();
        handler.Execute(Ctx(ActivationMode.hold, false, metadata), exec).Should().BeTrue();

        exec.DownCount.Should().Be(1);
        exec.UpCount.Should().Be(1);
        exec.ScheduledHolds.Should().BeEmpty();
        exec.CancelHoldCount.Should().Be(0);
    }

    [Fact]
    public void Execute_DelayedHold_UsesMetadataThreshold_WhenProvided()
    {
        HoldHandler handler = new();
        RecordingInputExecutor exec = new();

        ActivationModeMetadata metadata = new()
        {
            PressTriggerThreshold = 0.5f, OnPress = true, OnRelease = true, Retriggerable = true
        };

        handler.Execute(Ctx(ActivationMode.delayed_hold, true, metadata), exec).Should().BeTrue();

        exec.ScheduledHolds.Should().HaveCount(1);
        exec.ScheduledHolds[0].DelaySeconds.Should().Be(0.5f);
    }

    [Fact]
    public void Execute_DelayedHoldLong_UsesDefaultThreshold_WhenMetadataMissing()
    {
        HoldHandler handler = new();
        RecordingInputExecutor exec = new();

        ActivationModeMetadata metadata = new()
        {
            PressTriggerThreshold = 0, OnPress = true, OnRelease = true, Retriggerable = true
        };

        handler.Execute(Ctx(ActivationMode.delayed_hold_long, true, metadata), exec).Should().BeTrue();
        handler.Execute(Ctx(ActivationMode.delayed_hold_long, false, metadata), exec).Should().BeTrue();

        exec.ScheduledHolds.Should().HaveCount(1);
        exec.ScheduledHolds[0].DelaySeconds.Should().Be(1.5f);
        exec.CancelHoldCount.Should().Be(1);
        exec.UpCount.Should().Be(1);
    }

    private static ActivationExecutionContext Ctx(ActivationMode mode, bool isKeyDown, ActivationModeMetadata metadata) => new()
    {
        ActionName = "TestAction",
        Input = new ParsedInput { Type = InputType.Keyboard, Value = new object() },
        IsKeyDown = isKeyDown,
        Mode = mode,
        Metadata = metadata
    };

    private sealed record ScheduledHold(string ActionKey, float DelaySeconds);

    private sealed class RecordingInputExecutor : IInputExecutor
    {
        public int DownCount { get; private set; }
        public int UpCount { get; private set; }
        public int CancelHoldCount { get; private set; }
        public List<ScheduledHold> ScheduledHolds { get; } = new();

        public bool ExecutePress(ParsedInput input) => true;
        public bool ExecutePressNoRepeat(ParsedInput input) => true;

        public bool ExecuteDown(ParsedInput input, string actionKey)
        {
            DownCount++;
            return true;
        }

        public bool ExecuteUp(ParsedInput input, string actionKey)
        {
            UpCount++;
            return true;
        }

        public bool ScheduleDelayedPress(ParsedInput input, string actionKey, float delaySeconds) => true;

        public void CancelDelayedPress(string actionKey)
        {
        }

        public bool ScheduleDelayedHold(ParsedInput input, string actionKey, float delaySeconds)
        {
            ScheduledHolds.Add(new ScheduledHold(actionKey, delaySeconds));
            return true;
        }

        public void CancelDelayedHold(string actionKey) => CancelHoldCount++;
    }
}
