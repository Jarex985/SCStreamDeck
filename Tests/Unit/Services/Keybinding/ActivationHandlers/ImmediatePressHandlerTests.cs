using FluentAssertions;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Keybinding.ActivationHandlers;

namespace Tests.Unit.Services.Keybinding.ActivationHandlers;

public sealed class ImmediatePressHandlerTests
{
    [Fact]
    public void Execute_OnRelease_IsBlocked_WhenMultiTapBlockWasSetOnPress()
    {
        ImmediatePressHandler handler = new();
        RecordingInputExecutor exec = new();

        ActivationModeMetadata metadata = new()
        {
            OnPress = true,
            OnRelease = true,
            Retriggerable = false,
            MultiTapBlock = 1,
            ReleaseTriggerDelay = 0,
            ReleaseTriggerThreshold = 0
        };

        handler.Execute(Ctx(true, metadata), exec).Should().BeTrue();
        handler.Execute(Ctx(false, metadata), exec).Should().BeTrue();

        exec.PressNoRepeatCount.Should().Be(1);
        exec.DownCount.Should().Be(0);
        exec.UpCount.Should().Be(0);
    }

    [Fact]
    public void Execute_OnRelease_UsesReleaseTriggerDelay_BeforeReleaseTriggerThreshold()
    {
        ImmediatePressHandler handler = new();
        RecordingInputExecutor exec = new();

        ActivationModeMetadata metadata = new()
        {
            OnPress = false,
            OnRelease = true,
            Retriggerable = false,
            MultiTapBlock = 0,
            ReleaseTriggerDelay = 0.3f,
            ReleaseTriggerThreshold = 0.15f
        };

        handler.Execute(Ctx(true, metadata), exec).Should().BeTrue();
        handler.Execute(Ctx(false, metadata), exec).Should().BeTrue();

        exec.ScheduledPresses.Should().HaveCount(1);
        exec.ScheduledPresses[0].DelaySeconds.Should().Be(0.3f);
    }

    [Fact]
    public void Execute_OnRelease_UsesReleaseTriggerThreshold_WhenNoReleaseTriggerDelay()
    {
        ImmediatePressHandler handler = new();
        RecordingInputExecutor exec = new();

        ActivationModeMetadata metadata = new()
        {
            OnPress = false,
            OnRelease = true,
            Retriggerable = false,
            MultiTapBlock = 0,
            ReleaseTriggerDelay = 0,
            ReleaseTriggerThreshold = 0.15f
        };

        handler.Execute(Ctx(true, metadata), exec).Should().BeTrue();
        handler.Execute(Ctx(false, metadata), exec).Should().BeTrue();

        exec.ScheduledPresses.Should().HaveCount(1);
        exec.ScheduledPresses[0].DelaySeconds.Should().Be(0.15f);
    }

    [Fact]
    public void Execute_Retriggerable_AlwaysReleasesHold_OnKeyUp_EvenIfOnReleaseWasBlocked()
    {
        ImmediatePressHandler handler = new();
        RecordingInputExecutor exec = new();

        ActivationModeMetadata metadata = new()
        {
            OnPress = true,
            OnRelease = true,
            Retriggerable = true,
            MultiTapBlock = 1,
            ReleaseTriggerDelay = 0,
            ReleaseTriggerThreshold = 0
        };

        handler.Execute(Ctx(true, metadata), exec).Should().BeTrue();
        handler.Execute(Ctx(false, metadata), exec).Should().BeTrue();

        exec.DownCount.Should().Be(1);
        exec.UpCount.Should().Be(1);
        exec.PressNoRepeatCount.Should().Be(0);
        exec.ScheduledPresses.Should().BeEmpty();
    }

    private static ActivationExecutionContext Ctx(bool isKeyDown, ActivationModeMetadata metadata) => new()
    {
        ActionName = "TestAction",
        Input = new ParsedInput { Type = InputType.Keyboard, Value = new object() },
        IsKeyDown = isKeyDown,
        Mode = ActivationMode.press,
        Metadata = metadata
    };

    private sealed record ScheduledPress(string ActionKey, float DelaySeconds);

    private sealed class RecordingInputExecutor : IInputExecutor
    {
        public int PressCount { get; private set; }
        public int PressNoRepeatCount { get; private set; }
        public int DownCount { get; private set; }
        public int UpCount { get; private set; }
        public List<ScheduledPress> ScheduledPresses { get; } = new();

        public bool ExecutePress(ParsedInput input)
        {
            PressCount++;
            return true;
        }

        public bool ExecutePressNoRepeat(ParsedInput input)
        {
            PressNoRepeatCount++;
            return true;
        }

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

        public bool ScheduleDelayedPress(ParsedInput input, string actionKey, float delaySeconds)
        {
            ScheduledPresses.Add(new ScheduledPress(actionKey, delaySeconds));
            return true;
        }

        public void CancelDelayedPress(string actionKey)
        {
        }

        public bool ScheduleDelayedHold(ParsedInput input, string actionKey, float delaySeconds) => true;

        public void CancelDelayedHold(string actionKey)
        {
        }
    }
}
