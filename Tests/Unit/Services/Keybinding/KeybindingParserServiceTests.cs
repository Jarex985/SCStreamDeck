using FluentAssertions;
using SCStreamDeck.Common;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Keybinding;
using WindowsInput.Native;

namespace Tests.Unit.Services.Keybinding;

public sealed class KeybindingParserServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseBinding_ReturnsNull_ForEmpty(string? input)
    {
        ParsedInputResult? result = KeybindingParserService.ParseBinding(input!);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(SCConstants.Input.Mouse.WheelUp, 1)]
    [InlineData(SCConstants.Input.Mouse.WheelDown, -1)]
    public void ParseBinding_ParsesMouseWheel(string binding, int direction)
    {
        ParsedInputResult? result = KeybindingParserService.ParseBinding(binding);

        result.Should().NotBeNull();
        result.Type.Should().Be(InputType.MouseWheel);
        result.Value.Should().Be(direction);
    }

    [Fact]
    public void ParseBinding_ParsesMouseWheelWithModifiers()
    {
        ParsedInputResult? result = KeybindingParserService.ParseBinding("lalt+mwheel_up");

        result.Should().NotBeNull();
        result.Type.Should().Be(InputType.MouseWheel);
        result.Value.Should().BeOfType<ValueTuple<DirectInputKeyCode[], int>>();

        (DirectInputKeyCode[] modifiers, int direction) = ((DirectInputKeyCode[], int))result.Value;
        modifiers.Should().ContainSingle().Which.Should().Be(DirectInputKeyCode.DikLalt);
        direction.Should().Be(1);
    }

    [Fact]
    public void ParseBinding_ParsesMouseButtons()
    {
        ParsedInputResult? left = KeybindingParserService.ParseBinding(SCConstants.Input.Mouse.LeftButton);
        ParsedInputResult? right = KeybindingParserService.ParseBinding(SCConstants.Input.Mouse.RightButton);

        left.Should().NotBeNull();
        left.Type.Should().Be(InputType.MouseButton);
        left.Value.Should().Be(VirtualKeyCode.LBUTTON);

        right.Should().NotBeNull();
        right.Type.Should().Be(InputType.MouseButton);
        right.Value.Should().Be(VirtualKeyCode.RBUTTON);
    }

    [Fact]
    public void ParseBinding_ParsesKeyboardWithModifiers()
    {
        ParsedInputResult? result = KeybindingParserService.ParseBinding("lshift+f1");

        result.Should().NotBeNull();
        result.Type.Should().Be(InputType.Keyboard);

        (DirectInputKeyCode[] modifiers, DirectInputKeyCode[] keys) = ((DirectInputKeyCode[], DirectInputKeyCode[]))result.Value;
        modifiers.Should().ContainSingle().Which.Should().Be(DirectInputKeyCode.DikLshift);
        keys.Should().ContainSingle().Which.Should().Be(DirectInputKeyCode.DikF1);
    }

    #region Function Keys Tests

    [Theory]
    [InlineData(SCConstants.Input.Keyboard.F1, DirectInputKeyCode.DikF1)]
    [InlineData(SCConstants.Input.Keyboard.F2, DirectInputKeyCode.DikF2)]
    [InlineData(SCConstants.Input.Keyboard.F3, DirectInputKeyCode.DikF3)]
    [InlineData(SCConstants.Input.Keyboard.F4, DirectInputKeyCode.DikF4)]
    [InlineData(SCConstants.Input.Keyboard.F5, DirectInputKeyCode.DikF5)]
    [InlineData(SCConstants.Input.Keyboard.F6, DirectInputKeyCode.DikF6)]
    [InlineData(SCConstants.Input.Keyboard.F7, DirectInputKeyCode.DikF7)]
    [InlineData(SCConstants.Input.Keyboard.F8, DirectInputKeyCode.DikF8)]
    [InlineData(SCConstants.Input.Keyboard.F9, DirectInputKeyCode.DikF9)]
    [InlineData(SCConstants.Input.Keyboard.F10, DirectInputKeyCode.DikF10)]
    [InlineData(SCConstants.Input.Keyboard.F11, DirectInputKeyCode.DikF11)]
    [InlineData(SCConstants.Input.Keyboard.F12, DirectInputKeyCode.DikF12)]
    public void ParseBinding_ParsesFunctionKeys(string binding, DirectInputKeyCode expectedKey)
    {
        ParsedInputResult? result = KeybindingParserService.ParseBinding(binding);

        result.Should().NotBeNull();
        result.Type.Should().Be(InputType.Keyboard);

        (DirectInputKeyCode[] modifiers, DirectInputKeyCode[] keys) = ((DirectInputKeyCode[], DirectInputKeyCode[]))result.Value;
        modifiers.Should().BeEmpty();
        keys.Should().ContainSingle().Which.Should().Be(expectedKey);
    }

    #endregion

    #region Special Keys Tests

    [Theory]
    [InlineData(SCConstants.Input.Keyboard.Space, DirectInputKeyCode.DikSpace)]
    [InlineData(SCConstants.Input.Keyboard.Enter, DirectInputKeyCode.DikReturn)]
    [InlineData(SCConstants.Input.Keyboard.Tab, DirectInputKeyCode.DikTab)]
    [InlineData(SCConstants.Input.Keyboard.Escape, DirectInputKeyCode.DikEscape)]
    [InlineData(SCConstants.Input.Keyboard.Backspace, DirectInputKeyCode.DikBackspace)]
    [InlineData(SCConstants.Input.Keyboard.CapsLock, DirectInputKeyCode.DikCapital)]
    [InlineData(SCConstants.Input.Keyboard.NumLock, DirectInputKeyCode.DikNumlock)]
    [InlineData(SCConstants.Input.Keyboard.ScrollLock, DirectInputKeyCode.DikScroll)]
    public void ParseBinding_ParsesSpecialKeys(string binding, DirectInputKeyCode expectedKey)
    {
        ParsedInputResult? result = KeybindingParserService.ParseBinding(binding);

        result.Should().NotBeNull();
        result.Type.Should().Be(InputType.Keyboard);

        (DirectInputKeyCode[] modifiers, DirectInputKeyCode[] keys) = ((DirectInputKeyCode[], DirectInputKeyCode[]))result.Value;
        modifiers.Should().BeEmpty();
        keys.Should().ContainSingle().Which.Should().Be(expectedKey);
    }

    #endregion

    #region Navigation Keys Tests

    [Theory]
    [InlineData(SCConstants.Input.Keyboard.Up, DirectInputKeyCode.DikUp)]
    [InlineData(SCConstants.Input.Keyboard.Down, DirectInputKeyCode.DikDown)]
    [InlineData(SCConstants.Input.Keyboard.Left, DirectInputKeyCode.DikLeft)]
    [InlineData(SCConstants.Input.Keyboard.Right, DirectInputKeyCode.DikRight)]
    [InlineData(SCConstants.Input.Keyboard.Home, DirectInputKeyCode.DikHome)]
    [InlineData(SCConstants.Input.Keyboard.End, DirectInputKeyCode.DikEnd)]
    [InlineData(SCConstants.Input.Keyboard.PgUp, DirectInputKeyCode.DikPageUp)]
    [InlineData(SCConstants.Input.Keyboard.PgDown, DirectInputKeyCode.DikPageDown)]
    [InlineData(SCConstants.Input.Keyboard.Insert, DirectInputKeyCode.DikInsert)]
    [InlineData(SCConstants.Input.Keyboard.Delete, DirectInputKeyCode.DikDelete)]
    public void ParseBinding_ParsesNavigationKeys(string binding, DirectInputKeyCode expectedKey)
    {
        ParsedInputResult? result = KeybindingParserService.ParseBinding(binding);

        result.Should().NotBeNull();
        result.Type.Should().Be(InputType.Keyboard);

        (DirectInputKeyCode[] modifiers, DirectInputKeyCode[] keys) = ((DirectInputKeyCode[], DirectInputKeyCode[]))result.Value;
        modifiers.Should().BeEmpty();
        keys.Should().ContainSingle().Which.Should().Be(expectedKey);
    }

    #endregion

    #region Case Insensitivity Tests

    [Theory]
    [InlineData("F1", "f1")]
    [InlineData("SPACE", "space")]
    [InlineData("ENTER", "enter")]
    public void ParseBinding_IsCaseInsensitive(string key1, string key2)
    {
        ParsedInputResult? result1 = KeybindingParserService.ParseBinding(key1);
        ParsedInputResult? result2 = KeybindingParserService.ParseBinding(key2);

        result1.Should().NotBeNull();
        result2.Should().NotBeNull();

        (DirectInputKeyCode[] _, DirectInputKeyCode[] keys1) =
            ((DirectInputKeyCode[], DirectInputKeyCode[]))result1.Value;
        (DirectInputKeyCode[] _, DirectInputKeyCode[] keys2) =
            ((DirectInputKeyCode[], DirectInputKeyCode[]))result2.Value;

        keys1.Should().BeEquivalentTo(keys2);
    }

    #endregion

    #region Multiple Keys Tests

    [Fact]
    public void ParseBinding_ParsesMultipleKeys()
    {
        ParsedInputResult? result = KeybindingParserService.ParseBinding("lalt+rshift+f1");

        result.Should().NotBeNull();
        result.Type.Should().Be(InputType.Keyboard);

        (DirectInputKeyCode[] modifiers, DirectInputKeyCode[] keys) = ((DirectInputKeyCode[], DirectInputKeyCode[]))result.Value;
        modifiers.Should().ContainInOrder(DirectInputKeyCode.DikLalt, DirectInputKeyCode.DikRshift);
        keys.Should().ContainSingle().Which.Should().Be(DirectInputKeyCode.DikF1);
    }

    #endregion

    #region Invalid Input Tests

    [Theory]
    [InlineData("invalid_key")]
    [InlineData("unknown")]
    [InlineData("xyz")]
    public void ParseBinding_ReturnsNull_ForInvalidKeys(string binding)
    {
        ParsedInputResult? result = KeybindingParserService.ParseBinding(binding);

        result.Should().BeNull();
    }

    #endregion
}
