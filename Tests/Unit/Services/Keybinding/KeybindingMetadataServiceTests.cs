using FluentAssertions;
using SCStreamDeck.Common;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Keybinding;

namespace Tests.Unit.Services.Keybinding;

public sealed class KeybindingMetadataServiceTests
{
    [Fact]
    public void DetectLanguage_ReturnsDefault_WhenFileMissing()
    {
        KeybindingMetadataService service = new();

        string language = service.DetectLanguage(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        language.Should().Be(SCConstants.Localization.DefaultLanguage);
    }

    [Fact]
    public void DetectLanguage_ParsesLanguage_WhenPresent()
    {
        string tempDir = Directory.CreateTempSubdirectory().FullName;
        string userCfg = Path.Combine(tempDir, SCConstants.Files.UserConfigFileName);
        File.WriteAllLines(userCfg, new[] { "g_language = DE" });

        try
        {
            KeybindingMetadataService service = new();

            string language = service.DetectLanguage(tempDir);

            language.Should().Be("DE");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void NeedsRegeneration_ReturnsTrue_WhenFileMissing()
    {
        SCInstallCandidate install = new(
            "C:/Games",
            SCChannel.Live,
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
            Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".p4k"));

        KeybindingMetadataService service = new();

        bool result = service.NeedsRegeneration(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"), install);

        result.Should().BeTrue();
    }

    [Fact]
    public void NeedsRegeneration_ReturnsTrue_WhenMetadataMissing()
    {
        string tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "{}\n");

        SCInstallCandidate install = new(
            "C:/Games",
            SCChannel.Live,
            Path.GetTempPath(),
            tempFile);

        KeybindingMetadataService service = new();

        bool result = service.NeedsRegeneration(tempFile, install);

        result.Should().BeTrue();
        File.Delete(tempFile);
    }
}
