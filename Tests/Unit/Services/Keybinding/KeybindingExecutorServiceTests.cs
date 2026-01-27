using FluentAssertions;
using Moq;
using SCStreamDeck.Common;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Keybinding;
using WindowsInput;
using WindowsInput.Native;

namespace Tests.Unit.Services.Keybinding;

public sealed class KeybindingExecutorServiceTests
{
    [Fact]
    public async Task ExecutePressNoRepeatAsync_KeyboardSingleKey_ExecutesDelayedKeyPress()
    {
        KeybindingLoaderService loader = new(new SystemFileSystem());

        Mock<IKeyboardSimulator> keyboard = new(MockBehavior.Strict);
        keyboard.Setup(k => k.DelayedKeyPress(DirectInputKeyCode.DikF1, 50)).Returns(keyboard.Object);

        Mock<IInputSimulator> inputSimulator = new(MockBehavior.Strict);
        inputSimulator.SetupGet(i => i.Keyboard).Returns(keyboard.Object);

        KeybindingExecutorService service = new(loader, inputSimulator.Object);

        bool result = await service.ExecutePressNoRepeatAsync("TestAction", "f1");

        result.Should().BeTrue();
        keyboard.Verify(k => k.DelayedKeyPress(DirectInputKeyCode.DikF1, 50), Times.Once);
    }

    [Fact]
    public async Task ExecutePressNoRepeatAsync_MouseLeftButton_ExecutesLeftClick()
    {
        KeybindingLoaderService loader = new(new SystemFileSystem());

        Mock<IMouseSimulator> mouse = new(MockBehavior.Strict);
        mouse.Setup(m => m.LeftButtonClick()).Returns(mouse.Object);

        Mock<IInputSimulator> inputSimulator = new(MockBehavior.Strict);
        inputSimulator.SetupGet(i => i.Mouse).Returns(mouse.Object);

        KeybindingExecutorService service = new(loader, inputSimulator.Object);

        bool result = await service.ExecutePressNoRepeatAsync("TestAction", SCConstants.Input.Mouse.LeftButton);

        result.Should().BeTrue();
        mouse.Verify(m => m.LeftButtonClick(), Times.Once);
    }

    [Fact]
    public async Task ExecutePressNoRepeatAsync_MouseWheelUp_ExecutesVerticalScroll()
    {
        KeybindingLoaderService loader = new(new SystemFileSystem());

        Mock<IMouseSimulator> mouse = new(MockBehavior.Strict);
        mouse.Setup(m => m.VerticalScroll(1)).Returns(mouse.Object);

        Mock<IInputSimulator> inputSimulator = new(MockBehavior.Strict);
        inputSimulator.SetupGet(i => i.Mouse).Returns(mouse.Object);

        KeybindingExecutorService service = new(loader, inputSimulator.Object);

        bool result = await service.ExecutePressNoRepeatAsync("TestAction", SCConstants.Input.Mouse.WheelUp);

        result.Should().BeTrue();
        mouse.Verify(m => m.VerticalScroll(1), Times.Once);
    }

    [Fact]
    public async Task ExecutePressNoRepeatAsync_MouseWheelDown_ExecutesVerticalScroll()
    {
        KeybindingLoaderService loader = new(new SystemFileSystem());

        Mock<IMouseSimulator> mouse = new(MockBehavior.Strict);
        mouse.Setup(m => m.VerticalScroll(-1)).Returns(mouse.Object);

        Mock<IInputSimulator> inputSimulator = new(MockBehavior.Strict);
        inputSimulator.SetupGet(i => i.Mouse).Returns(mouse.Object);

        KeybindingExecutorService service = new(loader, inputSimulator.Object);

        bool result = await service.ExecutePressNoRepeatAsync("TestAction", SCConstants.Input.Mouse.WheelDown);

        result.Should().BeTrue();
        mouse.Verify(m => m.VerticalScroll(-1), Times.Once);
    }

    [Fact]
    public async Task ExecutePressNoRepeatAsync_InvalidBinding_ReturnsFalse_AndDoesNotExecute()
    {
        KeybindingLoaderService loader = new(new SystemFileSystem());

        Mock<IInputSimulator> inputSimulator = new(MockBehavior.Strict);

        KeybindingExecutorService service = new(loader, inputSimulator.Object);

        bool result = await service.ExecutePressNoRepeatAsync("TestAction", "$$invalid$$");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFalse_WhenContextInvalid()
    {
        KeybindingLoaderService loader = new(new SystemFileSystem());
        KeybindingExecutorService service = new(loader, Mock.Of<IInputSimulator>());

        KeybindingExecutionContext context = new()
        {
            ActionName = string.Empty, Binding = string.Empty, ActivationMode = ActivationMode.press, IsKeyDown = true
        };

        bool result = await service.ExecuteAsync(context);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFalse_WhenParseFails()
    {
        KeybindingLoaderService loader = new(new SystemFileSystem());
        KeybindingExecutorService service = new(loader, Mock.Of<IInputSimulator>());

        KeybindingExecutionContext context = new()
        {
            ActionName = "TestAction", Binding = "$$invalid$$", ActivationMode = ActivationMode.press, IsKeyDown = true
        };

        bool result = await service.ExecuteAsync(context);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ExecutesPressUsingMetadata()
    {
        KeybindingLoaderService loader = new(new SystemFileSystem());

        Mock<IKeyboardSimulator> keyboard = new();
        Mock<IInputSimulator> inputSimulator = new();
        inputSimulator.Setup(i => i.Keyboard).Returns(keyboard.Object);

        KeybindingExecutorService service = new(loader, inputSimulator.Object);

        KeybindingExecutionContext context = new()
        {
            ActionName = "TestAction", Binding = "F1", ActivationMode = ActivationMode.press, IsKeyDown = true
        };

        bool result = await service.ExecuteAsync(context);

        result.Should().BeTrue();
        keyboard.Verify(k => k.DelayedKeyPress(DirectInputKeyCode.DikF1, 50), Times.Once);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoaderServiceIsNull()
    {
        Action act = () => new KeybindingExecutorService(null!, Mock.Of<IInputSimulator>());

        act.Should().Throw<ArgumentNullException>().WithParameterName("loaderService");
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenInputSimulatorIsNull()
    {
        KeybindingLoaderService loader = new(new SystemFileSystem());

        Action act = () => new KeybindingExecutorService(loader, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("inputSimulator");
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        KeybindingLoaderService loader = new(new SystemFileSystem());
        KeybindingExecutorService service = new(loader, Mock.Of<IInputSimulator>());

        Func<Task> act = async () => await service.ExecuteAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        KeybindingLoaderService loader = new(new SystemFileSystem());
        KeybindingExecutorService service = new(loader, Mock.Of<IInputSimulator>());

        Action act = () =>
        {
            service.Dispose();
            service.Dispose();
        };

        act.Should().NotThrow();
    }
}
