using FluentAssertions;
using Newtonsoft.Json.Linq;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Keybinding;

namespace Tests.Unit.Services.Keybinding;

public sealed class FunctionsPayloadBuilderTests
{
    private const string DefaultCategory = "Flight";
    private const string DefaultLabel = "Boost";
    private const string DefaultMapName = "Gameplay";

    private static KeybindingAction CreateAction(
        string actionName,
        ActivationMode activationMode,
        string? keyboardBinding = null,
        string? mouseBinding = null,
        string? joystickBinding = null,
        string? gamepadBinding = null,
        string? uiLabel = null,
        string? uiCategory = null,
        string? mapLabel = null,
        string? mapName = null) =>
        new()
        {
            ActionName = actionName,
            MapName = mapName ?? DefaultMapName,
            MapLabel = mapLabel ?? string.Empty,
            UiLabel = uiLabel ?? DefaultLabel,
            UiCategory = uiCategory ?? DefaultCategory,
            KeyboardBinding = keyboardBinding ?? string.Empty,
            MouseBinding = mouseBinding ?? string.Empty,
            JoystickBinding = joystickBinding ?? string.Empty,
            GamepadBinding = gamepadBinding ?? string.Empty,
            ActivationMode = activationMode
        };

    private static JArray BuildPayload(params KeybindingAction[] actions) =>
        FunctionsPayloadBuilder.BuildGroupedFunctionsPayload(actions, nint.Zero);

    private static JObject GetFirstOption(JArray payload)
    {
        JObject group = (JObject)payload[0];
        JArray options = (JArray)group["options"]!;
        return (JObject)options[0];
    }

    private static string GetOptionValue(JArray payload)
    {
        JObject option = GetFirstOption(payload);
        return option["value"]!.Value<string>()!;
    }

    private static string GetOptionLegacyValue(JArray payload)
    {
        JObject option = GetFirstOption(payload);
        return option["legacyValue"]!.Value<string>()!;
    }

    private static string GetOptionBindingType(JArray payload)
    {
        JObject option = GetFirstOption(payload);
        return option["bindingType"]!.Value<string>()!;
    }

    private static (bool Disabled, string Reason) GetOptionDisabledStatus(JArray payload)
    {
        JObject option = GetFirstOption(payload);
        bool disabled = option["disabled"]!.Value<bool>();
        string reason = option["disabledReason"]!.Value<string>()!;
        return (disabled, reason);
    }

    private static JObject GetOptionDetails(JArray payload)
    {
        JObject option = GetFirstOption(payload);
        return (JObject)option["details"]!;
    }

    #region FormatActivationMode Tests

    [Theory]
    [InlineData(ActivationMode.tap, "Tap")]
    [InlineData(ActivationMode.tap_quicker, "Quick Tap")]
    [InlineData(ActivationMode.press, "Press")]
    [InlineData(ActivationMode.press_quicker, "Quick Press")]
    [InlineData(ActivationMode.hold, "Hold")]
    [InlineData(ActivationMode.delayed_press, "Delayed")]
    [InlineData(ActivationMode.delayed_press_quicker, "Quick Delay")]
    [InlineData(ActivationMode.delayed_press_medium, "Medium Delay")]
    [InlineData(ActivationMode.delayed_press_long, "Long Delay")]
    [InlineData(ActivationMode.delayed_hold, "Delayed Hold")]
    [InlineData(ActivationMode.delayed_hold_long, "Long Hold")]
    [InlineData(ActivationMode.double_tap, "Double Tap")]
    [InlineData(ActivationMode.all, "All")]
    [InlineData(ActivationMode.hold_toggle, "Toggle")]
    [InlineData(ActivationMode.smart_toggle, "Smart Toggle")]
    public void FormatActivationMode_ReturnsCorrectLabel_ForAllKnownModes(
        ActivationMode mode,
        string expected)
    {
        string result = FunctionsPayloadBuilder.FormatActivationMode(mode);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(ActivationMode.hold, ActivationMode.hold_no_retrigger, "Hold")]
    [InlineData(ActivationMode.delayed_hold, ActivationMode.delayed_hold_no_retrigger, "Delayed Hold")]
    [InlineData(ActivationMode.double_tap, ActivationMode.double_tap_nonblocking, "Double Tap")]
    public void FormatActivationMode_ReturnsSameLabel_ForDuplicateLabelModes(
        ActivationMode mode1,
        ActivationMode mode2,
        string expectedLabel)
    {
        string result1 = FunctionsPayloadBuilder.FormatActivationMode(mode1);
        string result2 = FunctionsPayloadBuilder.FormatActivationMode(mode2);

        result1.Should().Be(expectedLabel);
        result2.Should().Be(expectedLabel);
    }

