using FluentAssertions;
using SCStreamDeck.Common;
using WindowsInput.Native;

namespace Tests.Unit.Common;

public sealed class DirectInputDisplayMapperTests
{
    private const nint DummyHkl = 0;

    #region Modifier Keys

    [Theory]
    [InlineData("lshift", "L-Shift")]
    [InlineData("rshift", "R-Shift")]
    [InlineData("lctrl", "L-Ctrl")]
    [InlineData("rctrl", "R-Ctrl")]
    [InlineData("lalt", "L-Alt")]
    [InlineData("ralt", "R-Alt")]
    public void ToDisplay_ModifierKeys_ReturnsCorrectDisplay(string scKey, string expected)
    {
        string result = DirectInputDisplayMapper.ToDisplay(scKey, DummyHkl);
        result.Should().Be(expected);
    }

    #endregion

    #region Navigation Keys

    [Theory]
    [InlineData("up", "Up")]
    [InlineData("down", "Down")]
    [InlineData("left", "Left")]
    [InlineData("right", "Right")]
    [InlineData("home", "Home")]
    [InlineData("end", "End")]
    [InlineData("pgup", "PgUp")]
    [InlineData("pgdn", "PgDn")]
    [InlineData("insert", "Ins")]
    [InlineData("delete", "Del")]
    public void ToDisplay_NavigationKeys_ReturnsCorrectDisplay(string scKey, string expected)
    {
        string result = DirectInputDisplayMapper.ToDisplay(scKey, DummyHkl);
        result.Should().Be(expected);
    }

    #endregion

    #region Numpad Keys

    [Theory]
    [InlineData("np_0", "Num0")]
    [InlineData("np_1", "Num1")]
    [InlineData("np_2", "Num2")]
    [InlineData("np_3", "Num3")]
    [InlineData("np_4", "Num4")]
    [InlineData("np_5", "Num5")]
    [InlineData("np_6", "Num6")]
    [InlineData("np_7", "Num7")]
    [InlineData("np_8", "Num8")]
    [InlineData("np_9", "Num9")]
    [InlineData("np_multiply", "Num*")]
    [InlineData("np_add", "Num+")]
    [InlineData("np_subtract", "Num-")]
    [InlineData("np_divide", "Num/")]
    [InlineData("np_period", "Num.")]
    [InlineData("np_enter", "NumEnter")]
    public void ToDisplay_NumpadKeys_ReturnsCorrectDisplay(string scKey, string expected)
    {
        string result = DirectInputDisplayMapper.ToDisplay(scKey, DummyHkl);
        result.Should().Be(expected);
    }

    #endregion

    #region IsModifierKey

    [Theory]
    [InlineData(DirectInputKeyCode.DikLshift, true)]
    [InlineData(DirectInputKeyCode.DikRshift, true)]
    [InlineData(DirectInputKeyCode.DikLcontrol, true)]
    [InlineData(DirectInputKeyCode.DikRcontrol, true)]
    [InlineData(DirectInputKeyCode.DikLalt, true)]
    [InlineData(DirectInputKeyCode.DikRalt, true)]
    [InlineData(DirectInputKeyCode.DikA, false)]
    [InlineData(DirectInputKeyCode.DikF1, false)]
    [InlineData(DirectInputKeyCode.DikSpace, false)]
    public void IsModifierKey_DetectsModifiersCorrectly(DirectInputKeyCode dik, bool expected) =>
        DirectInputDisplayMapper.IsModifierKey(dik).Should().Be(expected);

    #endregion

    #region Letter Keys (Windows API Path)

    [Theory]
    [InlineData("a")]
    [InlineData("z")]
    [InlineData("0")]
    [InlineData("9")]
    [InlineData("f1")]
    [InlineData("space")]
    [InlineData("enter")]
    [InlineData("tab")]
    [InlineData("escape")]
    public void ToDisplay_NonFixedKeys_ReturnsDisplayString(string scKey)
    {
        string result = DirectInputDisplayMapper.ToDisplay(scKey, DummyHkl);
        result.Should().NotBeEmpty();
    }

    #endregion

    #region Punctuation Keys

    [Theory]
    [InlineData("minus")]
    [InlineData("equals")]
    [InlineData("comma")]
    [InlineData("period")]
    [InlineData("slash")]
    [InlineData("semicolon")]
    [InlineData("apostrophe")]
    public void ToDisplay_PunctuationKeys_ReturnsDisplayString(string scKey)
    {
        string result = DirectInputDisplayMapper.ToDisplay(scKey, DummyHkl);
        result.Should().NotBeEmpty();
    }

    #endregion

    #region Multiple Tokens

    [Fact]
    public void ToDisplay_MultipleTokens_JoinsWithPlus()
    {
        string result = DirectInputDisplayMapper.ToDisplay("lshift+apostrophe", DummyHkl);
        result.Should().Contain("+");
    }

    [Fact]
    public void ToDisplay_ThreeTokens_JoinsWithPlus()
    {
        string result = DirectInputDisplayMapper.ToDisplay("lshift+rctrl+a", DummyHkl);
        string[] parts = result.Split(" + ");
        parts.Should().HaveCount(3);
    }

    [Fact]
    public void ToDisplay_TokensWithSpaces_Trimmed()
    {
        string result = DirectInputDisplayMapper.ToDisplay(" lshift + a ", DummyHkl);
        result.Should().NotContain("  ");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ToDisplay_NullOrEmpty_ReturnsEmpty()
    {
        DirectInputDisplayMapper.ToDisplay(null, DummyHkl).Should().BeEmpty();
        DirectInputDisplayMapper.ToDisplay(" ", DummyHkl).Should().BeEmpty();
        DirectInputDisplayMapper.ToDisplay("", DummyHkl).Should().BeEmpty();
    }

    [Fact]
    public void ToDisplay_CaseInsensitive_Works()
    {
        string result1 = DirectInputDisplayMapper.ToDisplay("lshift", DummyHkl);
        string result2 = DirectInputDisplayMapper.ToDisplay("LSHIFT", DummyHkl);
        string result3 = DirectInputDisplayMapper.ToDisplay("LsHiFt", DummyHkl);
        result1.Should().Be(result2).And.Be(result3);
    }

    #endregion

    #region Windows API Integration

    [Fact]
    public void TryGetKeyNameTextFromDik_ReturnsSomethingOrNullButDoesNotThrow()
    {
        string? result = DirectInputDisplayMapper.TryGetKeyNameTextFromDik(DirectInputKeyCode.DikA);
        result.Should().NotBeNull();
    }

    [Fact]
    public void TryGetKeyNameTextFromDik_UnknownKey_DoesNotThrow()
    {
        string? result = DirectInputDisplayMapper.TryGetKeyNameTextFromDik(default);
        result.Should().BeNull();
    }

    #endregion
}
