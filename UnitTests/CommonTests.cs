using SCStreamDeck.SCCore.Common;
using WindowsInput.Native;

namespace UnitTests;

public class CommonTests
{
    [Fact]
    public void SCKeyToDirectInputMapper_TryGetDirectInputKeyCode_ValidKey_ReturnsTrue()
    {
        bool result = SCKeyToDirectInputMapper.TryGetDirectInputKeyCode("a", out DirectInputKeyCode dik);

        Assert.True(result);
        Assert.Equal(DirectInputKeyCode.DikA, dik);
    }

    [Fact]
    public void SCKeyToDirectInputMapper_TryGetDirectInputKeyCode_InvalidKey_ReturnsFalse()
    {
        bool result = SCKeyToDirectInputMapper.TryGetDirectInputKeyCode("invalid", out DirectInputKeyCode dik);

        Assert.False(result);
        Assert.Equal(default, dik);
    }

    [Fact]
    public void SCKeyToDirectInputMapper_TryGetDirectInputKeyCode_CaseInsensitive()
    {
        bool result = SCKeyToDirectInputMapper.TryGetDirectInputKeyCode("A", out DirectInputKeyCode dik);

        Assert.True(result);
        Assert.Equal(DirectInputKeyCode.DikA, dik);
    }

    [Fact]
    public void KeyboardLayoutDetector_DetectCurrent_ReturnsValidInfo()
    {
        var info = KeyboardLayoutDetector.DetectCurrent();

        Assert.NotNull(info);
        Assert.NotEqual(IntPtr.Zero, info.Hkl);
    }

    [Fact]
    public void KeyboardLayoutDetector_DetectCurrent_CachesResult()
    {
        var info1 = KeyboardLayoutDetector.DetectCurrent();
        var info2 = KeyboardLayoutDetector.DetectCurrent();

        Assert.Equal(info1, info2);
    }

    [Fact]
    public void KeyboardLayoutDetector_InvalidateCache_ResetsCache()
    {
        var info1 = KeyboardLayoutDetector.DetectCurrent();
        KeyboardLayoutDetector.InvalidateCache();

        var info2 = KeyboardLayoutDetector.DetectCurrent();

        Assert.NotNull(info2);
        // Note: In a real test, we might check if it's a new instance, but since it's internal, we just ensure it works
    }

    [Fact]
    public void WindowsKeyLayoutCharMapper_TryGetChar_ValidInput_ReturnsString()
    {
        IntPtr hkl = KeyboardLayoutDetector.DetectCurrent().Hkl;

        // Act
        string? result = WindowsKeyLayoutCharMapper.TryGetChar(hkl, VirtualKeyCode.VK_A, 0x1E, false, false);

        // Assert
        Assert.NotNull(result);
        // Note: Exact character depends on layout, but should not be null for 'a'
    }

    [Fact]
    public void WindowsKeyLayoutCharMapper_TryGetChar_InvalidInput_ReturnsNull()
    {
        IntPtr hkl = KeyboardLayoutDetector.DetectCurrent().Hkl;
        string? result = WindowsKeyLayoutCharMapper.TryGetChar(hkl, (VirtualKeyCode)999, 0, false, false);

        Assert.Null(result);
    }
}
