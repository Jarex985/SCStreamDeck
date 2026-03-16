using FluentAssertions;
using Newtonsoft.Json.Linq;
using SCStreamDeck.ActionKeys;
using SCStreamDeck.ActionKeys.Settings;
using SCStreamDeck.Models;

namespace Tests.Unit.ActionKeys;

public sealed class AdaptiveDialTests
{
    private static KeybindingAction CreateAction(
        string? keyboardBinding = null,
        string? mouseBinding = null) =>
        new()
        {
            ActionName = "test_action",
            MapName = "Gameplay",
            MapLabel = "Gameplay",
            UiLabel = "Test Action",
            UiDescription = "Test Description",
            UiCategory = "Flight",
            KeyboardBinding = keyboardBinding ?? string.Empty,
            MouseBinding = mouseBinding ?? string.Empty,
            JoystickBinding = string.Empty,
            GamepadBinding = string.Empty,
            ActivationMode = ActivationMode.press
        };

    #region ResolveRotationFunction

    [Fact]
    public void ResolveRotationFunction_ReturnsRotateRight_ForPositiveTicks()
    {
        DialSettings settings = new()
        {
            RotateLeftFunction = "left_function",
            RotateRightFunction = "right_function"
        };

        string? result = AdaptiveDial.ResolveRotationFunction(settings, 3);

        result.Should().Be("right_function");
    }

    [Fact]
    public void ResolveRotationFunction_ReturnsRotateLeft_ForNegativeTicks()
    {
        DialSettings settings = new()
        {
            RotateLeftFunction = "left_function",
            RotateRightFunction = "right_function"
        };

        string? result = AdaptiveDial.ResolveRotationFunction(settings, -2);

        result.Should().Be("left_function");
    }

