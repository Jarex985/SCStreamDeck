using SCStreamDeck.Common;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Keybinding;
using WindowsInput.Native;
using Xunit;

namespace SCStreamDeck.UnitTests.Keybinding.Parsing;

/// <summary>
/// Unit tests for KeybindingParserService.
/// Tests parsing of Star Citizen keybinding strings into executable inputs.
/// </summary>
public class KeybindingParserServiceTests
{
    private readonly KeybindingParserService _parser = new();

    #region ParseBinding - Input Validation Tests

    [Fact]
    public void ParseBinding_ShouldReturnNull_ForNullInput()
    {
        // Act
        var result = _parser.ParseBinding(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ParseBinding_ShouldReturnNull_ForEmptyString()
    {
        // Act
        var result = _parser.ParseBinding(string.Empty);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ParseBinding_ShouldReturnNull_ForWhitespace()
    {
        // Act
        var result = _parser.ParseBinding("   ");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region ParseBinding - Single Key Tests

    [Theory]
    [InlineData("a")]
    [InlineData("z")]
    [InlineData("0")]
    [InlineData("9")]
    public void ParseBinding_ShouldParseSingleLetterKeys(string binding)
    {
        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.Keyboard, result.Type);
    }

    [Theory]
    [InlineData("F1")]
    [InlineData("F5")]
    [InlineData("F12")]
    public void ParseBinding_ShouldParseFunctionKeys(string binding)
    {
        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.Keyboard, result.Type);
    }

    [Theory]
    [InlineData("space")]
    [InlineData("enter")]
    [InlineData("tab")]
    [InlineData("escape")]
    public void ParseBinding_ShouldParseSpecialKeys(string binding)
    {
        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.Keyboard, result.Type);
    }

    #endregion

    #region ParseBinding - Modifier + Key Tests

    [Theory]
    [InlineData("lshift+a")]
    [InlineData("rshift+z")]
    [InlineData("lctrl+space")]
    [InlineData("rctrl+f1")]
    [InlineData("lalt+tab")]
    [InlineData("ralt+enter")]
    public void ParseBinding_ShouldParseModifierPlusKey(string binding)
    {
        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.Keyboard, result.Type);
    }

    [Fact]
    public void ParseBinding_ShouldParseMultipleModifiers_WithKey()
    {
        // Arrange
        string binding = "lshift+lctrl+a";

        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.Keyboard, result.Type);

