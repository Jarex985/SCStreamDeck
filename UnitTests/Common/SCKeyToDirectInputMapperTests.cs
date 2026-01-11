using SCStreamDeck.Common;
using WindowsInput.Native;
using Xunit;

namespace SCStreamDeck.UnitTests.Common;

/// <summary>
/// Unit tests for SCKeyToDirectInputMapper.
/// Tests the mapping from Star Citizen key names to DirectInput key codes (layout-independent).
/// </summary>
public class SCKeyToDirectInputMapperTests
{
    #region Letter Mappings (A-Z)

    [Theory]
    [InlineData("a", DirectInputKeyCode.DikA)]
    [InlineData("A", DirectInputKeyCode.DikA)]
    [InlineData("b", DirectInputKeyCode.DikB)]
    [InlineData("B", DirectInputKeyCode.DikB)]
    [InlineData("c", DirectInputKeyCode.DikC)]
    [InlineData("d", DirectInputKeyCode.DikD)]
    [InlineData("e", DirectInputKeyCode.DikE)]
    [InlineData("f", DirectInputKeyCode.DikF)]
    [InlineData("g", DirectInputKeyCode.DikG)]
    [InlineData("h", DirectInputKeyCode.DikH)]
    [InlineData("i", DirectInputKeyCode.DikI)]
    [InlineData("j", DirectInputKeyCode.DikJ)]
    [InlineData("k", DirectInputKeyCode.DikK)]
    [InlineData("l", DirectInputKeyCode.DikL)]
    [InlineData("m", DirectInputKeyCode.DikM)]
    [InlineData("n", DirectInputKeyCode.DikN)]
    [InlineData("o", DirectInputKeyCode.DikO)]
    [InlineData("p", DirectInputKeyCode.DikP)]
    [InlineData("q", DirectInputKeyCode.DikQ)]
    [InlineData("r", DirectInputKeyCode.DikR)]
    [InlineData("s", DirectInputKeyCode.DikS)]
    [InlineData("t", DirectInputKeyCode.DikT)]
    [InlineData("u", DirectInputKeyCode.DikU)]
    [InlineData("v", DirectInputKeyCode.DikV)]
    [InlineData("w", DirectInputKeyCode.DikW)]
    [InlineData("x", DirectInputKeyCode.DikX)]
    [InlineData("y", DirectInputKeyCode.DikY)]
    [InlineData("z", DirectInputKeyCode.DikZ)]
    public void TryGetDirectInputKeyCode_ShouldMapLetters_CaseInsensitive(string scKey, DirectInputKeyCode expected)
    {
        // Act
        bool result = SCKeyToDirectInputMapper.TryGetDirectInputKeyCode(scKey, out DirectInputKeyCode actual);

        // Assert
        Assert.True(result);
        Assert.Equal(expected, actual);
    }

    #endregion

    #region Number Mappings (0-9)

    [Theory]
    [InlineData("0", DirectInputKeyCode.Dik0)]
    [InlineData("1", DirectInputKeyCode.Dik1)]
    [InlineData("2", DirectInputKeyCode.Dik2)]
    [InlineData("3", DirectInputKeyCode.Dik3)]
    [InlineData("4", DirectInputKeyCode.Dik4)]
    [InlineData("5", DirectInputKeyCode.Dik5)]
    [InlineData("6", DirectInputKeyCode.Dik6)]
    [InlineData("7", DirectInputKeyCode.Dik7)]
    [InlineData("8", DirectInputKeyCode.Dik8)]
    [InlineData("9", DirectInputKeyCode.Dik9)]
    public void TryGetDirectInputKeyCode_ShouldMapNumbers(string scKey, DirectInputKeyCode expected)
    {
        // Act
        bool result = SCKeyToDirectInputMapper.TryGetDirectInputKeyCode(scKey, out DirectInputKeyCode actual);

        // Assert
        Assert.True(result);
        Assert.Equal(expected, actual);
    }

    #endregion

    #region Punctuation Mappings

