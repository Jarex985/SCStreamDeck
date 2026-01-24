using FluentAssertions;
using SCStreamDeck.Common;
using SCStreamDeck.Models;

namespace Tests.Unit.Common;

public sealed class InputStringExtensionsTests
{
    #region Mouse Wheel Detection (GetInputType)

    [Theory]
    [InlineData("mwheel_up", InputType.MouseWheel)]
    [InlineData("mwheel_down", InputType.MouseWheel)]
    [InlineData("MWHEEL_UP", InputType.MouseWheel)]
    [InlineData("lshift+mwheel_up", InputType.MouseWheel)]
    [InlineData("mwheel_down+rctrl", InputType.MouseWheel)]
    [InlineData("lshift+alt+mwheel_up", InputType.MouseWheel)]
    public void GetInputType_MouseWheel_ReturnsMouseWheel(string input, InputType expected) =>
        input.GetInputType().Should().Be(expected);

    #endregion

    #region Mouse Axis Detection (GetInputType)

    [Theory]
    [InlineData("maxis_x", InputType.MouseAxis)]
    [InlineData("maxis_y", InputType.MouseAxis)]
    [InlineData("MAXIS_X", InputType.MouseAxis)]
    [InlineData("lshift+maxis_x", InputType.MouseAxis)]
    [InlineData("maxis_y+rctrl", InputType.MouseAxis)]
    public void GetInputType_MouseAxis_ReturnsMouseAxis(string input, InputType expected) =>
        input.GetInputType().Should().Be(expected);

    #endregion

    #region Keyboard Detection (GetInputType)

    [Theory]
    [InlineData("a", InputType.Keyboard)]
    [InlineData("z", InputType.Keyboard)]
    [InlineData("0", InputType.Keyboard)]
    [InlineData("9", InputType.Keyboard)]
    [InlineData("f1", InputType.Keyboard)]
    [InlineData("f12", InputType.Keyboard)]
    [InlineData("lshift", InputType.Keyboard)]
    [InlineData("rshift", InputType.Keyboard)]
    [InlineData("lctrl", InputType.Keyboard)]
    [InlineData("lalt", InputType.Keyboard)]
    [InlineData("space", InputType.Keyboard)]
    [InlineData("enter", InputType.Keyboard)]
    [InlineData("tab", InputType.Keyboard)]
    [InlineData("escape", InputType.Keyboard)]
    [InlineData("up", InputType.Keyboard)]
    [InlineData("down", InputType.Keyboard)]
    [InlineData("left", InputType.Keyboard)]
    [InlineData("right", InputType.Keyboard)]
    [InlineData("home", InputType.Keyboard)]
    [InlineData("end", InputType.Keyboard)]
    [InlineData("pgup", InputType.Keyboard)]
    [InlineData("pgdn", InputType.Keyboard)]
    [InlineData("insert", InputType.Keyboard)]
    [InlineData("delete", InputType.Keyboard)]
    [InlineData("minus", InputType.Keyboard)]
    [InlineData("comma", InputType.Keyboard)]
    [InlineData("period", InputType.Keyboard)]
    [InlineData("apostrophe", InputType.Keyboard)]
    [InlineData("semicolon", InputType.Keyboard)]
    [InlineData("lshift+a", InputType.Keyboard)]
    [InlineData("lctrl+alt+a", InputType.Keyboard)]
    [InlineData("rshift+enter", InputType.Keyboard)]
    public void GetInputType_KeyboardKeys_ReturnsKeyboard(string input, InputType expected) =>
        input.GetInputType().Should().Be(expected);

    #endregion

    #region Edge Cases (GetInputType)

    [Theory]
    [InlineData(null, InputType.Unknown)]
    [InlineData("", InputType.Unknown)]
    [InlineData("   ", InputType.Unknown)]
    [InlineData("\t", InputType.Unknown)]
    [InlineData("\n", InputType.Unknown)]
    [InlineData("\r\n", InputType.Unknown)]
    [InlineData("\t \n", InputType.Unknown)]
    public void GetInputType_NullEmptyWhitespace_ReturnsUnknown(string? input, InputType expected) =>
        input.GetInputType().Should().Be(expected);

