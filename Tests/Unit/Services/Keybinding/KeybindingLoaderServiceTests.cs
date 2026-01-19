using FluentAssertions;
using Newtonsoft.Json;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Keybinding;

namespace Tests.Unit.Services.Keybinding;

public sealed class KeybindingLoaderServiceTests
{
    [Fact]
    public async Task LoadKeybindingsAsync_Fails_WhenPathInvalid()
    {
        KeybindingLoaderService service = new();

        bool result = await service.LoadKeybindingsAsync("?invalid::path");

        result.Should().BeFalse();
        service.IsLoaded.Should().BeFalse();
    }

    [Fact]
    public async Task LoadKeybindingsAsync_Fails_WhenFileMissing()
    {
        KeybindingLoaderService service = new();
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
            KeybindingLoaderService service = new();

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
            Actions = new List<KeybindingActionData>
            {
                new() { Name = "action1", Category = "cat", MapName = "map", Label = "lbl", ActivationMode = ActivationMode.press }
            }
        };

        string tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, JsonConvert.SerializeObject(dataFile));

        try
        {
            KeybindingLoaderService service = new();

            bool result = await service.LoadKeybindingsAsync(tempFile);

            result.Should().BeTrue();
            service.IsLoaded.Should().BeTrue();
            service.TryGetAction("action1_cat", out KeybindingAction? action).Should().BeTrue();
            action!.ActionName.Should().Be("action1");
            service.GetActivationModes().Should().ContainKey("press");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
