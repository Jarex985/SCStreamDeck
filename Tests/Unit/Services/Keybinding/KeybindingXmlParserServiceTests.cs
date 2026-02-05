using System.Reflection;
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
    public void ParseXmlToActions_UsesNestedKeyboardInputdata_WhenKeyboardAttributeIsMissing()
    {
        const string xml = """
                           <root>
                             <ActivationMode name="press" onPress="1" />
                             <actionmap name="ui" UILabel="@map" UICategory="@category">
                               <action name="focus_on_chat_textinput" activationMode="press" UILabel="@ui_CIUIChatFocus" UIDescription="@ui_CIUIChatFocusDesc">
                                 <keyboard>
                                   <inputdata input="enter" />
                                   <inputdata input="np_enter" />
                                 </keyboard>
                               </action>
                             </actionmap>
                           </root>
                           """;

        List<KeybindingActionData> actions = _service.ParseXmlToActions(xml);

        actions.Should().ContainSingle();
        actions[0].Name.Should().Be("focus_on_chat_textinput");
        actions[0].Bindings.Keyboard.Should().Be("enter");
    }

    [Fact]
    public void ParseXmlToActions_MarksIsToggleCandidate_WhenActionNameContainsToggle()
    {
        const string xml = """
                           <root>
                             <ActivationMode name="press" onPress="1" />
                             <actionmap name="seat_general" UILabel="@map" UICategory="@category">
                               <action name="v_light_amplification_toggle" activationMode="press" UILabel="@label" UIDescription="@desc" keyboard="ralt+l" />
                             </actionmap>
                           </root>
                           """;

        List<KeybindingActionData> actions = _service.ParseXmlToActions(xml);

        actions.Should().ContainSingle();
        actions[0].Name.Should().Be("v_light_amplification_toggle");
        actions[0].IsToggleCandidate.Should().BeTrue();
    }

    [Fact]
    public void ParseXmlToActions_MarksIsToggleCandidate_WhenDescriptionContainsToggle()
    {
        const string xml = """
                           <root>
                             <ActivationMode name="press" onPress="1" />
                             <actionmap name="lights_controller" UILabel="@map" UICategory="@category">
                               <action name="v_lights" activationMode="press" UILabel="Headlights" UIDescription="@ui_CIToggleLightsDesc" keyboard="l" />
                             </actionmap>
                           </root>
                           """;

        List<KeybindingActionData> actions = _service.ParseXmlToActions(xml);

        actions.Should().ContainSingle();
        actions[0].Name.Should().Be("v_lights");
        actions[0].IsToggleCandidate.Should().BeTrue();
    }

    [Fact]
    public void ParseXmlToActions_AppliesCategoryFallback_WhenActionMapHasNoCategory()
    {
        string xml = @"<root>
  <ActivationMode name=""press"" onPress=""1"" />
  <actionmap name=""vehicle_mfd"" UILabel=""@mfd_label"">
    <action name=""v_mfd_power"" UILabel=""@label"" UIDescription=""@desc"" />
  </actionmap>
 </root>";

        List<KeybindingActionData> actions = _service.ParseXmlToActions(xml);

        actions.Should().ContainSingle();
        actions[0].Category.Should().Be("@mfd_label");
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

    private static ActivationMode? FindExactModeMatch(
        bool onPress,
        bool onHold,
        bool onRelease,
        bool retriggerable,
        Dictionary<string, ActivationModeMetadata> activationModes) =>
        typeof(KeybindingXmlParserService)
            .GetMethod("FindExactModeMatch", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [onPress, onHold, onRelease, retriggerable, activationModes]) as ActivationMode?;

    private static ActivationMode InferFromHeuristic(
        bool onPress,
        bool onHold,
        bool onRelease,
        bool retriggerable) =>
        typeof(KeybindingXmlParserService)
            .GetMethod("InferFromHeuristic", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [onPress, onHold, onRelease, retriggerable]) as ActivationMode? ?? ActivationMode.press;

    #region Heuristic Inference Tests (InferFromHeuristic)

    [Fact]
    public void ParseXmlToActions_InfersPressMode_WhenOnlyOnPress()
    {
        string xml = @"<root>
  <actionmap name=""test"" UILabel=""@map"" UICategory=""@category"">
    <action name=""action1"" UILabel=""@label"" UIDescription=""@desc"" onPress=""1"" />
  </actionmap>
</root>";

        List<KeybindingActionData> actions = _service.ParseXmlToActions(xml);

        actions.Should().ContainSingle();
        actions[0].ActivationMode.Should().Be(ActivationMode.press);
    }

    [Fact]
    public void ParseXmlToActions_InfersHoldRetriggerable_WhenOnPressOnReleaseRetriggerable()
    {
        string xml = @"<root>
  <actionmap name=""test"" UILabel=""@map"" UICategory=""@category"">
    <action name=""action1"" UILabel=""@label"" UIDescription=""@desc"" onPress=""1"" onRelease=""1"" retriggerable=""1"" />
  </actionmap>
</root>";

        List<KeybindingActionData> actions = _service.ParseXmlToActions(xml);

        actions.Should().ContainSingle();
        actions[0].ActivationMode.Should().Be(ActivationMode.hold);
    }

    [Fact]
    public void ParseXmlToActions_InfersHoldNoRetrigger_WhenOnPressOnReleaseNoRetriggerable()
    {
        string xml = @"<root>
  <actionmap name=""test"" UILabel=""@map"" UICategory=""@category"">
    <action name=""action1"" UILabel=""@label"" UIDescription=""@desc"" onPress=""1"" onRelease=""1"" retriggerable=""0"" />
  </actionmap>
</root>";

        List<KeybindingActionData> actions = _service.ParseXmlToActions(xml);

        actions.Should().ContainSingle();
        actions[0].ActivationMode.Should().Be(ActivationMode.hold_no_retrigger);
    }

    [Fact]
    public void ParseXmlToActions_InfersHold_WhenOnHoldIsTrue()
    {
        string xml = @"<root>
  <actionmap name=""test"" UILabel=""@map"" UICategory=""@category"">
    <action name=""action1"" UILabel=""@label"" UIDescription=""@desc"" onHold=""1"" />
  </actionmap>
</root>";

        List<KeybindingActionData> actions = _service.ParseXmlToActions(xml);

        actions.Should().ContainSingle();
        actions[0].ActivationMode.Should().Be(ActivationMode.hold);
    }

    [Fact]
    public void ParseXmlToActions_InfersTap_WhenOnlyOnRelease()
    {
        string xml = @"<root>
  <actionmap name=""test"" UILabel=""@map"" UICategory=""@category"">
    <action name=""action1"" UILabel=""@label"" UIDescription=""@desc"" onRelease=""1"" />
  </actionmap>
</root>";

        List<KeybindingActionData> actions = _service.ParseXmlToActions(xml);

        actions.Should().ContainSingle();
        actions[0].ActivationMode.Should().Be(ActivationMode.tap);
    }

    [Fact]
    public void ParseXmlToActions_InfersPress_WhenNoAttributes()
    {
        string xml = @"<root>
  <actionmap name=""test"" UILabel=""@map"" UICategory=""@category"">
    <action name=""action1"" UILabel=""@label"" UIDescription=""@desc"" />
  </actionmap>
</root>";

        List<KeybindingActionData> actions = _service.ParseXmlToActions(xml);

        actions.Should().ContainSingle();
        actions[0].ActivationMode.Should().Be(ActivationMode.press);
    }

    [Fact]
    public void ParseXmlToActions_InfersPress_WhenAllFalse()
    {
        string xml = @"<root>
  <actionmap name=""test"" UILabel=""@map"" UICategory=""@category"">
    <action name=""action1"" UILabel=""@label"" UIDescription=""@desc"" onPress=""0"" onHold=""0"" onRelease=""0"" />
  </actionmap>
</root>";

        List<KeybindingActionData> actions = _service.ParseXmlToActions(xml);

        actions.Should().ContainSingle();
        actions[0].ActivationMode.Should().Be(ActivationMode.press);
    }

    #endregion

    #region Exact Mode Match Tests (FindExactModeMatch)

    [Fact]
    public void ParseXmlToActions_UsesExactMatch_WhenModeMatchesExactly()
    {
        string xml = @"<root>
  <ActivationMode name=""delayed_press"" onPress=""1"" onHold=""0"" onRelease=""0"" retriggerable=""0"" />
  <actionmap name=""test"" UILabel=""@map"" UICategory=""@category"">
    <action name=""action1"" UILabel=""@label"" UIDescription=""@desc"" onPress=""1"" onHold=""0"" onRelease=""0"" retriggerable=""0"" />
  </actionmap>
</root>";

        List<KeybindingActionData> actions = _service.ParseXmlToActions(xml);

        actions.Should().ContainSingle();
        actions[0].ActivationMode.Should().Be(ActivationMode.delayed_press);
    }

    [Fact]
    public void ParseXmlToActions_SkipsPressTapModes_WhenOnPressAndOnPresent()
    {
        string xml = @"<root>
  <ActivationMode name=""press"" onPress=""1"" onHold=""0"" onRelease=""0"" retriggerable=""0"" />
  <ActivationMode name=""tap"" onPress=""0"" onHold=""0"" onRelease=""1"" retriggerable=""0"" />
  <actionmap name=""test"" UILabel=""@map"" UICategory=""@category"">
    <action name=""action1"" UILabel=""@label"" UIDescription=""@desc"" onPress=""1"" onRelease=""1"" />
  </actionmap>
</root>";

        List<KeybindingActionData> actions = _service.ParseXmlToActions(xml);

        actions.Should().ContainSingle();
        actions[0].ActivationMode.Should().Be(ActivationMode.hold_no_retrigger);
    }

    [Fact]
    public void ParseXmlToActions_UsesHeuristic_WhenNoExactMatch()
    {
        string xml = @"<root>
  <ActivationMode name=""hold"" onPress=""1"" onHold=""1"" onRelease=""1"" retriggerable=""1"" />
  <actionmap name=""test"" UILabel=""@map"" UICategory=""@category"">
    <action name=""action1"" UILabel=""@label"" UIDescription=""@desc"" onPress=""1"" onHold=""0"" onRelease=""0"" />
  </actionmap>
</root>";

        List<KeybindingActionData> actions = _service.ParseXmlToActions(xml);

        actions.Should().ContainSingle();
        actions[0].ActivationMode.Should().Be(ActivationMode.press);
    }

    #endregion

    #region ParseAction Edge Cases

    [Fact]
    public void ParseXmlToActions_SkipsActionsWithoutUILabel()
    {
        string xml = @"<root>
  <actionmap name=""test"" UILabel=""@map"" UICategory=""@category"">
    <action name=""action1"" activationMode=""press"" keyboard=""SPACE"" />
  </actionmap>
</root>";

        List<KeybindingActionData> actions = _service.ParseXmlToActions(xml);

        actions.Should().BeEmpty();
    }

    [Fact]
    public void ParseXmlToActions_SkipsActionsWithoutName()
    {
        string xml = @"<root>
  <actionmap name=""test"" UILabel=""@map"" UICategory=""@category"">
    <action UILabel=""@label"" UIDescription=""@desc"" activationMode=""press"" keyboard=""SPACE"" />
  </actionmap>
</root>";

        List<KeybindingActionData> actions = _service.ParseXmlToActions(xml);

        actions.Should().BeEmpty();
    }

    [Fact]
    public void ParseXmlToActions_NormalizesWhitespaceInBindings()
    {
        string xml = @"<root>
  <actionmap name=""test"" UILabel=""@map"" UICategory=""@category"">
    <action name=""action1"" UILabel=""@label"" activationMode=""press"" keyboard=""  SPACE  "" mouse=""  MOUSE1  "" />
  </actionmap>
</root>";

        List<KeybindingActionData> actions = _service.ParseXmlToActions(xml);

        actions.Should().ContainSingle();
        actions[0].Bindings.Keyboard.Should().Be("SPACE");
        actions[0].Bindings.Mouse.Should().Be("MOUSE1");
    }

    [Fact]
    public void ParseXmlToActions_SetsNullForEmptyBindings()
    {
        string xml = @"<root>
  <actionmap name=""test"" UILabel=""@map"" UICategory=""@category"">
    <action name=""action1"" UILabel=""@label"" activationMode=""press"" keyboard="""" mouse="""" joystick="""" gamepad=""""/>
  </actionmap>
</root>";

        List<KeybindingActionData> actions = _service.ParseXmlToActions(xml);

        actions.Should().ContainSingle();
        actions[0].Bindings.Keyboard.Should().BeNull();
        actions[0].Bindings.Mouse.Should().BeNull();
        actions[0].Bindings.Joystick.Should().BeNull();
        actions[0].Bindings.Gamepad.Should().BeNull();
    }

    [Fact]
    public void ParseXmlToActions_MovesMouseWheelFromKeyboardToMouse()
    {
        string xml = @"<root>
  <actionmap name=""test"" UILabel=""@map"" UICategory=""@category"">
    <action name=""action1"" UILabel=""@label"" activationMode=""press"" keyboard=""MWHEEL_UP"" />
  </actionmap>
</root>";

        List<KeybindingActionData> actions = _service.ParseXmlToActions(xml);

        actions.Should().ContainSingle();
        actions[0].Bindings.Keyboard.Should().BeNull();
        actions[0].Bindings.Mouse.Should().Be("MWHEEL_UP");
    }

    [Fact]
    public void ParseXmlToActions_MovesMouseButtonFromKeyboardToMouse()
    {
        string xml = @"<root>
  <actionmap name=""test"" UILabel=""@map"" UICategory=""@category"">
    <action name=""action1"" UILabel=""@label"" activationMode=""press"" keyboard=""MOUSE1"" />
  </actionmap>
</root>";

        List<KeybindingActionData> actions = _service.ParseXmlToActions(xml);

        actions.Should().ContainSingle();
        actions[0].Bindings.Keyboard.Should().BeNull();
        actions[0].Bindings.Mouse.Should().Be("MOUSE1");
    }

    [Fact]
    public void ParseXmlToActions_HmdPrefixInKeyboardIsRemoved()
    {
        string xml = @"<root>
  <actionmap name=""test"" UILabel=""@map"" UICategory=""@category"">
    <action name=""action1"" UILabel=""@label"" activationMode=""press"" keyboard=""HMD_SPACE"" />
  </actionmap>
</root>";

        List<KeybindingActionData> actions = _service.ParseXmlToActions(xml);

        actions.Should().ContainSingle();
        actions[0].Bindings.Keyboard.Should().BeNull();
    }

    #endregion

    #region ParseActivationModes Edge Cases

    [Fact]
    public void ParseActivationModes_SkipsModesWithoutName()
    {
        string xml = @"<root>
  <ActivationMode onPress=""1"" onHold=""0"" onRelease=""0"" />
</root>";

        Dictionary<string, ActivationModeMetadata> modes = _service.ParseActivationModes(xml);

        modes.Should().BeEmpty();
    }

    [Fact]
    public void ParseActivationModes_ParsesDefaultThresholdValues()
    {
        string xml = @"<root>
  <ActivationMode name=""test"" onPress=""1"" />
</root>";

        Dictionary<string, ActivationModeMetadata> modes = _service.ParseActivationModes(xml);

        modes.Should().ContainKey("test");
        modes["test"].PressTriggerThreshold.Should().Be(-1);
        modes["test"].ReleaseTriggerThreshold.Should().Be(-1);
        modes["test"].ReleaseTriggerDelay.Should().Be(0);
    }

    [Fact]
    public void ParseActivationModes_ParsesMultiTapDefaultValues()
    {
        string xml = @"<root>
  <ActivationMode name=""test"" onPress=""1"" />
</root>";

        Dictionary<string, ActivationModeMetadata> modes = _service.ParseActivationModes(xml);

        modes.Should().ContainKey("test");
        modes["test"].MultiTap.Should().Be(1);
        modes["test"].MultiTapBlock.Should().Be(1);
    }

    #endregion

    #region FindExactModeMatch Tests

    [Fact]
    public void FindExactModeMatch_ReturnsMatch_WhenExactMatchExists()
    {
        Dictionary<string, ActivationModeMetadata> modes = new()
        {
            ["press"] =
                new ActivationModeMetadata { OnPress = true, OnHold = false, OnRelease = false, Retriggerable = false },
            ["hold"] = new ActivationModeMetadata { OnPress = true, OnHold = false, OnRelease = true, Retriggerable = true }
        };

        ActivationMode? result = FindExactModeMatch(true, false, false, false, modes);

        result.Should().NotBeNull();
        result.Value.Should().Be(ActivationMode.press);
    }

    [Fact]
    public void FindExactModeMatch_ReturnsNull_WhenNoMatchExists()
    {
        Dictionary<string, ActivationModeMetadata> modes = new()
        {
            ["hold"] = new ActivationModeMetadata { OnPress = true, OnHold = false, OnRelease = true, Retriggerable = true }
        };

        ActivationMode? result = FindExactModeMatch(true, false, false, false, modes);

        result.Should().BeNull();
    }

    [Fact]
    public void FindExactModeMatch_SkipsPressTap_WhenOnPressAndOnRelease()
    {
        Dictionary<string, ActivationModeMetadata> modes = new()
        {
            ["press"] =
                new ActivationModeMetadata { OnPress = true, OnHold = false, OnRelease = false, Retriggerable = false },
            ["tap"] = new ActivationModeMetadata { OnPress = false, OnHold = false, OnRelease = true, Retriggerable = false }
        };

        ActivationMode? result = FindExactModeMatch(true, true, false, false, modes);

        result.Should().BeNull();
    }

    [Fact]
    public void FindExactModeMatch_SkipsTap_WhenOnPressAndOnRelease()
    {
        Dictionary<string, ActivationModeMetadata> modes = new()
        {
            ["press"] =
                new ActivationModeMetadata { OnPress = true, OnHold = false, OnRelease = false, Retriggerable = false },
            ["tap"] = new ActivationModeMetadata { OnPress = false, OnHold = false, OnRelease = true, Retriggerable = false }
        };

        ActivationMode? result = FindExactModeMatch(true, true, false, false, modes);

        result.Should().BeNull();
    }

    [Fact]
    public void FindExactModeMatch_MatchesAllAttributes()
    {
        Dictionary<string, ActivationModeMetadata> modes = new()
        {
            ["smart_toggle"] =
                new ActivationModeMetadata { OnPress = true, OnHold = false, OnRelease = true, Retriggerable = false }
        };

        ActivationMode? result = FindExactModeMatch(true, false, true, false, modes);

        result.Should().NotBeNull();
        result.Value.Should().Be(ActivationMode.smart_toggle);
    }

    #endregion

    #region InferFromHeuristic Tests

    [Fact]
    public void InferFromHeuristic_ReturnsPress_WhenOnlyOnPress()
    {
        ActivationMode result = InferFromHeuristic(true, false, false, false);

        result.Should().Be(ActivationMode.press);
    }

    [Fact]
    public void InferFromHeuristic_ReturnsHold_WhenOnPressAndOnReleaseAndRetriggerable()
    {
        ActivationMode result = InferFromHeuristic(true, false, true, true);

        result.Should().Be(ActivationMode.hold);
    }

    [Fact]
    public void InferFromHeuristic_ReturnsHoldNoRetrigger_WhenOnPressAndOnReleaseAndNotRetriggerable()
    {
        ActivationMode result = InferFromHeuristic(true, false, true, false);

        result.Should().Be(ActivationMode.hold_no_retrigger);
    }

    [Fact]
    public void InferFromHeuristic_ReturnsHold_WhenOnHold()
    {
        ActivationMode result = InferFromHeuristic(false, true, false, false);

        result.Should().Be(ActivationMode.hold);
    }

    [Fact]
    public void InferFromHeuristic_ReturnsTap_WhenOnlyOnRelease()
    {
        ActivationMode result = InferFromHeuristic(false, false, true, false);

        result.Should().Be(ActivationMode.tap);
    }

    [Fact]
    public void InferFromHeuristic_ReturnsPress_WhenAllFalse()
    {
        ActivationMode result = InferFromHeuristic(false, false, false, false);

        result.Should().Be(ActivationMode.press);
    }

    #endregion
}
