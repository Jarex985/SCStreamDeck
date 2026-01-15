using SCStreamDeck.Models;
using SCStreamDeck.Services.Keybinding;
using Newtonsoft.Json;
using Xunit;
#pragma warning disable CS8604 // Possible null reference argument.

namespace SCStreamDeck.UnitTests.IntegrationTests;

/// <summary>
/// Integration tests for the complete keybinding pipeline.
/// Tests the flow from JSON loading → parsing → validation.
/// </summary>
public class KeybindingPipelineTests
{
    private readonly IKeybindingParserService _parser = new KeybindingParserService();

    #region Pipeline - Valid Keybindings Tests

    [Fact]
    public async Task Pipeline_LoadParse_ShouldSucceed_ForValidBindings()
    {
        // Arrange
        string testJson = @"
        {
          ""metadata"": {
            ""extractedAt"": ""2023-01-01T00:00:00Z"",
            ""language"": ""english""
          },
          ""actions"": [
            {
              ""name"": ""test_action"",
              ""label"": ""Test Action"",
              ""description"": ""A test action"",
              ""category"": ""test"",
              ""mapName"": ""test_map"",
              ""mapLabel"": ""Test Map"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""lshift+a""
              }
            }
          ]
        }";

        // Act
        var data = JsonConvert.DeserializeObject<KeybindingDataFile>(testJson);
        Assert.NotNull(data);

        var parsed = _parser.ParseBinding(data!.Actions[0].Bindings.Keyboard);

