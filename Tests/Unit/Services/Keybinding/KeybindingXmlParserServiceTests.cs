using FluentAssertions;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Keybinding;

namespace Tests.Unit.Services.Keybinding;

public sealed class KeybindingXmlParserServiceTests
{
    private readonly KeybindingXmlParserService _service = new();

    [Fact]
    public void ParseActivationModes_ParsesAttributes()
    {
        const string xml = """
                           <root>
                             <ActivationMode name="press" onPress="1" onHold="0" onRelease="0" retriggerable="0" pressTriggerThreshold="0.2" releaseTriggerThreshold="0.3" releaseTriggerDelay="0.1" multiTap="1" multiTapBlock="2" />
                             <ActivationMode name="hold" onPress="1" onHold="1" onRelease="1" retriggerable="1" />
                           </root>
                           """;

        Dictionary<string, ActivationModeMetadata> modes = _service.ParseActivationModes(xml);

        modes.Should().ContainKey("press");
        modes["press"].OnPress.Should().BeTrue();
        modes["press"].OnHold.Should().BeFalse();
        modes["press"].OnRelease.Should().BeFalse();
        modes["press"].PressTriggerThreshold.Should().Be(0.2f);
        modes["press"].ReleaseTriggerThreshold.Should().Be(0.3f);
        modes["press"].ReleaseTriggerDelay.Should().Be(0.1f);
        modes["press"].MultiTap.Should().Be(1);
        modes["press"].MultiTapBlock.Should().Be(2);

        modes.Should().ContainKey("hold");
        modes["hold"].OnHold.Should().BeTrue();
        modes["hold"].OnRelease.Should().BeTrue();
        modes["hold"].Retriggerable.Should().BeTrue();
    }

    [Fact]
    public void ParseXmlToActions_ParsesActionsAndNormalizesBindings()
    {
        string xml = @"<root>
  <ActivationMode name=""press"" onPress=""1"" />
  <actionmap name=""spaceship_general"" UILabel=""@map"" UICategory=""@category"">
    <action name=""v_toggle"" UILabel=""@label"" UIDescription=""@desc"" activationMode=""press"" keyboard=""SPACE"" mouse=""MOUSE1"" />
    <action name=""wheel"" UILabel=""@wlabel"" keyboard=""MOUSE_WHEEL_UP"" />
  </actionmap>
</root>";

        List<KeybindingActionData> actions = _service.ParseXmlToActions(xml);

        actions.Should().HaveCount(2);

        KeybindingActionData action = actions[0];
        action.Name.Should().Be("v_toggle");
        action.MapName.Should().Be("spaceship_general");
        action.MapLabel.Should().Be("@map");
        action.Category.Should().Be("@category");
        action.ActivationMode.Should().Be(ActivationMode.press);
        action.Bindings.Keyboard.Should().Be("SPACE");
        action.Bindings.Mouse.Should().Be("MOUSE1");

        KeybindingActionData wheel = actions[1];
        wheel.Bindings.Keyboard.Should().Be("MOUSE_WHEEL_UP");
        wheel.Bindings.Mouse.Should().BeNull();
    }

    [Fact]
    public void ParseXmlToActions_InfersActivationModeWhenMissing()
    {
        string xml = @"<root>
  <ActivationMode name=""hold_no_retrigger"" onPress=""1"" onHold=""0"" onRelease=""1"" retriggerable=""0"" />
  <actionmap name=""spaceship_general"" UILabel=""@map"" UICategory=""@category"">
    <action name=""engine_cycle"" UILabel=""@label"" UIDescription=""@desc"" onPress=""1"" onRelease=""1"" />
  </actionmap>
</root>";

        List<KeybindingActionData> actions = _service.ParseXmlToActions(xml);

        actions.Should().ContainSingle();
        actions[0].ActivationMode.Should().Be(ActivationMode.hold_no_retrigger);
    }
}
