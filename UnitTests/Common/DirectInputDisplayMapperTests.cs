using SCStreamDeck.Common;
using WindowsInput.Native;
using Xunit;

namespace SCStreamDeck.UnitTests.Common;

/// <summary>
/// Unit tests for DirectInputDisplayMapper.
/// Tests mapping from DirectInput key codes to user-friendly display strings.
/// Note: Some tests may be platform-dependent due to Windows API usage.
/// </summary>
public class DirectInputDisplayMapperTests
{
    #region ToDisplay - Input Validation Tests

    [Fact]
    public void ToDisplay_ShouldReturnEmptyString_ForNullInput()
    {
        // Act
        string result = DirectInputDisplayMapper.ToDisplay(null, IntPtr.Zero);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ToDisplay_ShouldReturnEmptyString_ForEmptyString()
    {
        // Act
        string result = DirectInputDisplayMapper.ToDisplay(string.Empty, IntPtr.Zero);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ToDisplay_ShouldReturnEmptyString_ForWhitespace()
    {
        // Act
        string result = DirectInputDisplayMapper.ToDisplay("   ", IntPtr.Zero);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    #endregion

    #region ToDisplay - Simple Keys Tests

    [Theory]
    [InlineData("a")]
    [InlineData("b")]
    [InlineData("z")]
    [InlineData("0")]
    [InlineData("9")]
    public void ToDisplay_ShouldHandleSimpleKeys(string key)
    {
        // Act
        string result = DirectInputDisplayMapper.ToDisplay(key, IntPtr.Zero);

        // Assert
        Assert.NotEmpty(result);
        Assert.DoesNotContain("+", result); // Single key, no modifiers
    }

    #endregion

    #region ToDisplay - Modifier Keys Tests

    [Theory]
    [InlineData("lshift", "L-Shift")]
    [InlineData("rshift", "R-Shift")]
    [InlineData("lctrl", "L-Ctrl")]
    [InlineData("rctrl", "R-Ctrl")]
    [InlineData("lalt", "L-Alt")]
    [InlineData("ralt", "R-Alt")]
    public void ToDisplay_ShouldMapModifierKeys_Correctly(string scKey, string expectedDisplay)
    {
        // Act
        string result = DirectInputDisplayMapper.ToDisplay(scKey, IntPtr.Zero);

        // Assert
        Assert.Equal(expectedDisplay, result);
    }

    #endregion

    #region ToDisplay - Navigation Keys Tests

    [Theory]
    [InlineData("up", "Up")]
    [InlineData("down", "Down")]
    [InlineData("left", "Left")]
    [InlineData("right", "Right")]
    [InlineData("home", "Home")]
    [InlineData("end", "End")]
    public void ToDisplay_ShouldMapNavigationKeys_Correctly(string scKey, string expectedDisplay)
    {
        // Act
        string result = DirectInputDisplayMapper.ToDisplay(scKey, IntPtr.Zero);

        // Assert
        Assert.Equal(expectedDisplay, result);
    }

    [Theory]
    [InlineData("pgup", "PgUp")]
    [InlineData("pgdn", "PgDn")]
    [InlineData("insert", "Ins")]
    [InlineData("delete", "Del")]
    public void ToDisplay_ShouldMapSpecialNavigationKeys_Correctly(string scKey, string expectedDisplay)
    {
        // Act
        string result = DirectInputDisplayMapper.ToDisplay(scKey, IntPtr.Zero);

        // Assert
        Assert.Equal(expectedDisplay, result);
    }

    #endregion

    #region ToDisplay - Numpad Keys Tests

    [Theory]
    [InlineData("np_0", "Num0")]
    [InlineData("np_1", "Num1")]
    [InlineData("np_5", "Num5")]
    [InlineData("np_9", "Num9")]
    public void ToDisplay_ShouldMapNumpadNumbers_Correctly(string scKey, string expectedDisplay)
    {
        // Act
        string result = DirectInputDisplayMapper.ToDisplay(scKey, IntPtr.Zero);

        // Assert
        Assert.Equal(expectedDisplay, result);
    }

    [Theory]
    [InlineData("np_multiply", "Num*")]
    [InlineData("np_add", "Num+")]
    [InlineData("np_subtract", "Num-")]
    [InlineData("np_divide", "Num/")]
    [InlineData("np_period", "Num.")]
    [InlineData("np_enter", "NumEnter")]
    public void ToDisplay_ShouldMapNumpadOperators_Correctly(string scKey, string expectedDisplay)
    {
        // Act
        string result = DirectInputDisplayMapper.ToDisplay(scKey, IntPtr.Zero);

        // Assert
        Assert.Equal(expectedDisplay, result);
    }

    #endregion

    #region ToDisplay - Key Combinations Tests

    [Theory]
    [InlineData("lshift+a")]
    [InlineData("lctrl+space")]
    [InlineData("lalt+f1")]
    public void ToDisplay_ShouldHandleKeyCombinations(string binding)
    {
        // Act
        string result = DirectInputDisplayMapper.ToDisplay(binding, IntPtr.Zero);

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains(" + ", result);
    }

    [Fact]
    public void ToDisplay_ShouldJoinMultipleKeys_WithPlus()
    {
        // Arrange
        string binding = "lshift+lctrl+a";

        // Act
        string result = DirectInputDisplayMapper.ToDisplay(binding, IntPtr.Zero);

        // Assert
        Assert.Contains(" + ", result);
        Assert.Equal(3, result.Split(" + ").Length); // 2 separators = 3 parts
    }

    [Theory]
    [InlineData("lshift+a", "L-Shift")]
    [InlineData("rctrl+b", "R-Ctrl")]
    [InlineData("lalt+c", "L-Alt")]
    public void ToDisplay_ShouldIncludeModifierInCombination(string binding, string expectedModifier)
    {
        // Act
        string result = DirectInputDisplayMapper.ToDisplay(binding, IntPtr.Zero);

        // Assert
        Assert.Contains(expectedModifier, result);
    }

    #endregion

    #region ToDisplay - Whitespace Handling Tests

    [Theory]
    [InlineData(" lshift + a ", "L-Shift")]
    [InlineData("  lctrl  +  b  ", "L-Ctrl")]
    [InlineData("lalt + space", "L-Alt")]
    public void ToDisplay_ShouldTrimWhitespace(string binding, string expectedModifier)
    {
        // Act
        string result = DirectInputDisplayMapper.ToDisplay(binding, IntPtr.Zero);

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains(expectedModifier, result);
    }

    #endregion

    #region ToDisplay - Case Handling Tests

    [Theory]
    [InlineData("lshift+A")]
    [InlineData("LSHIFT+a")]
    [InlineData("LShift+A")]
    public void ToDisplay_ShouldHandleCaseVariations(string binding)
    {
        // Act
        string result = DirectInputDisplayMapper.ToDisplay(binding, IntPtr.Zero);

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains("L-Shift", result);
    }

    #endregion

    #region ToDisplay - Unknown Keys Tests

    [Theory]
    [InlineData("unknown_key")]
    [InlineData("invalid")]
    [InlineData("xyz")]
    public void ToDisplay_ShouldReturnUnknownKey_Unmapped(string unknownKey)
    {
        // Act
        string result = DirectInputDisplayMapper.ToDisplay(unknownKey, IntPtr.Zero);

        // Assert
        Assert.NotEmpty(result);
        // For unknown keys, it returns the key itself (lowercased)
        Assert.Equal(unknownKey.ToLowerInvariant(), result);
    }

    #endregion

    #region ToDisplay - Real-World Star Citizen Bindings Tests

    [Theory]
    [InlineData("lshift+y")]
    [InlineData("lalt+z")]
    [InlineData("lctrl+f1")]
    [InlineData("lshift+apostrophe")]
    [InlineData("lalt+minus")]
    public void ToDisplay_ShouldHandleRealSCBindings(string binding)
    {
        // Act
        string result = DirectInputDisplayMapper.ToDisplay(binding, IntPtr.Zero);

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains(" + ", result);
    }

    [Theory]
    [InlineData("y")]
    [InlineData("z")]
    [InlineData("apostrophe")]
    public void ToDisplay_ShouldHandleLayoutSensitiveKeys(string key)
    {
        // Act
        string result = DirectInputDisplayMapper.ToDisplay(key, IntPtr.Zero);

        // Assert
        Assert.NotEmpty(result);
    }

    #endregion

    #region IsModifierKey Tests

    [Theory]
    [InlineData(DirectInputKeyCode.DikLshift)]
    [InlineData(DirectInputKeyCode.DikRshift)]
    [InlineData(DirectInputKeyCode.DikLcontrol)]
    [InlineData(DirectInputKeyCode.DikRcontrol)]
    [InlineData(DirectInputKeyCode.DikLalt)]
    [InlineData(DirectInputKeyCode.DikRalt)]
    public void IsModifierKey_ShouldReturnTrue_ForModifierKeys(DirectInputKeyCode dik)
    {
        // Act
        bool result = DirectInputDisplayMapper.IsModifierKey(dik);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(DirectInputKeyCode.DikA)]
    [InlineData(DirectInputKeyCode.Dik0)]
    [InlineData(DirectInputKeyCode.DikF1)]
    [InlineData(DirectInputKeyCode.DikSpace)]
    [InlineData(DirectInputKeyCode.DikUp)]
    public void IsModifierKey_ShouldReturnFalse_ForNonModifierKeys(DirectInputKeyCode dik)
    {
        // Act
        bool result = DirectInputDisplayMapper.IsModifierKey(dik);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region TryGetKeyNameTextFromDik Tests

    [Theory]
    [InlineData(DirectInputKeyCode.DikA)]
    [InlineData(DirectInputKeyCode.DikF1)]
    [InlineData(DirectInputKeyCode.DikSpace)]
    public void TryGetKeyNameTextFromDik_ShouldReturnKeyName_ForValidKeys(DirectInputKeyCode dik)
    {
        // Act
        string? result = DirectInputDisplayMapper.TryGetKeyNameTextFromDik(dik);

        // Assert
        // Note: This may return null in some environments (e.g., non-Windows)
        // We just verify it doesn't throw an exception
        Assert.NotNull(result);
    }

    #endregion

    #region Complex Combinations Tests

    [Theory]
    [InlineData("lshift+lctrl+a")]
    [InlineData("lshift+lalt+rshift+b")]
    [InlineData("lctrl+rctrl+c")]
    public void ToDisplay_ShouldHandleMultipleModifiers(string binding)
    {
        // Act
        string result = DirectInputDisplayMapper.ToDisplay(binding, IntPtr.Zero);

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains(" + ", result);
    }

    [Fact]
    public void ToDisplay_ShouldHandleVeryLongCombinations()
    {
        // Arrange
        string longBinding = "lshift+lctrl+lalt+rshift+rctrl+ralt+a";

        // Act
        string result = DirectInputDisplayMapper.ToDisplay(longBinding, IntPtr.Zero);

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains(" + ", result);
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void ToDisplay_ShouldHandleMultiplePlusSigns()
    {
        // Arrange
        string binding = "a+++b";

        // Act
        string result = DirectInputDisplayMapper.ToDisplay(binding, IntPtr.Zero);

        // Assert
        Assert.NotEmpty(result);
    }

    [Fact]
    public void ToDisplay_ShouldHandleOnlyPlusSign()
    {
        // Arrange
        string binding = "+";

        // Act
        string result = DirectInputDisplayMapper.ToDisplay(binding, IntPtr.Zero);

        // Assert
        Assert.NotEmpty(result);
    }

    [Fact]
    public void ToDisplay_ShouldHandleEmptyTokens()
    {
        // Arrange
        string binding = "lshift++a";

        // Act
        string result = DirectInputDisplayMapper.ToDisplay(binding, IntPtr.Zero);

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains("L-Shift", result);
    }

    #endregion

    #region Special Characters Tests

    [Theory]
    [InlineData("minus")]
    [InlineData("equals")]
    [InlineData("lbracket")]
    [InlineData("rbracket")]
    [InlineData("semicolon")]
    public void ToDisplay_ShouldHandleSpecialCharacters(string scKey)
    {
        // Act
        string result = DirectInputDisplayMapper.ToDisplay(scKey, IntPtr.Zero);

        // Assert
        Assert.NotEmpty(result);
    }

    #endregion

    #region Function Keys Tests

    [Theory]
    [InlineData("f1")]
    [InlineData("f5")]
    [InlineData("f10")]
    [InlineData("f12")]
    public void ToDisplay_ShouldHandleFunctionKeys(string scKey)
    {
        // Act
        string result = DirectInputDisplayMapper.ToDisplay(scKey, IntPtr.Zero);

        // Assert
        Assert.NotEmpty(result);
    }

    #endregion

    #region Real-World German vs English Layout Tests

    [Theory]
    [InlineData("lshift+z")] // Z is on different position in DE vs EN
    [InlineData("lshift+y")] // Y is on different position in DE vs EN
    [InlineData("lshift+apostrophe")] // Apostrophe position differs
    public void ToDisplay_ShouldHandleLayoutDifferences(string binding)
    {
        // Act
        string result = DirectInputDisplayMapper.ToDisplay(binding, IntPtr.Zero);

        // Assert
        Assert.NotEmpty(result);
        // The actual display may vary based on keyboard layout, but it should not throw
    }

    #endregion
}