    [Theory]
    [InlineData("minus", DirectInputKeyCode.DikMinus)]
    [InlineData("equals", DirectInputKeyCode.DikEquals)]
    [InlineData("lbracket", DirectInputKeyCode.DikLbracket)]
    [InlineData("rbracket", DirectInputKeyCode.DikRbracket)]
    [InlineData("backslash", DirectInputKeyCode.DikBackslash)]
    [InlineData("semicolon", DirectInputKeyCode.DikSemicolon)]
    [InlineData("apostrophe", DirectInputKeyCode.DikApostrophe)]
    [InlineData("grave", DirectInputKeyCode.DikGrave)]
    [InlineData("comma", DirectInputKeyCode.DikComma)]
    [InlineData("period", DirectInputKeyCode.DikPeriod)]
    [InlineData("slash", DirectInputKeyCode.DikSlash)]
    public void TryGetDirectInputKeyCode_ShouldMapPunctuation(string scKey, DirectInputKeyCode expected)
    {
        // Act
        bool result = SCKeyToDirectInputMapper.TryGetDirectInputKeyCode(scKey, out DirectInputKeyCode actual);

        // Assert
        Assert.True(result);
        Assert.Equal(expected, actual);
    }

    #endregion

    #region Function Key Mappings

    [Theory]
    [InlineData("f1", DirectInputKeyCode.DikF1)]
    [InlineData("f2", DirectInputKeyCode.DikF2)]
    [InlineData("f3", DirectInputKeyCode.DikF3)]
    [InlineData("f4", DirectInputKeyCode.DikF4)]
    [InlineData("f5", DirectInputKeyCode.DikF5)]
    [InlineData("f6", DirectInputKeyCode.DikF6)]
    [InlineData("f7", DirectInputKeyCode.DikF7)]
    [InlineData("f8", DirectInputKeyCode.DikF8)]
    [InlineData("f9", DirectInputKeyCode.DikF9)]
    [InlineData("f10", DirectInputKeyCode.DikF10)]
    [InlineData("f11", DirectInputKeyCode.DikF11)]
    [InlineData("f12", DirectInputKeyCode.DikF12)]
    public void TryGetDirectInputKeyCode_ShouldMapFunctionKeys(string scKey, DirectInputKeyCode expected)
    {
        // Act
        bool result = SCKeyToDirectInputMapper.TryGetDirectInputKeyCode(scKey, out DirectInputKeyCode actual);

        // Assert
        Assert.True(result);
        Assert.Equal(expected, actual);
    }

    #endregion

    #region Special Key Mappings

    [Theory]
    [InlineData("escape", DirectInputKeyCode.DikEscape)]
    [InlineData("space", DirectInputKeyCode.DikSpace)]
    [InlineData("enter", DirectInputKeyCode.DikReturn)]
    [InlineData("tab", DirectInputKeyCode.DikTab)]
    [InlineData("backspace", DirectInputKeyCode.DikBackspace)]
    [InlineData("capslock", DirectInputKeyCode.DikCapital)]
    [InlineData("numlock", DirectInputKeyCode.DikNumlock)]
    [InlineData("scrolllock", DirectInputKeyCode.DikScroll)]
    public void TryGetDirectInputKeyCode_ShouldMapSpecialKeys(string scKey, DirectInputKeyCode expected)
    {
        // Act
        bool result = SCKeyToDirectInputMapper.TryGetDirectInputKeyCode(scKey, out DirectInputKeyCode actual);

        // Assert
        Assert.True(result);
        Assert.Equal(expected, actual);
    }

    #endregion

    #region Modifier Key Mappings

