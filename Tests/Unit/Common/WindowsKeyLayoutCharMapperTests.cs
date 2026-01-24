using FluentAssertions;
using SCStreamDeck.Common;
using WindowsInput.Native;

namespace Tests.Unit.Common;

public sealed class WindowsKeyLayoutCharMapperTests
{
    private const nint DummyHkl = 0;

    [Fact]
    public void TryGetChar_InvalidKey_ReturnsNull()
    {
        string? result = WindowsKeyLayoutCharMapper.TryGetChar(DummyHkl, 0, 0, false, false);

        result.Should().BeNull();
    }

    [Fact]
    public void TryGetChar_ShiftAndAltGr_DoNotThrow()
    {
        string? result = WindowsKeyLayoutCharMapper.TryGetChar(DummyHkl, VirtualKeyCode.VK_A, 30, true, true);

        result.Should().BeNull();
    }
}
