using FluentAssertions;
using SCStreamDeck.Common;
using WindowsInput.Native;

namespace Tests.Unit.Common;

public sealed class SCKeyToDirectInputMapperTests
{
    [Fact]
    public void TryGetDirectInputKeyCode_KnownKey_ReturnsCode()
    {
        bool result = SCKeyToDirectInputMapper.TryGetDirectInputKeyCode("a", out DirectInputKeyCode code);

        result.Should().BeTrue();
        code.Should().Be(DirectInputKeyCode.DikA);
    }

    [Fact]
    public void TryGetDirectInputKeyCode_TrimsAndIsCaseInsensitive()
    {
        bool result = SCKeyToDirectInputMapper.TryGetDirectInputKeyCode("  F1 ", out DirectInputKeyCode code);

        result.Should().BeTrue();
        code.Should().Be(DirectInputKeyCode.DikF1);
    }

    [Fact]
    public void TryGetDirectInputKeyCode_InvalidKey_ReturnsFalse()
    {
        bool result = SCKeyToDirectInputMapper.TryGetDirectInputKeyCode("not_a_key", out DirectInputKeyCode code);

        result.Should().BeFalse();
        code.Should().Be(default);
    }

    [Fact]
    public void TryGetDirectInputKeyCode_EmptyInput_ReturnsFalse()
    {
        bool result = SCKeyToDirectInputMapper.TryGetDirectInputKeyCode(" ", out DirectInputKeyCode code);

        result.Should().BeFalse();
        code.Should().Be(default);
    }
}