        if (result.Value is (DirectInputKeyCode[] modifiers, DirectInputKeyCode[] keys))
        {
            Assert.Equal(2, modifiers.Length);
            Assert.Single(keys);
        }
    }

    [Theory]
    [InlineData("lshift+lctrl+lalt+rshift+rctrl+ralt+a")]
    [InlineData("lshift+lctrl+f1")]
    public void ParseBinding_ShouldParseComplexModifierCombinations(string binding)
    {
        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.Keyboard, result.Type);
    }

    #endregion

    #region ParseBinding - Case Insensitivity Tests

    [Theory]
    [InlineData("a")]
    [InlineData("A")]
    [InlineData("lshift+a")]
    [InlineData("LSHIFT+A")]
    [InlineData("LShift+A")]
    public void ParseBinding_ShouldBeCaseInsensitive(string binding)
    {
        // Act
        var result1 = _parser.ParseBinding(binding);
        var result2 = _parser.ParseBinding(binding.ToLowerInvariant());

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(result1!.Type, result2!.Type);
    }

    #endregion

    #region ParseBinding - Whitespace Handling Tests

    [Theory]
    [InlineData(" lshift + a ")]
    [InlineData("  lctrl  +  b  ")]
    [InlineData("lalt + space")]
    [InlineData(" lshift + lctrl + c ")]
    public void ParseBinding_ShouldHandleWhitespace(string binding)
    {
        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.Keyboard, result.Type);
    }

    #endregion

    #region ParseBinding - Mouse Button Tests

    [Theory]
    [InlineData("mouse1")]
    [InlineData("mouse2")]
    [InlineData("mouse3")]
    [InlineData("mouse4")]
    [InlineData("mouse5")]
    public void ParseBinding_ShouldParseMouseButtons(string binding)
    {
        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.MouseButton, result.Type);

        if (result.Value is VirtualKeyCode vk)
        {
            Assert.NotEqual(default, vk);
        }
    }

    [Theory]
    [InlineData("lmb")]
    [InlineData("rmb")]
    [InlineData("mmb")]
    public void ParseBinding_ShouldParseNamedMouseButtons(string binding)
    {
        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.MouseButton, result.Type);
    }

    #endregion

    #region ParseBinding - Mouse Wheel Tests

    [Theory]
    [InlineData("mwheel_up")]
    [InlineData("MWHEEL_UP")]
    public void ParseBinding_ShouldParseMouseWheelUp(string binding)
    {
        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.MouseWheel, result.Type);

        if (result.Value is int wheelDirection)
        {
            Assert.Equal(-1, wheelDirection); // Negative = scroll UP
        }
    }

    [Theory]
    [InlineData("mwheel_down")]
    [InlineData("MWHEEL_DOWN")]
    public void ParseBinding_ShouldParseMouseWheelDown(string binding)
    {
        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.MouseWheel, result.Type);

        if (result.Value is int wheelDirection)
        {
            Assert.Equal(1, wheelDirection); // Positive = scroll DOWN
        }
    }

    #endregion

    #region ParseBinding - Mouse Wheel With Modifiers Tests

    [Theory]
    [InlineData("lalt+mwheel_up")]
    [InlineData("ralt+mwheel_up")]
    [InlineData("lshift+mwheel_up")]
    [InlineData("lctrl+mwheel_up")]
    public void ParseBinding_ShouldParseMouseWheelUp_WithModifiers(string binding)
    {
        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.MouseWheel, result.Type);

        if (result.Value is (DirectInputKeyCode[] modifiers, int wheelDirection))
        {
            Assert.Single(modifiers);
            Assert.Equal(-1, wheelDirection);
        }
    }

    [Theory]
    [InlineData("lalt+mwheel_down")]
    [InlineData("rctrl+mwheel_down")]
    [InlineData("lshift+rshift+mwheel_down")]
    public void ParseBinding_ShouldParseMouseWheelDown_WithModifiers(string binding)
    {
        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.MouseWheel, result.Type);

        if (result.Value is (DirectInputKeyCode[] modifiers, int wheelDirection))
        {
            Assert.NotEmpty(modifiers);
            Assert.Equal(1, wheelDirection);
        }
    }

    [Fact]
    public void ParseBinding_ShouldNotParseMouseWheel_WithoutWheelToken()
    {
        // Arrange
        string binding = "lalt+lctrl"; // Only modifiers, no wheel

        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        // Should return null because there's no actual key/wheel to press
        Assert.Null(result);
    }

    #endregion

    #region ParseBinding - Invalid Bindings Tests

    [Theory]
    [InlineData("invalid_key")]
    [InlineData("unknown")]
    [InlineData("xyz")]
    [InlineData("f13")]
    [InlineData("f0")]
    public void ParseBinding_ShouldReturnNull_ForInvalidKeys(string invalidBinding)
    {
        // Act
        var result = _parser.ParseBinding(invalidBinding);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ParseBinding_ShouldReturnNull_ForOnlyModifiers()
    {
        // Arrange
        string binding = "lshift+lctrl+lalt";

        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ParseBinding_ShouldReturnNull_ForEmptyTokens()
    {
        // Arrange
        string binding = "a+++";

        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        // Should parse 'a' and ignore the empty tokens
        Assert.NotNull(result);
    }

    #endregion

    #region ParseBinding - Modifier Keys Tests

    [Theory]
    [InlineData("lshift")]
    [InlineData("rshift")]
    [InlineData("lctrl")]
    [InlineData("rctrl")]
    [InlineData("lalt")]
    [InlineData("ralt")]
    public void ParseBinding_ShouldReturnNull_ForModifierOnly(string modifier)
    {
        // Act
        var result = _parser.ParseBinding(modifier);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region ParseBinding - Navigation Keys Tests

    [Theory]
    [InlineData("up")]
    [InlineData("down")]
    [InlineData("left")]
    [InlineData("right")]
    [InlineData("home")]
    [InlineData("end")]
    [InlineData("pgup")]
    [InlineData("pgdown")]
    [InlineData("insert")]
    [InlineData("delete")]
    public void ParseBinding_ShouldParseNavigationKeys(string binding)
    {
        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.Keyboard, result.Type);
    }

    #endregion

    #region ParseBinding - Numpad Keys Tests

    [Theory]
    [InlineData("np_0")]
    [InlineData("np_5")]
    [InlineData("np_9")]
    public void ParseBinding_ShouldParseNumpadNumbers(string binding)
    {
        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.Keyboard, result.Type);
    }

    [Theory]
    [InlineData("np_multiply")]
    [InlineData("np_add")]
    [InlineData("np_subtract")]
    [InlineData("np_divide")]
    [InlineData("np_period")]
    [InlineData("np_enter")]
    public void ParseBinding_ShouldParseNumpadOperators(string binding)
    {
        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.Keyboard, result.Type);
    }

    #endregion

    #region ParseBinding - Punctuation Keys Tests

    [Theory]
    [InlineData("minus")]
    [InlineData("equals")]
    [InlineData("lbracket")]
    [InlineData("rbracket")]
    [InlineData("semicolon")]
    [InlineData("apostrophe")]
    [InlineData("comma")]
    [InlineData("period")]
    [InlineData("slash")]
    public void ParseBinding_ShouldParsePunctuationKeys(string binding)
    {
        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.Keyboard, result.Type);
    }

    #endregion

    #region ParseBinding - Real-World Star Citizen Bindings Tests

    [Theory]
    [InlineData("lshift+y")] // Common in SC
    [InlineData("lshift+z")] // Common in SC
    [InlineData("lshift+apostrophe")] // Common in SC for German layouts
    [InlineData("lctrl+f4")] // Toggle UI
    [InlineData("lalt+y")] // Toggle landing gear
    [InlineData("lshift+b")] // Boost
    [InlineData("lctrl+i")] // Shield up
    [InlineData("lctrl+k")] // Shield down
    public void ParseBinding_ShouldParseRealSCBindings(string binding)
    {
        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.Keyboard, result.Type);
    }

    #endregion

    #region ParseBinding - Multiple Keys (Chord) Tests

    [Theory]
    [InlineData("a+b")]
    [InlineData("f1+f2")]
    [InlineData("lshift+a+b")]
    [InlineData("lshift+lctrl+a+b+c")]
    public void ParseBinding_ShouldParseMultipleNonModifierKeys(string binding)
    {
        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.Keyboard, result.Type);

        if (result.Value is (DirectInputKeyCode[] modifiers, DirectInputKeyCode[] keys))
        {
            Assert.True(keys.Length >= 2);
        }
    }

    #endregion

    #region ParseBinding - Edge Cases Tests

    [Fact]
    public void ParseBinding_ShouldHandleUppercaseBinding()
    {
        // Arrange
        string binding = "LSHIFT+CTRL+F1";

        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.Keyboard, result.Type);
    }

    [Fact]
    public void ParseBinding_ShouldHandleMixedCaseBinding()
    {
        // Arrange
        string binding = "LShift+LcTrl+A";

        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.Keyboard, result.Type);
    }

    [Fact]
    public void ParseBinding_ShouldHandleBindingWithExtraWhitespace()
    {
        // Arrange
        string binding = "  lshift   +   lctrl   +   a  ";

        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.Keyboard, result.Type);
    }

    #endregion

    #region ParseBinding - Modifier Key Validation Tests

    [Theory]
    [InlineData("lshift+a", DirectInputKeyCode.DikLshift)]
    [InlineData("rshift+a", DirectInputKeyCode.DikRshift)]
    [InlineData("lctrl+a", DirectInputKeyCode.DikLcontrol)]
    [InlineData("rctrl+a", DirectInputKeyCode.DikRcontrol)]
    [InlineData("lalt+a", DirectInputKeyCode.DikLalt)]
    [InlineData("ralt+a", DirectInputKeyCode.DikRalt)]
    public void ParseBinding_ShouldCorrectlyParseModifierKeys(string binding, DirectInputKeyCode expectedModifier)
    {
        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.Keyboard, result.Type);

        if (result.Value is (DirectInputKeyCode[] modifiers, DirectInputKeyCode[] keys))
        {
            Assert.Single(modifiers);
            Assert.Equal(expectedModifier, modifiers[0]);
        }
    }

    #endregion

    #region ParseBinding - Mouse Button Validation Tests

    [Theory]
    [InlineData("mouse1", VirtualKeyCode.LBUTTON)]
    [InlineData("mouse2", VirtualKeyCode.RBUTTON)]
    [InlineData("mouse3", VirtualKeyCode.MBUTTON)]
    [InlineData("mouse4", VirtualKeyCode.XBUTTON1)]
    [InlineData("mouse5", VirtualKeyCode.XBUTTON2)]
    public void ParseBinding_ShouldCorrectlyParseMouseButtons(string binding, VirtualKeyCode expectedButton)
    {
        // Act
        var result = _parser.ParseBinding(binding);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(InputType.MouseButton, result.Type);

        if (result.Value is VirtualKeyCode vk)
        {
            Assert.Equal(expectedButton, vk);
        }
    }

    #endregion
}
