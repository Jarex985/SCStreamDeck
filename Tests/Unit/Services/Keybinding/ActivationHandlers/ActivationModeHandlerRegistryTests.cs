using FluentAssertions;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Keybinding.ActivationHandlers;

namespace Tests.Unit.Services.Keybinding.ActivationHandlers;

public sealed class ActivationModeHandlerRegistryTests
{
    [Fact]
    public void Execute_WithPressMode_UsesImmediatePressHandler()
    {
        ActivationModeHandlerRegistry registry = new();
        RecordingInputExecutor exec = new();

        ActivationExecutionContext context = new()
        {
            ActionName = "TestAction",
            Input = new ParsedInput { Type = InputType.Keyboard, Value = new object() },
            IsKeyDown = true,
            Mode = ActivationMode.press,
            Metadata = new ActivationModeMetadata { OnPress = true, Retriggerable = false }
        };

        registry.Execute(context, exec).Should().BeTrue();
        exec.PressNoRepeatCount.Should().Be(1);
        exec.DownCount.Should().Be(0);
        exec.ScheduledHolds.Should().BeEmpty();
    }

    [Fact]
    public void Execute_WithDelayedPressMode_SchedulesDelayedHold_WithDefaultDelay()
    {
        ActivationModeHandlerRegistry registry = new();
        RecordingInputExecutor exec = new();

        ActivationExecutionContext context = new()
        {
            ActionName = "TestAction",
            Input = new ParsedInput { Type = InputType.Keyboard, Value = new object() },
            IsKeyDown = true,
            Mode = ActivationMode.delayed_press,
            Metadata = new ActivationModeMetadata { PressTriggerThreshold = 0 }
        };

        registry.Execute(context, exec).Should().BeTrue();
        exec.ScheduledHolds.Should().ContainSingle();
        exec.ScheduledHolds[0].DelaySeconds.Should().Be(0.25f);
    }

    [Fact]
    public void Execute_WithHoldMode_ExecutesDown()
    {
        ActivationModeHandlerRegistry registry = new();
        RecordingInputExecutor exec = new();

        ActivationExecutionContext context = new()
        {
            ActionName = "TestAction",
            Input = new ParsedInput { Type = InputType.Keyboard, Value = new object() },
            IsKeyDown = true,
            Mode = ActivationMode.hold,
            Metadata = new ActivationModeMetadata()
        };

        registry.Execute(context, exec).Should().BeTrue();
        exec.DownCount.Should().Be(1);
        exec.PressNoRepeatCount.Should().Be(0);
    }

    [Fact]
    public void Execute_WithSmartToggleMode_ExecutesSingleToggle_OnKeyUp()
    {
        ActivationModeHandlerRegistry registry = new();
        RecordingInputExecutor exec = new();

        ActivationModeMetadata metadata = new() { ReleaseTriggerDelay = 2.0f, OnPress = true, OnRelease = true };

        ActivationExecutionContext keyDown = new()
        {
            ActionName = "TestAction",
            Input = new ParsedInput { Type = InputType.Keyboard, Value = new object() },
            IsKeyDown = true,
            Mode = ActivationMode.smart_toggle,
            Metadata = metadata
        };

        ActivationExecutionContext keyUp = new()
        {
            ActionName = "TestAction",
            Input = new ParsedInput { Type = InputType.Keyboard, Value = new object() },
            IsKeyDown = false,
            Mode = ActivationMode.smart_toggle,
            Metadata = metadata
        };

        registry.Execute(keyDown, exec).Should().BeTrue();
        exec.PressNoRepeatCount.Should().Be(0, "SmartToggle should not toggle on key down");

        registry.Execute(keyUp, exec).Should().BeTrue();
        exec.PressNoRepeatCount.Should().Be(1);

        Thread.Sleep(100);
        exec.PressNoRepeatCount.Should().Be(1, "timer should have been disposed on key up");
    }

    [Fact]
    public void Execute_WithUnknownMode_UsesDefaultPressHandler()
    {
        ActivationModeHandlerRegistry registry = new();
        RecordingInputExecutor exec = new();

        ActivationExecutionContext context = new()
        {
            ActionName = "TestAction",
            Input = new ParsedInput { Type = InputType.Keyboard, Value = new object() },
            IsKeyDown = true,
            Mode = (ActivationMode)12345,
            Metadata = new ActivationModeMetadata { OnPress = true, Retriggerable = false }
        };

        registry.Execute(context, exec).Should().BeTrue();
        exec.PressNoRepeatCount.Should().Be(1);
    }

    private sealed record ScheduledHold(string ActionKey, float DelaySeconds);

    private sealed class RecordingInputExecutor : IInputExecutor
    {
        public int PressNoRepeatCount { get; private set; }
        public int DownCount { get; private set; }
        public List<ScheduledHold> ScheduledHolds { get; } = new();

        public bool ExecutePress(ParsedInput input) => true;

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

        public bool ExecuteUp(ParsedInput input, string actionKey) => true;

        public bool ScheduleDelayedPress(ParsedInput input, string actionKey, float delaySeconds) => true;

        public void CancelDelayedPress(string actionKey)
        {
        }

        public bool ScheduleDelayedHold(ParsedInput input, string actionKey, float delaySeconds)
        {
            ScheduledHolds.Add(new ScheduledHold(actionKey, delaySeconds));
            return true;
        }

        public void CancelDelayedHold(string actionKey)
        {
        }
    }
}