        // Assert
        Assert.NotNull(parsed);
        Assert.NotNull(data.Actions);
        Assert.Single(data.Actions);
        Assert.Equal("test_action", data.Actions[0].Name);
        Assert.Equal("lshift+a", data.Actions[0].Bindings.Keyboard);
    }

    [Fact]
    public async Task Pipeline_ShouldHandleMultipleActions()
    {
        // Arrange
        string testJson = @"
        {
          ""metadata"": {
            ""extractedAt"": ""2023-01-01T00:00:00Z"",
            ""language"": ""english""
          },
          ""actions"": [
            {
              ""name"": ""action1"",
              ""label"": ""Action 1"",
              ""description"": ""The first action"",
              ""category"": ""test"",
              ""mapName"": ""test_map"",
              ""mapLabel"": ""Test Map"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""w""
              }
            },
            {
              ""name"": ""action2"",
              ""label"": ""Action 2"",
              ""description"": ""The second action"",
              ""category"": ""test"",
              ""mapName"": ""test_map"",
              ""mapLabel"": ""Test Map"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""a""
              }
            },
            {
              ""name"": ""action3"",
              ""label"": ""Action 3"",
              ""description"": ""The third action"",
              ""category"": ""test"",
              ""mapName"": ""test_map"",
              ""mapLabel"": ""Test Map"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""s""
              }
            },
            {
              ""name"": ""action4"",
              ""label"": ""Action 4"",
              ""description"": ""The fourth action"",
              ""category"": ""test"",
              ""mapName"": ""test_map"",
              ""mapLabel"": ""Test Map"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""d""
              }
            }
          ]
        }";

        // Act
        var data = JsonConvert.DeserializeObject<KeybindingDataFile>(testJson);
        Assert.NotNull(data);

        var results = data!.Actions
            .Select(action => _parser.ParseBinding(action.Bindings.Keyboard))
            .ToList();

        // Assert
        Assert.Equal(4, results.Count);
        Assert.All(results, result => Assert.NotNull(result));
    }

    #endregion

    #region Pipeline - Complex Bindings Tests

    [Fact]
    public async Task Pipeline_ShouldHandleComplexModifierCombinations()
    {
        // Arrange
        string testJson = @"
        {
          ""metadata"": {
            ""extractedAt"": ""2023-01-01T00:00:00Z"",
            ""language"": ""english""
          },
          ""actions"": [
            {
              ""name"": ""complex1"",
              ""label"": ""Complex Action 1"",
              ""description"": ""A complex action with multiple modifiers"",
              ""category"": ""test"",
              ""mapName"": ""test_map"",
              ""mapLabel"": ""Test Map"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""lshift+lctrl+a""
              }
            },
            {
              ""name"": ""complex2"",
              ""label"": ""Complex Action 2"",
              ""description"": ""Another complex action"",
              ""category"": ""test"",
              ""mapName"": ""test_map"",
              ""mapLabel"": ""Test Map"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""lalt+rshift+z""
              }
            }
          ]
        }";

        // Act
        var data = JsonConvert.DeserializeObject<KeybindingDataFile>(testJson);
        Assert.NotNull(data);

        var result1 = _parser.ParseBinding(data!.Actions[0].Bindings.Keyboard);
        var result2 = _parser.ParseBinding(data.Actions[1].Bindings.Keyboard);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
    }

    #endregion

    #region Pipeline - Multiple ActionMaps Tests

    [Fact]
    public async Task Pipeline_ShouldHandleMultipleActionMaps()
    {
        // Arrange
        string testJson = @"
        {
          ""metadata"": {
            ""extractedAt"": ""2023-01-01T00:00:00Z"",
            ""language"": ""english""
          },
          ""actions"": [
            {
              ""name"": ""v_throttle"",
              ""label"": ""Throttle Up"",
              ""description"": ""Increase throttle"",
              ""category"": ""spaceship_general"",
              ""mapName"": ""spaceship_movement"",
              ""mapLabel"": ""Spaceship Movement"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""w""
              }
            },
            {
              ""name"": ""ui_toggle"",
              ""label"": ""UI Toggle"",
              ""description"": ""Toggle UI"",
              ""category"": ""player"",
              ""mapName"": ""player_general"",
              ""mapLabel"": ""Player General"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""f1""
              }
            },
            {
              ""name"": ""v_attack1_group1"",
              ""label"": ""Primary Fire"",
              ""description"": ""Fire primary weapon"",
              ""category"": ""spaceship_weapons"",
              ""mapName"": ""spaceship_weapons"",
              ""mapLabel"": ""Spaceship Weapons"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""x""
              }
            }
          ]
        }";

        // Act
        var data = JsonConvert.DeserializeObject<KeybindingDataFile>(testJson);
        Assert.NotNull(data);

        Assert.Equal(3, data!.Actions.Count);

        var allParsed = data.Actions
            .Select(action => _parser.ParseBinding(action.Bindings.Keyboard ?? action.Bindings.Mouse))
            .ToList();

        // Assert
        Assert.Equal(3, allParsed.Count);
        Assert.All(allParsed, result => Assert.NotNull(result));
    }

    #endregion

    #region Pipeline - Edge Cases Tests

    [Fact]
    public async Task Pipeline_ShouldHandleEmptyActionMap()
    {
        // Arrange
        string testJson = @"
        {
          ""metadata"": {
            ""extractedAt"": ""2023-01-01T00:00:00Z"",
            ""language"": ""english""
          },
          ""actions"": []
        }";

        // Act
        var data = JsonConvert.DeserializeObject<KeybindingDataFile>(testJson);
        Assert.NotNull(data);

        Assert.NotNull(data!.Actions);
        Assert.Empty(data.Actions);
    }

    [Fact]
    public async Task Pipeline_ShouldHandleInvalidBindingsInActionMap()
    {
        // Arrange
        string testJson = @"
        {
          ""metadata"": {
            ""extractedAt"": ""2023-01-01T00:00:00Z"",
            ""language"": ""english""
          },
          ""actions"": [
            {
              ""name"": ""valid"",
              ""label"": ""Valid Action"",
              ""description"": ""A valid action"",
              ""category"": ""test"",
              ""mapName"": ""test_map"",
              ""mapLabel"": ""Test Map"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""w""
              }
            },
            {
              ""name"": ""invalid"",
              ""label"": ""Invalid Action"",
              ""description"": ""An invalid action"",
              ""category"": ""test"",
              ""mapName"": ""test_map"",
              ""mapLabel"": ""Test Map"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""invalid_key""
              }
            },
            {
              ""name"": ""also_valid"",
              ""label"": ""Also Valid"",
              ""description"": ""Another valid action"",
              ""category"": ""test"",
              ""mapName"": ""test_map"",
              ""mapLabel"": ""Test Map"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""a""
              }
            }
          ]
        }";

        // Act
        var data = JsonConvert.DeserializeObject<KeybindingDataFile>(testJson);
        Assert.NotNull(data);

        var results = data!.Actions
            .Select(action => _parser.ParseBinding(action.Bindings.Keyboard))
            .ToList();

        // Assert
        Assert.Equal(3, results.Count);
        Assert.NotNull(results[0]);
        Assert.Null(results[1]); // Invalid binding
        Assert.NotNull(results[2]);
    }

    #endregion

    #region Pipeline - Real-World Scenarios Tests

    [Fact]
    public async Task Pipeline_ShouldHandleRealSCMovementBindings()
    {
        // Arrange - Typical SC spaceship movement bindings
        string testJson = @"
        {
          ""metadata"": {
            ""extractedAt"": ""2023-01-01T00:00:00Z"",
            ""language"": ""english""
          },
          ""actions"": [
            {
              ""name"": ""v_throttle"",
              ""label"": ""Throttle Up"",
              ""description"": ""Increase throttle"",
              ""category"": ""spaceship_general"",
              ""mapName"": ""spaceship_movement"",
              ""mapLabel"": ""Spaceship Movement"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""w""
              }
            },
            {
              ""name"": ""v_brake"",
              ""label"": ""Throttle Down"",
              ""description"": ""Decrease throttle"",
              ""category"": ""spaceship_general"",
              ""mapName"": ""spaceship_movement"",
              ""mapLabel"": ""Spaceship Movement"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""s""
              }
            },
            {
              ""name"": ""v_yaw_left"",
              ""label"": ""Yaw Left"",
              ""description"": ""Rotate spaceship left"",
              ""category"": ""spaceship_general"",
              ""mapName"": ""spaceship_movement"",
              ""mapLabel"": ""Spaceship Movement"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""a""
              }
            },
            {
              ""name"": ""v_yaw_right"",
              ""label"": ""Yaw Right"",
              ""description"": ""Rotate spaceship right"",
              ""category"": ""spaceship_general"",
              ""mapName"": ""spaceship_movement"",
              ""mapLabel"": ""Spaceship Movement"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""d""
              }
            },
            {
              ""name"": ""v_pitch_up"",
              ""label"": ""Pitch Up"",
              ""description"": ""Tilt spaceship up"",
              ""category"": ""spaceship_general"",
              ""mapName"": ""spaceship_movement"",
              ""mapLabel"": ""Spaceship Movement"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""lshift+s""
              }
            },
            {
              ""name"": ""v_pitch_down"",
              ""label"": ""Pitch Down"",
              ""description"": ""Tilt spaceship down"",
              ""category"": ""spaceship_general"",
              ""mapName"": ""spaceship_movement"",
              ""mapLabel"": ""Spaceship Movement"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""lshift+w""
              }
            },
            {
              ""name"": ""v_afterburner"",
              ""label"": ""Afterburner"",
              ""description"": ""Engage afterburner"",
              ""category"": ""spaceship_general"",
              ""mapName"": ""spaceship_movement"",
              ""mapLabel"": ""Spaceship Movement"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""lshift+b""
              }
            },
            {
              ""name"": ""v_toggle_gear"",
              ""label"": ""Toggle Landing Gear"",
              ""description"": ""Deploy or retract landing gear"",
              ""category"": ""spaceship_general"",
              ""mapName"": ""spaceship_movement"",
              ""mapLabel"": ""Spaceship Movement"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""lalt+n""
              }
            }
          ]
        }";

        // Act
        var data = JsonConvert.DeserializeObject<KeybindingDataFile>(testJson);
        Assert.NotNull(data);

        var results = data!.Actions
            .Select(action => _parser.ParseBinding(action.Bindings.Keyboard))
            .ToList();

        // Assert
        Assert.Equal(8, results.Count);
        Assert.All(results, result => Assert.NotNull(result));
    }

    [Fact]
    public async Task Pipeline_ShouldHandleRealSCWeaponBindings()
    {
        // Arrange - Typical SC weapon bindings
        string testJson = @"
        {
          ""metadata"": {
            ""extractedAt"": ""2023-01-01T00:00:00Z"",
            ""language"": ""english""
          },
          ""actions"": [
            {
              ""name"": ""v_attack1_group1"",
              ""label"": ""Primary Fire"",
              ""description"": ""Fire primary weapon"",
              ""category"": ""spaceship_weapons"",
              ""mapName"": ""spaceship_weapons"",
              ""mapLabel"": ""Spaceship Weapons"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""x""
              }
            },
            {
              ""name"": ""v_weapon_launch_missile"",
              ""label"": ""Launch Missile"",
              ""description"": ""Fire missile weapon"",
              ""category"": ""spaceship_weapons"",
              ""mapName"": ""spaceship_weapons"",
              ""mapLabel"": ""Spaceship Weapons"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""lctrl+g""
              }
            },
            {
              ""name"": ""v_weapon_cycle_gimbal_mode"",
              ""label"": ""Cycle Gimbal Mode"",
              ""description"": ""Change gimbal mode of weapons"",
              ""category"": ""spaceship_weapons"",
              ""mapName"": ""spaceship_weapons"",
              ""mapLabel"": ""Spaceship Weapons"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""lctrl+r""
              }
            },
            {
              ""name"": ""v_weapon_launch_countermeasure"",
              ""label"": ""Launch Countermeasure"",
              ""description"": ""Fire countermeasure weapon"",
              ""category"": ""spaceship_weapons"",
              ""mapName"": ""spaceship_weapons"",
              ""mapLabel"": ""Spaceship Weapons"",
              ""activationMode"": ""press"",
              ""bindings"": {
                ""keyboard"": ""z""
              }
            }
          ]
        }";

        // Act
        var data = JsonConvert.DeserializeObject<KeybindingDataFile>(testJson);
        Assert.NotNull(data);

        var results = data!.Actions
            .Select(action => _parser.ParseBinding(action.Bindings.Keyboard))
            .ToList();

        // Assert
        Assert.Equal(4, results.Count);
        Assert.All(results, result => Assert.NotNull(result));
    }

    #endregion
}