    [Fact]
    public void ResolveRotationFunction_ReturnsNull_ForZeroTicks()
    {
        DialSettings settings = new()
        {
            RotateLeftFunction = "left_function",
            RotateRightFunction = "right_function"
        };

        string? result = AdaptiveDial.ResolveRotationFunction(settings, 0);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(int.MaxValue)]
    public void ResolveRotationFunction_ReturnsRightFunction_ForAnyPositiveTicks(int ticks)
    {
        DialSettings settings = new() { RotateRightFunction = "right" };

        string? result = AdaptiveDial.ResolveRotationFunction(settings, ticks);

        result.Should().Be("right");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    [InlineData(int.MinValue)]
    public void ResolveRotationFunction_ReturnsLeftFunction_ForAnyNegativeTicks(int ticks)
    {
        DialSettings settings = new() { RotateLeftFunction = "left" };

        string? result = AdaptiveDial.ResolveRotationFunction(settings, ticks);

        result.Should().Be("left");
    }

    [Fact]
    public void ResolveRotationFunction_ReturnsNull_WhenRightFunctionIsNull_AndPositiveTicks()
    {
        DialSettings settings = new() { RotateLeftFunction = "left" };

        string? result = AdaptiveDial.ResolveRotationFunction(settings, 1);

        result.Should().BeNull();
    }

    [Fact]
    public void ResolveRotationFunction_ReturnsNull_WhenLeftFunctionIsNull_AndNegativeTicks()
    {
        DialSettings settings = new() { RotateRightFunction = "right" };

        string? result = AdaptiveDial.ResolveRotationFunction(settings, -1);

        result.Should().BeNull();
    }

    [Fact]
    public void ResolveRotationFunction_ThrowsArgumentNullException_ForNullSettings()
    {
        Action act = () => AdaptiveDial.ResolveRotationFunction(null!, 1);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ResolveExecutableBinding

    [Fact]
    public void ResolveExecutableBinding_PrefersKeyboardBinding_WhenPresent()
    {
        KeybindingAction action = CreateAction(keyboardBinding: "f1", mouseBinding: "mouse1");

        string? result = AdaptiveDial.ResolveExecutableBinding(action);

        result.Should().Be("f1");
    }

    [Fact]
    public void ResolveExecutableBinding_ReturnsMouseButton_WhenNoKeyboardBindingExists()
    {
        KeybindingAction action = CreateAction(mouseBinding: "mouse1");

        string? result = AdaptiveDial.ResolveExecutableBinding(action);

        result.Should().Be("mouse1");
    }

    [Fact]
    public void ResolveExecutableBinding_ReturnsMouseWheel_WhenNoKeyboardBindingExists()
    {
        KeybindingAction action = CreateAction(mouseBinding: "mwheel_up");

        string? result = AdaptiveDial.ResolveExecutableBinding(action);

        result.Should().Be("mwheel_up");
    }

    [Fact]
    public void ResolveExecutableBinding_ReturnsNull_ForUnsupportedAxisBinding()
    {
        KeybindingAction action = CreateAction(mouseBinding: "maxis_x");

        string? result = AdaptiveDial.ResolveExecutableBinding(action);

        result.Should().BeNull();
    }

    [Fact]
    public void ResolveExecutableBinding_ReturnsNull_WhenNoSupportedBindingExists()
    {
        KeybindingAction action = CreateAction();

        string? result = AdaptiveDial.ResolveExecutableBinding(action);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("mouse1")]
    [InlineData("mouse2")]
    [InlineData("mouse3")]
    public void ResolveExecutableBinding_ReturnsMouseButton_ForAllMouseButtonVariants(string mouseBinding)
    {
        KeybindingAction action = CreateAction(mouseBinding: mouseBinding);

        string? result = AdaptiveDial.ResolveExecutableBinding(action);

        result.Should().Be(mouseBinding);
    }

    [Theory]
    [InlineData("mwheel_up")]
    [InlineData("mwheel_down")]
    public void ResolveExecutableBinding_ReturnsMouseWheel_ForAllWheelVariants(string mouseBinding)
    {
        KeybindingAction action = CreateAction(mouseBinding: mouseBinding);

        string? result = AdaptiveDial.ResolveExecutableBinding(action);

        result.Should().Be(mouseBinding);
    }

    [Theory]
    [InlineData("maxis_x")]
    [InlineData("maxis_y")]
    public void ResolveExecutableBinding_ReturnsNull_ForAllMouseAxisVariants(string mouseBinding)
    {
        KeybindingAction action = CreateAction(mouseBinding: mouseBinding);

        string? result = AdaptiveDial.ResolveExecutableBinding(action);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("f1")]
    [InlineData("a")]
    [InlineData("lshift+space")]
    [InlineData("lctrl+alt+f4")]
    public void ResolveExecutableBinding_ReturnsKeyboardBinding_ForVariousKeyboardCombos(string keyboardBinding)
    {
        KeybindingAction action = CreateAction(keyboardBinding: keyboardBinding);

        string? result = AdaptiveDial.ResolveExecutableBinding(action);

        result.Should().Be(keyboardBinding);
    }

    [Fact]
    public void ResolveExecutableBinding_ThrowsArgumentNullException_ForNullAction()
    {
        Action act = () => AdaptiveDial.ResolveExecutableBinding(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region DialSettings

    [Fact]
    public void DialSettings_DefaultValues_AreAllNull()
    {
        DialSettings settings = new();

        settings.RotateLeftFunction.Should().BeNull();
        settings.RotateRightFunction.Should().BeNull();
        settings.PressFunction.Should().BeNull();
        settings.ClickSoundPath.Should().BeNull();
    }

    [Fact]
    public void DialSettings_SerializesAndDeserializes_UsingExpectedPropertyNames()
    {
        DialSettings settings = new()
        {
            RotateLeftFunction = "left_function",
            RotateRightFunction = "right_function",
            PressFunction = "press_function",
            ClickSoundPath = @"C:\\Audio\\click.wav"
        };

        JObject json = JObject.FromObject(settings);
        DialSettings? roundTripped = json.ToObject<DialSettings>();

        json.Value<string>("rotateLeftFunction").Should().Be("left_function");
        json.Value<string>("rotateRightFunction").Should().Be("right_function");
        json.Value<string>("pressFunction").Should().Be("press_function");
        json.Value<string>("clickSoundPath").Should().Be(@"C:\\Audio\\click.wav");
        roundTripped.Should().NotBeNull();
        roundTripped!.RotateLeftFunction.Should().Be("left_function");
        roundTripped.RotateRightFunction.Should().Be("right_function");
        roundTripped.PressFunction.Should().Be("press_function");
        roundTripped.ClickSoundPath.Should().Be(@"C:\\Audio\\click.wav");
    }

    [Fact]
    public void DialSettings_DeserializesPartialJson_LeavingMissingPropertiesNull()
    {
        JObject json = new() { ["rotateLeftFunction"] = "only_left" };

        DialSettings? settings = json.ToObject<DialSettings>();

        settings.Should().NotBeNull();
        settings!.RotateLeftFunction.Should().Be("only_left");
        settings.RotateRightFunction.Should().BeNull();
        settings.PressFunction.Should().BeNull();
        settings.ClickSoundPath.Should().BeNull();
    }

    [Fact]
    public void DialSettings_SerializesNullProperties_AsJsonNulls()
    {
        DialSettings settings = new() { RotateLeftFunction = "left" };

        JObject json = JObject.FromObject(settings);

        json["rotateLeftFunction"]!.Value<string>().Should().Be("left");
        json["rotateRightFunction"]!.Type.Should().Be(JTokenType.Null);
        json["pressFunction"]!.Type.Should().Be(JTokenType.Null);
        json["clickSoundPath"]!.Type.Should().Be(JTokenType.Null);
    }

    [Fact]
    public void DialSettings_RoundTrip_EmptyStringValues_ArePreserved()
    {
        DialSettings settings = new()
        {
            RotateLeftFunction = "",
            RotateRightFunction = "",
            PressFunction = "",
            ClickSoundPath = ""
        };

        JObject json = JObject.FromObject(settings);
        DialSettings? roundTripped = json.ToObject<DialSettings>();

        roundTripped!.RotateLeftFunction.Should().Be("");
        roundTripped.RotateRightFunction.Should().Be("");
        roundTripped.PressFunction.Should().Be("");
        roundTripped.ClickSoundPath.Should().Be("");
    }

    #endregion
}
