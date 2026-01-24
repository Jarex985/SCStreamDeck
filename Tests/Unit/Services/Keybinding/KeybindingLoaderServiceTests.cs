using System.Reflection;
using FluentAssertions;
using Newtonsoft.Json;
using SCStreamDeck.Common;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Keybinding;

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

namespace Tests.Unit.Services.Keybinding;

public sealed class KeybindingLoaderServiceTests
{
    [Fact]
    public async Task LoadKeybindingsAsync_Fails_WhenPathInvalid()
    {
        KeybindingLoaderService service = new(new SystemFileSystem());

        bool result = await service.LoadKeybindingsAsync("?invalid::path");

        result.Should().BeFalse();
        service.IsLoaded.Should().BeFalse();
    }

    [Fact]
    public async Task LoadKeybindingsAsync_Fails_WhenFileMissing()
    {
        KeybindingLoaderService service = new(new SystemFileSystem());
        string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");

        bool result = await service.LoadKeybindingsAsync(tempPath);

        result.Should().BeFalse();
        service.IsLoaded.Should().BeFalse();
    }

    [Fact]
    public async Task LoadKeybindingsAsync_Fails_WhenInvalidJson()
    {
        string tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "not json");

        try
        {
            KeybindingLoaderService service = new(new SystemFileSystem());

            bool result = await service.LoadKeybindingsAsync(tempFile);

            result.Should().BeFalse();
            service.IsLoaded.Should().BeFalse();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LoadKeybindingsAsync_LoadsActionsAndMetadata_WhenValid()
    {
        KeybindingDataFile dataFile = new()
        {
            Metadata = new KeybindingMetadata
            {
                Language = "EN",
                DataP4KPath = "C:/Data.p4k",
                ActivationModes = new Dictionary<string, ActivationModeMetadata> { { "press", new ActivationModeMetadata() } }
            },
            Actions =
            [
                new KeybindingActionData
                {
                    Name = "action1",
                    Category = "cat",
                    MapName = "map",
                    Label = "lbl",
                    ActivationMode = ActivationMode.press
                }
            ]
        };

        string tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, JsonConvert.SerializeObject(dataFile));

        try
        {
            KeybindingLoaderService service = new(new SystemFileSystem());

            bool result = await service.LoadKeybindingsAsync(tempFile);

            result.Should().BeTrue();
            service.IsLoaded.Should().BeTrue();
            service.TryGetAction("action1_cat", out KeybindingAction? action).Should().BeTrue();
            action!.ActionName.Should().Be("action1");
            service.GetActivationModes().Should().ContainKey("press");
            service.GetActivationModesByMode().Should().ContainKey(ActivationMode.press);
            service.GetMetadata("action1_cat").Should().NotBeNull();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static KeybindingAction MapActionTest(KeybindingActionData action) =>
        typeof(KeybindingLoaderService)
            .GetMethod("MapAction", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [action]) as KeybindingAction ?? throw new NullReferenceException();

    #region MapAction

    [Fact]
    public void MapAction_MapsAllFields()
    {
        KeybindingActionData actionData = new()
        {
            Name = "test_action",
            Label = "@label",
            Description = "@description",
            Category = "@category",
            MapName = "test_map",
            MapLabel = "@map",
            ActivationMode = ActivationMode.press,
            Bindings = new InputBindings { Keyboard = "SPACE", Mouse = "MOUSE1", Joystick = "js1_button1", Gamepad = "gp_a" }
        };

        KeybindingAction result = MapActionTest(actionData);

        result.ActionName.Should().Be("test_action");
        result.MapName.Should().Be("test_map");
        result.UiLabel.Should().Be("@label");
        result.UiDescription.Should().Be("@description");
        result.UiCategory.Should().Be("@category");
        result.KeyboardBinding.Should().Be("SPACE");
        result.MouseBinding.Should().Be("MOUSE1");
        result.JoystickBinding.Should().Be("js1_button1");
        result.GamepadBinding.Should().Be("gp_a");
        result.ActivationMode.Should().Be(ActivationMode.press);
    }

    [Fact]
    public void MapAction_HandlesNullFields()
    {
        KeybindingActionData actionData = new()
        {
            Name = "test_action",
            Label = null,
            Description = null,
            Category = null,
            MapName = null,
            MapLabel = null,
            ActivationMode = ActivationMode.press,
            Bindings = null
        };

        KeybindingAction result = MapActionTest(actionData);

        result.ActionName.Should().Be("test_action");
        result.MapName.Should().BeEmpty();
        result.UiLabel.Should().BeEmpty();
        result.UiDescription.Should().BeEmpty();
        result.UiCategory.Should().BeEmpty();
        result.KeyboardBinding.Should().BeEmpty();
        result.MouseBinding.Should().BeEmpty();
        result.JoystickBinding.Should().BeEmpty();
        result.GamepadBinding.Should().BeEmpty();
    }

    [Fact]
    public void MapAction_HandlesEmptyFields()
    {
        KeybindingActionData actionData = new()
        {
            Name = "test_action",
            Label = "",
            Description = "",
            Category = "",
            MapName = "",
            MapLabel = "",
            ActivationMode = ActivationMode.tap,
            Bindings = new InputBindings()
        };

        KeybindingAction result = MapActionTest(actionData);

        result.ActionName.Should().Be("test_action");
        result.MapName.Should().BeEmpty();
        result.UiLabel.Should().BeEmpty();
        result.UiDescription.Should().BeEmpty();
        result.UiCategory.Should().BeEmpty();
        result.KeyboardBinding.Should().BeEmpty();
        result.MouseBinding.Should().BeEmpty();
        result.JoystickBinding.Should().BeEmpty();
        result.GamepadBinding.Should().BeEmpty();
        result.ActivationMode.Should().Be(ActivationMode.tap);
    }

    #endregion
}