    [Theory]
    [InlineData("lshift", DirectInputKeyCode.DikLshift)]
    [InlineData("rshift", DirectInputKeyCode.DikRshift)]
    [InlineData("lctrl", DirectInputKeyCode.DikLcontrol)]
    [InlineData("rctrl", DirectInputKeyCode.DikRcontrol)]
    [InlineData("lalt", DirectInputKeyCode.DikLalt)]
    [InlineData("ralt", DirectInputKeyCode.DikRalt)]
    public void TryGetDirectInputKeyCode_ShouldMapModifierKeys(string scKey, DirectInputKeyCode expected)
    {
        // Act
        bool result = SCKeyToDirectInputMapper.TryGetDirectInputKeyCode(scKey, out DirectInputKeyCode actual);

        // Assert
        Assert.True(result);
        Assert.Equal(expected, actual);
    }

    #endregion

    #region Navigation Key Mappings

    [Theory]
    [InlineData("up", DirectInputKeyCode.DikUp)]
    [InlineData("down", DirectInputKeyCode.DikDown)]
    [InlineData("left", DirectInputKeyCode.DikLeft)]
    [InlineData("right", DirectInputKeyCode.DikRight)]
    [InlineData("home", DirectInputKeyCode.DikHome)]
    [InlineData("end", DirectInputKeyCode.DikEnd)]
    [InlineData("pgup", DirectInputKeyCode.DikPageUp)]
    [InlineData("pgdn", DirectInputKeyCode.DikPageDown)]
    [InlineData("insert", DirectInputKeyCode.DikInsert)]
    [InlineData("delete", DirectInputKeyCode.DikDelete)]
    public void TryGetDirectInputKeyCode_ShouldMapNavigationKeys(string scKey, DirectInputKeyCode expected)
    {
        // Act
        bool result = SCKeyToDirectInputMapper.TryGetDirectInputKeyCode(scKey, out DirectInputKeyCode actual);

        // Assert
        Assert.True(result);
        Assert.Equal(expected, actual);
    }

    #endregion

    #region Numpad Key Mappings

    [Theory]
    [InlineData("np_0", DirectInputKeyCode.DikNumpad0)]
    [InlineData("np_1", DirectInputKeyCode.DikNumpad1)]
    [InlineData("np_2", DirectInputKeyCode.DikNumpad2)]
    [InlineData("np_3", DirectInputKeyCode.DikNumpad3)]
    [InlineData("np_4", DirectInputKeyCode.DikNumpad4)]
    [InlineData("np_5", DirectInputKeyCode.DikNumpad5)]
    [InlineData("np_6", DirectInputKeyCode.DikNumpad6)]
    [InlineData("np_7", DirectInputKeyCode.DikNumpad7)]
    [InlineData("np_8", DirectInputKeyCode.DikNumpad8)]
    [InlineData("np_9", DirectInputKeyCode.DikNumpad9)]
    public void TryGetDirectInputKeyCode_ShouldMapNumpadNumbers(string scKey, DirectInputKeyCode expected)
    {
        // Act
        bool result = SCKeyToDirectInputMapper.TryGetDirectInputKeyCode(scKey, out DirectInputKeyCode actual);

        // Assert
        Assert.True(result);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("np_multiply", DirectInputKeyCode.DikMultiply)]
    [InlineData("np_add", DirectInputKeyCode.DikAdd)]
    [InlineData("np_subtract", DirectInputKeyCode.DikSubtract)]
    [InlineData("np_divide", DirectInputKeyCode.DikDivide)]
    [InlineData("np_period", DirectInputKeyCode.DikDecimal)]
    [InlineData("np_enter", DirectInputKeyCode.DikNumpadenter)]
    public void TryGetDirectInputKeyCode_ShouldMapNumpadOperators(string scKey, DirectInputKeyCode expected)
    {
        // Act
        bool result = SCKeyToDirectInputMapper.TryGetDirectInputKeyCode(scKey, out DirectInputKeyCode actual);

        // Assert
        Assert.True(result);
        Assert.Equal(expected, actual);
    }

    #endregion

    #region Invalid Keys

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid_key")]
    [InlineData("f13")]
    [InlineData("mouse1")]
    [InlineData("mwheel_up")]
    [InlineData("unknown")]
    [InlineData("xyz")]
    public void TryGetDirectInputKeyCode_ShouldReturnFalse_ForInvalidKeys(string invalidKey)
    {
        // Act
        bool result = SCKeyToDirectInputMapper.TryGetDirectInputKeyCode(invalidKey, out DirectInputKeyCode dik);

        // Assert
        Assert.False(result);
        Assert.Equal(default, dik);
    }

