using System.Collections.Concurrent;
using FluentAssertions;
using Moq;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Keybinding.ActivationHandlers;
using WindowsInput;
using WindowsInput.Native;

namespace Tests.Unit.Services.Keybinding.ActivationHandlers;

public sealed class KeybindingInputExecutorTests
{
    [Fact]
    public void ExecutePress_Keyboard_NoModifiers_UsesDelayedKeyPress()
    {
        (KeybindingInputExecutor executor, _, Mock<IKeyboardSimulator> keyboard) = CreateExecutor();

        ParsedInput input = new()
        {
            Type = InputType.Keyboard, Value = (Array.Empty<DirectInputKeyCode>(), new[] { DirectInputKeyCode.DikF1 })
        };

        keyboard.Setup(k => k.DelayedKeyPress(DirectInputKeyCode.DikF1, 50)).Returns(keyboard.Object);

        executor.ExecutePress(input).Should().BeTrue();

        keyboard.Verify(k => k.DelayedKeyPress(DirectInputKeyCode.DikF1, 50), Times.Once);
    }

    [Fact]
    public void ExecutePress_Keyboard_WithModifiers_UsesDelayedModifiedKeyStroke()
    {
        (KeybindingInputExecutor executor, _, Mock<IKeyboardSimulator> keyboard) = CreateExecutor();

        DirectInputKeyCode[] modifiers = [DirectInputKeyCode.DikLcontrol, DirectInputKeyCode.DikLalt];
        ParsedInput input = new() { Type = InputType.Keyboard, Value = (modifiers, new[] { DirectInputKeyCode.DikC }) };

        keyboard
            .Setup(k => k.DelayedModifiedKeyStroke(
                It.Is<IEnumerable<DirectInputKeyCode>>(m => m.SequenceEqual(modifiers)),
                DirectInputKeyCode.DikC,
                50))
            .Returns(keyboard.Object);

        executor.ExecutePress(input).Should().BeTrue();

        keyboard.Verify(k => k.DelayedModifiedKeyStroke(
            It.Is<IEnumerable<DirectInputKeyCode>>(m => m.SequenceEqual(modifiers)),
            DirectInputKeyCode.DikC,
            50), Times.Once);
    }

    [Fact]
    public void ExecutePressNoRepeat_MouseButton_LeftButton_InvokesMouseClick()
    {
        (KeybindingInputExecutor executor, Mock<IMouseSimulator> mouse, _) = CreateExecutor();

        ParsedInput input = new() { Type = InputType.MouseButton, Value = VirtualKeyCode.LBUTTON };

        mouse.Setup(m => m.LeftButtonClick()).Returns(mouse.Object);

        executor.ExecutePressNoRepeat(input).Should().BeTrue();

        mouse.Verify(m => m.LeftButtonClick(), Times.Once);
    }

    [Fact]
    public void ExecuteDownUp_MouseButton_IsIdempotent_PerActionKey()
    {
        (KeybindingInputExecutor executor, Mock<IMouseSimulator> mouse, _) = CreateExecutor();

        ParsedInput input = new() { Type = InputType.MouseButton, Value = VirtualKeyCode.LBUTTON };

        mouse.Setup(m => m.LeftButtonDown()).Returns(mouse.Object);
        mouse.Setup(m => m.LeftButtonUp()).Returns(mouse.Object);

        executor.ExecuteDown(input, "Action").Should().BeTrue();
        executor.ExecuteDown(input, "Action").Should().BeTrue();
        executor.ExecuteUp(input, "Action").Should().BeTrue();
        executor.ExecuteUp(input, "Action").Should().BeTrue();

        mouse.Verify(m => m.LeftButtonDown(), Times.Once);
        mouse.Verify(m => m.LeftButtonUp(), Times.Once);
    }

    [Fact]
    public void ExecuteDownUp_Keyboard_NoModifiers_CallsKeyDownAndUp_AndRemovesTimer()
    {
        (KeybindingInputExecutor executor, _, Mock<IKeyboardSimulator> keyboard) = CreateExecutor();

        ParsedInput input = new()
        {
            Type = InputType.Keyboard, Value = (Array.Empty<DirectInputKeyCode>(), new[] { DirectInputKeyCode.DikF1 })
        };

        keyboard.Setup(k => k.KeyDown(DirectInputKeyCode.DikF1)).Returns(keyboard.Object);
        keyboard.Setup(k => k.KeyUp(DirectInputKeyCode.DikF1)).Returns(keyboard.Object);

        executor.ExecuteDown(input, "Action").Should().BeTrue();

        // Ensure ExecuteUp removes and disposes any outstanding timer.
        executor.ScheduleDelayedPress(new ParsedInput { Type = InputType.Keyboard, Value = input.Value }, "Action", 10);
        executor.ExecuteUp(input, "Action").Should().BeTrue();

        keyboard.Verify(k => k.KeyDown(DirectInputKeyCode.DikF1), Times.Once);
        keyboard.Verify(k => k.KeyUp(DirectInputKeyCode.DikF1), Times.Once);
    }

    private static (KeybindingInputExecutor Executor, Mock<IMouseSimulator> Mouse, Mock<IKeyboardSimulator> Keyboard)
        CreateExecutor()
    {
        Mock<IMouseSimulator> mouse = new(MockBehavior.Strict);
        Mock<IKeyboardSimulator> keyboard = new(MockBehavior.Strict);
        Mock<IInputSimulator> inputSimulator = new(MockBehavior.Strict);

        inputSimulator.SetupGet(i => i.Mouse).Returns(mouse.Object);
        inputSimulator.SetupGet(i => i.Keyboard).Returns(keyboard.Object);

        ConcurrentDictionary<string, byte> holdStates = new(StringComparer.OrdinalIgnoreCase);
        ConcurrentDictionary<string, Timer> activationTimers = new(StringComparer.OrdinalIgnoreCase);

        KeybindingInputExecutor executor = new(inputSimulator.Object, holdStates, activationTimers);
        return (executor, mouse, keyboard);
    }
}
