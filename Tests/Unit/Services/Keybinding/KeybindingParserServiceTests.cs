using FluentAssertions;
using SCStreamDeck.Common;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Keybinding;
using WindowsInput.Native;

namespace Tests.Unit.Services.Keybinding;

public sealed class KeybindingParserServiceTests
{
    private readonly KeybindingParserService _service = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseBinding_ReturnsNull_ForEmpty(string? input)
    {
        ParsedInputResult? result = _service.ParseBinding(input!);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(SCConstants.Input.Mouse.WheelUp, 1)]
    [InlineData(SCConstants.Input.Mouse.WheelDown, -1)]
    public void ParseBinding_ParsesMouseWheel(string binding, int direction)
    {
        ParsedInputResult? result = _service.ParseBinding(binding);

        result.Should().NotBeNull();
        result!.Type.Should().Be(InputType.MouseWheel);
        result.Value.Should().Be(direction);
    }

    [Fact]
    public void ParseBinding_ParsesMouseWheelWithModifiers()
    {
        ParsedInputResult? result = _service.ParseBinding("lalt+mwheel_up");

        result.Should().NotBeNull();
        result!.Type.Should().Be(InputType.MouseWheel);
        result.Value.Should().BeOfType<ValueTuple<DirectInputKeyCode[], int>>();

        (DirectInputKeyCode[] modifiers, int direction) = ((DirectInputKeyCode[], int))result.Value;
        modifiers.Should().ContainSingle().Which.Should().Be(DirectInputKeyCode.DikLalt);
        direction.Should().Be(1);
    }

    [Fact]
    public void ParseBinding_ParsesMouseButtons()
    {
        ParsedInputResult? left = _service.ParseBinding(SCConstants.Input.Mouse.LeftButton);
        ParsedInputResult? right = _service.ParseBinding(SCConstants.Input.Mouse.RightButton);

        left.Should().NotBeNull();
        left!.Type.Should().Be(InputType.MouseButton);
        left.Value.Should().Be(VirtualKeyCode.LBUTTON);

        right.Should().NotBeNull();
        right!.Type.Should().Be(InputType.MouseButton);
        right.Value.Should().Be(VirtualKeyCode.RBUTTON);
    }

    [Fact]
    public void ParseBinding_ParsesKeyboardWithModifiers()
    {
        ParsedInputResult? result = _service.ParseBinding("lshift+f1");

        result.Should().NotBeNull();
        result!.Type.Should().Be(InputType.Keyboard);

        (DirectInputKeyCode[] modifiers, DirectInputKeyCode[] keys) = ((DirectInputKeyCode[], DirectInputKeyCode[]))result.Value;
        modifiers.Should().ContainSingle().Which.Should().Be(DirectInputKeyCode.DikLshift);
        keys.Should().ContainSingle().Which.Should().Be(DirectInputKeyCode.DikF1);
    }
}