    [Fact]
    public void TryGetDirectInputKeyCode_ShouldReturnFalse_ForNullKey()
    {
        // Act
        bool result = SCKeyToDirectInputMapper.TryGetDirectInputKeyCode(null!, out DirectInputKeyCode dik);

        // Assert
        Assert.False(result);
        Assert.Equal(default, dik);
    }

    #endregion

    #region Case Insensitivity

    [Theory]
    [InlineData("A", "a")]
    [InlineData("LCTRL", "lctrl")]
    [InlineData("F1", "f1")]
    [InlineData("SPACE", "space")]
    public void TryGetDirectInputKeyCode_ShouldBeCaseInsensitive(string uppercase, string lowercase)
    {
        // Act
        bool resultUpper = SCKeyToDirectInputMapper.TryGetDirectInputKeyCode(uppercase, out DirectInputKeyCode dikUpper);
        bool resultLower = SCKeyToDirectInputMapper.TryGetDirectInputKeyCode(lowercase, out DirectInputKeyCode dikLower);

        // Assert
        Assert.True(resultUpper);
        Assert.True(resultLower);
        Assert.Equal(dikUpper, dikLower);
    }

    #endregion

    #region Common Star Citizen Keys

    [Theory]
    [InlineData("y", DirectInputKeyCode.DikY)] // Common for German vs English layouts
    [InlineData("z", DirectInputKeyCode.DikZ)] // Common for German vs English layouts
    [InlineData("apostrophe", DirectInputKeyCode.DikApostrophe)]
    [InlineData("minus", DirectInputKeyCode.DikMinus)]
    [InlineData("equals", DirectInputKeyCode.DikEquals)]
    [InlineData("lshift", DirectInputKeyCode.DikLshift)]
    [InlineData("lalt", DirectInputKeyCode.DikLalt)]
    public void TryGetDirectInputKeyCode_ShouldHandleCommonSCKeys(string scKey, DirectInputKeyCode expected)
    {
        // Act
        bool result = SCKeyToDirectInputMapper.TryGetDirectInputKeyCode(scKey, out DirectInputKeyCode actual);

        // Assert
        Assert.True(result);
        Assert.Equal(expected, actual);
    }

    #endregion

    #region Complete Coverage Test

    [Fact]
    public void AllMappedKeys_ShouldBeCovered()
    {
        // This test ensures all keys in the analysis document are mapped
        string[] expectedKeys = new[]
        {
            // Letters
            "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z",
            // Numbers
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
            // Punctuation
            "minus", "equals", "lbracket", "rbracket", "backslash", "semicolon", "apostrophe", "grave", "comma", "period", "slash",
            // Function keys
            "f1", "f2", "f3", "f4", "f5", "f6", "f7", "f8", "f9", "f10", "f11", "f12",
            // Special keys
            "escape", "space", "enter", "tab", "backspace", "capslock", "numlock", "scrolllock",
            // Modifiers
            "lshift", "rshift", "lctrl", "rctrl", "lalt", "ralt",
            // Navigation
            "up", "down", "left", "right", "home", "end", "pgup", "pgdn", "insert", "delete",
            // Numpad numbers
            "np_0", "np_1", "np_2", "np_3", "np_4", "np_5", "np_6", "np_7", "np_8", "np_9",
            // Numpad operators
            "np_multiply", "np_add", "np_subtract", "np_divide", "np_period", "np_enter"
        };

        // Act & Assert
        foreach (string key in expectedKeys)
        {
            bool result = SCKeyToDirectInputMapper.TryGetDirectInputKeyCode(key, out DirectInputKeyCode dik);
            Assert.True(result, $"Key '{key}' should be mapped");
            Assert.NotEqual(default, dik);
        }
    }

    #endregion
}
