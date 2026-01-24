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
    public void ApplyOverrides_UpdatesActions()
    {
        List<KeybindingActionData> actions =
        [
            new() { Name = "throttle_up", Bindings = new InputBindings() },
            new() { Name = "pitch_up", Bindings = new InputBindings() },
            new() { Name = "roll_left", Bindings = new InputBindings() },
            new() { Name = "aim", Bindings = new InputBindings() }
        ];

        UserOverrides overrides = new(
            new Dictionary<string, string?> { { "throttle_up", "1" } },
            new Dictionary<string, string?> { { "pitch_up", "WHEEL_UP" } },
            new Dictionary<string, string?> { { "roll_left", "X" } },
            new Dictionary<string, string?> { { "aim", "A" } });

        UserOverrideParser.ApplyOverrides(actions, overrides);

        actions.Single(a => a.Name == "throttle_up").Bindings.Keyboard.Should().Be("1");
        actions.Single(a => a.Name == "pitch_up").Bindings.Mouse.Should().Be("WHEEL_UP");
        actions.Single(a => a.Name == "roll_left").Bindings.Joystick.Should().Be("X");
        actions.Single(a => a.Name == "aim").Bindings.Gamepad.Should().Be("A");
    }
}
