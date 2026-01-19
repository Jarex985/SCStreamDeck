using FluentAssertions;
using SCStreamDeck.Models;
using Tests.Integration.Testing;

namespace Tests.Integration;

/// <summary>
/// Integration coverage for the keybinding processing pipeline.
/// Uses real services (no mocks) to exercise loading and mapping of processed keybindings from LIVE-keybindings.json.
/// </summary>
public sealed class KeybindingPipelineTests(KeybindingPipelineFixture fixture) : IClassFixture<KeybindingPipelineFixture>
{
    [Fact]
    public async Task LoadKeybindings_ProducesActionsAndActivationModes()
    {
        if (!File.Exists(fixture.KeybindingsPath))
        {
            return;
        }

        bool loaded = await fixture.LoaderService.LoadKeybindingsAsync(fixture.KeybindingsPath);

        loaded.Should().BeTrue("a valid keybindings JSON is required for integration coverage");
        fixture.LoaderService.IsLoaded.Should().BeTrue();

        IReadOnlyList<KeybindingAction> actions = fixture.LoaderService.GetAllActions();
        actions.Should().NotBeEmpty();
        actions.Should().Contain(a => !string.IsNullOrWhiteSpace(a.KeyboardBinding) ||
                                      !string.IsNullOrWhiteSpace(a.MouseBinding),
            "at least one binding should be present for integration validation");

        IReadOnlyDictionary<string, ActivationModeMetadata> activationModes = fixture.LoaderService.GetActivationModes();
        activationModes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task KeybindingsContainMultipleActionMapsAndModifiers()
    {
        if (!File.Exists(fixture.KeybindingsPath))
        {
            return;
        }

        bool loaded = await fixture.LoaderService.LoadKeybindingsAsync(fixture.KeybindingsPath);
        loaded.Should().BeTrue();

        IReadOnlyList<KeybindingAction> actions = fixture.LoaderService.GetAllActions();

        actions.Should().Contain(a => a.MapName.Contains("spaceship", StringComparison.OrdinalIgnoreCase));
        actions.Should().Contain(a => a.MapName.Contains("onfoot", StringComparison.OrdinalIgnoreCase));

        actions.Should().Contain(a => a.KeyboardBinding.Contains("+"),
            "modifier combinations (e.g., Ctrl+Shift) should be preserved in keyboard bindings");

        actions.Should().Contain(a => a.MouseBinding.Contains("MOUSE", StringComparison.OrdinalIgnoreCase),
            "mouse bindings should be present for mouse-centric actions");
    }
}
