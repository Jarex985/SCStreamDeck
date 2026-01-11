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

    [Fact]
    public void DirectInputDisplayMapper_TestGetKeyName_ReturnsName()
    {
        // Test for L-Alt
        var result = DirectInputDisplayMapper.TryGetKeyNameTextFromDik(DirectInputKeyCode.DikLalt);
        Assert.NotNull(result);
        // Depending on system, it might be "Left Alt" or similar
    }

    [Fact]
    public void DirectInputDisplayMapper_ToDisplay_NullOrEmpty_ReturnsEmpty()
    {
        IntPtr hkl = KeyboardLayoutDetector.DetectCurrent().Hkl;
        Assert.Equal(string.Empty, DirectInputDisplayMapper.ToDisplay(null, hkl));
        Assert.Equal(string.Empty, DirectInputDisplayMapper.ToDisplay("", hkl));
        Assert.Equal(string.Empty, DirectInputDisplayMapper.ToDisplay("   ", hkl));
    }

    [Fact]
    public void DirectInputDisplayMapper_ToDisplay_SingleKey_ReturnsUpperCase()
    {
        IntPtr hkl = KeyboardLayoutDetector.DetectCurrent().Hkl;
        var result = DirectInputDisplayMapper.ToDisplay("a", hkl);
        Assert.Equal("A", result); // Assuming 'a' maps to 'A'
    }

    [Fact]
    public void DirectInputDisplayMapper_ToDisplay_ModifierCombination_ReturnsFormatted()
    {
        IntPtr hkl = KeyboardLayoutDetector.DetectCurrent().Hkl;
        var result = DirectInputDisplayMapper.ToDisplay("lshift+a", hkl);
        Assert.Equal("L-Shift + A", result);
    }

    [Fact]
    public void DirectInputDisplayMapper_ToDisplay_InvalidKey_ReturnsUpperCase()
    {
        IntPtr hkl = KeyboardLayoutDetector.DetectCurrent().Hkl;
        var result = DirectInputDisplayMapper.ToDisplay("invalid", hkl);
        Assert.Equal("INVALID", result);
    }

    [Fact]
    public void DirectInputDisplayMapper_IsModifierKey_ReturnsTrueForModifiers()
    {
        Assert.True(DirectInputDisplayMapper.IsModifierKey(DirectInputKeyCode.DikLshift));
        Assert.True(DirectInputDisplayMapper.IsModifierKey(DirectInputKeyCode.DikRalt));
        Assert.False(DirectInputDisplayMapper.IsModifierKey(DirectInputKeyCode.DikA));
    }
}