    #endregion

    #region IsModifierOnly

    [Theory]
    [InlineData("lalt", true)]
    [InlineData("ralt", true)]
    [InlineData("lshift", true)]
    [InlineData("rshift", true)]
    [InlineData("lctrl", true)]
    [InlineData("rctrl", true)]
    [InlineData("LALT", true)]
    [InlineData("lalt+rshift", false)]
    [InlineData("a", false)]
    [InlineData("mouse1", false)]
    [InlineData("mwheel_up", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsModifierOnly_ReturnsExpected(string? input, bool expected) => input.IsModifierOnly().Should().Be(expected);

    #endregion

    #region IsMouseWheel

    [Theory]
    [InlineData("mwheel_up", true)]
    [InlineData("mwheel_down", true)]
    [InlineData("MWHEEL_UP", true)]
    [InlineData("MWHEEL_DOWN", true)]
    [InlineData("mouse1", false)]
    [InlineData("mouse2", false)]
    [InlineData("lshift+mwheel_up", false)]
    [InlineData("mwheel_down+rshift", false)]
    [InlineData("lshift+alt+mwheel_up", false)]
    [InlineData("maxis_x", false)]
    [InlineData("a", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsMouseWheel_ReturnsExpected(string? input, bool expected) => input.IsMouseWheel().Should().Be(expected);

    #endregion

    #region IsMouseButton

    [Theory]
    [InlineData("mouse1", true)]
    [InlineData("mouse2", true)]
    [InlineData("mouse3", true)]
    [InlineData("mouse4", true)]
    [InlineData("mouse5", true)]
    [InlineData("lmb", true)]
    [InlineData("rmb", true)]
    [InlineData("mmb", true)]
    [InlineData("MOUSE1", true)]
    [InlineData("LMB", true)]
    [InlineData("mwheel_up", false)]
    [InlineData("mwheel_down", false)]
    [InlineData("maxis_x", false)]
    [InlineData("lshift+mouse1", false)]
    [InlineData("mouse2+rshift", false)]
    [InlineData("lshift+alt+mouse3", false)]
    [InlineData("a", false)]
    [InlineData("lshift", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("xlmb", false)]
    [InlineData("lmbx", false)]
    [InlineData("xmouse1", true)]
    [InlineData("mouse1x", true)]
    public void IsMouseButton_ReturnsExpected(string? input, bool expected) => input.IsMouseButton().Should().Be(expected);

    #endregion

    #region Mouse Button Detection (GetInputType)

    [Theory]
    [InlineData("mouse1", InputType.MouseButton)]
    [InlineData("mouse2", InputType.MouseButton)]
    [InlineData("mouse3", InputType.MouseButton)]
    [InlineData("mouse4", InputType.MouseButton)]
    [InlineData("mouse5", InputType.MouseButton)]
    [InlineData("lmb", InputType.MouseButton)]
    [InlineData("rmb", InputType.MouseButton)]
    [InlineData("mmb", InputType.MouseButton)]
    [InlineData("MOUSE1", InputType.MouseButton)]
    [InlineData("lshift+mouse1", InputType.MouseButton)]
    [InlineData("mouse1+rshift", InputType.MouseButton)]
    [InlineData("lshift+rctrl+mouse2", InputType.MouseButton)]
    public void GetInputType_MouseButtons_ReturnsMouseButton(string input, InputType expected) =>
        input.GetInputType().Should().Be(expected);

    [Theory]
    [InlineData("xlmb", InputType.Keyboard)]
    [InlineData("lmbx", InputType.Keyboard)]
    [InlineData("xmouse1", InputType.MouseButton)]
    [InlineData("mouse1x", InputType.MouseButton)]
    public void GetInputType_MouseButtonExactMatch_PartialMatchesNotRecognized(string input, InputType expected) =>
        input.GetInputType().Should().Be(expected);

    #endregion
}
