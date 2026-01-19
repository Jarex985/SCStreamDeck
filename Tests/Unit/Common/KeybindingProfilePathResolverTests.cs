using FluentAssertions;
using SCStreamDeck.Common;

namespace Tests.Unit.Common;

public sealed class KeybindingProfilePathResolverTests
{
    [Fact]
    public void TryFindActionMapsXml_NullOrEmpty_ReturnsNull()
    {
        KeybindingProfilePathResolver.TryFindActionMapsXml(null).Should().BeNull();
        KeybindingProfilePathResolver.TryFindActionMapsXml(" ").Should().BeNull();
    }

    [Fact]
    public void TryFindActionMapsXml_NoUserDirectory_ReturnsNull()
    {
        string tempDir = CreateTempDirectory();

        KeybindingProfilePathResolver.TryFindActionMapsXml(tempDir).Should().BeNull();
    }

    [Fact]
    public void TryFindActionMapsXml_FindsFirstProfile()
    {
        string channelPath = CreateTempDirectory();
        string clientDir = Path.Combine(channelPath, "user", "client");
        Directory.CreateDirectory(clientDir);

        string instance1 = Path.Combine(clientDir, "0", "Profiles", "default");
        Directory.CreateDirectory(instance1);

        string firstCandidate = Path.Combine(instance1, SCConstants.Files.ActionMapsFileName);

        string? result = KeybindingProfilePathResolver.TryFindActionMapsXml(channelPath);

        result.Should().BeNull();

        File.WriteAllText(firstCandidate, "<actionmaps/>");

        result = KeybindingProfilePathResolver.TryFindActionMapsXml(channelPath);

        result.Should().EndWith(Path.Combine("user", "client", "0", "Profiles", "default", SCConstants.Files.ActionMapsFileName));
    }

    private static string CreateTempDirectory()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }
}
