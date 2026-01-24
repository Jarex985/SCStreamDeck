using FluentAssertions;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Keybinding.ActivationHandlers;

namespace Tests.Unit.Services.Keybinding.ActivationHandlers;

public sealed class SmartToggleHandlerTests
{
    [Fact]
    public void Execute_ShortPress_ExecutesSingleToggle_OnKeyUp()
    {
        SmartToggleHandler handler = new();
        SmartToggleRecordingExecutor exec = new();

        ActivationModeMetadata metadata = new() { ReleaseTriggerDelay = 2.0f, OnPress = true, OnRelease = true };

        handler.Execute(Ctx(true, metadata), exec).Should().BeTrue();
        handler.Execute(Ctx(false, metadata), exec).Should().BeTrue();

        exec.PressNoRepeatCount.Should().Be(1);

        // Ensure the delayed auto-toggle didn't fire after KeyUp disposed the timer.
        Thread.Sleep(100);
        exec.PressNoRepeatCount.Should().Be(1);
    }

    [Fact]
    public void Execute_LongPress_ExecutesAutoToggle_ThenSecondToggle_OnKeyUp()
    {
        SmartToggleHandler handler = new();
        SmartToggleRecordingExecutor exec = new();

        ActivationModeMetadata metadata = new() { ReleaseTriggerDelay = 0.05f, OnPress = true, OnRelease = true };

        handler.Execute(Ctx(true, metadata), exec).Should().BeTrue();

        exec.FirstPress.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
        exec.PressNoRepeatCount.Should().BeGreaterThanOrEqualTo(1);

        handler.Execute(Ctx(false, metadata), exec).Should().BeTrue();

        SpinWait.SpinUntil(() => exec.PressNoRepeatCount >= 2, TimeSpan.FromSeconds(1)).Should().BeTrue();
        exec.PressNoRepeatCount.Should().Be(2);
    }

    private static ActivationExecutionContext Ctx(bool isKeyDown, ActivationModeMetadata metadata) => new()
    {
        ActionName = "TestAction",
        Input = new ParsedInput { Type = InputType.Keyboard, Value = new object() },
        IsKeyDown = isKeyDown,
        Mode = ActivationMode.smart_toggle,
        Metadata = metadata
    };

    private sealed class SmartToggleRecordingExecutor : IInputExecutor
    {
        private int _pressNoRepeatCount;
        public ManualResetEventSlim FirstPress { get; } = new(false);
        public int PressNoRepeatCount => Volatile.Read(ref _pressNoRepeatCount);

        public bool ExecutePress(ParsedInput input) => true;

        public bool ExecutePressNoRepeat(ParsedInput input)
        {
            int value = Interlocked.Increment(ref _pressNoRepeatCount);
            if (value >= 1)
            {
                FirstPress.Set();
            }

            return true;
        }

        public bool ExecuteDown(ParsedInput input, string actionKey) => true;
        public bool ExecuteUp(ParsedInput input, string actionKey) => true;
        public bool ScheduleDelayedPress(ParsedInput input, string actionKey, float delaySeconds) => true;

        public void CancelDelayedPress(string actionKey)
        {
        }

        public bool ScheduleDelayedHold(ParsedInput input, string actionKey, float delaySeconds) => true;

        public void CancelDelayedHold(string actionKey)
        {
        }
    }
}
