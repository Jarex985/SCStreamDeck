using FluentAssertions;
using SCStreamDeck.Common;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Keybinding;
using Newtonsoft.Json;

namespace Tests.Unit.Services.Keybinding;

public sealed class KeybindingMetadataServiceTests
{
    [Fact]
    public void DetectLanguage_ReturnsDefault_WhenFileMissing()
    {
        KeybindingMetadataService service = new(new SystemFileSystem());

        string language = service.DetectLanguage(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        language.Should().Be(SCConstants.Localization.DefaultLanguage);
    }

    [Fact]
    public void DetectLanguage_ParsesLanguage_WhenPresent()
    {
        string tempDir = Directory.CreateTempSubdirectory().FullName;
        string userCfg = Path.Combine(tempDir, SCConstants.Files.UserConfigFileName);
        File.WriteAllLines(userCfg, ["g_language = DE"]);

        try
        {
            KeybindingMetadataService service = new(new SystemFileSystem());

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

        KeybindingMetadataService service = new(new SystemFileSystem());

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

        KeybindingMetadataService service = new(new SystemFileSystem());

        bool result = service.NeedsRegeneration(tempFile, install);

        result.Should().BeTrue();
        File.Delete(tempFile);
    }

    [Fact]
    public void NeedsRegeneration_ReturnsTrue_WhenActionMapsAppearsAfterMetadataWasGenerated()
    {
        string tempDir = Directory.CreateTempSubdirectory().FullName;

        try
        {
            string channelPath = Path.Combine(tempDir, "LIVE");
            Directory.CreateDirectory(channelPath);

            string dataP4K = Path.Combine(channelPath, "Data.p4k");
            File.WriteAllText(dataP4K, "test");
            DateTime p4KWrite = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);
            File.SetLastWriteTime(dataP4K, p4KWrite);
            FileInfo p4KInfo = new(dataP4K);

            string actionMapsPath = Path.Combine(channelPath, "user", "client", "0", "Profiles", "default", "actionmaps.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(actionMapsPath)!);
            File.WriteAllText(actionMapsPath, "<ActionMaps />");
            DateTime amWrite = new(2026, 1, 1, 12, 1, 0, DateTimeKind.Local);
            File.SetLastWriteTime(actionMapsPath, amWrite);

            string jsonPath = Path.Combine(tempDir, "keybindings.json");
            KeybindingDataFile data = new()
            {
                Metadata = new KeybindingMetadata
                {
                    ExtractedAt = DateTime.UtcNow,
                    Language = "english",
                    DataP4KPath = dataP4K,
                    DataP4KSize = p4KInfo.Length,
                    DataP4KLastWrite = p4KWrite,
                    ActionMapsPath = null,
                    ActionMapsSize = null,
                    ActionMapsLastWrite = null
                }
            };
            File.WriteAllText(jsonPath, JsonConvert.SerializeObject(data, Formatting.Indented));

            SCInstallCandidate install = new(tempDir, SCChannel.Live, channelPath, dataP4K);
            KeybindingMetadataService service = new(new SystemFileSystem());

            bool result = service.NeedsRegeneration(jsonPath, install);

            result.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void NeedsRegeneration_ReturnsTrue_WhenActionMapsPathChanged()
    {
        string tempDir = Directory.CreateTempSubdirectory().FullName;

        try
        {
            string channelPath = Path.Combine(tempDir, "LIVE");
            Directory.CreateDirectory(channelPath);

            string dataP4K = Path.Combine(channelPath, "Data.p4k");
            File.WriteAllText(dataP4K, "test");
            DateTime p4KWrite = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);
            File.SetLastWriteTime(dataP4K, p4KWrite);
            FileInfo p4KInfo = new(dataP4K);

            // Detected path will be instance 0.
            string detectedActionMapsPath =
                Path.Combine(channelPath, "user", "client", "0", "Profiles", "default", "actionmaps.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(detectedActionMapsPath)!);
            File.WriteAllText(detectedActionMapsPath, "<ActionMaps />");
            DateTime amWrite = new(2026, 1, 1, 12, 1, 0, DateTimeKind.Local);
            File.SetLastWriteTime(detectedActionMapsPath, amWrite);
            FileInfo amInfo = new(detectedActionMapsPath);

            // Metadata claims a different instance path.
            string previousActionMapsPath =
                Path.Combine(channelPath, "user", "client", "1", "Profiles", "default", "actionmaps.xml");

            string jsonPath = Path.Combine(tempDir, "keybindings.json");
            KeybindingDataFile data = new()
            {
                Metadata = new KeybindingMetadata
                {
                    ExtractedAt = DateTime.UtcNow,
                    Language = "english",
                    DataP4KPath = dataP4K,
                    DataP4KSize = p4KInfo.Length,
                    DataP4KLastWrite = p4KWrite,
                    ActionMapsPath = previousActionMapsPath,
                    ActionMapsSize = amInfo.Length,
                    ActionMapsLastWrite = amWrite
                }
            };
            File.WriteAllText(jsonPath, JsonConvert.SerializeObject(data, Formatting.Indented));

            SCInstallCandidate install = new(tempDir, SCChannel.Live, channelPath, dataP4K);
            KeybindingMetadataService service = new(new SystemFileSystem());

            bool result = service.NeedsRegeneration(jsonPath, install);

            result.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void NeedsRegeneration_ReturnsFalse_WhenActionMapsUnchangedAndMetadataMatches()
    {
        string tempDir = Directory.CreateTempSubdirectory().FullName;

        try
        {
            string channelPath = Path.Combine(tempDir, "LIVE");
            Directory.CreateDirectory(channelPath);

            string dataP4K = Path.Combine(channelPath, "Data.p4k");
            File.WriteAllText(dataP4K, "test");
            DateTime p4KWrite = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);
            File.SetLastWriteTime(dataP4K, p4KWrite);
            FileInfo p4KInfo = new(dataP4K);

            string actionMapsPath = Path.Combine(channelPath, "user", "client", "0", "Profiles", "default", "actionmaps.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(actionMapsPath)!);
            File.WriteAllText(actionMapsPath, "<ActionMaps />");
            DateTime amWrite = new(2026, 1, 1, 12, 1, 0, DateTimeKind.Local);
            File.SetLastWriteTime(actionMapsPath, amWrite);
            FileInfo amInfo = new(actionMapsPath);

            string jsonPath = Path.Combine(tempDir, "keybindings.json");
            KeybindingDataFile data = new()
            {
                Metadata = new KeybindingMetadata
                {
                    ExtractedAt = DateTime.UtcNow,
                    Language = "english",
                    DataP4KPath = dataP4K,
                    DataP4KSize = p4KInfo.Length,
                    DataP4KLastWrite = p4KWrite,
                    ActionMapsPath = actionMapsPath.Replace('\\', '/'),
                    ActionMapsSize = amInfo.Length,
                    ActionMapsLastWrite = amWrite
                }
            };
            File.WriteAllText(jsonPath, JsonConvert.SerializeObject(data, Formatting.Indented));

            SCInstallCandidate install = new(tempDir, SCChannel.Live, channelPath, dataP4K);
            KeybindingMetadataService service = new(new SystemFileSystem());

            bool result = service.NeedsRegeneration(jsonPath, install);

            result.Should().BeFalse();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