    [Theory]
    [InlineData(ActivationMode.hold_no_retrigger, ActivationMode.hold)]
    [InlineData(ActivationMode.delayed_hold_no_retrigger, ActivationMode.delayed_hold)]
    [InlineData(ActivationMode.double_tap_nonblocking, ActivationMode.double_tap)]
    public void FormatActivationMode_MatchesPrimaryModeLabel_ForDuplicateModes(
        ActivationMode duplicateMode,
        ActivationMode primaryMode)
    {
        string duplicateResult = FunctionsPayloadBuilder.FormatActivationMode(duplicateMode);
        string primaryResult = FunctionsPayloadBuilder.FormatActivationMode(primaryMode);

        duplicateResult.Should().Be(primaryResult);
    }

    [Fact]
    public void FormatActivationMode_UsesFallback_ForFutureUnknownMode()
    {
        ActivationMode unknownMode = (ActivationMode)999;
        string expected = unknownMode.ToString();

        string result = FunctionsPayloadBuilder.FormatActivationMode(unknownMode);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(ActivationMode.tap, "Tap")]
    [InlineData(ActivationMode.hold, "Hold")]
    [InlineData(ActivationMode.double_tap, "Double Tap")]
    public void FormatActivationMode_PreservesExactOutput_ForPrimaryModes(
        ActivationMode mode,
        string expected)
    {
        string result = FunctionsPayloadBuilder.FormatActivationMode(mode);

        result.Should().Be(expected);
    }

    [Fact]
    public void FormatActivationMode_HandlesAllEnumValues()
    {
        Array values = Enum.GetValues(typeof(ActivationMode));

        foreach (ActivationMode mode in values)
        {
            string result = FunctionsPayloadBuilder.FormatActivationMode(mode);

            result.Should().NotBeNullOrEmpty(
                $"ActivationMode.{mode} should not return null or empty");
        }
    }

    [Fact]
    public void FormatActivationMode_AllConstantsUsed()
    {
        string[] expectedLabels =
        [
            "Tap", "Quick Tap", "Press", "Quick Press",
            "Hold", "Double Tap", "All", "Toggle",
            "Smart Toggle", "Delayed", "Quick Delay",
            "Medium Delay", "Long Delay", "Delayed Hold", "Long Hold"
        ];

        Array values = Enum.GetValues(typeof(ActivationMode));

        foreach (ActivationMode mode in values)
        {
            string result = FunctionsPayloadBuilder.FormatActivationMode(mode);

            expectedLabels.Should().Contain(
                result,
                $"ActivationMode.{mode} returned unexpected label '{result}'");
        }
    }

    #endregion

    #region BuildGroupedFunctionsPayload Tests

    [Fact]
    public void BuildGroupedFunctionsPayload_InferBindingType_PrioritizesKeyboard()
    {
        KeybindingAction action = CreateAction(
            "kb_action",
            ActivationMode.press,
            "f1",
            "mouse1");

        JArray payload = BuildPayload(action);

        GetOptionBindingType(payload).Should().Be("keyboard");
    }

    [Fact]
    public void BuildGroupedFunctionsPayload_UsesStableV2Value_AndIncludesLegacyValue()
    {
        KeybindingAction action = CreateAction(
            "v_engineering_assignment_weapons_increase",
            ActivationMode.press,
            keyboardBinding: "f1",
            uiCategory: "FLIGHT");

        JArray payload = BuildPayload(action);

        GetOptionValue(payload).Should().Be($"v2|{action.ActionName}|{DefaultMapName}");
        GetOptionLegacyValue(payload).Should().Be($"{action.ActionName}_FLIGHT");
    }

    [Fact]
    public void BuildGroupedFunctionsPayload_UsesMapLabelAsGroupLabel()
    {
        KeybindingAction action = CreateAction(
            "ship_boost",
            ActivationMode.press,
            keyboardBinding: "f1",
            uiCategory: "FLIGHT",
            mapLabel: "Flight - Movement");

        JArray payload = BuildPayload(action);

        JObject group = (JObject)payload.Single();
        group["label"]!.Value<string>().Should().Be("Flight - Movement");
        GetOptionLegacyValue(payload).Should().Be("ship_boost_FLIGHT");
    }

