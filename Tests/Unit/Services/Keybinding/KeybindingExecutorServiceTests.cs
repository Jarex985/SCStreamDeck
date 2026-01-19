using FluentAssertions;
using Moq;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Keybinding;
using WindowsInput;
using WindowsInput.Native;

namespace Tests.Unit.Services.Keybinding;

public sealed class KeybindingExecutorServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsFalse_WhenContextInvalid()
    {
        Mock<IKeybindingLoaderService> loader = new();
        Mock<IKeybindingParserService> parser = new();
        KeybindingExecutorService service = new(loader.Object, parser.Object, Mock.Of<IInputSimulator>());

        KeybindingExecutionContext context = new()
        {
            ActionName = string.Empty,
            Binding = string.Empty,
            ActivationMode = ActivationMode.press,
            IsKeyDown = true
        };

        bool result = await service.ExecuteAsync(context);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFalse_WhenParseFails()
    {
        Mock<IKeybindingLoaderService> loader = new();
        Mock<IKeybindingParserService> parser = new();
        parser.Setup(p => p.ParseBinding(It.IsAny<string>())).Returns((ParsedInputResult?)null);
        KeybindingExecutorService service = new(loader.Object, parser.Object, Mock.Of<IInputSimulator>());

        KeybindingExecutionContext context = new()
        {
            ActionName = "TestAction",
            Binding = "invalid",
            ActivationMode = ActivationMode.press,
            IsKeyDown = true
        };

        bool result = await service.ExecuteAsync(context);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ExecutesPressUsingMetadata()
    {
        Mock<IKeybindingLoaderService> loader = new();
        loader.Setup(l => l.GetMetadata("TestAction")).Returns(new ActivationModeMetadata { OnPress = true });

        ParsedInputResult parsed = new(InputType.Keyboard, (Array.Empty<DirectInputKeyCode>(), new[] { DirectInputKeyCode.DikF1 }));
        Mock<IKeybindingParserService> parser = new();
        parser.Setup(p => p.ParseBinding("F1")).Returns(parsed);

        Mock<IKeyboardSimulator> keyboard = new();
        Mock<IInputSimulator> inputSimulator = new();
        inputSimulator.Setup(i => i.Keyboard).Returns(keyboard.Object);

        KeybindingExecutorService service = new(loader.Object, parser.Object, inputSimulator.Object);

        KeybindingExecutionContext context = new()
        {
            ActionName = "TestAction",
            Binding = "F1",
            ActivationMode = ActivationMode.press,
            IsKeyDown = true
        };

        bool result = await service.ExecuteAsync(context);

        result.Should().BeTrue();
        keyboard.Verify(k => k.DelayedKeyPress(DirectInputKeyCode.DikF1, 50), Times.Once);
    }
}
