using FluentAssertions;
using SCStreamDeck.Common;
using WindowsInput.Native;

namespace Tests.Unit.Common;

public sealed class DirectInputDisplayMapperTests
{
    private const nint DummyHkl = 0;

    [Fact]
    public void ToDisplay_NullOrEmpty_ReturnsEmpty()
    {
        DirectInputDisplayMapper.ToDisplay(null, DummyHkl).Should().BeEmpty();
        DirectInputDisplayMapper.ToDisplay(" ", DummyHkl).Should().BeEmpty();
    }

    [Fact]
    public void ToDisplay_SingleKnownToken_ReturnsFixedName()
    {
        string result = DirectInputDisplayMapper.ToDisplay("lshift", DummyHkl);

        result.Should().Be("L-Shift");
    }

    [Fact]
    public void ToDisplay_MultipleTokens_JoinsWithPlus()
    {
        string result = DirectInputDisplayMapper.ToDisplay("lshift+apostrophe", DummyHkl);

        result.Should().Contain("+");
    }

    [Fact]
    public void IsModifierKey_DetectsModifiers()
    {
        DirectInputDisplayMapper.IsModifierKey(DirectInputKeyCode.DikLshift).Should().BeTrue();
        DirectInputDisplayMapper.IsModifierKey(DirectInputKeyCode.DikA).Should().BeFalse();
    }

    [Fact]
    public void TryGetKeyNameTextFromDik_ReturnsSomethingOrNullButDoesNotThrow()
    {
        string? result = DirectInputDisplayMapper.TryGetKeyNameTextFromDik(DirectInputKeyCode.DikA);

        result.Should().NotBeNull();
    }
}
