using FluentAssertions;
using SCStreamDeck.Common;
using SCStreamDeck.Models;

namespace Tests.Unit.Common;

public sealed class UserOverrideParserTests
{
    [Fact]
    public void Parse_InvalidPath_ReturnsNull() => UserOverrideParser.Parse("invalid::path").Should().BeNull();

    [Fact]
    public void Parse_MissingFile_ReturnsNull()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "missing.xml");

        UserOverrideParser.Parse(tempFile).Should().BeNull();
    }

    [Fact]
    public void Parse_ValidOverrides_ReturnsData()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string filePath = Path.Combine(tempDir, "actionmaps.xml");
        File.WriteAllText(filePath, """
                                    <actionmap>
                                      <action name="throttle_up">
                                        <rebind input="kb_1"/>
                                      </action>
                                      <action name="pitch_up">
                                        <rebind input="mo_WHEEL_UP"/>
                                      </action>
                                      <action name="roll_left">
                                        <rebind input="js_X"/>
                                      </action>
                                      <action name="aim">
                                        <rebind input="gp_A"/>
                                      </action>
                                    </actionmap>
                                    """);

        UserOverrides? overrides = UserOverrideParser.Parse(filePath);

        overrides.Should().NotBeNull();
        overrides.Keyboard.Should().ContainKey("throttle_up").WhoseValue.Should().Be("1");
        overrides.Mouse.Should().ContainKey("pitch_up").WhoseValue.Should().Be("WHEEL_UP");
        overrides.Joystick.Should().ContainKey("roll_left").WhoseValue.Should().Be("X");
        overrides.Gamepad.Should().ContainKey("aim").WhoseValue.Should().Be("A");
        overrides.HasOverrides.Should().BeTrue();
    }

    [Fact]
    public void ApplyOverrides_KeyboardOverride_ClearsMouseBinding()
    {
        List<KeybindingActionData> actions =
        [
            new()
            {
                Name = "v_operator_mode_cycle_forward",
                MapName = "test",
                Bindings = new InputBindings { Mouse = "mouse3" }
            }
        ];

        UserOverrides overrides = new(
            new Dictionary<string, string?> { { "v_operator_mode_cycle_forward", "pgdn" } },
            new Dictionary<string, string?>(),
            new Dictionary<string, string?>(),
            new Dictionary<string, string?>(),
            new Dictionary<string, IReadOnlyDictionary<string, string?>>(),
            new Dictionary<string, IReadOnlyDictionary<string, string?>>(),
            new Dictionary<string, IReadOnlyDictionary<string, string?>>(),
            new Dictionary<string, IReadOnlyDictionary<string, string?>>());

        UserOverrideParser.ApplyOverrides(actions, overrides);

        actions[0].Bindings.Keyboard.Should().Be("pgdn");
        actions[0].Bindings.Mouse.Should().BeNull();
    }

    [Fact]
    public void ApplyOverrides_MouseOverride_ClearsKeyboardBinding()
    {
        List<KeybindingActionData> actions =
        [
            new()
            {
                Name = "v_operator_mode_cycle_forward",
                MapName = "test",
                Bindings = new InputBindings { Keyboard = "pgdn" }
            }
        ];

        UserOverrides overrides = new(
            new Dictionary<string, string?>(),
            new Dictionary<string, string?> { { "v_operator_mode_cycle_forward", "mouse3" } },
            new Dictionary<string, string?>(),
            new Dictionary<string, string?>(),
            new Dictionary<string, IReadOnlyDictionary<string, string?>>(),
            new Dictionary<string, IReadOnlyDictionary<string, string?>>(),
            new Dictionary<string, IReadOnlyDictionary<string, string?>>(),
            new Dictionary<string, IReadOnlyDictionary<string, string?>>());

        UserOverrideParser.ApplyOverrides(actions, overrides);

        actions[0].Bindings.Mouse.Should().Be("mouse3");
        actions[0].Bindings.Keyboard.Should().BeNull();
    }

    [Fact]
    public void ApplyOverrides_KeyboardOverride_WithMouseValue_NormalizesToMouse()
    {
        List<KeybindingActionData> actions =
        [
            new()
            {
                Name = "v_toggle_mining_laser_fire",
                MapName = "test",
                Bindings = new InputBindings { Keyboard = "x" }
            }
        ];

        UserOverrides overrides = new(
            new Dictionary<string, string?> { { "v_toggle_mining_laser_fire", "mouse1" } },
            new Dictionary<string, string?>(),
            new Dictionary<string, string?>(),
            new Dictionary<string, string?>(),
            new Dictionary<string, IReadOnlyDictionary<string, string?>>(),
            new Dictionary<string, IReadOnlyDictionary<string, string?>>(),
            new Dictionary<string, IReadOnlyDictionary<string, string?>>(),
            new Dictionary<string, IReadOnlyDictionary<string, string?>>());

        UserOverrideParser.ApplyOverrides(actions, overrides);

        actions[0].Bindings.Mouse.Should().Be("mouse1");
        actions[0].Bindings.Keyboard.Should().BeNull();
    }

    [Fact]
    public void ApplyOverrides_UpdatesActions()
    {
        List<KeybindingActionData> actions =
        [
            new() { Name = "throttle_up", MapName = "test", Bindings = new InputBindings() },
            new() { Name = "pitch_up", MapName = "test", Bindings = new InputBindings() },
            new() { Name = "roll_left", MapName = "test", Bindings = new InputBindings() },
            new() { Name = "aim", MapName = "test", Bindings = new InputBindings() }
        ];

        UserOverrides overrides = new(
            new Dictionary<string, string?> { { "throttle_up", "1" } },
            new Dictionary<string, string?> { { "pitch_up", "WHEEL_UP" } },
            new Dictionary<string, string?> { { "roll_left", "X" } },
            new Dictionary<string, string?> { { "aim", "A" } },
            new Dictionary<string, IReadOnlyDictionary<string, string?>>(),
            new Dictionary<string, IReadOnlyDictionary<string, string?>>(),
            new Dictionary<string, IReadOnlyDictionary<string, string?>>(),
            new Dictionary<string, IReadOnlyDictionary<string, string?>>());

        UserOverrideParser.ApplyOverrides(actions, overrides);

        actions.Single(a => a.Name == "throttle_up").Bindings.Keyboard.Should().Be("1");
        actions.Single(a => a.Name == "pitch_up").Bindings.Mouse.Should().Be("WHEEL_UP");
        actions.Single(a => a.Name == "roll_left").Bindings.Joystick.Should().Be("X");
        actions.Single(a => a.Name == "aim").Bindings.Gamepad.Should().Be("A");
    }

    [Fact]
    public void ApplyOverrides_DuplicateActionNamesAcrossMaps_WithIdenticalOverrides_UpdatesAll()
    {
        List<KeybindingActionData> actions =
        [
            new() { Name = "v_open_all_doors", MapName = "vehicle_general", Bindings = new InputBindings() },
            new() { Name = "v_open_all_doors", MapName = "spaceship_general", Bindings = new InputBindings() }
        ];

        UserOverrides overrides = new(
            new Dictionary<string, string?> { { "v_open_all_doors", "lshift+lbracket" } },
            new Dictionary<string, string?>(),
            new Dictionary<string, string?>(),
            new Dictionary<string, string?>(),
            new Dictionary<string, IReadOnlyDictionary<string, string?>>
            {
                {
                    "spaceship_defensive",
                    new Dictionary<string, string?> { { "v_open_all_doors", "lshift+lbracket" } }
                },
                {
                    "seat_general",
                    new Dictionary<string, string?> { { "v_open_all_doors", "lshift+lbracket" } }
                }
            },
            new Dictionary<string, IReadOnlyDictionary<string, string?>>(),
            new Dictionary<string, IReadOnlyDictionary<string, string?>>(),
            new Dictionary<string, IReadOnlyDictionary<string, string?>>());

        UserOverrideParser.ApplyOverrides(actions, overrides);

        actions.Single(a => a.MapName == "vehicle_general").Bindings.Keyboard.Should().Be("lshift+lbracket");
        actions.Single(a => a.MapName == "spaceship_general").Bindings.Keyboard.Should().Be("lshift+lbracket");
    }

    [Fact]
    public void Parse_DuplicateActionNamesAcrossMaps_WithIdenticalOverrides_DerivesGlobalOverride()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string filePath = Path.Combine(tempDir, "actionmaps.xml");
        File.WriteAllText(filePath, """
                                    <root>
                                      <actionmap name="spaceship_defensive">
                                        <action name="v_open_all_doors">
                                          <rebind input="kb1_lshift+lbracket"/>
                                        </action>
                                      </actionmap>
                                      <actionmap name="seat_general">
                                        <action name="v_open_all_doors">
                                          <rebind input="kb1_lshift+lbracket"/>
                                        </action>
                                      </actionmap>
                                    </root>
                                    """);

        UserOverrides? overrides = UserOverrideParser.Parse(filePath);

        overrides.Should().NotBeNull();
        overrides.Keyboard.Should().ContainKey("v_open_all_doors").WhoseValue.Should().Be("lshift+lbracket");
        overrides.KeyboardByMap.Should().ContainKey("spaceship_defensive");
        overrides.KeyboardByMap.Should().ContainKey("seat_general");
        overrides.HasOverrides.Should().BeTrue();
    }
}