    [Fact]
    public void BuildGroupedFunctionsPayload_FallsBackToMapName_WhenMapLabelMissing()
    {
        KeybindingAction action = CreateAction(
            "ship_boost",
            ActivationMode.press,
            keyboardBinding: "f1",
            uiCategory: "FLIGHT",
            mapLabel: "",
            mapName: "Gameplay");

        JArray payload = BuildPayload(action);

        JObject group = (JObject)payload.Single();
        group["label"]!.Value<string>().Should().Be("Gameplay");
    }

    [Fact]
    public void BuildGroupedFunctionsPayload_InferBindingType_FallsBackToMouseAxis()
    {
        KeybindingAction action = CreateAction(
            "axis_action",
            ActivationMode.press,
            mouseBinding: "maxis_x");

        JArray payload = BuildPayload(action);

        GetOptionBindingType(payload).Should().Be("mouseaxis");
    }

    [Fact]
    public void BuildGroupedFunctionsPayload_InferBindingType_ReturnsUnboundWhenNoBindings()
    {
        KeybindingAction action = CreateAction("unbound_action", ActivationMode.press);

        JArray payload = BuildPayload(action);

        GetOptionBindingType(payload).Should().Be("unbound");
    }

    [Fact]
    public void BuildGroupedFunctionsPayload_DisabledStatus_EnabledForKeyboardOrMouseButton()
    {
        KeybindingAction action = CreateAction(
            "enabled_action",
            ActivationMode.press,
            "f1");

        JArray payload = BuildPayload(action);
        (bool disabled, string reason) = GetOptionDisabledStatus(payload);

        disabled.Should().BeFalse();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void BuildGroupedFunctionsPayload_DisabledStatus_AxisOnlyShowsAxisReason()
    {
        KeybindingAction action = CreateAction(
            "axis_only_action",
            ActivationMode.press,
            mouseBinding: "maxis_y");

        JArray payload = BuildPayload(action);
        (bool disabled, string reason) = GetOptionDisabledStatus(payload);

        disabled.Should().BeTrue();
        reason.Should().Be("Axis (Dial only)");
    }

    [Fact]
    public void BuildGroupedFunctionsPayload_DisabledStatus_ControllerOnlyShowsControllerReason()
    {
        KeybindingAction action = CreateAction(
            "controller_action",
            ActivationMode.press,
            joystickBinding: "js1");

        JArray payload = BuildPayload(action);
        (bool disabled, string reason) = GetOptionDisabledStatus(payload);

        disabled.Should().BeTrue();
        reason.Should().Be("Controller bind (not supported yet)");
    }

    [Fact]
    public void BuildGroupedFunctionsPayload_DisabledStatus_NoBindingsHasEmptyReason()
    {
        KeybindingAction action = CreateAction("no_bindings_action", ActivationMode.press);

        JArray payload = BuildPayload(action);
        (bool disabled, string reason) = GetOptionDisabledStatus(payload);

        disabled.Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void BuildGroupedFunctionsPayload_DisambiguatesDuplicateLabels_AddsSuffix()
    {
        KeybindingAction actionOne = CreateAction(
            "vehicle_boost_up",
            ActivationMode.press,
            mouseBinding: "mouse1",
            uiLabel: "Boost",
            uiCategory: "Flight");
        KeybindingAction actionTwo = CreateAction(
            "vehicle_boost_down",
            ActivationMode.press,
            mouseBinding: "mouse2",
            uiLabel: "Boost",
            uiCategory: "Flight");

        JArray payload = BuildPayload(actionOne, actionTwo);
        JObject group = (JObject)payload[0];
        JArray options = (JArray)group["options"]!;
        List<string> labels = options.Select(option => option["text"]!.Value<string>()!).ToList();

        labels.Should().Contain(label => label.StartsWith("Boost (Up", StringComparison.Ordinal));
        labels.Should().Contain(label => label.StartsWith("Boost (Down", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildGroupedFunctionsPayload_DisambiguatesDuplicateLabels_AppendsActivationMode()
    {
        KeybindingAction actionOne = CreateAction(
            "ship_power_on",
            ActivationMode.press,
            mouseBinding: "mouse1",
            uiLabel: "Power",
            uiCategory: "Flight");
        KeybindingAction actionTwo = CreateAction(
            "ship_power_off",
            ActivationMode.hold,
            mouseBinding: "mouse2",
            uiLabel: "Power",
            uiCategory: "Flight");

        JArray payload = BuildPayload(actionOne, actionTwo);
        JObject group = (JObject)payload[0];
        JArray options = (JArray)group["options"]!;
        List<string> labels = options.Select(option => option["text"]!.Value<string>()!).ToList();

        labels.Should().Contain(label => label.Contains("Press", StringComparison.Ordinal));
        labels.Should().Contain(label => label.Contains("Hold", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildGroupedFunctionsPayload_FindCommonPrefix_TrimsToUnderscoreBoundary()
    {
        KeybindingAction actionOne = CreateAction(
            "scanner_mode_primary",
            ActivationMode.press,
            mouseBinding: "mouse1",
            uiLabel: "Scanner",
            uiCategory: "Flight");
        KeybindingAction actionTwo = CreateAction(
            "scanner_mode_secondary",
            ActivationMode.press,
            mouseBinding: "mouse2",
            uiLabel: "Scanner",
            uiCategory: "Flight");

        JArray payload = BuildPayload(actionOne, actionTwo);
        JObject group = (JObject)payload[0];
        JArray options = (JArray)group["options"]!;
        List<string> labels = options.Select(option => option["text"]!.Value<string>()!).ToList();

        labels.Should().Contain(label => label.StartsWith("Scanner (Primary", StringComparison.Ordinal));
        labels.Should().Contain(label => label.StartsWith("Scanner (Secondary", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildGroupedFunctionsPayload_ToGroupedEntry_DedupesDeviceAndRaw()
    {
        KeybindingAction actionOne = CreateAction(
            "dup_action_one",
            ActivationMode.press,
            mouseBinding: "mouse1",
            uiLabel: "Duplicate",
            uiCategory: "Flight");
        KeybindingAction actionTwo = CreateAction(
            "dup_action_two",
            ActivationMode.press,
            mouseBinding: "mouse1",
            uiLabel: "Duplicate",
            uiCategory: "Flight");

        JArray payload = BuildPayload(actionOne, actionTwo);
        JObject details = GetOptionDetails(payload);
        JArray devices = (JArray)details["devices"]!;
        JObject mouseDevice = (JObject)devices.Single(device => device["device"]!.Value<string>() == "Mouse");
        JArray bindings = (JArray)mouseDevice["bindings"]!;

        bindings.Should().HaveCount(1);
    }

    [Fact]
    public void BuildGroupedFunctionsPayload_OptionAndDetailsHaveStableShape_ForPropertyInspectorContract()
    {
        KeybindingAction action = CreateAction(
            "kb_action",
            ActivationMode.press,
            "f1",
            uiLabel: "Boost",
            uiCategory: "Flight");

        JArray payload = BuildPayload(action);

        JObject group = (JObject)payload.Single();
        group.Properties().Select(p => p.Name).Should().Equal("label", "options");

        JArray options = (JArray)group["options"]!;
        JObject option = (JObject)options.Single();

        option.Properties().Select(p => p.Name)
            .Should()
            .Equal("value", "legacyValue", "text", "bindingType", "searchText", "details", "disabled", "disabledReason");

        JObject details = (JObject)option["details"]!;
        details.Properties().Select(p => p.Name)
            .Should()
            .Equal("label", "description", "actionName", "activationMode", "devices", "isBound", "hasAxis", "hasButton");

        details["actionName"]!.Value<string>().Should().Be("kb_action");
        details["activationMode"]!.Value<string>().Should().Be("press");

        JArray devices = (JArray)details["devices"]!;
        devices.Should().HaveCount(1);

        JObject keyboardDevice = (JObject)devices.Single();
        keyboardDevice.Properties().Select(p => p.Name).Should().Equal("device", "bindings");
        keyboardDevice["device"]!.Value<string>().Should().Be("Keyboard");

        JArray bindings = (JArray)keyboardDevice["bindings"]!;
        bindings.Should().HaveCount(1);

        JObject binding = (JObject)bindings.Single();
        binding.Properties().Select(p => p.Name).Should().Equal("raw", "display", "sourceActionName");
        binding["raw"]!.Value<string>().Should().Be("f1");
        binding["sourceActionName"]!.Value<string>().Should().Be("kb_action");
    }

    #endregion
}
